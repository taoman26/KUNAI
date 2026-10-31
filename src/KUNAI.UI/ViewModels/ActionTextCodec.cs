using KUNAI.Actions.Macros;
using KUNAI.Core.Domain;
using KUNAI.Core.Shortcuts;

namespace KUNAI.UI.ViewModels;

/// <summary>
/// 画面上の 1 つのテキスト(Action 列)と <see cref="ButtonMapping"/> の相互変換・検証。
/// 単一アクションは "Ctrl+C"、マクロはセミコロン区切りの "Ctrl+C; Delay(100); Ctrl+V"。
/// </summary>
public static class ActionTextCodec
{
    private const char Separator = ';';

    public static string Format(ButtonMapping mapping) =>
        mapping.IsMacro ? string.Join("; ", mapping.Macro!) : mapping.Action;

    /// <summary>テキストの内容を <paramref name="mapping"/> に反映する。セミコロンを含めばマクロ、含まなければ単一アクション。</summary>
    public static void Apply(string? text, ButtonMapping mapping)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Contains(Separator))
        {
            mapping.Macro = Split(trimmed);
            mapping.Action = string.Empty;
        }
        else
        {
            mapping.Macro = null;
            mapping.Action = trimmed;
        }
    }

    /// <summary>問題があればその説明、なければ null。空文字は「未割り当て」として有効。</summary>
    public static string? Validate(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var isMacro = trimmed.Contains(Separator);
        var steps = isMacro ? Split(trimmed) : new List<string> { trimmed };

        foreach (var step in steps)
        {
            if (isMacro && step.StartsWith("Delay(", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MacroStep.Parse(step);
                    continue;
                }
                catch (FormatException ex)
                {
                    return ex.Message;
                }
            }

            if (!StandardActionParser.TryParse(step, out _) && !ShortcutParser.TryParse(step, out _))
            {
                return $"Unknown action: '{step}'";
            }
        }

        return null;
    }

    private static List<string> Split(string text) =>
        text.Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
}
