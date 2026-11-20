using System.Net;
using System.Text.Json;
using Mcctl.Api;
using Mcctl.Discovery;
using Mcctl.Events;
using Mcctl.Models;

namespace Mcctl.Core.Tests;

public class ClientTests
{
    private static (MusicCastDevice Dev, FakeDevice Fake) Create(string host = "10.0.0.5")
    {
        var fake = new FakeDevice();
        return (new MusicCastDevice(host, new HttpClient(fake)), fake);
    }

    [Fact]
    public async Task BuildsYxcUrlsAndQueries()
    {
        var (dev, fake) = Create();
        await dev.Main.SetVolumeAsync(25);
        await dev.Zone("zone2").SetMuteAsync(true);
        await dev.Main.SetInputAsync("net_radio", "autoplay_disabled");
        await dev.Main.SetPowerAsync(PowerState.Standby);
        await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.PlayPause);
        await dev.NetUsb.SetListControlAsync(ListControlType.Return);
        await dev.Tuner.SetFrequencyAsync("fm", 80000);

        Assert.Equal(
        [
            "main/setVolume?volume=25",
            "zone2/setMute?enable=true",
            "main/setInput?input=net_radio&mode=autoplay_disabled",
            "main/setPower?power=standby",
            "netusb/setPlayback?playback=play_pause",
            "netusb/setListControl?list_id=main&type=return&zone=main",
            "tuner/setFreq?band=fm&tuning=direct&num=80000",
        ], fake.Requests.Select(r => r.PathAndQuery));
    }

    [Theory]
    [InlineData(PlaybackCommand.PlayPause, "play_pause")]
    [InlineData(PlaybackCommand.FastReverseStart, "fast_reverse_start")]
    [InlineData(PowerState.Standby, "standby")]
    [InlineData(ListControlType.Return, "return")]
    public void QueryFormatsEnumsAsSnakeCase(Enum value, string expected)
        => Assert.Equal(expected, Query.Format(value));

    [Fact]
    public void QueryEscapesAndSkipsNulls()
        => Assert.Equal("a=x%20y&c=false", new Query().Add("a", "x y").Add("b", null).Add("c", false).ToString());

    [Fact]
    public void DeviceErrorClassification()
    {
        Assert.True(new YxcException(4, "x").IsDeviceError);
        Assert.True(new HttpRequestException().IsConnectionError);
        Assert.False(new InvalidOperationException().IsDeviceError);
    }

    [Fact]
    public async Task NonZeroResponseCodeThrows()
    {
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "main/setSoundProgram", "{\"response_code\":4}");
        var ex = await Assert.ThrowsAsync<YxcException>(() => dev.Main.SetSoundProgramAsync("bogus"));
        Assert.Equal(4, ex.ResponseCode);
        Assert.Contains("Invalid parameter", ex.Message);
    }

    [Fact]
    public async Task EventHeadersAreSentWhenSubscribed()
    {
        var (dev, fake) = Create();
        await dev.SubscribeEventsAsync(41100);
        var req = Assert.Single(fake.Requests);
        Assert.Equal("41100", req.Headers.Get("X-AppPort"));
        Assert.StartsWith("MusicCast/", req.Headers.Get("X-AppName")); // the device ignores other app names
    }

    [Fact]
    public async Task ParsesFeaturesAndStatus()
    {
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "system/getDeviceInfo", """{"response_code":0,"model_name":"WX-030","device_id":"AABBCC","system_version":2.71,"api_version":2.11}""");
        fake.On("10.0.0.5", "system/getFeatures", """
            {"response_code":0,
             "system":{"func_list":["wired_lan"],"zone_num":1,
               "input_list":[{"id":"net_radio","distribution_enable":true,"play_info_type":"netusb"},
                             {"id":"tuner","distribution_enable":true,"play_info_type":"tuner"},
                             {"id":"aux","distribution_enable":true,"play_info_type":"none"}]},
             "zone":[{"id":"main","func_list":["power","volume","mute","tone_control"],"input_list":["net_radio","tuner","aux"],
                      "sound_program_list":["music","movie"],
                      "range_step":[{"id":"volume","min":0,"max":60,"step":1},{"id":"tone_control","min":-12,"max":12,"step":1}]}],
             "tuner":{"func_list":["fm","dab"],"preset":{"type":"common","num":40}},
             "netusb":{"func_list":["recent_info"],"preset":{"num":40}}}
            """);
        fake.On("10.0.0.5", "system/getNameText", """{"response_code":0,"id":"main","text":"Living Room"}""");
        fake.On("10.0.0.5", "main/getStatus", """
            {"response_code":0,"power":"on","sleep":0,"volume":31,"mute":false,"max_volume":60,"input":"net_radio",
             "distribution_enable":true,"sound_program":"music","tone_control":{"mode":"manual","bass":2,"treble":-1},
             "link_control":"standard","disable_flags":0,"new_field_from_future":"x"}
            """);

        await dev.InitializeAsync();
        Assert.Equal("Living Room", dev.DisplayName);
        Assert.Equal("WX-030", dev.Info!.ModelName);
        Assert.Equal(60, dev.Features!.FindZone("main")!.Range("volume")!.Max);
        Assert.Equal(PlayInfoTypes.Tuner, dev.GetPlayInfoType("tuner"));
        Assert.Equal(PlayInfoTypes.NetUsb, dev.GetPlayInfoType("spotify")); // inferred, not in feature list

        var st = await dev.Main.GetStatusAsync();
        Assert.True(st.IsOn);
        Assert.Equal(31, st.Volume);
        Assert.Equal(2, st.ToneControl!.Bass);
        Assert.True(st.Extra!.ContainsKey("new_field_from_future"));
    }

    [Fact]
    public async Task FullListPagesThroughEightItemPages()
    {
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "netusb/getListInfo", req =>
        {
            var q = System.Web.HttpUtility.ParseQueryString(req.RequestUri!.Query);
            var index = int.Parse(q["index"]!);
            var items = Enumerable.Range(index, Math.Min(8, 19 - index))
                .Select(i => new { text = $"Item {i}", thumbnail = "", attribute = i % 2 == 0 ? 2 : 1 });
            return JsonSerializer.Serialize(new { response_code = 0, input = "server", menu_layer = 1, max_line = 19, index, menu_name = "NAS", list_info = items });
        });

        var list = await dev.NetUsb.GetFullListAsync("server");
        Assert.Equal(19, list.ListInfoItems.Count);
        Assert.Equal("Item 18", list.ListInfoItems[18].Text);
        Assert.True(list.ListInfoItems[0].IsContainer);   // attribute 2
        Assert.False(list.ListInfoItems[0].IsPlayable);
        Assert.False(list.ListInfoItems[1].IsContainer);  // attribute 1
        Assert.Equal(3, fake.Requests.Count);
    }

    [Theory]
    [InlineData(2, true, false)]            // server / NAS folder, radiko region (R-N1000A)
    [InlineData(125829122, true, false)]    // NAS folder with service specific high bits
    [InlineData(4, false, true)]            // radiko station
    public void ListItemAttributeMapping(long attribute, bool container, bool playable)
    {
        var item = new ListItem { Attribute = attribute };
        Assert.Equal(container, item.IsContainer);
        Assert.Equal(playable, item.IsPlayable);
    }

    [Fact]
    public async Task EnterFolderBindsListAndWaitsForAsyncLoad()
    {
        var (dev, fake) = Create();
        dev.NetUsb.ListPollInterval = dev.NetUsb.ListFirstPollDelay = TimeSpan.FromMilliseconds(10);
        var selected = false;
        var pollsAfterSelect = 0;
        fake.On("10.0.0.5", "netusb/setListControl", _ => { selected = true; return """{"response_code":0}"""; });
        fake.On("10.0.0.5", "netusb/getListInfo", _ =>
        {
            // The device keeps returning the old layer for a while after select.
            var loaded = selected && ++pollsAfterSelect >= 3;
            return loaded
                ? """{"response_code":0,"input":"server","menu_layer":1,"max_line":3,"menu_name":"nas","list_info":[{"text":"音楽","attribute":2}]}"""
                : """{"response_code":0,"input":"server","menu_layer":0,"max_line":2,"menu_name":"SERVER","list_info":[{"text":"nas","attribute":2}]}""";
        });

        var after = await dev.NetUsb.EnterFolderAsync("server", 1);

        Assert.Equal(1, after.MenuLayer);
        Assert.Equal("nas", after.MenuName);
        var paths = fake.Requests.Select(r => r.PathAndQuery).ToList();
        // getListInfo for the input must come first so the device's single cursor is bound to "server".
        Assert.StartsWith("netusb/getListInfo?input=server", paths[0]);
        Assert.Equal("netusb/setListControl?list_id=main&type=select&index=1&zone=main", paths[1]);
    }

    [Fact]
    public async Task EnterFolderToleratesAccessErrorWhileLoading()
    {
        // R-N1000A answers getListInfo with response_code 100 while a NAS folder is still loading.
        var (dev, fake) = Create();
        dev.NetUsb.ListPollInterval = dev.NetUsb.ListFirstPollDelay = TimeSpan.FromMilliseconds(10);
        var selected = false;
        var polls = 0;
        fake.On("10.0.0.5", "netusb/setListControl", _ => { selected = true; return """{"response_code":0}"""; });
        fake.On("10.0.0.5", "netusb/getListInfo", _ => !selected
            ? """{"response_code":0,"input":"server","menu_layer":0,"max_line":2,"menu_name":"SERVER","list_info":[]}"""
            : ++polls < 3
                ? """{"response_code":100}"""
                : """{"response_code":0,"input":"server","menu_layer":1,"max_line":3,"menu_name":"nas","list_info":[]}""");

        var after = await dev.NetUsb.EnterFolderAsync("server", 1);
        Assert.Equal("nas", after.MenuName);
    }

    [Fact]
    public async Task EnterFolderRetriesOnceWhenServerWakesUp()
    {
        // Cold NAS: the first select reports Access error and the list falls back to the top layer.
        var (dev, fake) = Create();
        dev.NetUsb.ListPollInterval = dev.NetUsb.ListFirstPollDelay = TimeSpan.FromMilliseconds(10);
        var selects = 0;
        var pollsSinceSelect = 0;
        fake.On("10.0.0.5", "netusb/setListControl", _ => { selects++; pollsSinceSelect = 0; return """{"response_code":0}"""; });
        fake.On("10.0.0.5", "netusb/getListInfo", _ =>
        {
            const string top = """{"response_code":0,"input":"server","menu_layer":0,"max_line":2,"menu_name":"SERVER","list_info":[]}""";
            if (selects == 0) return top;
            pollsSinceSelect++;
            return selects == 1
                ? pollsSinceSelect == 2 ? """{"response_code":100}""" : top
                : """{"response_code":0,"input":"server","menu_layer":1,"max_line":3,"menu_name":"nas","list_info":[]}""";
        });

        var after = await dev.NetUsb.EnterFolderAsync("server", 1);
        Assert.Equal("nas", after.MenuName);
        Assert.Equal(2, selects);
    }

    [Fact]
    public async Task SearchSelectsEntryThenPostsStringWithoutIndex()
    {
        // Sequence verified on R-N1000A (Net Radio → Radio → Search).
        var (dev, fake) = Create();
        dev.NetUsb.ListPollInterval = dev.NetUsb.ListFirstPollDelay = dev.NetUsb.SearchInputDelay = TimeSpan.FromMilliseconds(10);
        var searched = false;
        fake.On("10.0.0.5", "netusb/setSearchString", _ => { searched = true; return """{"response_code":0}"""; });
        fake.On("10.0.0.5", "netusb/getListInfo", _ => searched
            ? """{"response_code":0,"input":"net_radio","menu_layer":2,"max_line":439,"menu_name":"Search","list_info":[]}"""
            : """{"response_code":0,"input":"net_radio","menu_layer":1,"max_line":10,"menu_name":"Radio","list_info":[]}""");

        var result = await dev.NetUsb.SearchAsync("net_radio", 1, "jazz");

        Assert.Equal("Search", result.MenuName);
        var calls = fake.Requests.Select(r => r.PathAndQuery).ToList();
        Assert.Equal("netusb/getListInfo?input=net_radio&index=0&size=8&lang=en", calls[0]);
        Assert.Equal("netusb/setListControl?list_id=main&type=select&index=1&zone=main", calls[1]);
        Assert.Equal("netusb/setSearchString", calls[2]);

        var body = JsonDocument.Parse(fake.Requests[2].Body!).RootElement;
        Assert.Equal("jazz", body.GetProperty("string").GetString());
        Assert.False(body.TryGetProperty("index", out _)); // an index makes the device select another entry

        // The device's HTTP server rejects "application/json; charset=utf-8" with response_code 2.
        Assert.Equal("application/json", fake.ContentTypes[2]);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(9, 8)]
    [InlineData(17, 16)]
    public async Task SelectBindsThePageContainingTheItem(int index, int expectedPageStart)
    {
        // The device resolves select against the page it returned last; the target must be in that page.
        var (dev, fake) = Create();
        dev.NetUsb.ListPollInterval = dev.NetUsb.ListFirstPollDelay = TimeSpan.FromMilliseconds(10);
        var selected = false;
        fake.On("10.0.0.5", "netusb/setListControl", _ => { selected = true; return """{"response_code":0}"""; });
        fake.On("10.0.0.5", "netusb/getListInfo", _ => selected
            ? """{"response_code":0,"input":"server","menu_layer":2,"max_line":3,"menu_name":"Folder","list_info":[]}"""
            : """{"response_code":0,"input":"server","menu_layer":1,"max_line":30,"menu_name":"Music","list_info":[]}""");

        await dev.NetUsb.EnterFolderAsync("server", index);

        Assert.Equal($"netusb/getListInfo?input=server&index={expectedPageStart}&size=8&lang=en", fake.Requests[0].PathAndQuery);
    }

    [Fact]
    public async Task ReturnAtTopLayerDoesNotCallDevice()
    {
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "netusb/getListInfo", """{"response_code":0,"input":"radiko","menu_layer":0,"max_line":8,"menu_name":"radiko.jp","list_info":[]}""");
        await dev.NetUsb.ReturnAsync("radiko");
        Assert.DoesNotContain(fake.Requests, r => r.PathAndQuery.StartsWith("netusb/setListControl"));
    }

    [Fact]
    public async Task DistributionInfoAcceptsArrayBuildDisable()
    {
        // R-N1000A (API 2.17) returns build_disable as an array, older firmware as a bool.
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "dist/getDistributionInfo",
            """{"response_code":0,"group_id":"00000000000000000000000000000000","group_name":"","role":"none","server_zone":"main","client_list":[],"build_disable":[],"audio_dropout":false}""");
        var d = await dev.Distribution.GetDistributionInfoAsync();
        Assert.False(d.HasGroup);
        Assert.Equal(JsonValueKind.Array, d.BuildDisable!.Value.ValueKind);
    }

    [Fact]
    public async Task ParsesSignalInfoAndDescribesFormat()
    {
        // Real R-N1000A response while playing an MP3 from a DLNA server.
        var (dev, fake) = Create();
        fake.On("10.0.0.5", "main/getSignalInfo", """{"response_code":0,"audio":{"error":0,"format":"MP3","fs":"44.1 kHz","bit":"","bitrate":0}}""");
        var signal = await dev.Main.GetSignalInfoAsync();
        Assert.Equal("MP3 · 44.1 kHz", signal.Audio!.Describe());

        var hiRes = new AudioSignal { Format = "FLAC", Fs = "96 kHz", Bit = "24" };
        Assert.Equal("FLAC · 96 kHz · 24 bit", hiRes.Describe());
        Assert.Equal("AAC · 48 kHz · 256 kbps", new AudioSignal { Format = "AAC", Fs = "48 kHz", Bitrate = 256 }.Describe());
    }

    [Fact]
    public void ResolvesRelativeAlbumArt()
    {
        var (dev, _) = Create();
        Assert.Equal("http://10.0.0.5/YamahaRemoteControl/AlbumART/AlbumART1.jpg",
            dev.ResolveUrl("/YamahaRemoteControl/AlbumART/AlbumART1.jpg")!.ToString());
        Assert.Equal("https://cdn.example/a.png", dev.ResolveUrl("https://cdn.example/a.png")!.ToString());
        Assert.Null(dev.ResolveUrl(""));
    }
}

