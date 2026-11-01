using KUNAI.Core.Abstractions;
using KUNAI.Input.Wiimote;
using KUNAI.Platform.Linux;
using KUNAI.Platform.Linux.Bluetooth;
using KUNAI.Tests.Fakes;

namespace KUNAI.Tests.Platform;

public sealed class LinuxWiimotePairingTests
{
    private const string Mac = "00:1E:35:AA:BB:CC";

    private sealed class FakeCtl : IBluetoothCtl
    {
        public string ScanOutput { get; set; } = string.Empty;

        public string DevicesOutput { get; set; } = string.Empty;

        public bool Missing { get; set; }

        public List<string> Commands { get; } = new();

        public Task<string> RunAsync(string arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Commands.Add(arguments);
            if (Missing)
            {
                throw new FileNotFoundException("bluetoothctl was not found");
            }

            return Task.FromResult(arguments.Contains("scan") ? ScanOutput : arguments == "devices" ? DevicesOutput : string.Empty);
        }
    }

    private sealed class FakeConnector : IL2capConnector
    {
        public List<string> Attempts { get; } = new();

        public HashSet<string> Failing { get; } = new();

        public FakeWiimoteConnection Connection { get; } = new();

        public IWiimoteConnection Connect(string address)
        {
            Attempts.Add(address);
            return Failing.Contains(address) ? throw new IOException("refused") : Connection;
        }
    }

    private readonly FakeLogService _log = new();
    private readonly LinuxWiimoteLink _link = new();
    private readonly FakeCtl _ctl = new();
    private readonly FakeConnector _connector = new();

    private LinuxWiimotePairing Create() => new(_log, _link, _ctl, _connector);

    [Fact]
    public async Task WiimoteSeenInScan_IsConnectedAndHandedToTheTransport()
    {
        _ctl.ScanOutput = $"Discovery started\n[NEW] Device {Mac} Nintendo RVL-CNT-01\n";

        var result = await Create().TryPairAsync(default);

        Assert.Equal(WiimotePairingResult.Paired, result);
        Assert.Equal(new[] { Mac }, _connector.Attempts);
        var transport = new LinuxWiimoteTransport(new FakeWiimoteTransport { AvailableFromAttempt = 0 }, _link);
        Assert.Same(_connector.Connection, transport.TryOpen());
        Assert.Null(transport.TryOpen()); // 受け渡しは 1 回だけ
        Assert.Contains("Wii Remote connected", _log.Messages);
    }

    [Fact]
    public async Task NameFromChangeLineAndAnsiColors_AreUnderstood()
    {
        _ctl.ScanOutput = $"\u001b[0;93m[CHG]\u001b[0m Device {Mac} RSSI: -50\n[CHG] Device {Mac} Name: Nintendo RVL-CNT-01\n";

        Assert.Equal(WiimotePairingResult.Paired, await Create().TryPairAsync(default));
    }

    [Fact]
    public async Task KnownDeviceName_ComesFromTheDevicesList()
    {
        _ctl.ScanOutput = $"[CHG] Device {Mac} RSSI: -60\n";
        _ctl.DevicesOutput = $"Device {Mac} Nintendo RVL-CNT-01\nDevice 11:22:33:44:55:66 Headset\n";

        Assert.Equal(WiimotePairingResult.Paired, await Create().TryPairAsync(default));
    }

    [Fact]
    public async Task KnownButNotSeenInThisScan_IsIgnored()
    {
        _ctl.ScanOutput = "Discovery started\n";
        _ctl.DevicesOutput = $"Device {Mac} Nintendo RVL-CNT-01\n";

        Assert.Equal(WiimotePairingResult.NoDeviceFound, await Create().TryPairAsync(default));
        Assert.Empty(_connector.Attempts);
    }

    [Fact]
    public async Task OtherDevices_AreIgnored()
    {
        _ctl.ScanOutput = "[NEW] Device 11:22:33:44:55:66 Headset\n[NEW] Device 11:22:33:44:55:77 Nintendo RVL-WBC-01\n";

        Assert.Equal(WiimotePairingResult.NoDeviceFound, await Create().TryPairAsync(default));
    }

    [Fact]
    public async Task ConnectionFailure_TriesTheNextCandidateThenReportsFailure()
    {
        const string other = "00:1E:35:11:22:33";
        _ctl.ScanOutput = $"[NEW] Device {Mac} Nintendo RVL-CNT-01\n[NEW] Device {other} Nintendo RVL-CNT-01\n";
        _connector.Failing.Add(Mac);

        Assert.Equal(WiimotePairingResult.Paired, await Create().TryPairAsync(default));
        Assert.Equal(new[] { Mac, other }, _connector.Attempts);

        _connector.Failing.Add(other);
        Assert.Equal(WiimotePairingResult.Failed, await Create().TryPairAsync(default));
        Assert.Contains(_log.Messages, m => m.Contains("refused"));
    }

    [Fact]
    public async Task MissingBluetoothctl_MeansNotSupportedAndIsLoggedOnce()
    {
        _ctl.Missing = true;
        var pairing = Create();

        Assert.Equal(WiimotePairingResult.NotSupported, await pairing.TryPairAsync(default));
        Assert.Equal(WiimotePairingResult.NotSupported, await pairing.TryPairAsync(default));
        Assert.Single(_log.Messages);
    }

    [Fact]
    public async Task UnusedPendingConnection_IsDisposedOnTheNextAttempt()
    {
        var stale = new FakeWiimoteConnection();
        _link.Set(stale);

        await Create().TryPairAsync(default);

        Assert.True(stale.IsDisposed);
    }

    [Fact]
    public void Transport_FallsBackToTheInnerTransport()
    {
        var inner = new FakeWiimoteTransport();
        var transport = new LinuxWiimoteTransport(inner, _link);

        Assert.Same(inner.Connection, transport.TryOpen());
    }

    [Fact]
    public void ParseAddress_ReversesTheBytes()
    {
        Assert.Equal(new byte[] { 0xCC, 0xBB, 0xAA, 0x35, 0x1E, 0x00 }, L2capConnector.ParseAddress(Mac));
        Assert.Throws<ArgumentException>(() => L2capConnector.ParseAddress("nope"));
    }

    [Fact]
    public void BuildSockAddr_HasTheL2capLayout()
    {
        var addr = L2capConnector.BuildSockAddr(L2capConnector.ParseAddress(Mac), 0x13);

        Assert.Equal(14, addr.Length);
        Assert.Equal(new byte[] { 31, 0, 0x13, 0x00, 0xCC, 0xBB, 0xAA, 0x35, 0x1E, 0x00, 0, 0, 0, 0 }, addr);
    }
}
