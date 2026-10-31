using KUNAI.Actions.Macros;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Actions;

public class MacroExecutorTests
{
    private readonly List<string> _journal = new();
    private readonly FakeActionExecutor _actions;
    private readonly FakeDelayProvider _delays;
    private readonly MacroExecutor _executor;

    public MacroExecutorTests()
    {
        _actions = new FakeActionExecutor(_journal);
        _delays = new FakeDelayProvider(_journal);
        _executor = new MacroExecutor(_actions, _delays);
    }

    [Fact]
    public async Task Executes_CopyPasteMacro_InOrder()
    {
        await _executor.ExecuteAsync(new[] { "Ctrl+C", "Delay(100)", "Ctrl+V" });

        Assert.Equal(new[] { "Ctrl+C", "delay:100", "Ctrl+V" }, _journal);
    }

    [Fact]
    public async Task Executes_SaveAndSwitchMacro_InOrder()
    {
        await _executor.ExecuteAsync(new[] { "Ctrl+S", "Delay(300)", "Alt+Tab" });

        Assert.Equal(new[] { "Ctrl+S", "delay:300", "Alt+Tab" }, _journal);
        Assert.Equal(new[] { TimeSpan.FromMilliseconds(300) }, _delays.Delays);
    }

    [Fact]
    public async Task Supports_MultipleDelays_AndStandardActions()
    {
        await _executor.ExecuteAsync(new[] { "LaunchTerminal", "Delay(500)", "Ctrl+L", "Delay(10)", "Enter" });

        Assert.Equal(new[] { "LaunchTerminal", "delay:500", "Ctrl+L", "delay:10", "Enter" }, _journal);
    }

    [Theory]
    [InlineData("delay(50)")]
    [InlineData("DELAY(50)")]
    [InlineData("  Delay( 50 )  ")]
    public async Task Delay_IsCaseAndWhitespaceTolerant(string step)
    {
        await _executor.ExecuteAsync(new[] { step });

        Assert.Equal(new[] { "delay:50" }, _journal);
        Assert.Empty(_actions.Executed);
    }

    [Fact]
    public async Task ZeroDelay_IsAllowed()
    {
        await _executor.ExecuteAsync(new[] { "A", "Delay(0)", "B" });

        Assert.Equal(new[] { "A", "delay:0", "B" }, _journal);
    }

    [Fact]
    public async Task EmptyMacro_DoesNothing()
    {
        await _executor.ExecuteAsync(Array.Empty<string>());

        Assert.Empty(_journal);
    }

    [Theory]
    [InlineData("Delay(abc)")]
    [InlineData("Delay(-1)")]
    [InlineData("Delay()")]
    [InlineData("Delay(100")]
    [InlineData("Delay(1.5)")]
    [InlineData("Delay(60001)")]
    [InlineData("Delay(99999999999)")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidStep_ThrowsFormatException_BeforeExecutingAnything(string invalid)
    {
        var steps = new[] { "Ctrl+C", invalid, "Ctrl+V" };

        await Assert.ThrowsAsync<FormatException>(() => _executor.ExecuteAsync(steps));

        Assert.Empty(_journal);
    }

    [Fact]
    public async Task MaxDelay_IsAccepted()
    {
        await _executor.ExecuteAsync(new[] { "Delay(60000)" });

        Assert.Equal(new[] { "delay:60000" }, _journal);
    }

    [Fact]
    public async Task ActionFailure_StopsMacro_AndPropagates()
    {
        _actions.ThrowOn = "Ctrl+V";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(new[] { "Ctrl+C", "Delay(100)", "Ctrl+V", "Delay(100)", "Enter" }));

        Assert.Equal(new[] { "Ctrl+C", "delay:100" }, _journal);
    }

    [Fact]
    public async Task AlreadyCancelled_ExecutesNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _executor.ExecuteAsync(new[] { "Ctrl+C" }, cts.Token));

        Assert.Empty(_journal);
    }

    [Fact]
    public async Task CancelledDuringMacro_StopsRemainingSteps()
    {
        using var cts = new CancellationTokenSource();
        var cancelling = new CancelAfterFirstActionExecutor(cts, _journal);
        var executor = new MacroExecutor(cancelling, _delays);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync(new[] { "Ctrl+C", "Delay(100)", "Ctrl+V" }, cts.Token));

        Assert.Equal(new[] { "Ctrl+C" }, _journal);
    }

    [Fact]
    public async Task NullSteps_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _executor.ExecuteAsync(null!));
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MacroExecutor(null!, _delays));
        Assert.Throws<ArgumentNullException>(() => new MacroExecutor(_actions, null!));
    }

    [Fact]
    public async Task TaskDelayProvider_ZeroDelayCompletes_AndCancellationIsHonoured()
    {
        var provider = new TaskDelayProvider();
        await provider.DelayAsync(TimeSpan.Zero);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.DelayAsync(TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public void MacroStep_Parse_DistinguishesDelayFromAction()
    {
        var delay = MacroStep.Parse("Delay(250)");
        var action = MacroStep.Parse("  Ctrl+C ");

        Assert.Equal(MacroStepKind.Delay, delay.Kind);
        Assert.Equal(250, delay.DelayMs);
        Assert.Equal(MacroStepKind.Action, action.Kind);
        Assert.Equal("Ctrl+C", action.Action);
    }

    private sealed class CancelAfterFirstActionExecutor : KUNAI.Core.Abstractions.IActionExecutor
    {
        private readonly CancellationTokenSource _cts;
        private readonly List<string> _journal;

        public CancelAfterFirstActionExecutor(CancellationTokenSource cts, List<string> journal)
        {
            _cts = cts;
            _journal = journal;
        }

        public Task ExecuteAsync(string action)
        {
            _journal.Add(action);
            _cts.Cancel();
            return Task.CompletedTask;
        }
    }
}