public class LinkTests
{
    [Fact]
    public async Task LinkSendsClientThenServerThenStart()
    {
        var fake = new FakeDevice();
        var http = new HttpClient(fake);
        using var server = new MusicCastDevice("10.0.0.1", http);
        using var client = new MusicCastDevice("10.0.0.2", http);
        fake.On("10.0.0.1", "dist/getDistributionInfo", """{"response_code":0,"group_id":"","role":"none","client_list":[]}""");
        fake.On("10.0.0.2", "dist/getDistributionInfo", """{"response_code":0,"group_id":"","role":"none","client_list":[]}""");
        fake.On("10.0.0.2", "main/getStatus", """{"response_code":0,"power":"on","input":"aux"}""");

        var gid = await MusicCastLink.LinkAsync(server, [client], groupName: "Party");
        Assert.Equal(32, gid.Length);

        var seq = fake.Requests.Select(r => $"{r.Host} {r.Method} {r.PathAndQuery}").ToList();
        int Idx(string s) => seq.FindIndex(x => x.Contains(s));
        Assert.True(Idx("10.0.0.2 POST dist/setClientInfo") < Idx("10.0.0.1 POST dist/setServerInfo"));
        Assert.True(Idx("10.0.0.1 POST dist/setServerInfo") < Idx("10.0.0.1 GET dist/startDistribution?num=0"));
        Assert.Contains("10.0.0.2 GET main/setInput?input=mc_link", seq);

        var serverBody = JsonDocument.Parse(fake.Requests.First(r => r.PathAndQuery == "dist/setServerInfo").Body!).RootElement;
        Assert.Equal(gid, serverBody.GetProperty("group_id").GetString());
        Assert.Equal("add", serverBody.GetProperty("type").GetString());
        Assert.Equal("10.0.0.2", serverBody.GetProperty("client_list")[0].GetString());

        var clientBody = JsonDocument.Parse(fake.Requests.First(r => r.PathAndQuery == "dist/setClientInfo").Body!).RootElement;
        Assert.Equal("10.0.0.1", clientBody.GetProperty("server_ip_address").GetString());
        Assert.Equal("main", clientBody.GetProperty("zone")[0].GetString());
    }

