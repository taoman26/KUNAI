namespace KUNAI.Core.Shortcuts;

/// <summary>"Ctrl+Shift+T" のような文字列を <see cref="KeyShortcut"/> に変換する。大文字小文字は区別しない。</summary>
public static class ShortcutParser
{
    private static readonly Dictionary<string, KeyModifiers> ModifierNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = KeyModifiers.Ctrl,
            ["Control"] = KeyModifiers.Ctrl,
            ["Shift"] = KeyModifiers.Shift,
            ["Alt"] = KeyModifiers.Alt,
            ["Win"] = KeyModifiers.Win,
            ["Windows"] = KeyModifiers.Win,
            ["Super"] = KeyModifiers.Win,
            ["Meta"] = KeyModifiers.Win,
            ["Cmd"] = KeyModifiers.Win
        };

    private static readonly Dictionary<string, KeyCode> KeyNames = BuildKeyNames();

    /// <summary>解析する。不正な場合は <see cref="FormatException"/>。</summary>
    public static KeyShortcut Parse(string shortcut)
    {
        if (!TryParse(shortcut, out var result))
        {
            throw new FormatException($"Invalid shortcut: '{shortcut}'");
        }

        return result;
    }

    /// <summary>解析を試みる。成功時 true。</summary>
    public static bool TryParse(string? shortcut, out KeyShortcut result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return false;
        }

        var text = shortcut.Trim();
        if (text == "+")
        {
            result = new KeyShortcut(KeyModifiers.None, KeyCode.Plus);
            return true;
        }

        // 末尾が "++" の場合は主キーが '+'(例: "Ctrl++")。
        var keyToken = string.Empty;
        if (text.EndsWith("++", StringComparison.Ordinal))
        {
            keyToken = "+";
            text = text[..^1];
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries);
        if (keyToken.Length == 0)
        {
            keyToken = tokens[^1];
            tokens = tokens[..^1];
        }
        else
        {
            // text は "Ctrl+" のように末尾が '+' で終わる。空要素を除く。
            tokens = tokens.Where(t => t.Length > 0).ToArray();
        }

        var modifiers = KeyModifiers.None;
        foreach (var token in tokens)
        {
            if (!ModifierNames.TryGetValue(token, out var modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (!KeyNames.TryGetValue(keyToken, out var key))
        {
            return false;
        }

        result = new KeyShortcut(modifiers, key);
        return true;
    }

    /// <summary>ログ・文字列化用のキー名。D1 → "1"。</summary>
    public static string GetDisplayName(KeyCode key) => key switch
    {
        >= KeyCode.D0 and <= KeyCode.D9 => ((int)key - (int)KeyCode.D0).ToString(),
        KeyCode.Plus => "Plus",
        _ => key.ToString()
    };

    private static Dictionary<string, KeyCode> BuildKeyNames()
    {
        var map = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Enum.GetValues<KeyCode>())
        {
            map[key.ToString()] = key;
        }

        // 数字は "1" でも "D1" でも受け付ける。
        for (var i = 0; i <= 9; i++)
        {
            map[i.ToString()] = KeyCode.D0 + i;
        }

        map["Return"] = KeyCode.Enter;
        map["Esc"] = KeyCode.Escape;
        map["Del"] = KeyCode.Delete;
        map["Ins"] = KeyCode.Insert;
        map["PgUp"] = KeyCode.PageUp;
        map["PgDn"] = KeyCode.PageDown;
        map["PrtSc"] = KeyCode.PrintScreen;
        map["-"] = KeyCode.Minus;
        map["="] = KeyCode.Equals;
        map[","] = KeyCode.Comma;
        map["."] = KeyCode.Period;
        map["/"] = KeyCode.Slash;
        map["\\"] = KeyCode.Backslash;
        map[";"] = KeyCode.Semicolon;
        map["'"] = KeyCode.Quote;
        map["["] = KeyCode.BracketLeft;
        map["]"] = KeyCode.BracketRight;
        map["`"] = KeyCode.Grave;
        map["+"] = KeyCode.Plus;
        map["Mute"] = KeyCode.VolumeMute;

        return map;
    }
}
