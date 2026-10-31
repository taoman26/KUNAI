using KUNAI.Core.Abstractions;
using KUNAI.Core.Shortcuts;

namespace KUNAI.Platform.Windows;

/// <summary>Win32 SendInput によるキー送出(Windows 専用)。</summary>
public sealed class WindowsSyntheticKeyboard : ISyntheticKeyboard
{
    private readonly IKeyEventInjector _injector;

    public WindowsSyntheticKeyboard()
        : this(CreateWin32Injector())
    {
    }

    internal WindowsSyntheticKeyboard(IKeyEventInjector injector)
    {
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
    }

    /// <summary>
    /// 例: "Ctrl+C", "Ctrl+Shift+T", "Alt+Tab", "Win+D"。
    /// 不正な文字列は <see cref="FormatException"/>。管理者権限で動くウィンドウへは(UIPI により)届かない場合がある。
    /// </summary>
    public Task SendShortcutAsync(string shortcut)
    {
        var parsed = ShortcutParser.Parse(shortcut);
        var events = WindowsKeyEventBuilder.Build(parsed);

        try
        {
            _injector.Inject(events);
        }
        catch
        {
            // 押下だけ届いて解放が届かなかった場合に備え、修飾キーを解放して固着を防ぐ。
            try
            {
                _injector.Inject(WindowsKeyEventBuilder.BuildModifierRelease(parsed.Modifiers));
            }
            catch
            {
                // 後始末の失敗は元の例外を優先する。
            }

            throw;
        }

        return Task.CompletedTask;
    }

    private static IKeyEventInjector CreateWin32Injector()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsSyntheticKeyboard is only supported on Windows.");
        }

        return new Win32KeyEventInjector();
    }
}