    [Fact]
    public async Task UnlinkLastClientDissolvesGroup()
    {
        var fake = new FakeDevice();
        var http = new HttpClient(fake);
        using var server = new MusicCastDevice("10.0.0.1", http);
        using var client = new MusicCastDevice("10.0.0.2", http);
        fake.On("10.0.0.1", "dist/getDistributionInfo",
            """{"response_code":0,"group_id":"0123456789abcdef0123456789abcdef","role":"server","server_zone":"main","client_list":[{"ip_address":"10.0.0.2","data_type":"base"}]}""");

        await MusicCastLink.UnlinkAsync(server, [client]);
        var seq = fake.Requests.Select(r => $"{r.Host} {r.PathAndQuery}").ToList();
        Assert.Contains("10.0.0.1 dist/stopDistribution", seq);
        var last = fake.Requests.Last(r => r.PathAndQuery == "dist/setServerInfo");
        Assert.Equal("", JsonDocument.Parse(last.Body!).RootElement.GetProperty("group_id").GetString());
    }
}

public class DiscoveryAndEventTests
{
    [Fact]
    public void ParsesSsdpLocation()
    {
        const string resp = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nLocation: http://192.168.1.20:49154/MediaRenderer/desc.xml\r\nST: upnp:rootdevice\r\n\r\n";
        Assert.Equal("http://192.168.1.20:49154/MediaRenderer/desc.xml", MusicCastDiscovery.ParseLocation(resp));
    }

