using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;
using KUNAI.Core.Shortcuts;

namespace KUNAI.Actions.Executors;

/// <summary>
/// 単一のアクションを実行する。
/// 標準アクション名(LaunchBrowser など)なら <see cref="ISystemActions"/>、
/// それ以外はショートカットとして解析して <see cref="ISyntheticKeyboard"/> に渡す。
/// "BrowserBack" のように両方に該当する名前は標準アクションを優先する。
/// </summary>
public sealed class ActionExecutor : IActionExecutor
{
    private readonly ISyntheticKeyboard _keyboard;
    private readonly ISystemActions _systemActions;
    private readonly ILogService _log;

    public ActionExecutor(ISyntheticKeyboard keyboard, ISystemActions systemActions, ILogService log)
    {
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <exception cref="ArgumentException">アクションが空。</exception>
    /// <exception cref="FormatException">標準アクションでもショートカットでもない。</exception>
    public async Task ExecuteAsync(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action must not be empty.", nameof(action));
        }

        var text = action.Trim();

        if (StandardActionParser.TryParse(text, out var standard))
        {
            await _systemActions.ExecuteAsync(standard).ConfigureAwait(false);
            _log.Add($"{standard} Executed");
            return;
        }

        if (ShortcutParser.TryParse(text, out var shortcut))
        {
            await _keyboard.SendShortcutAsync(text).ConfigureAwait(false);
            _log.Add($"{shortcut} Sent");
            return;
        }

        throw new FormatException($"Unknown action: '{action}'. Expected a shortcut such as Ctrl+C or a standard action such as LaunchBrowser.");
    }
}
