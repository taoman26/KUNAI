using KUNAI.Core.Abstractions;
using KUNAI.Platform.Windows;
using KUNAI.Platform.Windows.Bluetooth;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Platform;

public sealed class WindowsWiimotePairingTests
{
    private const ulong WiimoteAddress = 0x0017AB123456;
    private const ulong RadioAddress = 0x001122334455;

    private sealed class FakeBluetoothApi : IBluetoothApi
    {
        public List<BluetoothRadio> Radios { get; } = new() { new BluetoothRadio(1, RadioAddress, "PC") };

        public List<BluetoothDevice> Devices { get; } = new();

        /// <summary>これらの PIN だけ認証に成功する。</summary>
        public List<byte[]> AcceptedPins { get; } = new();

        public List<byte[]> TriedPins { get; } = new();

        public List<string> Calls { get; } = new();

        public bool Disposed { get; private set; }

        public Exception? GetRadiosThrows { get; set; }

        public IReadOnlyList<BluetoothRadio> GetRadios()
        {
            if (GetRadiosThrows is not null)
            {
                throw GetRadiosThrows;
            }

            return Radios;
        }

        public IReadOnlyList<BluetoothDevice> Discover(BluetoothRadio radio, int timeoutMultiplier)
        {
            Calls.Add("discover");
            return Devices;
        }

        public uint Authenticate(BluetoothRadio radio, BluetoothDevice device, byte[] pin)
        {
            Calls.Add("auth");
            TriedPins.Add(pin);
            return AcceptedPins.Any(p => p.SequenceEqual(pin)) ? 0u : 1244u;
        }

        public uint EnableHidService(BluetoothRadio radio, BluetoothDevice device)
        {
            Calls.Add("hid");
            return 0;
        }

        public uint Remove(BluetoothDevice device)
        {
            Calls.Add("remove");
            return 0;
        }

        public void Dispose() => Disposed = true;
    }

    private readonly FakeLogService _log = new();
    private readonly FakeBluetoothApi _api = new();

    private WindowsWiimotePairing Create() => new(_log, () => _api);

    private static BluetoothDevice Wiimote(bool connected = false, bool remembered = false, bool authenticated = false) =>
        new(WiimoteAddress, "Nintendo RVL-CNT-01", connected, remembered, authenticated);

    [Fact]
    public void PinFromAddress_IsTheSixLowBytesLittleEndian()
    {
        Assert.Equal(new byte[] { 0x56, 0x34, 0x12, 0xAB, 0x17, 0x00 }, WindowsWiimotePairing.PinFromAddress(WiimoteAddress));
    }

    [Theory]
    [InlineData("Nintendo RVL-CNT-01", true)]
    [InlineData("Nintendo RVL-CNT-01-TR", true)]
    [InlineData("nintendo rvl-cnt-01", true)]
    [InlineData("Nintendo RVL-WBC-01", false)]
    [InlineData("Some Headset", false)]
    [InlineData("", false)]
    public void IsWiimote_MatchesOnlyRemoteControllers(string name, bool expected)
    {
        Assert.Equal(expected, WindowsWiimotePairing.IsWiimote(new BluetoothDevice(1, name, false, false, false)));
    }

    [Fact]
    public async Task NoRadio_ReportsNoDeviceAndLogsOnce()
    {
        _api.Radios.Clear();
        var pairing = Create();

        Assert.Equal(WiimotePairingResult.NoDeviceFound, await pairing.TryPairAsync(default));
        Assert.Equal(WiimotePairingResult.NoDeviceFound, await pairing.TryPairAsync(default));

        Assert.Single(_log.Messages);
        Assert.True(_api.Disposed);
    }

    [Fact]
    public async Task NoWiimoteNearby_ReportsNoDevice()
    {
        _api.Devices.Add(new BluetoothDevice(9, "Headset", false, false, false));

        Assert.Equal(WiimotePairingResult.NoDeviceFound, await Create().TryPairAsync(default));
        Assert.DoesNotContain("auth", _api.Calls);
    }

    [Fact]
    public async Task Wiimote_IsAuthenticatedWithItsOwnAddressFirstThenHidIsEnabled()
    {
        _api.Devices.Add(Wiimote());
        _api.AcceptedPins.Add(WindowsWiimotePairing.PinFromAddress(WiimoteAddress));

        var result = await Create().TryPairAsync(default);

        Assert.Equal(WiimotePairingResult.Paired, result);
        Assert.Single(_api.TriedPins);
        Assert.Equal(new[] { "discover", "auth", "hid" }, _api.Calls);
        Assert.Contains("Wii Remote paired", _log.Messages);
    }

    [Fact]
    public async Task WhenOwnAddressIsRejected_TheHostAddressIsTried()
    {
        _api.Devices.Add(Wiimote());
        _api.AcceptedPins.Add(WindowsWiimotePairing.PinFromAddress(RadioAddress));

        var result = await Create().TryPairAsync(default);

        Assert.Equal(WiimotePairingResult.Paired, result);
        Assert.Equal(2, _api.TriedPins.Count);
    }

    [Fact]
    public async Task WhenNoPinWorks_ReportsFailureWithTheErrorCode()
    {
        _api.Devices.Add(Wiimote());

        var result = await Create().TryPairAsync(default);

        Assert.Equal(WiimotePairingResult.Failed, result);
        Assert.Contains(_log.Messages, m => m.Contains("1244"));
        Assert.DoesNotContain("hid", _api.Calls);
    }

    [Fact]
    public async Task StalePairing_IsRemovedBeforeAuthenticating()
    {
        _api.Devices.Add(Wiimote(remembered: true, authenticated: true));
        _api.AcceptedPins.Add(WindowsWiimotePairing.PinFromAddress(WiimoteAddress));

        await Create().TryPairAsync(default);

        Assert.Equal(new[] { "discover", "remove", "auth", "hid" }, _api.Calls);
    }

    [Fact]
    public async Task AlreadyConnectedWiimote_OnlyEnablesHid()
    {
        _api.Devices.Add(Wiimote(connected: true, remembered: true, authenticated: true));

        var result = await Create().TryPairAsync(default);

        Assert.Equal(WiimotePairingResult.Paired, result);
        Assert.Equal(new[] { "discover", "hid" }, _api.Calls);
    }

    [Fact]
    public async Task MissingBluetoothLibrary_MeansNotSupported()
    {
        _api.GetRadiosThrows = new DllNotFoundException("bthprops.cpl");

        Assert.Equal(WiimotePairingResult.NotSupported, await Create().TryPairAsync(default));
    }

    [Fact]
    public async Task UnexpectedError_ReportsFailureAndLogs()
    {
        _api.GetRadiosThrows = new InvalidOperationException("boom");

        Assert.Equal(WiimotePairingResult.Failed, await Create().TryPairAsync(default));
        Assert.Contains(_log.Messages, m => m.Contains("boom"));
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().TryPairAsync(cts.Token));
    }

    [Fact]
    public async Task FoundMessage_IsLoggedOncePerDevice()
    {
        _api.Devices.Add(Wiimote());
        var pairing = Create();

        await pairing.TryPairAsync(default);
        await pairing.TryPairAsync(default);

        Assert.Single(_log.Messages, m => m.StartsWith("Wii Remote found"));
    }
}
