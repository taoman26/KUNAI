using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Platform.Windows;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Platform;

public class WindowsSystemActionsTests
{
    private readonly FakeSyntheticKeyboard _keyboard = new();
    private readonly FakeProcessLauncher _launcher = new();
    private int _lockCalls;
    private bool _lockResult = true;

    private WindowsSystemActions Create(SystemActionsOptions? options = null) =>
        new(_keyboard, _launcher, options ?? new SystemActionsOptions(), () =>
        {
            _lockCalls++;
            return _lockResult;
        });

    [Fact]
    public async Task LaunchBrowser_ShellExecutesTheUrl()
    {
        await Create(new SystemActionsOptions(BrowserUrl: "https://example.com")).ExecuteAsync(StandardAction.LaunchBrowser);

        var started = Assert.Single(_launcher.Started);
        Assert.Equal("https://example.com", started.FileName);
        Assert.True(started.UseShellExecute);
    }

    [Fact]
    public async Task LaunchTerminal_PrefersWindowsTerminal_ThenFallsBack()
    {
        await Create().ExecuteAsync(StandardAction.LaunchTerminal);
        Assert.Equal(new[] { "wt.exe" }, _launcher.Commands);

        _launcher.Started.Clear();
        _launcher.MissingExecutables.Add("wt.exe");
        await Create().ExecuteAsync(StandardAction.LaunchTerminal);
        Assert.Equal(new[] { "powershell.exe" }, _launcher.Commands);

        _launcher.Started.Clear();
        _launcher.MissingExecutables.Add("powershell.exe");
        await Create().ExecuteAsync(StandardAction.LaunchTerminal);
        Assert.Equal(new[] { "cmd.exe" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_PrefersConfiguredCommand()
    {
        await Create(new SystemActionsOptions(TerminalCommand: "pwsh.exe")).ExecuteAsync(StandardAction.LaunchTerminal);

        Assert.Equal(new[] { "pwsh.exe" }, _launcher.Commands);
    }

    [Fact]
    public async Task LaunchTerminal_WhenNothingAvailable_Throws()
    {
        _launcher.MissingExecutables.UnionWith(new[] { "wt.exe", "powershell.exe", "cmd.exe" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create().ExecuteAsync(StandardAction.LaunchTerminal));
    }

    [Fact]
    public async Task LockScreen_CallsLockWorkStation()
    {
        await Create().ExecuteAsync(StandardAction.LockScreen);

        Assert.Equal(1, _lockCalls);
    }

    [Fact]
    public async Task LockScreen_WhenApiFails_Throws()
    {
        _lockResult = false;

        await Assert.ThrowsAsync<System.ComponentModel.Win32Exception>(
            () => Create().ExecuteAsync(StandardAction.LockScreen));
    }

    [Fact]
    public async Task VolumeAndBrowserActions_SendMediaAndBrowserKeys()
    {
        var actions = Create();

        await actions.ExecuteAsync(StandardAction.VolumeUp);
        await actions.ExecuteAsync(StandardAction.VolumeDown);
        await actions.ExecuteAsync(StandardAction.Mute);
        await actions.ExecuteAsync(StandardAction.BrowserBack);
        await actions.ExecuteAsync(StandardAction.BrowserForward);

        Assert.Equal(
            new[] { "VolumeUp", "VolumeDown", "VolumeMute", "BrowserBack", "BrowserForward" },
            _keyboard.Sent);
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
        Assert.Throws<ArgumentNullException>(() => new WindowsSystemActions(null!, _launcher, options));
        Assert.Throws<ArgumentNullException>(() => new WindowsSystemActions(_keyboard, null!, options));
        Assert.Throws<ArgumentNullException>(() => new WindowsSystemActions(_keyboard, _launcher, null!));
    }
}