    [Fact]
    public void ParsesYamahaDeviceDescription()
    {
        const string xml = """
            <?xml version="1.0"?>
            <root xmlns="urn:schemas-upnp-org:device-1-0" xmlns:yamaha="urn:schemas-yamaha-com:device-1-0">
              <device>
                <friendlyName>Kitchen</friendlyName>
                <modelName>WX-021</modelName>
                <serialNumber>Y123</serialNumber>
                <UDN>uuid:9ab0c000-f668-11de-9976-00a0ded00001</UDN>
              </device>
              <yamaha:X_device>
                <yamaha:X_URLBase>http://192.168.1.21:80/</yamaha:X_URLBase>
                <yamaha:X_serviceList>
                  <yamaha:X_service>
                    <yamaha:X_specType>urn:schemas-yamaha-com:service:X_YamahaExtendedControl:1</yamaha:X_specType>
                    <yamaha:X_yxcControlURL>/YamahaExtendedControl/v1/</yamaha:X_yxcControlURL>
                  </yamaha:X_service>
                </yamaha:X_serviceList>
              </yamaha:X_device>
            </root>
            """;
        var d = MusicCastDiscovery.ParseDescription(xml, new Uri("http://192.168.1.21:49154/desc.xml"));
        Assert.NotNull(d);
        Assert.Equal("192.168.1.21", d!.Host);
        Assert.Equal("Kitchen", d.FriendlyName);
        Assert.Equal("WX-021", d.ModelName);
        Assert.Equal("http://192.168.1.21/YamahaExtendedControl/v1/", d.YxcBaseUri.ToString());
    }

