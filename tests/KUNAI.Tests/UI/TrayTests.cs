using Avalonia.Controls;
using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Tests.Fakes;
using KUNAI.UI.DependencyInjection;
using KUNAI.UI.Tray;
using KUNAI.UI.ViewModels;

namespace KUNAI.Tests.UI;

public class TrayTests
{
    [Theory]
    [InlineData(WindowCloseReason.WindowClosing, false, true)]   // ユーザーが × を押した → 非表示
    [InlineData(WindowCloseReason.WindowClosing, true, false)]   // Exit 中 → 閉じる
    [InlineData(WindowCloseReason.ApplicationShutdown, false, false)]
    [InlineData(WindowCloseReason.OSShutdown, false, false)]     // OS のシャットダウンを妨げない
    [InlineData(WindowCloseReason.OwnerWindowClosing, false, false)]
    public void Close_IsReplacedByHide_OnlyWhenTheUserClosesTheWindow(WindowCloseReason reason, bool exiting, bool expected)
    {
        Assert.Equal(expected, TrayPolicy.ShouldHideInsteadOfClose(reason, exiting));
    }

    [Theory]
    [InlineData(WindowState.Minimized, false, true)]
    [InlineData(WindowState.Minimized, true, false)]
    [InlineData(WindowState.Normal, false, false)]
    [InlineData(WindowState.Maximized, false, false)]
    [InlineData(WindowState.FullScreen, false, false)]
    public void Minimize_IsReplacedByHide(WindowState state, bool exiting, bool expected)
    {
        Assert.Equal(expected, TrayPolicy.ShouldHideOnStateChange(state, exiting));
    }

    private readonly FakeWiimoteService _wiimote = new();

    private MainViewModel Create() =>
        new(_wiimote, new LayerManager(), new FakeProfileRepository(), new FakeInputOrchestrator(),
            new FakeLogService(), new ImmediateUiDispatcher());

    [Fact]
    public void ShowWindowCommand_RaisesShowWindowRequested()
    {
        var vm = Create();
        var count = 0;
        vm.ShowWindowRequested += (_, _) => count++;

        vm.ShowWindowCommand.Execute(null);

        Assert.Equal(1, count);
    }

    [Fact]
    public void ExitCommand_RaisesExitRequested_AndNotShow()
    {
        var vm = Create();
        var exit = 0;
        var show = 0;
        vm.ExitRequested += (_, _) => exit++;
        vm.ShowWindowRequested += (_, _) => show++;

        vm.ExitCommand.Execute(null);

        Assert.Equal(1, exit);
        Assert.Equal(0, show);
    }

    [Fact]
    public void Commands_WithoutSubscribers_DoNotThrow()
    {
        var vm = Create();

        vm.ShowWindowCommand.Execute(null);
        vm.ExitCommand.Execute(null);
    }

    [Fact]
    public void TrayToolTip_FollowsConnectionStateAndBattery()
    {
        var vm = Create();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("KUNAI - Disconnected", vm.TrayToolTip);

        vm.State = ConnectionState.Connected;
        vm.BatteryPercent = 80;

        Assert.Equal("KUNAI - Connected (Battery 80%)", vm.TrayToolTip);
        Assert.Contains(nameof(MainViewModel.TrayToolTip), changed);
    }

    [Fact]
    public void KunaiOptions_HideToTray_IsOnByDefault()
    {
        Assert.True(new KunaiOptions().HideToTray);
        Assert.False(new KunaiOptions(HideToTray: false).HideToTray);
    }

    [Fact]
    public void TrayIcon_IsDistinctForEachConnectionState()
    {
        var uris = Enum.GetValues<ConnectionState>().Select(TrayPolicy.IconFor).ToList();

        Assert.Equal(uris.Count, uris.Distinct().Count());
        Assert.EndsWith("icon_tray_disconnected.png", TrayPolicy.IconFor(ConnectionState.Disconnected).AbsolutePath);
        Assert.EndsWith("icon_tray_connecting.png", TrayPolicy.IconFor(ConnectionState.Connecting).AbsolutePath);
        Assert.EndsWith("icon_tray_connected.png", TrayPolicy.IconFor(ConnectionState.Connected).AbsolutePath);
    }
}
