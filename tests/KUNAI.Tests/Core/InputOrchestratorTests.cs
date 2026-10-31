using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Core;

public class InputOrchestratorTests
{
    private readonly FakeWiimoteService _wiimote = new();
    private readonly LayerManager _layers = new();
    private readonly FakeActionExecutor _actions = new();
    private readonly FakeMacroExecutor _macros = new();
    private readonly FakeLogService _log = new();
    private readonly InputOrchestrator _orchestrator;

    public InputOrchestratorTests()
    {
        _orchestrator = new InputOrchestrator(_wiimote, _layers, _actions, _macros, _log);
        _orchestrator.SetProfile(CreateProfile());
        _orchestrator.Start();
    }

    private static Profile CreateProfile()
    {
        var profile = Profile.CreateEmpty("test");
        void Map(int layer, WiimoteButton button, string action) =>
            profile.Layers[layer].Mappings.Add(new ButtonMapping { Button = button, Action = action });

        Map(0, WiimoteButton.A, "Enter");
        Map(0, WiimoteButton.One, "Ctrl+C");
        Map(0, WiimoteButton.Two, "Ctrl+V");
        Map(1, WiimoteButton.A, "Ctrl+Z");
        Map(1, WiimoteButton.One, "Ctrl+Shift+Z");
        Map(2, WiimoteButton.A, "Alt+Tab");
        Map(3, WiimoteButton.A, "LaunchTerminal");
        Map(0, WiimoteButton.B, "Escape");     // 無視されるべき
        Map(0, WiimoteButton.Home, "Escape");  // 無視されるべき
        Map(0, WiimoteButton.Up, "");          // 空のアクション
        profile.Layers[0].Mappings.Add(new ButtonMapping
        {
            Button = WiimoteButton.Plus,
            Macro = new List<string> { "Ctrl+S", "Delay(300)", "Alt+Tab" }
        });
        return profile;
    }

    private async Task PressAsync(WiimoteButton button)
    {
        _wiimote.Press(button);
        await _orchestrator.WhenIdleAsync();
    }

