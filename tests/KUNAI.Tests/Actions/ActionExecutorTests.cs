using KUNAI.Actions.Executors;
using KUNAI.Core.Domain;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Actions;

public class ActionExecutorTests
{
    private readonly FakeSyntheticKeyboard _keyboard = new();
    private readonly FakeSystemActions _system = new();
    private readonly FakeLogService _log = new();
    private readonly ActionExecutor _executor;

    public ActionExecutorTests()
    {
        _executor = new ActionExecutor(_keyboard, _system, _log);
    }

    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Ctrl+Shift+T")]
    [InlineData("Alt+Tab")]
    [InlineData("Win+D")]
    [InlineData("Enter")]
    public async Task Shortcut_IsSentToKeyboard(string shortcut)
    {
        await _executor.ExecuteAsync(shortcut);

        Assert.Equal(new[] { shortcut }, _keyboard.Sent);
        Assert.Empty(_system.Executed);
    }

    [Fact]
    public async Task Shortcut_LogsNormalizedName()
    {
        await _executor.ExecuteAsync("ctrl+z");

        Assert.Equal(new[] { "Ctrl+Z Sent" }, _log.Messages);
    }

    [Theory]
    [InlineData("LaunchBrowser", StandardAction.LaunchBrowser)]
    [InlineData("LaunchTerminal", StandardAction.LaunchTerminal)]
    [InlineData("VolumeUp", StandardAction.VolumeUp)]
    [InlineData("VolumeDown", StandardAction.VolumeDown)]
    [InlineData("Mute", StandardAction.Mute)]
    [InlineData("LockScreen", StandardAction.LockScreen)]
    [InlineData("BrowserBack", StandardAction.BrowserBack)]
    [InlineData("BrowserForward", StandardAction.BrowserForward)]
    [InlineData("launchbrowser", StandardAction.LaunchBrowser)]
    [InlineData("  LockScreen  ", StandardAction.LockScreen)]
    public async Task StandardAction_IsDelegatedToSystemActions(string text, StandardAction expected)
    {
        await _executor.ExecuteAsync(text);

        Assert.Equal(new[] { expected }, _system.Executed);
        Assert.Empty(_keyboard.Sent);
        Assert.Equal(new[] { $"{expected} Executed" }, _log.Messages);
    }

    [Fact]
    public async Task BrowserBack_PrefersStandardActionOverKeyCode()
    {
        await _executor.ExecuteAsync("BrowserBack");

        Assert.Equal(new[] { StandardAction.BrowserBack }, _system.Executed);
        Assert.Empty(_keyboard.Sent);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("5")]
    public async Task NumericText_IsAShortcut_NotAnEnumValue(string text)
    {
        await _executor.ExecuteAsync(text);

        Assert.Equal(new[] { text }, _keyboard.Sent);
        Assert.Empty(_system.Executed);
    }

    [Theory]
    [InlineData("Ctrl+Nonsense")]
    [InlineData("LaunchRocket")]
    [InlineData("Ctrl+")]
    public async Task UnknownAction_ThrowsFormatException_AndDoesNothing(string text)
    {
        await Assert.ThrowsAsync<FormatException>(() => _executor.ExecuteAsync(text));

        Assert.Empty(_keyboard.Sent);
        Assert.Empty(_system.Executed);
        Assert.Empty(_log.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyAction_ThrowsArgumentException(string text)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _executor.ExecuteAsync(text));
    }

    [Fact]
    public async Task NullAction_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _executor.ExecuteAsync(null!));
    }

    [Fact]
    public async Task KeyboardFailure_Propagates_AndIsNotLoggedAsSent()
    {
        _keyboard.ThrowOn = "Ctrl+C";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _executor.ExecuteAsync("Ctrl+C"));

        Assert.Empty(_log.Messages);
    }

    [Fact]
    public async Task SystemActionFailure_Propagates_AndIsNotLoggedAsExecuted()
    {
        _system.ThrowOn = StandardAction.LockScreen;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _executor.ExecuteAsync("LockScreen"));

        Assert.Empty(_log.Messages);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ActionExecutor(null!, _system, _log));
        Assert.Throws<ArgumentNullException>(() => new ActionExecutor(_keyboard, null!, _log));
        Assert.Throws<ArgumentNullException>(() => new ActionExecutor(_keyboard, _system, null!));
    }
}