    [Fact]
    public void NonYamahaDescriptionIsIgnored()
    {
        const string xml = """<root xmlns="urn:schemas-upnp-org:device-1-0"><device><friendlyName>TV</friendlyName></device></root>""";
        Assert.Null(MusicCastDiscovery.ParseDescription(xml, new Uri("http://1.2.3.4/")));
    }

    [Fact]
    public void ParsesEventPayload()
    {
        var e = MusicCastEventListener.Parse(IPAddress.Parse("10.0.0.5"),
            """{"main":{"volume":42,"mute":true,"status_updated":true},"netusb":{"play_time":17},"device_id":"AABB"}""")!;
        Assert.Equal("AABB", e.DeviceId);
        Assert.True(e.TryGetInt("main", "volume", out var v) && v == 42);
        Assert.True(e.TryGetBool("main", "mute", out var m) && m);
        Assert.True(e.HasFlag("main", "status_updated"));
        Assert.False(e.HasFlag("netusb", "play_info_updated"));
        Assert.Equal(["main", "netusb"], e.Sections);
    }

    [Fact]
    public async Task ListenerReceivesUdpDatagram()
    {
        await using var listener = new MusicCastEventListener(0);
        var tcs = new TaskCompletionSource<MusicCastEvent>();
        listener.EventReceived += (_, e) => tcs.TrySetResult(e);
        listener.Start();

        using var udp = new System.Net.Sockets.UdpClient();
        var payload = System.Text.Encoding.UTF8.GetBytes("""{"main":{"power":"standby"}}""");
        await udp.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, listener.Port));

        var ev = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(ev.TryGetString("main", "power", out var p) && p == "standby");
    }
}
