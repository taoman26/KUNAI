using KUNAI.Core.Shortcuts;
using KUNAI.Platform.Linux.Native;

namespace KUNAI.Platform.Linux;

/// <summary>ショートカットをキーイベント列(修飾キー押下 → 主キー押下/解放 → 修飾キー解放)へ展開する。OS 非依存。</summary>
internal static class LinuxKeyEventBuilder
{
    public static IReadOnlyList<LinuxKeyEvent> Build(KeyShortcut shortcut)
    {
        if (!LinuxKeyCodes.TryGet(shortcut.Key, out var code))
        {
            throw new NotSupportedException($"Key '{shortcut.Key}' is not supported on Linux.");
        }

        var modifiers = GetModifierCodes(shortcut.Modifiers);
        var events = new List<LinuxKeyEvent>(modifiers.Count * 2 + 2);

        foreach (var modifier in modifiers)
        {
            events.Add(new LinuxKeyEvent(modifier, IsDown: true));
        }

        events.Add(new LinuxKeyEvent(code, IsDown: true));
        events.Add(new LinuxKeyEvent(code, IsDown: false));

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            events.Add(new LinuxKeyEvent(modifiers[i], IsDown: false));
        }

        return events;
    }

    /// <summary>修飾キーの解放だけのイベント列(送出失敗時の後始末用)。</summary>
    public static IReadOnlyList<LinuxKeyEvent> BuildModifierRelease(KeyModifiers modifiers) =>
        GetModifierCodes(modifiers).AsEnumerable().Reverse().Select(c => new LinuxKeyEvent(c, IsDown: false)).ToList();

    private static List<ushort> GetModifierCodes(KeyModifiers modifiers)
    {
        var list = new List<ushort>(4);
        if (modifiers.HasFlag(KeyModifiers.Ctrl)) list.Add(LinuxKeyCodes.KeyLeftCtrl);
        if (modifiers.HasFlag(KeyModifiers.Shift)) list.Add(LinuxKeyCodes.KeyLeftShift);
        if (modifiers.HasFlag(KeyModifiers.Alt)) list.Add(LinuxKeyCodes.KeyLeftAlt);
        if (modifiers.HasFlag(KeyModifiers.Win)) list.Add(LinuxKeyCodes.KeyLeftMeta);
        return list;
    }
}
