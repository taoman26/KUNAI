using KUNAI.Core.Shortcuts;

namespace KUNAI.Platform.Linux.Native;

/// <summary><see cref="KeyCode"/> から Linux evdev のキーコード(input-event-codes.h の KEY_*)への変換表。</summary>
internal static class LinuxKeyCodes
{
    public const ushort KeyLeftCtrl = 29;
    public const ushort KeyLeftShift = 42;
    public const ushort KeyLeftAlt = 56;
    public const ushort KeyLeftMeta = 125;

    private static readonly Dictionary<KeyCode, ushort> Table = Build();

    /// <summary>仮想キーボードに「持っている」と宣言する全キーコード。</summary>
    public static IEnumerable<ushort> AllCodes =>
        Table.Values.Concat(new[] { KeyLeftCtrl, KeyLeftShift, KeyLeftAlt, KeyLeftMeta }).Distinct();

    public static bool TryGet(KeyCode key, out ushort code) => Table.TryGetValue(key, out code);

    private static Dictionary<KeyCode, ushort> Build()
    {
        var map = new Dictionary<KeyCode, ushort>
        {
            [KeyCode.A] = 30, [KeyCode.B] = 48, [KeyCode.C] = 46, [KeyCode.D] = 32, [KeyCode.E] = 18,
            [KeyCode.F] = 33, [KeyCode.G] = 34, [KeyCode.H] = 35, [KeyCode.I] = 23, [KeyCode.J] = 36,
            [KeyCode.K] = 37, [KeyCode.L] = 38, [KeyCode.M] = 50, [KeyCode.N] = 49, [KeyCode.O] = 24,
            [KeyCode.P] = 25, [KeyCode.Q] = 16, [KeyCode.R] = 19, [KeyCode.S] = 31, [KeyCode.T] = 20,
            [KeyCode.U] = 22, [KeyCode.V] = 47, [KeyCode.W] = 17, [KeyCode.X] = 45, [KeyCode.Y] = 21,
            [KeyCode.Z] = 44,

            // KEY_1..KEY_9 = 2..10, KEY_0 = 11
            [KeyCode.D1] = 2, [KeyCode.D2] = 3, [KeyCode.D3] = 4, [KeyCode.D4] = 5, [KeyCode.D5] = 6,
            [KeyCode.D6] = 7, [KeyCode.D7] = 8, [KeyCode.D8] = 9, [KeyCode.D9] = 10, [KeyCode.D0] = 11,

            // KEY_F1..KEY_F10 = 59..68, KEY_F11 = 87, KEY_F12 = 88
            [KeyCode.F1] = 59, [KeyCode.F2] = 60, [KeyCode.F3] = 61, [KeyCode.F4] = 62, [KeyCode.F5] = 63,
            [KeyCode.F6] = 64, [KeyCode.F7] = 65, [KeyCode.F8] = 66, [KeyCode.F9] = 67, [KeyCode.F10] = 68,
            [KeyCode.F11] = 87, [KeyCode.F12] = 88,

            [KeyCode.Escape] = 1,
            [KeyCode.Backspace] = 14,
            [KeyCode.Tab] = 15,
            [KeyCode.Enter] = 28,
            [KeyCode.Space] = 57,

            [KeyCode.Home] = 102, [KeyCode.Up] = 103, [KeyCode.PageUp] = 104,
            [KeyCode.Left] = 105, [KeyCode.Right] = 106, [KeyCode.End] = 107,
            [KeyCode.Down] = 108, [KeyCode.PageDown] = 109,
            [KeyCode.Insert] = 110, [KeyCode.Delete] = 111,

            [KeyCode.Minus] = 12,
            [KeyCode.Equals] = 13,
            [KeyCode.Plus] = 13, // KEY_EQUAL(Windows 実装と同様、'=' と同じキー)
            [KeyCode.BracketLeft] = 26,
            [KeyCode.BracketRight] = 27,
            [KeyCode.Semicolon] = 39,
            [KeyCode.Quote] = 40,
            [KeyCode.Grave] = 41,
            [KeyCode.Backslash] = 43,
            [KeyCode.Comma] = 51,
            [KeyCode.Period] = 52,
            [KeyCode.Slash] = 53,

            [KeyCode.PrintScreen] = 99, // KEY_SYSRQ(X11 では Print に解決される)

            [KeyCode.VolumeMute] = 113,
            [KeyCode.VolumeDown] = 114,
            [KeyCode.VolumeUp] = 115,
            [KeyCode.MediaPlayPause] = 164,
            [KeyCode.MediaNext] = 163,
            [KeyCode.MediaPrevious] = 165,

            [KeyCode.BrowserBack] = 158,
            [KeyCode.BrowserForward] = 159,
            [KeyCode.BrowserHome] = 172,
            [KeyCode.BrowserRefresh] = 173
        };

        return map;
    }
}
