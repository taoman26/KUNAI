using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;
using KUNAI.Input.Wiimote;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Input;

public sealed class WiimoteServicePairingTests
{
    private static readonly WiimoteServiceOptions Options = new(
        TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(5), TimeSpan.FromMinutes(10), TimeSpan.FromMilliseconds(10));

    private sealed class ScriptedPairing : IWiimotePairing
    {
        private readonly Func<int, WiimotePairingResult> _script;
        private int _calls;

        public ScriptedPairing(Func<int, WiimotePairingResult> script) => _script = script;

        public int Calls => _calls;

        public Task<WiimotePairingResult> TryPairAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_script(Interlocked.Increment(ref _calls)));
    }

    [Fact]
    public async Task WhenNotFound_PairingIsTriedAndTheDeviceIsThenOpened()
    {
        var transport = new FakeWiimoteTransport { AvailableFromAttempt = 3 };
        var pairing = new ScriptedPairing(n => n == 1 ? WiimotePairingResult.Paired : WiimotePairingResult.NoDeviceFound);
        await using var service = new WiimoteService(transport, Options, pairing);

        await service.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, service.State);
        Assert.True(pairing.Calls >= 1);
    }

    [Fact]
    public async Task WhenDeviceIsAlreadyAvailable_PairingIsNotTried()
    {
        var transport = new FakeWiimoteTransport();
        var pairing = new ScriptedPairing(_ => WiimotePairingResult.Paired);
        await using var service = new WiimoteService(transport, Options, pairing);

        await service.ConnectAsync();

        Assert.Equal(0, pairing.Calls);
    }

    [Fact]
    public async Task NotSupported_StopsFurtherPairingAttempts()
    {
        var transport = new FakeWiimoteTransport { AvailableFromAttempt = 0 };
        var pairing = new ScriptedPairing(_ => WiimotePairingResult.NotSupported);
        await using var service = new WiimoteService(transport, Options, pairing);

        await Assert.ThrowsAsync<TimeoutException>(service.ConnectAsync);

        Assert.Equal(1, pairing.Calls);
        Assert.Equal(ConnectionState.Disconnected, service.State);
    }

    [Fact]
    public async Task NoDeviceFound_IsRetriedUntilTheTimeout()
    {
        var transport = new FakeWiimoteTransport { AvailableFromAttempt = 0 };
        var pairing = new ScriptedPairing(_ => WiimotePairingResult.NoDeviceFound);
        await using var service = new WiimoteService(transport, Options, pairing);

        await Assert.ThrowsAsync<TimeoutException>(service.ConnectAsync);

        Assert.True(pairing.Calls > 1);
    }

    [Fact]
    public void DefaultOptions_HavePairingInterval()
    {
        Assert.True(WiimoteServiceOptions.Default.PairingInterval > TimeSpan.Zero);
        Assert.True(WiimoteServiceOptions.Default.ConnectTimeout >= TimeSpan.FromSeconds(20));
    }
}
