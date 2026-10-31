using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;
using KUNAI.Input.Wiimote;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Input;

public sealed class WiimoteServiceTests : IAsyncDisposable
{
    private static readonly WiimoteServiceOptions FastOptions =
        new(TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(5), TimeSpan.FromMinutes(10));

    private readonly FakeWiimoteTransport _transport = new();
    private readonly WiimoteService _service;
    private readonly List<string> _events = new();
    private readonly object _eventsLock = new();

    public WiimoteServiceTests()
    {
        _service = new WiimoteService(_transport, FastOptions);
        _service.ConnectionStateChanged += (_, s) => Record($"state:{s}");
        _service.ButtonPressed += (_, e) => Record($"down:{e.Button}");
        _service.ButtonReleased += (_, e) => Record($"up:{e.Button}");
        _service.BatteryChanged += (_, p) => Record($"battery:{p}");
    }

    public async ValueTask DisposeAsync() => await _service.DisposeAsync();

    private void Record(string text)
    {
        lock (_eventsLock)
        {
            _events.Add(text);
        }
    }

    private List<string> Events()
    {
        lock (_eventsLock)
        {
            return _events.ToList();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {what}");
            }

            await Task.Delay(5);
        }
    }

    private static byte[] Buttons(byte byte1, byte byte2) => new byte[] { 0x30, byte1, byte2 };

    [Fact]
    public async Task Connect_WhenDeviceIsPresent_BecomesConnected_AndConfiguresTheRemote()
    {
        await _service.ConnectAsync();

        Assert.True(_service.IsConnected);
        Assert.Equal(ConnectionState.Connected, _service.State);
        Assert.Equal(new[] { "state:Connecting", "state:Connected" }, Events());

        // LED1 → レポートモード → ステータス要求 の順
        Assert.Equal(
            new[] { new byte[] { 0x11, 0x10 }, new byte[] { 0x12, 0x00, 0x30 }, new byte[] { 0x15, 0x00 } },
            _transport.Connection.Written);
    }

    [Fact]
    public async Task Connect_KeepsScanning_UntilTheDeviceAppears()
    {
        _transport.AvailableFromAttempt = 4;

        await _service.ConnectAsync();

        Assert.True(_service.IsConnected);
        Assert.Equal(4, _transport.Attempts);
    }

    [Fact]
    public async Task Connect_WhenNoDeviceWithinTimeout_ThrowsTimeout_AndReturnsToDisconnected()
    {
        _transport.AvailableFromAttempt = 0;

        await Assert.ThrowsAsync<TimeoutException>(() => _service.ConnectAsync());

        Assert.False(_service.IsConnected);
        Assert.Equal(new[] { "state:Connecting", "state:Disconnected" }, Events());
    }

    [Fact]
    public async Task Connect_WhenAlreadyConnected_DoesNothing()
    {
        await _service.ConnectAsync();
        var attempts = _transport.Attempts;

        await _service.ConnectAsync();

        Assert.Equal(attempts, _transport.Attempts);
    }

    [Fact]
    public async Task ButtonReports_RaisePressedAndReleasedOnce_PerChange()
    {
        await _service.ConnectAsync();

        _transport.Connection.Enqueue(Buttons(0, 0x08));        // A 押下
        _transport.Connection.Enqueue(Buttons(0, 0x08));        // 変化なし
        _transport.Connection.Enqueue(Buttons(0, 0x08 | 0x04)); // B を追加
        _transport.Connection.Enqueue(Buttons(0, 0x04));        // A を離す
        _transport.Connection.Enqueue(Buttons(0, 0));           // B を離す

        await WaitUntilAsync(() => Events().Contains("up:B"), "B released");

        Assert.Equal(
            new[] { "state:Connecting", "state:Connected", "down:A", "down:B", "up:A", "up:B" },
            Events());
    }

    [Fact]
    public async Task StatusReport_UpdatesBattery_AndResendsReportMode()
    {
        await _service.ConnectAsync();
        var writesBefore = _transport.Connection.Written.Count;

        _transport.Connection.Enqueue(0x20, 0, 0, 0, 0, 0, 100);
        await WaitUntilAsync(() => Events().Contains("battery:50"), "battery 50% event");

        Assert.Equal(50, _service.BatteryPercent);
        await WaitUntilAsync(() => _transport.Connection.Written.Count > writesBefore, "report mode resent");
        Assert.Equal(new byte[] { 0x12, 0x00, 0x30 }, _transport.Connection.Written[^1]);
    }

    [Fact]
    public async Task SameBatteryValue_DoesNotRaiseAgain()
    {
        await _service.ConnectAsync();

        _transport.Connection.Enqueue(0x20, 0, 0, 0, 0, 0, 200);
        _transport.Connection.Enqueue(0x20, 0, 0, 0, 0, 0, 200);
        _transport.Connection.Enqueue(0x20, 0, 0, 0, 0, 0, 100);
        await WaitUntilAsync(() => Events().Contains("battery:50"), "battery 50% event");

        Assert.Equal(new[] { "battery:100", "battery:50" }, Events().Where(e => e.StartsWith("battery")));
    }

    [Fact]
    public async Task ReportsOutsideTheDataRange_AreIgnored()
    {
        await _service.ConnectAsync();

        _transport.Connection.Enqueue(0x10, 0xFF, 0xFF);    // 全ビットが立っていても、範囲外の ID は無視される
        _transport.Connection.Enqueue(0x00, 0xFF, 0xFF);
        _transport.Connection.Enqueue(Buttons(0, 0x08));    // A のみ
        await WaitUntilAsync(() => Events().Contains("down:A"), "A pressed");

        Assert.Equal(new[] { "down:A" }, Events().Where(e => e.StartsWith("down:")));
    }

    [Fact]
    public async Task DeviceLoss_ReleasesHeldButtons_ThenDisconnects()
    {
        await _service.ConnectAsync();
        _transport.Connection.Enqueue(Buttons(0, 0x04)); // B を押したまま
        await WaitUntilAsync(() => Events().Contains("down:B"), "B pressed");

        _transport.Connection.FailNextRead(new IOException("device lost"));
        await WaitUntilAsync(() => _service.State == ConnectionState.Disconnected, "disconnect");

        var events = Events();
        Assert.True(events.IndexOf("up:B") < events.IndexOf("state:Disconnected"),
            "held buttons must be released before the disconnect is reported");
        Assert.True(_transport.Connection.IsDisposed);
        Assert.Equal(0, _service.BatteryPercent);
    }

    [Fact]
    public async Task CanReconnect_AfterDeviceLoss()
    {
        await _service.ConnectAsync();
        _transport.Connection.FailNextRead(new IOException("device lost"));
        await WaitUntilAsync(() => _service.State == ConnectionState.Disconnected, "disconnect");

        var second = new FakeWiimoteTransport();
        var service = new WiimoteService(second, FastOptions);
        await service.ConnectAsync();

        Assert.True(service.IsConnected);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task Disconnect_StopsReading_ClosesConnection_AndReportsDisconnected()
    {
        await _service.ConnectAsync();

        await _service.DisconnectAsync();

        Assert.False(_service.IsConnected);
        Assert.True(_transport.Connection.IsDisposed);
        Assert.Equal("state:Disconnected", Events()[^1]);
        Assert.Equal(1, Events().Count(e => e == "state:Disconnected"));
    }

    [Fact]
    public async Task Disconnect_WhenNotConnected_IsHarmless()
    {
        await _service.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, _service.State);
        Assert.Empty(Events());
    }

    [Fact]
    public async Task Disconnect_DuringScan_CancelsConnectWithoutError()
    {
        _transport.AvailableFromAttempt = 0;
        var slow = new WiimoteService(
            _transport,
            new WiimoteServiceOptions(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(5), TimeSpan.FromMinutes(1)));

        var connect = slow.ConnectAsync();
        await WaitUntilAsync(() => slow.State == ConnectionState.Connecting && _transport.Attempts > 1, "scanning");

        await slow.DisconnectAsync();
        await connect; // 例外なしで完了する

        Assert.Equal(ConnectionState.Disconnected, slow.State);
        await slow.DisposeAsync();
    }

    [Fact]
    public async Task ThrowingSubscriber_DoesNotStopTheReadLoop()
    {
        _service.ButtonPressed += (_, e) =>
        {
            if (e.Button == WiimoteButton.A) throw new InvalidOperationException("subscriber bug");
        };
        await _service.ConnectAsync();

        _transport.Connection.Enqueue(Buttons(0, 0x08));
        _transport.Connection.Enqueue(Buttons(0, 0));
        _transport.Connection.Enqueue(Buttons(0, 0x02)); // One

        await WaitUntilAsync(() => Events().Contains("down:One"), "One pressed after subscriber failure");
        Assert.True(_service.IsConnected);
    }

    [Fact]
    public async Task InitializationWriteFailure_ThrowsAndDisconnects()
    {
        var transport = new ThrowingWriteTransport();
        var service = new WiimoteService(transport, FastOptions);

        await Assert.ThrowsAsync<IOException>(() => service.ConnectAsync());

        Assert.Equal(ConnectionState.Disconnected, service.State);
        Assert.True(transport.Connection.IsDisposed);
        await service.DisposeAsync();
    }

    [Fact]
    public void Constructor_RejectsNullTransport()
    {
        Assert.Throws<ArgumentNullException>(() => new WiimoteService(null!));
    }

    private sealed class ThrowingWriteTransport : IWiimoteTransport
    {
        public ThrowingConnection Connection { get; } = new();

        public IWiimoteConnection? TryOpen() => Connection;
    }

    private sealed class ThrowingConnection : IWiimoteConnection
    {
        public bool IsDisposed { get; private set; }

        public int InputReportLength => 22;

        public int Read(byte[] buffer) => 0;

        public void Write(byte[] report) => throw new IOException("write failed");

        public void Dispose() => IsDisposed = true;
    }
}
