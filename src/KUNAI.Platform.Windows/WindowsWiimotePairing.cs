using KUNAI.Core.Abstractions;
using KUNAI.Platform.Windows.Bluetooth;

namespace KUNAI.Platform.Windows;

/// <summary>
/// Windows で Wii Remote を自動ペアリングする。
/// Windows の標準 UI は PIN 入力を求めるが、その PIN は Bluetooth アドレスのバイナリ値で手入力できない。
/// そこで探索で見つけた Wii Remote に対し、アプリが PIN を計算して認証し、HID サービスを有効にする。
/// <list type="bullet">
/// <item>1+2 同時押し(探索可能モード)の場合の PIN は、Wii Remote 自身のアドレス(下位バイトから 6 バイト)。</item>
/// <item>赤い SYNC ボタンの場合の PIN は、PC 側アダプターのアドレス(同形式)。両方を順に試す。</item>
/// </list>
/// </summary>
public sealed class WindowsWiimotePairing : IWiimotePairing
{
    /// <summary>探索 1 回の長さ(× 1.28 秒)。</summary>
    private const int DiscoveryTimeoutMultiplier = 3;

    private readonly ILogService _log;
    private readonly Func<IBluetoothApi> _apiFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ulong _lastReportedAddress;
    private bool _noRadioReported;

    public WindowsWiimotePairing(ILogService log)
        : this(log, () => new Win32BluetoothApi())
    {
    }

    internal WindowsWiimotePairing(ILogService log, Func<IBluetoothApi> apiFactory)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _apiFactory = apiFactory ?? throw new ArgumentNullException(nameof(apiFactory));
    }

    public async Task<WiimotePairingResult> TryPairAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Bluetooth API はブロッキング呼び出しのため、スレッドプールで実行する。
            return await Task.Run(() => TryPair(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Wii Remote の PIN: アドレス整数の下位バイトから 6 バイト。</summary>
    internal static byte[] PinFromAddress(ulong address)
    {
        var bytes = new byte[6];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)((address >> (8 * i)) & 0xFF);
        }

        return bytes;
    }

    internal static bool IsWiimote(BluetoothDevice device) =>
        device.Name.StartsWith("Nintendo RVL-CNT-01", StringComparison.OrdinalIgnoreCase);

    private WiimotePairingResult TryPair(CancellationToken token)
    {
        try
        {
            using var api = _apiFactory();
            var radios = api.GetRadios();
            if (radios.Count == 0)
            {
                if (!_noRadioReported)
                {
                    _noRadioReported = true;
                    _log.Add("Bluetooth adapter not found (is Bluetooth turned on?)");
                }

                return WiimotePairingResult.NoDeviceFound;
            }

            var sawWiimote = false;
            foreach (var radio in radios)
            {
                token.ThrowIfCancellationRequested();
                foreach (var device in api.Discover(radio, DiscoveryTimeoutMultiplier).Where(IsWiimote))
                {
                    sawWiimote = true;
                    token.ThrowIfCancellationRequested();
                    if (PairDevice(api, radio, device))
                    {
                        return WiimotePairingResult.Paired;
                    }
                }
            }

            return sawWiimote ? WiimotePairingResult.Failed : WiimotePairingResult.NoDeviceFound;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _log.Add($"Bluetooth API unavailable: {ex.Message}");
            return WiimotePairingResult.NotSupported;
        }
        catch (Exception ex)
        {
            _log.Add($"Pairing error: {ex.Message}");
            return WiimotePairingResult.Failed;
        }
    }

    private bool PairDevice(IBluetoothApi api, BluetoothRadio radio, BluetoothDevice device)
    {
        if (_lastReportedAddress != device.Address)
        {
            _lastReportedAddress = device.Address;
            _log.Add($"Wii Remote found ({device.Name}), pairing...");
        }

        // すでに接続済みなら認証は不要。HID サービスだけ確実に有効にする。
        if (device.Connected && device.Authenticated)
        {
            api.EnableHidService(radio, device);
            return true;
        }

        // 以前のペアリング情報が残っていると、新しい認証が失敗したり古い鍵で接続しようとしたりするため消す。
        if (device.Remembered || device.Authenticated)
        {
            api.Remove(device);
        }

        var pins = new[] { PinFromAddress(device.Address), PinFromAddress(radio.Address) };
        uint lastError = 0;
        foreach (var pin in pins)
        {
            lastError = api.Authenticate(radio, device, pin);
            if (lastError == 0)
            {
                var hidError = api.EnableHidService(radio, device);
                if (hidError != 0)
                {
                    _log.Add($"Paired, but enabling the HID service returned error {hidError}");
                }

                _log.Add("Wii Remote paired");
                return true;
            }
        }

        _log.Add($"Pairing failed (error {lastError}). Press 1+2 again and keep the LEDs blinking.");
        return false;
    }
}
