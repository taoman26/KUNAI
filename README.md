# KUNAI — Programmable Wiimote Macro Pad

Nintendo Wii Remote を、プログラマ向けの **左手デバイス**(マクロパッド)にするクロスプラットフォームアプリです。
QMK のレイヤー、Stream Deck のアクション、TourBox の片手操作の考え方を、安価で入手しやすい Wii Remote で実現します。

- Windows 11 / Ubuntu 22.04+ / Xubuntu 22.04+ / Debian 系 Linux(X11 優先)
- .NET 8 LTS + Avalonia 11(MVVM は CommunityToolkit.Mvvm、ReactiveUI は不使用)
- プロファイルは JSON。ショートカット、OS 機能、マクロ(`Delay` 対応)を割り当て可能

> **状態: MVP(実機未検証の部分あり)** — 開発環境(WSL2)には Wii Remote も Bluetooth も無いため、
> Wii Remote との実通信、Windows の `SendInput`、Linux の `uinput` への実書き込みは **まだ実機で動作確認していません**。
> 検証済みの範囲は [検証状況](#検証状況) を参照してください。

## レイヤー

B と Home を押している間だけ切り替わる、QMK 風のモメンタリーレイヤーです。離すと元のレイヤーへ戻ります。

| レイヤー | 条件 | 例: A | 例: 1 | 例: 2 |
|---|---|---|---|---|
| Layer 0 | 通常 | `Enter` | `Ctrl+C` | `Ctrl+V` |
| Layer 1 | B 押下中 | `Ctrl+Z` | `Ctrl+Shift+Z` | `Ctrl+X` |
| Layer 2 | Home 押下中 | `Alt+Tab` | `Win+D` | `BrowserBack` |
| Layer 3 | B + Home 押下中 | `LaunchTerminal` | `LaunchBrowser` | `LockScreen` |

B と Home は **レイヤー切替専用**で、アクションは割り当てられません(JSON に書いても無視されます)。

## ビルドと実行

必要なもの: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
dotnet build                       # ソリューション全体
dotnet test                        # 単体テスト(361 件)
dotnet run --project src/KUNAI.UI  # 起動(デスクトップ環境が必要)

# 配布用(フレームワーク依存。実行先に .NET 8 Runtime が必要)
dotnet publish src/KUNAI.UI -c Release -r linux-x64 --self-contained false -o out/linux
dotnet publish src/KUNAI.UI -c Release -r win-x64   --self-contained false -o out/win
```

## Wii Remote の接続

アプリの「Connect」は、OS に **接続済み(ペアリング済み)** の Wii Remote を最大 20 秒間探します。
先に OS 側の Bluetooth でペアリングしてください。

### Windows 11
1. 設定 → Bluetooth とデバイス → デバイスを追加 → Bluetooth
2. Wii Remote の **1 と 2 を同時押し**(LED が点滅している間に)して検出
3. PIN を求められたら「PIN を使わずに接続」(またはスキップ)
4. 一部の Bluetooth アダプターでは、ペアリングしても HID として認識されないことがあります

### Linux
```bash
bluetoothctl
  power on
  scan on            # Wii Remote の 1 と 2 を同時押し
  pair  <MAC>        # PIN を求められたら空のまま Enter
  connect <MAC>
  trust <MAC>
```
カーネルの `hid-wiimote` ドライバが担当します。KUNAI は HidSharp 経由で `/dev/hidraw*` からレポートを読みます。

## Linux の権限設定(uinput / hidraw)

キー送出は `/dev/uinput` に仮想キーボードを作って行うため、一般ユーザーに権限が必要です。
権限が無いと、ログに `Cannot open /dev/uinput (Permission denied)` と設定手順が出ます(アプリは落ちません)。

```bash
sudo cp tools/linux/99-kunai.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules && sudo udevadm trigger
sudo usermod -aG input "$USER"     # 再ログインが必要
sudo modprobe uinput
```

- 最初のキー送出時に仮想デバイスを作るため、**最初の 1 回だけ約 0.3 秒遅れます**。
- uinput はカーネル層で動くので、X11 でも Wayland でも使える見込みです(Wayland は未検証)。
- 画面ロックは `loginctl lock-session`(→ `xdg-screensaver lock` → `xflock4`)です。デスクトップ環境が反応しない場合は、実際にロックされない可能性があります。

## プロファイル

起動時に自動ロードし、終了時に自動保存します(切り替え時も、編集中の内容を自動保存します)。

| OS | 保存先 |
|---|---|
| Linux | `~/.config/KUNAI/profiles/` |
| Windows | `%APPDATA%\KUNAI\profiles\` |

初回起動時、保存先が空なら同梱の `default` / `coding` / `writing` / `browser` がコピーされます。

```json
{
  "name": "coding",
  "layers": [
    {
      "layerNumber": 0,
      "mappings": [
        { "button": "A",    "action": "Enter" },
        { "button": "One",  "action": "Ctrl+C" },
        { "button": "Plus", "action": "", "macro": ["Ctrl+S", "Delay(300)", "Alt+Tab"] }
      ]
    }
  ]
}
```

- `button`: `A` `One` `Two` `Plus` `Minus` `Up` `Down` `Left` `Right`(`B` と `Home` は不可)
- `action`: ショートカットまたは標準アクション名
- `macro`: 指定すると `action` より優先されます。`Delay(ミリ秒)` で待機(0〜60000)
- 画面の Action 欄では、マクロを `Ctrl+S; Delay(300); Alt+Tab` のようにセミコロン区切りで入力します

### ショートカット
`Ctrl+Shift+T` のように `+` でつなぎます(大文字小文字は区別しません)。

- 修飾キー: `Ctrl` `Shift` `Alt` `Win`(`Super` / `Meta` / `Cmd` も同じ)
- キー: `A`〜`Z`、`0`〜`9`、`F1`〜`F12`、`Enter` `Escape` `Tab` `Space` `Backspace` `Delete` `Insert`、
  `Home` `End` `PageUp` `PageDown`、`Up` `Down` `Left` `Right`、記号(`- = , . / \ ; ' [ ] \``)、
  `BrowserBack` `BrowserForward` `BrowserRefresh` `BrowserHome`、`VolumeUp` `VolumeDown` `VolumeMute`、
  `MediaPlayPause` `MediaNext` `MediaPrevious`、`PrintScreen`

### 標準アクション
| 名前 | Windows | Linux |
|---|---|---|
| `LaunchBrowser` | 既定ブラウザで URL を開く | `xdg-open <URL>` |
| `LaunchTerminal` | `wt.exe` → `powershell.exe` → `cmd.exe` | `x-terminal-emulator` → `gnome-terminal` → `konsole` → `xfce4-terminal` → `xterm` |
| `VolumeUp` / `VolumeDown` / `Mute` | メディアキー送出 | `pactl`(±5%)。無ければメディアキー |
| `LockScreen` | `LockWorkStation` | `loginctl lock-session` ほか |
| `BrowserBack` / `BrowserForward` | ブラウザキー送出 | `Alt+←` / `Alt+→` |

標準アクション名はショートカットより優先されます(`BrowserBack` は標準アクションとして扱われます)。

## アーキテクチャ

```
KUNAI.UI (Composition Root / Avalonia / MVVM)
   ├─→ KUNAI.Input            HidSharp による Wii Remote 接続          ─┐
   ├─→ KUNAI.Actions          ActionExecutor / MacroExecutor            ├─→ KUNAI.Core（参照ゼロ）
   ├─→ KUNAI.Platform.Windows SendInput / LockWorkStation               │    ドメイン・抽象・LayerManager
   └─→ KUNAI.Platform.Linux   uinput / xdg-open / pactl / loginctl     ─┘    ProfileRepository 等
```

Core は Platform を参照せず、抽象(`ISyntheticKeyboard`, `ISystemActions`, `IWiimoteService` など)だけを持ちます。
実装は UI の `Program.cs`(DI)で OS に応じて結び付けます。
詳細は [docs/architecture.md](docs/architecture.md) と [docs/class-diagram.md](docs/class-diagram.md) を参照。

## 検証状況

| 項目 | 状況 |
|---|---|
| ビルド(Linux)/ publish(linux-x64, win-x64) | 確認済み |
| 単体テスト 361 件(行カバレッジ 約 79%) | 合格 |
| 画面描画(ヘッドレス)・DI 解決・プロファイル読み書き | 確認済み |
| Wii Remote の実接続・ボタン読み取り・電池取得 | **未検証**(Fake の転送層でのみテスト) |
| Windows `SendInput` によるキー送出 / `LockWorkStation` | **未検証**(イベント列・構造体サイズのみテスト) |
| Linux `uinput` への実書き込み | **未検証**(`/dev/uinput` の権限が無い環境で開発) |
| Linux のブラウザ/端末/音量/ロックの外部コマンド | **未検証**(起動コマンドと引数のみテスト) |
| 実ウィンドウでの操作(マウス・キーボード) | **未検証** |

## 既知の制限と今後

- Wii Remote を **横持ち**にした場合の十字キー回転は未対応(本体の物理位置どおり)
- 1 台のみ対応(複数台の同時接続は未対応)
- Wayland 専用の対応、アプリ別プロファイル自動切替、Nunchuk / MotionPlus、振る・傾けるジェスチャー、ダブルタップは将来拡張
  (インターフェースは `IGestureRecognizer` のみ定義済み)
- ログの最大件数は 1000 件、Delay は 1 回あたり最大 60 秒

## ライセンス

[MIT License](LICENSE)
