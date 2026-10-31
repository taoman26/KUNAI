using KUNAI.Core.Shortcuts;
using KUNAI.Platform.Windows.Native;

namespace KUNAI.Platform.Windows;

/// <summary>ショートカットをキーイベント列(修飾キー押下 → 主キー押下/解放 → 修飾キー解放)へ展開する。OS 非依存。</summary>
internal static class WindowsKeyEventBuilder
{
    public static IReadOnlyList<KeyEvent> Build(KeyShortcut shortcut)
    {
        if (!VirtualKeyMap.TryGet(shortcut.Key, out var vk, out var extended))
        {
            throw new NotSupportedException($"Key '{shortcut.Key}' is not supported on Windows.");
        }

        var modifiers = GetModifierKeys(shortcut.Modifiers);
        var events = new List<KeyEvent>(modifiers.Count * 2 + 2);

        foreach (var modifier in modifiers)
        {
            events.Add(modifier with { IsKeyUp = false });
        }

        events.Add(new KeyEvent(vk, extended, IsKeyUp: false));
        events.Add(new KeyEvent(vk, extended, IsKeyUp: true));

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            events.Add(modifiers[i] with { IsKeyUp = true });
        }

        return events;
    }

    /// <summary>修飾キーの解放だけのイベント列(送出失敗時に押しっぱなしを防ぐ後始末用)。</summary>
    public static IReadOnlyList<KeyEvent> BuildModifierRelease(KeyModifiers modifiers) =>
        GetModifierKeys(modifiers).Select(m => m with { IsKeyUp = true }).Reverse().ToList();

    private static List<KeyEvent> GetModifierKeys(KeyModifiers modifiers)
    {
        var list = new List<KeyEvent>(4);
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) list.Add(new KeyEvent(VirtualKeyMap.VkLControl, false, false));
        if (modifiers.HasFlag(KeyModifiers.Shift)) list.Add(new KeyEvent(VirtualKeyMap.VkLShift, false, false));
        if (modifiers.HasFlag(KeyModifiers.Alt)) list.Add(new KeyEvent(VirtualKeyMap.VkLMenu, false, false));
        if (modifiers.HasFlag(KeyModifiers.Win)) list.Add(new KeyEvent(VirtualKeyMap.VkLWin, true, false));
        return list;
    }
}
