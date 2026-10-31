namespace KUNAI.Core.Shortcuts;

/// <summary>解析済みのショートカット。修飾キーと主キーの組。</summary>
public readonly record struct KeyShortcut(KeyModifiers Modifiers, KeyCode Key)
{
    /// <summary>"Ctrl+Shift+T" 形式の文字列(修飾キーは Ctrl, Shift, Alt, Win の順)。</summary>
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(KeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
        parts.Add(ShortcutParser.GetDisplayName(Key));
        return string.Join('+', parts);
    }
}
