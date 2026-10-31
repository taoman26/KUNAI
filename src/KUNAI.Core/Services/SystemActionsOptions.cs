namespace KUNAI.Core.Services;

/// <summary>標準アクション(<see cref="Domain.StandardAction"/>)の挙動を調整する設定。</summary>
/// <param name="BrowserUrl">LaunchBrowser で開く URL。既定ブラウザで開かれる。</param>
/// <param name="TerminalCommand">LaunchTerminal で優先して使うコマンド。null なら OS ごとの候補を順に試す。</param>
/// <param name="VolumeStepPercent">VolumeUp / VolumeDown 1 回あたりの音量変化(Linux のみ。Windows は OS 既定の刻み)。</param>
public sealed record SystemActionsOptions(
    string BrowserUrl = "https://duckduckgo.com",
    string? TerminalCommand = null,
    int VolumeStepPercent = 5);
