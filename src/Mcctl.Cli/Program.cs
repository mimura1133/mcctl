using System.Text.Json;
using Mcctl;
using Mcctl.Api;
using Mcctl.Discovery;
using Mcctl.Events;

// mcctl – command line front end for Mcctl.Core (handy for scripting and for checking the protocol).
Console.OutputEncoding = System.Text.Encoding.UTF8;
if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

var cmd = args[0].ToLowerInvariant();
try
{
    if (cmd == "discover")
    {
        var verbose = args.Contains("--verbose") || args.Contains("-v");
        var found = await MusicCastDiscovery.DiscoverAsync(TimeSpan.FromSeconds(4),
            log: verbose ? m => Console.Error.WriteLine(m) : null);
        foreach (var d in found)
        {
            Console.WriteLine($"{d.Host,-16} {d.ModelName,-14} {d.FriendlyName}");
        }

        if (found.Count == 0)
        {
            Console.WriteLine("No MusicCast device found.");
        }

        return 0;
    }

    if (args.Length < 2)
    {
        PrintUsage();
        return 1;
    }

    using var dev = new MusicCastDevice(args[1]);
    var zone = Opt("--zone") ?? "main";

    // Optional: make every request register for UDP events (to debug notifications).
    if (Opt("--event-port") is { } eventPort)
    {
        dev.Client.EventPort = int.Parse(eventPort);
    }

    if (Opt("--app-name") is { } appName)
    {
        dev.Client.EventAppName = appName;
    }
    string Arg(int i) => args.Length > i ? args[i] : throw new ArgumentException($"Missing argument #{i}.");
    async Task<string> ResolveInputAsync() => Opt("--input") ?? (await dev.Zone(zone).GetStatusAsync()).Input;

    switch (cmd)
    {
        case "info":
            await dev.InitializeAsync();
            Dump(dev.Info);
            Console.WriteLine($"Name   : {dev.DisplayName}");
            Console.WriteLine($"Zones  : {string.Join(", ", dev.ZoneIds)}");
            Console.WriteLine($"Inputs : {string.Join(", ", dev.Features!.FindZone(zone)?.InputList ?? [])}");
            Console.WriteLine($"Sound  : {string.Join(", ", dev.Features!.FindZone(zone)?.SoundProgramList ?? [])}");
            break;
        case "features":
            Dump(await dev.System.GetFeaturesAsync());
            break;
        case "status":
            Dump(await dev.Zone(zone).GetStatusAsync());
            break;
        case "playinfo":
            Dump(await dev.NetUsb.GetPlayInfoAsync());
            break;
        case "power":
            await dev.Zone(zone).SetPowerAsync(Arg(2) switch { "on" => PowerState.On, "off" or "standby" => PowerState.Standby, _ => PowerState.Toggle });
            break;
        case "volume":
            var v = Arg(2);
            if (v == "up") await dev.Zone(zone).VolumeUpAsync();
            else if (v == "down") await dev.Zone(zone).VolumeDownAsync();
            else await dev.Zone(zone).SetVolumeAsync(int.Parse(v));
            break;
        case "mute":
            await dev.Zone(zone).SetMuteAsync(Arg(2) is "on" or "true" or "1");
            break;
        case "input":
            await dev.Zone(zone).SetInputAsync(Arg(2));
            break;
        case "program":
            await dev.Zone(zone).SetSoundProgramAsync(Arg(2));
            break;
        case "play": await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Play); break;
        case "pause": await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Pause); break;
        case "stop": await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Stop); break;
        case "next": await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Next); break;
        case "prev": await dev.NetUsb.SetPlaybackAsync(PlaybackCommand.Previous); break;
        case "presets":
            var presets = await dev.NetUsb.GetPresetInfoAsync();
            for (var i = 0; i < presets.PresetInfo.Count; i++)
            {
                if (!presets.PresetInfo[i].IsEmpty)
                {
                    Console.WriteLine($"{i + 1,3}: [{presets.PresetInfo[i].Input}] {presets.PresetInfo[i].Text}");
                }
            }

            break;
        case "recall":
            await dev.NetUsb.RecallPresetAsync(int.Parse(Arg(2)), zone);
            break;
        case "list":
            var input = await ResolveInputAsync();
            var list = await dev.NetUsb.GetFullListAsync(input);
            Console.WriteLine($"[{list.MenuName}] layer={list.MenuLayer} lines={list.MaxLine}");
            for (var i = 0; i < list.ListInfoItems.Count; i++)
            {
                Console.WriteLine($"{i,4}: {list.ListInfoItems[i].Text}  (attr=0x{list.ListInfoItems[i].Attribute:X})");
            }

            break;
        case "select":
        {
            var inp = await ResolveInputAsync();
            var after = await dev.NetUsb.EnterFolderAsync(inp, int.Parse(Arg(2)), zone);
            Console.WriteLine($"[{after.MenuName}] layer={after.MenuLayer} lines={after.MaxLine}");
            break;
        }
        case "back":
        {
            var inp = await ResolveInputAsync();
            var after = await dev.NetUsb.ReturnAsync(inp, zone);
            Console.WriteLine($"[{after.MenuName}] layer={after.MenuLayer} lines={after.MaxLine}");
            break;
        }
        case "playitem":
        {
            var inp = await ResolveInputAsync();
            await dev.NetUsb.PlayItemAsync(inp, int.Parse(Arg(2)), zone);
            break;
        }
        case "search":
        {
            // mcctl search <host> <index of the search item> <text> [--input id]
            var inp = await ResolveInputAsync();
            var text = string.Join(' ', args.Skip(3).TakeWhile(a => !a.StartsWith("--")));
            var after = await dev.NetUsb.SearchAsync(inp, int.Parse(Arg(2)), text, zone);
            Console.WriteLine($"[{after.MenuName}] layer={after.MenuLayer} lines={after.MaxLine}");
            break;
        }
        case "dist":
            Dump(await dev.Distribution.GetDistributionInfoAsync());
            break;
        case "link":
            var clients = args.Skip(2).Where(a => !a.StartsWith("--")).Select(h => new MusicCastDevice(h)).ToList();
            var gid = await MusicCastLink.LinkAsync(dev, clients, zone);
            Console.WriteLine($"Linked. group_id={gid}");
            clients.ForEach(c => c.Dispose());
            break;
        case "unlink":
            var rm = args.Skip(2).Where(a => !a.StartsWith("--")).Select(h => new MusicCastDevice(h)).ToList();
            if (rm.Count == 0) await MusicCastLink.DissolveAsync(dev, []);
            else await MusicCastLink.UnlinkAsync(dev, rm);
            rm.ForEach(c => c.Dispose());
            break;
        case "events":
            await using (var listener = new MusicCastEventListener(int.Parse(Opt("--port") ?? MusicCastEventListener.DefaultPort.ToString())))
            {
                listener.EventReceived += (_, e) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {e}");
                listener.Start();
                await dev.SubscribeEventsAsync(listener.Port);
                Console.WriteLine($"Listening on UDP {listener.Port}. Ctrl+C to quit.");
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
                while (!cts.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromMinutes(5), cts.Token); }
                    catch (OperationCanceledException) { break; }
                    await dev.System.KeepEventAliveAsync();
                }
            }

            break;
        case "raw":
            // mcctl raw <host> netusb/getPlayInfo                    (GET)
            // mcctl raw <host> netusb/setSearchString '{"string":"x"}' (POST with a JSON body)
            var resp = args.Length > 3 && !args[3].StartsWith("--")
                ? await dev.Client.PostAsync(Arg(2), JsonDocument.Parse(Arg(3)).RootElement)
                : await dev.Client.GetAsync(Arg(2));
            Dump(resp);
            break;
        default:
            PrintUsage();
            return 1;
    }

    return 0;
}
catch (YxcException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or FormatException)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 3;
}

