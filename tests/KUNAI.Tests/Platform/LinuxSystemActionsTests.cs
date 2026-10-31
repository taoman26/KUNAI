using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Platform.Linux;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Platform;

public class LinuxSystemActionsTests
{
    private readonly FakeSyntheticKeyboard _keyboard = new();
    private readonly FakeProcessLauncher _launcher = new();

    private LinuxSystemActions Create(SystemActionsOptions? options = null) =>
        new(_keyboard, _launcher, options ?? new SystemActionsOptions());

    [Fact]
    public async Task LaunchBrowser_RunsXdgOpenWithConfiguredUrl()
    {
        await Create(new SystemActionsOptions(BrowserUrl: "https://example.com")).ExecuteAsync(StandardAction.LaunchBrowser);

        Assert.Equal(new[] { "xdg-open https://example.com" }, _launcher.Commands);
        Assert.False(_launcher.Started[0].UseShellExecute);
    }

    [Fact]
    public async Task LaunchBrowser_WithoutXdgOpen_Throws()
    {
        _launcher.MissingExecutables.Add("xdg-open");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create().ExecuteAsync(StandardAction.LaunchBrowser));
    }

    [Fact]
    public async Task LaunchTerminal_UsesXTerminalEmulatorFirst()
    {
        await Create().ExecuteAsync(StandardAction.LaunchTerminal);

        Assert.Equal(new[] { "x-terminal-emulator" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_FallsBackThroughCandidates()
    {
        _launcher.MissingExecutables.UnionWith(new[] { "x-terminal-emulator", "gnome-terminal", "konsole" });

        await Create().ExecuteAsync(StandardAction.LaunchTerminal);

        Assert.Equal(new[] { "xfce4-terminal" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_PrefersConfiguredCommand()
    {
        await Create(new SystemActionsOptions(TerminalCommand: "alacritty")).ExecuteAsync(StandardAction.LaunchTerminal);

        Assert.Equal(new[] { "alacritty" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_WhenConfiguredCommandMissing_FallsBackToDefaults()
    {
        _launcher.MissingExecutables.Add("alacritty");

        await Create(new SystemActionsOptions(TerminalCommand: "alacritty")).ExecuteAsync(StandardAction.LaunchTerminal);

        Assert.Equal(new[] { "x-terminal-emulator" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_WhenNothingAvailable_ThrowsWithTriedList()
    {
        _launcher.MissingExecutables.UnionWith(new[]
        {
            "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create().ExecuteAsync(StandardAction.LaunchTerminal));

        Assert.Contains("xterm", ex.Message);
    }

    [Fact]
    public async Task LockScreen_UsesLoginctl_ThenFallsBack()
    {
        await Create().ExecuteAsync(StandardAction.LockScreen);
        Assert.Equal(new[] { "loginctl lock-session" }, _launcher.Commands);

        _launcher.Started.Clear();
        _launcher.MissingExecutables.UnionWith(new[] { "loginctl", "xdg-screensaver" });
        await Create().ExecuteAsync(StandardAction.LockScreen);
        Assert.Equal(new[] { "xflock4" }, _launcher.Commands);
    }

    [Fact]
    public async Task Volume_UsesPactlWithConfiguredStep()
    {
        var actions = Create(new SystemActionsOptions(VolumeStepPercent: 10));

        await actions.ExecuteAsync(StandardAction.VolumeUp);
        await actions.ExecuteAsync(StandardAction.VolumeDown);
        await actions.ExecuteAsync(StandardAction.Mute);

        Assert.Equal(
            new[]
            {
                "pactl set-sink-volume @DEFAULT_SINK@ +10%",
                "pactl set-sink-volume @DEFAULT_SINK@ -10%",
                "pactl set-sink-mute @DEFAULT_SINK@ toggle"
            },
            _launcher.Commands);
        Assert.Empty(_keyboard.Sent);
    }

    [Fact]
    public async Task Volume_WithoutPactl_FallsBackToMediaKeys()
    {
        _launcher.MissingExecutables.Add("pactl");
        var actions = Create();

        await actions.ExecuteAsync(StandardAction.VolumeUp);
        await actions.ExecuteAsync(StandardAction.VolumeDown);
        await actions.ExecuteAsync(StandardAction.Mute);

        Assert.Equal(new[] { "VolumeUp", "VolumeDown", "VolumeMute" }, _keyboard.Sent);
    }

    [Fact]
    public async Task BrowserNavigation_SendsAltArrows()
    {
        var actions = Create();

        await actions.ExecuteAsync(StandardAction.BrowserBack);
        await actions.ExecuteAsync(StandardAction.BrowserForward);

        Assert.Equal(new[] { "Alt+Left", "Alt+Right" }, _keyboard.Sent);
        Assert.Empty(_launcher.Started);
    }

    [Fact]
    public async Task UnknownEnumValue_IsRejected()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Create().ExecuteAsync((StandardAction)999));
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var options = new SystemActionsOptions();
        Assert.Throws<ArgumentNullException>(() => new LinuxSystemActions(null!, _launcher, options));
        Assert.Throws<ArgumentNullException>(() => new LinuxSystemActions(_keyboard, null!, options));
        Assert.Throws<ArgumentNullException>(() => new LinuxSystemActions(_keyboard, _launcher, null!));
    }
}
