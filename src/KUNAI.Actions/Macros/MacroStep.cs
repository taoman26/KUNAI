using System.Globalization;
using System.Text.RegularExpressions;

namespace KUNAI.Actions.Macros;

public enum MacroStepKind
{
    /// <summary>ショートカットまたは標準アクション。</summary>
    Action,

    /// <summary>"Delay(ms)" による待機。</summary>
    Delay
}

/// <summary>マクロの 1 ステップ。</summary>
public sealed partial record MacroStep(MacroStepKind Kind, string Action, int DelayMs)
{
    /// <summary>1 回の Delay に指定できる上限(ミリ秒)。</summary>
    public const int MaxDelayMs = 60_000;

    private const string DelayPrefix = "Delay(";

    public static MacroStep CreateAction(string action) => new(MacroStepKind.Action, action, 0);

    public static MacroStep CreateDelay(int milliseconds) => new(MacroStepKind.Delay, string.Empty, milliseconds);

    /// <summary>
    /// "Delay(100)" は待機、それ以外は実行するアクションとして解釈する。
    /// "Delay(" で始まるのに形式が不正なものは <see cref="FormatException"/>。
    /// </summary>
    public static MacroStep Parse(string step)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            throw new FormatException("Macro step must not be empty.");
        }

        var text = step.Trim();
        if (!text.StartsWith(DelayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return CreateAction(text);
        }

        var match = DelayPattern().Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms))
        {
            throw new FormatException($"Invalid delay step: '{step}'. Expected Delay(<milliseconds>).");
        }

        if (ms > MaxDelayMs)
        {
            throw new FormatException($"Delay must be at most {MaxDelayMs} ms: '{step}'.");
        }

        return CreateDelay(ms);
    }

    [GeneratedRegex(@"^Delay\(\s*(\d+)\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DelayPattern();
}