string? Opt(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static void Dump(object? o) => Console.WriteLine(JsonSerializer.Serialize(o, Cli.PrettyJson));

static void PrintUsage() => Console.WriteLine("""
    mcctl - command-line controller for Yamaha MusicCast devices

      mcctl discover
      mcctl info|features|status|playinfo|dist <host> [--zone main]
      mcctl power <host> on|standby|toggle [--zone z]
      mcctl volume <host> <n>|up|down       mcctl mute <host> on|off
      mcctl input <host> <input_id>          mcctl program <host> <sound_program>
      mcctl play|pause|stop|next|prev <host>
      mcctl presets <host>                   mcctl recall <host> <num>
      mcctl list|back <host> [--input id]    mcctl select|playitem <host> <index> [--input id]
      mcctl search <host> <index> <text> [--input id]
      mcctl link <server> <client> [...]     mcctl unlink <server> [client ...]
      mcctl events <host> [--port 41100]     mcctl raw <host> <path?query> [json-body]
      options: --zone <id>  --event-port <udp port> --app-name <name>  (send event registration headers)
    """);

/// <summary>Shared state for the top-level program (options are cached; creating them per call is costly).</summary>
internal static class Cli
{
    public static readonly JsonSerializerOptions PrettyJson = new(YxcJson.Options) { WriteIndented = true };
}
