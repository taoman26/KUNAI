namespace KUNAI.Core.Shortcuts;

/// <summary>OS 非依存のキー識別子。各プラットフォーム実装が VK / evdev コードへ変換する。</summary>
public enum KeyCode
{
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    Enter, Escape, Tab, Space, Backspace, Delete, Insert,
    Home, End, PageUp, PageDown,
    Up, Down, Left, Right,

    Minus, Equals, Comma, Period, Slash, Backslash,
    Semicolon, Quote, BracketLeft, BracketRight, Grave,
    Plus,

    BrowserBack, BrowserForward, BrowserRefresh, BrowserHome,
    VolumeUp, VolumeDown, VolumeMute,
    MediaPlayPause, MediaNext, MediaPrevious,
    PrintScreen
}
