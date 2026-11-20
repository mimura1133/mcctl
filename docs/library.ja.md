[English](library.md) | **日本語**

# Mcctl.Core ライブラリの使い方

`Mcctl.Core` は、ヤマハの MusicCast 対応機器を LAN 経由で操作するための .NET ライブラリです (net11.0、外部パッケージなし)。`mcctl-gui.exe` と `mcctl.exe` もこのライブラリの上に作られています。利用するプロジェクトから `src/Mcctl.Core/Mcctl.Core.csproj` を参照してください。

## 機器を見つけて操作する

```csharp
using Mcctl;
using Mcctl.Api;
using Mcctl.Discovery;

var found = await MusicCastDiscovery.DiscoverAsync();
using var dev = new MusicCastDevice(found[0].Host);
await dev.InitializeAsync();                     // 機器情報、機能情報、ルーム名

await dev.Main.SetPowerAsync(PowerState.On);
await dev.Main.SetInputAsync("net_radio");
await dev.Main.SetVolumeAsync(30);
await dev.NetUsb.RecallPresetAsync(1);
var now = await dev.NetUsb.GetPlayInfoAsync();   // now.Track / now.Artist / dev.ResolveUrl(now.AlbumartUrl)
```

`dev.Main` (または `dev.Zone("zone2")`) でゾーンを操作し、`dev.NetUsb`・`dev.Tuner`・`dev.Cd`・`dev.Distribution` でその他の機能を扱います。機器がエラーを返すと `YxcException` が発生します。機器や通信が原因の失敗では `ex.IsDeviceError` が true になります。

## ブラウズと検索

```csharp
var list = await dev.NetUsb.GetFullListAsync("net_radio");          // 現在の階層の全項目
await dev.NetUsb.EnterFolderAsync("net_radio", 0);                  // 「ラジオ」を開く
var results = await dev.NetUsb.SearchAsync("net_radio", 1, "jazz"); // ラジオ → 検索する
await dev.NetUsb.PlayItemAsync("net_radio", 3);                     // 結果を再生
await dev.NetUsb.ReturnAsync("net_radio");                          // 1 階層戻る
```

各項目の種類は `ListItem.IsContainer`・`IsPlayable`・`IsSearchable` で判別できます。これらのメソッドは、機器が新しいリストを読み込み終えるまで待ってから戻ります。

## プッシュ通知

```csharp
using Mcctl.Events;

await using var listener = new MusicCastEventListener();
listener.EventReceived += (_, e) => Console.WriteLine(e.Raw);
listener.Start();
await dev.SubscribeEventsAsync(listener.Port);   // 10 分以内ごとに再送が必要
```

## MusicCast Link

```csharp
using var kitchen = new MusicCastDevice("192.168.1.21");
await MusicCastLink.LinkAsync(dev, [kitchen]);   // dev がグループのマスターになります
await MusicCastLink.UnlinkAsync(dev, [kitchen]);
```

MusicCast Link は未テストです。
