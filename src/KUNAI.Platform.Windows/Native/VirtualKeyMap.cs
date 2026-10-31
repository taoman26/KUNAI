using KUNAI.Core.Shortcuts;

namespace KUNAI.Platform.Windows.Native;

/// <summary><see cref="KeyCode"/> から Windows の仮想キーコード(VK_*)への変換表。</summary>
internal static class VirtualKeyMap
{
    public const ushort VkLControl = 0xA2;
    public const ushort VkLShift = 0xA0;
    public const ushort VkLMenu = 0xA4;
    public const ushort VkLWin = 0x5B;

    private static readonly Dictionary<KeyCode, (ushort Vk, bool Extended)> Table = Build();

    public static bool TryGet(KeyCode key, out ushort virtualKey, out bool isExtended)
    {
        if (Table.TryGetValue(key, out var entry))
        {
            virtualKey = entry.Vk;
            isExtended = entry.Extended;
            return true;
        }

        virtualKey = 0;
        isExtended = false;
        return false;
    }

    private static Dictionary<KeyCode, (ushort, bool)> Build()
    {
        var map = new Dictionary<KeyCode, (ushort, bool)>();

        for (var i = 0; i < 26; i++) map[KeyCode.A + i] = ((ushort)(0x41 + i), false);
        for (var i = 0; i < 10; i++) map[KeyCode.D0 + i] = ((ushort)(0x30 + i), false);
        for (var i = 0; i < 12; i++) map[KeyCode.F1 + i] = ((ushort)(0x70 + i), false);

        map[KeyCode.Enter] = (0x0D, false);
        map[KeyCode.Escape] = (0x1B, false);
        map[KeyCode.Tab] = (0x09, false);
        map[KeyCode.Space] = (0x20, false);
        map[KeyCode.Backspace] = (0x08, false);

        // ナビゲーションキーはテンキー側と区別するため拡張キーフラグが必要。
        map[KeyCode.Delete] = (0x2E, true);
        map[KeyCode.Insert] = (0x2D, true);
        map[KeyCode.Home] = (0x24, true);
        map[KeyCode.End] = (0x23, true);
        map[KeyCode.PageUp] = (0x21, true);
        map[KeyCode.PageDown] = (0x22, true);
        map[KeyCode.Left] = (0x25, true);
        map[KeyCode.Up] = (0x26, true);
        map[KeyCode.Right] = (0x27, true);
        map[KeyCode.Down] = (0x28, true);
        map[KeyCode.PrintScreen] = (0x2C, true);

        map[KeyCode.Minus] = (0xBD, false);
        map[KeyCode.Equals] = (0xBB, false);
        map[KeyCode.Plus] = (0xBB, false); // VK_OEM_PLUS(US 配列では '=' と同じキー。Ctrl+Plus はブラウザの拡大)
        map[KeyCode.Comma] = (0xBC, false);
        map[KeyCode.Period] = (0xBE, false);
        map[KeyCode.Slash] = (0xBF, false);
        map[KeyCode.Backslash] = (0xDC, false);
        map[KeyCode.Semicolon] = (0xBA, false);
        map[KeyCode.Quote] = (0xDE, false);
        map[KeyCode.BracketLeft] = (0xDB, false);
        map[KeyCode.BracketRight] = (0xDD, false);
        map[KeyCode.Grave] = (0xC0, false);

        map[KeyCode.BrowserBack] = (0xA6, false);
        map[KeyCode.BrowserForward] = (0xA7, false);
        map[KeyCode.BrowserRefresh] = (0xA8, false);
        map[KeyCode.BrowserHome] = (0xAC, false);
        map[KeyCode.VolumeMute] = (0xAD, false);
        map[KeyCode.VolumeDown] = (0xAE, false);
        map[KeyCode.VolumeUp] = (0xAF, false);
        map[KeyCode.MediaNext] = (0xB0, false);
        map[KeyCode.MediaPrevious] = (0xB1, false);
        map[KeyCode.MediaPlayPause] = (0xB3, false);

        return map;
    }
}
