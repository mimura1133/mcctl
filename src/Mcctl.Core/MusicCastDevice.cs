using System.Collections.Concurrent;
using Mcctl.Api;
using Mcctl.Models;

namespace Mcctl;

/// <summary>
/// High level entry point for one MusicCast device.
/// <code>
/// using var dev = new MusicCastDevice("192.168.1.20");
/// await dev.InitializeAsync();
/// await dev.Zone("main").SetPowerAsync(PowerState.On);
/// await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Play);
/// </code>
/// </summary>
public sealed class MusicCastDevice : IDisposable
{
    private readonly ConcurrentDictionary<string, ZoneApi> _zones = new();

    public MusicCastDevice(string host, HttpClient? httpClient = null)
        : this(new YxcClient(host, httpClient))
    {
    }

    public MusicCastDevice(YxcClient client)
    {
        Client = client;
        System = new SystemApi(client);
        NetUsb = new NetUsbApi(client);
        Tuner = new TunerApi(client);
        Cd = new CdApi(client);
        Distribution = new DistributionApi(client);
    }

    public YxcClient Client { get; }

    public string Host => Client.Host;

    public SystemApi System { get; }

    public NetUsbApi NetUsb { get; }

    public TunerApi Tuner { get; }

    public CdApi Cd { get; }

    public DistributionApi Distribution { get; }

    /// <summary>Filled by <see cref="InitializeAsync"/>.</summary>
    public DeviceInfo? Info { get; private set; }

    /// <summary>Filled by <see cref="InitializeAsync"/>.</summary>
    public Features? Features { get; private set; }

    /// <summary>Room name set in the app (system/getNameText?id=main or getLocationInfo).</summary>
    public string? FriendlyName { get; set; }

    public string DisplayName => FriendlyName ?? Info?.ModelName ?? Host;

    public IReadOnlyList<string> ZoneIds => Features?.Zone.Select(z => z.Id).ToList() ?? [Models.ZoneIds.Main];

    public ZoneApi Zone(string zone = Models.ZoneIds.Main) => _zones.GetOrAdd(zone, z => new ZoneApi(Client, z));

    public ZoneApi Main => Zone(Models.ZoneIds.Main);

    /// <summary>Reads device info, features and the room name.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Info = await System.GetDeviceInfoAsync(ct).ConfigureAwait(false);
        Features = await System.GetFeaturesAsync(ct).ConfigureAwait(false);
        FriendlyName = await GetRoomNameAsync(ct).ConfigureAwait(false) ?? FriendlyName;
    }

    /// <summary>Room name from getNameText, falling back to the network name on older firmware.</summary>
    private async Task<string?> GetRoomNameAsync(CancellationToken ct)
    {
        try
        {
            var name = await System.GetNameTextAsync(Models.ZoneIds.Main, ct).ConfigureAwait(false);
            return name.Text ?? name.ZoneList?.FirstOrDefault(z => z.Id == Models.ZoneIds.Main)?.Text;
        }
        catch (Exception ex) when (ex.IsDeviceError && !ct.IsCancellationRequested)
        {
        }

        try
        {
            var net = await System.GetNetworkStatusAsync(ct).ConfigureAwait(false);
            return string.IsNullOrEmpty(net.NetworkName) ? null : net.NetworkName;
        }
        catch (Exception ex) when (ex.IsDeviceError && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Which API family (netusb / tuner / cd / none) provides now-playing info for <paramref name="input"/>.</summary>
    public string GetPlayInfoType(string input)
        => Features?.FindInput(input)?.PlayInfoType ?? InferPlayInfoType(input);

    /// <summary>
    /// Enables UDP event notifications to <paramref name="port"/> on this host.
    /// The device keeps sending events for ~10 minutes after the last request carrying the headers,
    /// so call this periodically (e.g. every 5 minutes) or keep issuing requests.
    /// </summary>
    public async Task SubscribeEventsAsync(int port, CancellationToken ct = default)
    {
        Client.EventPort = port;
        await System.KeepEventAliveAsync(ct).ConfigureAwait(false);
    }

    public Uri? ResolveUrl(string? url) => Client.ResolveUrl(url);

    internal static string InferPlayInfoType(string input) => input switch
    {
        "tuner" or "am" or "fm" or "dab" => PlayInfoTypes.Tuner,
        "cd" => PlayInfoTypes.Cd,
        "net_radio" or "server" or "usb" or "bluetooth" or "airplay" or "spotify" or "deezer" or "napster" or "pandora"
            or "siriusxm" or "juke" or "tidal" or "qobuz" or "amazon_music" or "alexa" or "mc_link" or "radiko"
            or "rhapsody" or "playqueue" or "mc_playlist" or "qplay" or "chromecast" => PlayInfoTypes.NetUsb,
        _ => PlayInfoTypes.None,
    };

    public override string ToString() => $"{DisplayName} ({Host})";

    public void Dispose() => Client.Dispose();
}