    [Fact]
    public async Task Layer0_ButtonExecutesItsAction_AndLogsPress()
    {
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Enter" }, _actions.Executed);
        Assert.Equal(new[] { "A Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task HoldingB_UsesLayer1_AndLogsLayerBeforePress()
    {
        _wiimote.Press(WiimoteButton.B);
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Ctrl+Z" }, _actions.Executed);
        Assert.Equal(new[] { "Layer1", "A Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task HoldingHome_UsesLayer2()
    {
        _wiimote.Press(WiimoteButton.Home);
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Alt+Tab" }, _actions.Executed);
    }

    [Theory]
    [InlineData(WiimoteButton.B, WiimoteButton.Home)]
    [InlineData(WiimoteButton.Home, WiimoteButton.B)]
    public async Task HoldingBoth_UsesLayer3(WiimoteButton first, WiimoteButton second)
    {
        _wiimote.Press(first);
        _wiimote.Press(second);
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "LaunchTerminal" }, _actions.Executed);
    }

    [Fact]
    public async Task ReleasingLayerButton_ReturnsToLayer0()
    {
        _wiimote.Press(WiimoteButton.B);
        _wiimote.Release(WiimoteButton.B);
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Enter" }, _actions.Executed);
        Assert.Equal(new[] { "Layer1", "Layer0", "A Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task LayerButtons_NeverTriggerTheirOwnMappings_OrPressLogs()
    {
        await PressAsync(WiimoteButton.B);
        await PressAsync(WiimoteButton.Home);

        Assert.Empty(_actions.Executed);
        Assert.DoesNotContain("B Pressed", _log.Messages);
        Assert.DoesNotContain("Home Pressed", _log.Messages);
    }

    [Fact]
    public async Task UnmappedButton_IsLoggedButDoesNothing()
    {
        await PressAsync(WiimoteButton.Down);

        Assert.Empty(_actions.Executed);
        Assert.Empty(_macros.Executed);
        Assert.Equal(new[] { "Down Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task EmptyAction_DoesNothing()
    {
        await PressAsync(WiimoteButton.Up);

        Assert.Empty(_actions.Executed);
        Assert.Equal(new[] { "Up Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task MacroMapping_RunsMacroExecutor_NotActionExecutor()
    {
        await PressAsync(WiimoteButton.Plus);

        Assert.Empty(_actions.Executed);
        Assert.Equal(new[] { new[] { "Ctrl+S", "Delay(300)", "Alt+Tab" } }, _macros.Executed);
    }

    [Fact]
    public async Task NoProfile_PressesAreLoggedButNothingRuns()
    {
        var orchestrator = new InputOrchestrator(_wiimote, new LayerManager(), _actions, _macros, _log);
        _orchestrator.Stop();
        orchestrator.Start();

        _wiimote.Press(WiimoteButton.A);
        await orchestrator.WhenIdleAsync();

        Assert.Empty(_actions.Executed);
        Assert.Equal(new[] { "A Pressed" }, _log.Messages);
    }

    [Fact]
    public async Task ActionFailure_IsLogged_AndLaterActionsStillRun()
    {
        _actions.ThrowOn = "Enter";

        _wiimote.Press(WiimoteButton.A);
        await PressAsync(WiimoteButton.One);

        Assert.Equal(new[] { "Ctrl+C" }, _actions.Executed);
        Assert.Contains("Error: Failed: Enter", _log.Messages);
    }

    [Fact]
    public async Task Actions_RunOneAtATime_InPressOrder_WithoutBlockingInput()
    {
        var gate = new TaskCompletionSource();
        var slow = new GatedActionExecutor(gate.Task);
        var orchestrator = new InputOrchestrator(_wiimote, new LayerManager(), slow, _macros, _log);
        _orchestrator.Stop();
        orchestrator.SetProfile(CreateProfile());
        orchestrator.Start();

        _wiimote.Press(WiimoteButton.A);   // 待機させる
        _wiimote.Press(WiimoteButton.One); // 入力スレッドは塞がれず、ここまで戻ってくる
        _wiimote.Press(WiimoteButton.Two);

        await slow.FirstStarted; // 最初のアクションが実際に始まるまで待つ
        Assert.Equal(new[] { "Enter" }, slow.Started); // その完了まで次は始まらない

        gate.SetResult();
        await orchestrator.WhenIdleAsync();

        Assert.Equal(new[] { "Enter", "Ctrl+C", "Ctrl+V" }, slow.Started);
    }

    [Fact]
    public async Task Layer_IsDecidedAtPressTime_NotAtExecutionTime()
    {
        var gate = new TaskCompletionSource();
        var slow = new GatedActionExecutor(gate.Task);
        var orchestrator = new InputOrchestrator(_wiimote, new LayerManager(), slow, _macros, _log);
        _orchestrator.Stop();
        orchestrator.SetProfile(CreateProfile());
        orchestrator.Start();

        _wiimote.Press(WiimoteButton.A);   // Layer0 "Enter"(完了待ち)
        _wiimote.Press(WiimoteButton.B);
        _wiimote.Press(WiimoteButton.A);   // Layer1 "Ctrl+Z"
        _wiimote.Release(WiimoteButton.B); // 実行前に Layer0 に戻る

        gate.SetResult();
        await orchestrator.WhenIdleAsync();

        Assert.Equal(new[] { "Enter", "Ctrl+Z" }, slow.Started);
    }

    [Fact]
    public async Task Disconnect_ResetsLayer_AndLogsConnectionStates()
    {
        _wiimote.SetState(ConnectionState.Connecting);
        _wiimote.SetState(ConnectionState.Connected);
        _wiimote.Press(WiimoteButton.B);
        _wiimote.SetState(ConnectionState.Disconnected);
        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Enter" }, _actions.Executed);
        Assert.Equal(
            new[] { "Connecting", "Connected", "Layer1", "Disconnected", "Layer0", "A Pressed" },
            _log.Messages);
    }

    [Fact]
    public async Task Stop_UnsubscribesFromEvents()
    {
        _orchestrator.Stop();

        await PressAsync(WiimoteButton.A);

        Assert.Empty(_actions.Executed);
        Assert.Empty(_log.Messages);
        Assert.Equal(0, _wiimote.PressedSubscriberCount);
    }

    [Fact]
    public async Task Start_IsIdempotent()
    {
        _orchestrator.Start();
        _orchestrator.Start();

        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Enter" }, _actions.Executed);
        Assert.Equal(1, _wiimote.PressedSubscriberCount);
    }

    [Fact]
    public async Task Restart_ResumesHandling()
    {
        _orchestrator.Stop();
        _orchestrator.Start();

        await PressAsync(WiimoteButton.A);

        Assert.Equal(new[] { "Enter" }, _actions.Executed);
    }

    [Fact]
    public async Task EditingProfileAfterPress_DoesNotAffectQueuedMacro()
    {
        var gate = new TaskCompletionSource();
        var slow = new GatedActionExecutor(gate.Task);
        var profile = CreateProfile();
        var orchestrator = new InputOrchestrator(_wiimote, new LayerManager(), slow, _macros, _log);
        _orchestrator.Stop();
        orchestrator.SetProfile(profile);
        orchestrator.Start();

        _wiimote.Press(WiimoteButton.A);    // ゲートで待機
        _wiimote.Press(WiimoteButton.Plus); // マクロをキューへ
        profile.FindMapping(0, WiimoteButton.Plus)!.Macro!.Clear();

        gate.SetResult();
        await orchestrator.WhenIdleAsync();

        Assert.Equal(new[] { new[] { "Ctrl+S", "Delay(300)", "Alt+Tab" } }, _macros.Executed);
    }

    [Fact]
    public void Constructor_AndSetProfile_RejectNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new InputOrchestrator(null!, _layers, _actions, _macros, _log));
        Assert.Throws<ArgumentNullException>(() => new InputOrchestrator(_wiimote, null!, _actions, _macros, _log));
        Assert.Throws<ArgumentNullException>(() => new InputOrchestrator(_wiimote, _layers, null!, _macros, _log));
        Assert.Throws<ArgumentNullException>(() => new InputOrchestrator(_wiimote, _layers, _actions, null!, _log));
        Assert.Throws<ArgumentNullException>(() => new InputOrchestrator(_wiimote, _layers, _actions, _macros, null!));
        Assert.Throws<ArgumentNullException>(() => _orchestrator.SetProfile(null!));
    }

    private sealed class GatedActionExecutor : KUNAI.Core.Abstractions.IActionExecutor
    {
        private readonly Task _gate;
        private int _count;

        public GatedActionExecutor(Task gate)
        {
            _gate = gate;
        }

        private readonly TaskCompletionSource _firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Started { get; } = new();

        public Task FirstStarted => _firstStarted.Task;

        public async Task ExecuteAsync(string action)
        {
            Started.Add(action);
            _firstStarted.TrySetResult();

            // 最初の 1 件だけゲートが開くまで待たせる。
            if (Interlocked.Increment(ref _count) == 1)
            {
                await _gate;
            }
        }
    }
}
