[English](README.md) | **日本語**

# mcctl (unofficial controller for MusicCast, Windows)

**mcctl** は、ヤマハの MusicCast 対応機器 (スピーカー、AV レシーバー、サウンドバー、ネットワークプレーヤー) を Windows から操作する非公式のコントローラーです。

リポジトリ: <https://github.com/mimura1133/mcctl>

> mcctl は独立したプロジェクトであり、ヤマハ株式会社とは関係がなく、同社による承認・後援を受けたものではありません。
> Yamaha および MusicCast は、ヤマハ株式会社の商標または登録商標です。

```
Mcctl.slnx
├─ src/Mcctl.Core   … プロトコルライブラリ (net11.0、外部パッケージなし)
├─ src/Mcctl.App    … Windows GUI、mcctl-gui.exe (WPF、Fluent テーマ、日本語 / 英語)
├─ src/Mcctl.Cli    … コマンドラインツール mcctl.exe (デバッグ・スクリプト用)
├─ tests/Mcctl.Core.Tests … xUnit テスト (疑似 HTTP デバイス)
├─ docs/library.ja.md … Mcctl.Core ライブラリの使い方
└─ tools/gen_resx.py … strings.tsv から UI 文字列リソースを生成
```

## ビルド

```bash
git clone https://github.com/mimura1133/mcctl.git
cd mcctl
dotnet build src/Mcctl.App -c Release
```

出力: `src/Mcctl.App/bin/Release/net11.0-windows/mcctl-gui.exe`

テストは NuGet パッケージの取得が必要なため、使い捨てのコンテナで実行します。

```bash
docker run --rm -v "%CD%:/src:ro" mcr.microsoft.com/dotnet/sdk:11.0 bash -c "cp -r /src /w && cd /w && dotnet test tests/Mcctl.Core.Tests"
```

## 機能 (GUI)

| 分類 | できること |
|---|---|
| ルーム | SSDP による自動検出、IP アドレスでの手動追加、一覧での電源状態表示、ルーム一覧の折りたたみ |
| 基本操作 | 電源 オン / スタンバイ、音量 (スライダーと ±)、ミュート、入力切替 (入力は ABC 順)、サウンドプログラム、スリープタイマー、ゾーン切替 (メイン / ゾーン 2 …) |
| 再生中 | 曲名・アーティスト・アルバム、アートワーク (切り取らずに全体を表示)、オーディオフォーマット (例: 「MP3 · 44.1 kHz」)、経過時間、再生 / 一時停止 / 前へ / 次へ / 停止、シャッフル / リピート、CD トレイ |
| ブラウズ | ネットラジオ、サーバー (DLNA)、USB、radiko、ストリーミングサービスなどのフォルダー階層の移動と再生、検索 (例: ネットラジオ → ラジオ → 検索する) |
| プリセット | ネットワークのお気に入りの呼び出し・登録、最近再生した項目の呼び出し、チューナープリセットの呼び出し |
| チューナー | AM / FM / DAB の切替、周波数の上下、自動選局、直接選局、プリセットの送り |
| サウンド | 低音 / 高音、ピュアダイレクト・エンハンサー・クリアボイス・バスエクステンションなどの切替 |
| リンク | MusicCast Link (マルチルーム): 他のルームをこのルームにリンク、リンク解除、グループの解散 |
| ミニプレーヤー | ウィンドウが 720 × 480 より小さくなると、コンパクトなプレーヤー表示に切り替わります (アートワーク、曲名、再生操作、音量、入力、常に手前に表示) |
| 表示言語 | 日本語 / 英語。ルーム一覧の下部から実行中に切り替えられます (「システム」は Windows の表示言語に従います) |
| 状態の自動反映 | UDP のプッシュ通知を受信。受信できない場合は 15 秒ごとのポーリングで反映 |

設定は `%APPDATA%\mcctl\` に保存されます: `hosts.json` (IP アドレスで追加したルーム)、`settings.json` (表示言語)。

### UI 文字列

UI の文言は `src/Mcctl.App/Localization/strings.tsv` (キー、日本語、英語) で管理しています。編集後はリソースを再生成してください (Python 3 が必要です)。

```bash
python tools/gen_resx.py src/Mcctl.App/Localization
```

## ライブラリの使い方

C# から機器を操作するライブラリ (Mcctl.Core) の使い方は [docs/library.ja.md](docs/library.ja.md) を参照してください。

## CLI (mcctl)

```
mcctl discover [--verbose]
mcctl status 192.168.1.20
mcctl volume 192.168.1.20 25
mcctl input 192.168.1.20 spotify
mcctl list 192.168.1.20 --input net_radio
mcctl select 192.168.1.20 0 --input net_radio
mcctl search 192.168.1.20 1 jazz --input net_radio
mcctl link 192.168.1.20 192.168.1.21
mcctl events 192.168.1.20
mcctl raw 192.168.1.20 "netusb/getRecentInfo"
mcctl raw 192.168.1.20 netusb/setSearchString "{\"string\":\"jazz\"}"
```

## 未対応の機能 / 制限事項
- ストリーミングサービスのアカウントへのサインイン (Spotify などは、各サービスのアプリから Connect の出力先として利用できます)
- 機器の初期ネットワーク設定 (Wi-Fi 設定、ファームウェア更新)、アラームと時計の設定、プレイリストと再生キューの編集
- ヤマハ R-N1000A で動作を確認しています (検出、状態、サーバー / ネットラジオ / radiko のブラウズ、検索、アートワーク、オーディオフォーマット)。
- 次の項目は未テストです。
  - MusicCast Link
  - サウンド機能 (トーンコントロール、ピュアダイレクト、エンハンサーなどの切替)
  - CD ドライブ内蔵機器での CD 再生 (トラック情報、再生操作、トレイ開閉)
  - プリセットの登録・呼び出し
  - スリープタイマー

## ライセンス

[MIT License](LICENSE) です。このライセンスは、ヤマハの商標に関する権利を許諾するものではありません。
