# KUNAI アーキテクチャ

## プロジェクト依存関係（矢印は「参照する」）

```
                  ┌─────────────────────────────┐
                  │ KUNAI.UI (Composition Root) │  Avalonia / MVVM / Program.cs / DI
                  └──┬───────┬───────┬───────┬──┘
                     ▼       ▼       ▼       ▼
              ┌──────────┐ ┌────────┐ ┌──────────────────────────┐
              │  Input   │ │Actions │ │ Platform.Windows / Linux │
              │(HidSharp)│ │        │ │ SyntheticKeyboard 等     │
              └────┬─────┘ └───┬────┘ └────────────┬─────────────┘
                   └───────────┼───────────────────┘
                               ▼
                  ┌─────────────────────────────┐
                  │ KUNAI.Core （参照ゼロ）      │  ドメイン / 抽象 / LayerManager / ProfileRepository
                  └─────────────────────────────┘

  KUNAI.Tests → Core, Input, Actions, Platform.*, UI
```

依頼仕様の `UI → Core → Input → Platform` は、文字どおりだと Core が Platform を参照してしまうため、
**Core を最内層**にして Input / Actions / Platform を Core の抽象の実装側に置いた（依存性逆転）。

## 実行時のデータフロー

```
Wii Remote ─(Bluetooth HID)→ WiimoteService [Input]
                                   │ ButtonPressed / ButtonReleased
                                   ▼
                      InputOrchestrator [Core]
                       ├─→ ILayerManager      押下状態 → ActiveLayer
                       ├─→ Profile.FindMapping(押した時点のレイヤー, ボタン)
                       ├─→ IActionExecutor    [Actions]  単一アクション
                       │      ├─ 標準アクション名 → ISystemActions   [Platform]
                       │      └─ ショートカット   → ISyntheticKeyboard [Platform]
                       ├─→ IMacroExecutor     [Actions]  Delay(ms) 対応・事前検証
                       └─→ ILogService        → UI のログ（最大 1000 件）
```

- アクションは入力スレッドとは別のタスクで、押した順に 1 つずつ実行する。
- 実行するレイヤーは「押した時点」のレイヤーで決まる。
- アクションの失敗は `Error: ...` としてログに残り、後続は止まらない。

## レイヤー遷移

状態は「B / Home が押されているか」の 2 フラグだけを持ち、`ActiveLayer = (B ? 1 : 0) | (Home ? 2 : 0)` で毎回導出する。
押す順序・離す順序に依存せず、切断時は `Reset()` で Layer0 に戻る。

## 設計判断

| 判断 | 理由 |
|---|---|
| `ButtonMapping.Macro`（`List<string>?`）を追加 | 仕様の `Action: string` とマクロ配列を両立させ、JSON を読みやすくする |
| `ISystemActions`, `IProcessLauncher`, `IUiDispatcher`, `IDelayProvider` を Core に追加 | OS 機能・プロセス起動・UI スレッド・待機を抽象化し、テスト可能にする |
| `ShortcutParser` を Core に置く | Windows / Linux の両キーボードが解析を共有し、OS 固有部分を変換表だけにする |
| `IWiimoteTransport` で HidSharp を分離 | 実機なしで接続・読み取り・切断・再接続をテストできる |
| 画面は `x:Class` なしの `MainWindow.axaml` | 「コードビハインド禁止」を守る（残る `App.axaml.cs` は起動・終了のつなぎ込みのみ） |
| 切替時・終了時は検証なしで自動保存、Save ボタンのみ検証 | 編集中の内容を失わない |
| B / Home はアクション割り当て不可 | レイヤー切替専用にして挙動を単純にする |
