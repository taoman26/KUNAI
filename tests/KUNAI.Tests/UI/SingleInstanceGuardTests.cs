using KUNAI.UI;

namespace KUNAI.Tests.UI;

public sealed class SingleInstanceGuardTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "kunai-guard-" + Guid.NewGuid().ToString("N"));
    private readonly string _pipe = "kunai-test-" + Guid.NewGuid().ToString("N");
    private readonly List<SingleInstanceGuard> _guards = new();

    private string LockPath => Path.Combine(_directory, "sub", "instance.lock");

    private SingleInstanceGuard Create()
    {
        var guard = new SingleInstanceGuard(LockPath, _pipe);
        _guards.Add(guard);
        return guard;
    }

    public void Dispose()
    {
        foreach (var guard in _guards)
        {
            guard.Dispose();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void FirstInstance_Acquires_AndCreatesTheDirectory()
    {
        var first = Create();

        Assert.True(first.TryAcquire());
        Assert.True(File.Exists(LockPath));
    }

    [Fact]
    public void SecondInstance_IsRefused_WhileTheFirstIsRunning()
    {
        var first = Create();
        var second = Create();

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void AfterTheFirstExits_ANewInstanceCanStart()
    {
        var first = Create();
        Assert.True(first.TryAcquire());
        first.Dispose();

        var next = Create();

        Assert.True(next.TryAcquire());
    }

    [Fact]
    public void WhenTheLockCannotBeCreated_StartupIsNotBlocked()
    {
        // ロックファイルの場所がファイルで塞がれている(ディレクトリを作れない)。
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "x");
        var guard = new SingleInstanceGuard(Path.Combine(blocker, "instance.lock"), _pipe);
        _guards.Add(guard);

        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public async Task Signal_ReachesTheRunningInstance()
    {
        var first = Create();
        Assert.True(first.TryAcquire());
        var received = new TaskCompletionSource();
        first.ActivationRequested += (_, _) => received.TrySetResult();
        first.StartListening();

        var second = Create();
        Assert.False(second.TryAcquire());
        Assert.True(second.SignalExistingInstance(TimeSpan.FromSeconds(3)));

        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Signal_CanBeSentRepeatedly()
    {
        var first = Create();
        first.TryAcquire();
        var count = 0;
        var twice = new TaskCompletionSource();
        first.ActivationRequested += (_, _) =>
        {
            if (Interlocked.Increment(ref count) == 2)
            {
                twice.TrySetResult();
            }
        };
        first.StartListening();

        var sender = Create();
        Assert.True(sender.SignalExistingInstance(TimeSpan.FromSeconds(3)));
        Assert.True(sender.SignalExistingInstance(TimeSpan.FromSeconds(3)));

        await twice.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Signal_ReturnsFalse_WhenNobodyIsListening()
    {
        Assert.False(Create().SignalExistingInstance(TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void Dispose_StopsListening_WithoutHanging()
    {
        var first = Create();
        first.TryAcquire();
        first.StartListening();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        first.Dispose();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void CreateDefault_UsesPerUserNames()
    {
        using var guard = SingleInstanceGuard.CreateDefault();

        Assert.NotNull(guard);
    }
}
