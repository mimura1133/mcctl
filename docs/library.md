**English** | [日本語](library.ja.md)

# Using the Mcctl.Core library

`Mcctl.Core` is a .NET library (net11.0, no external packages) for controlling Yamaha MusicCast–compatible devices over the LAN. It is what `mcctl-gui.exe` and `mcctl.exe` are built on. Reference `src/Mcctl.Core/Mcctl.Core.csproj` from your project.

## Find a device and control it

```csharp
using Mcctl;
using Mcctl.Api;
using Mcctl.Discovery;

var found = await MusicCastDiscovery.DiscoverAsync();
using var dev = new MusicCastDevice(found[0].Host);
await dev.InitializeAsync();                     // device info, capabilities, room name

await dev.Main.SetPowerAsync(PowerState.On);
await dev.Main.SetInputAsync("net_radio");
await dev.Main.SetVolumeAsync(30);
await dev.NetUsb.RecallPresetAsync(1);
var now = await dev.NetUsb.GetPlayInfoAsync();   // now.Track / now.Artist / dev.ResolveUrl(now.AlbumartUrl)
```

`dev.Main` (or `dev.Zone("zone2")`) controls a zone; `dev.NetUsb`, `dev.Tuner`, `dev.Cd` and `dev.Distribution` cover the other device functions. Device errors are thrown as `YxcException`; `ex.IsDeviceError` is true for any device or network failure.

## Browse and search

```csharp
var list = await dev.NetUsb.GetFullListAsync("net_radio");          // current layer, all lines
await dev.NetUsb.EnterFolderAsync("net_radio", 0);                  // open "Radio"
var results = await dev.NetUsb.SearchAsync("net_radio", 1, "jazz"); // Radio → Search
await dev.NetUsb.PlayItemAsync("net_radio", 3);                     // play a result
await dev.NetUsb.ReturnAsync("net_radio");                          // go back one layer
```

`ListItem.IsContainer`, `IsPlayable` and `IsSearchable` tell you what each line is. The helpers wait until the device has finished loading the new list.

## Push notifications

```csharp
using Mcctl.Events;

await using var listener = new MusicCastEventListener();
listener.EventReceived += (_, e) => Console.WriteLine(e.Raw);
listener.Start();
await dev.SubscribeEventsAsync(listener.Port);   // resend at least every 10 minutes
```

## MusicCast Link

```csharp
using var kitchen = new MusicCastDevice("192.168.1.21");
await MusicCastLink.LinkAsync(dev, [kitchen]);   // dev becomes the group master
await MusicCastLink.UnlinkAsync(dev, [kitchen]);
```

MusicCast Link is untested.
