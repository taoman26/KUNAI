namespace KUNAI.Core.Domain;

/// <summary>ショートカット送出ではなく、OS 機能を呼び出す標準アクション。</summary>
public enum StandardAction
{
    LaunchBrowser,
    LaunchTerminal,
    VolumeUp,
    VolumeDown,
    Mute,
    LockScreen,
    BrowserBack,
    BrowserForward
}
