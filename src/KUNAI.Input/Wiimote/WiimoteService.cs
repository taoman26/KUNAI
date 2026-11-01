using System.Diagnostics;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Input.Wiimote;

/// <summary>
/// Wii Remote への接続とボタン入力の通知。
/// <see cref="ConnectAsync"/> は Wii Remote が見つかるまで一定時間スキャンし続ける(ペアリング済みで接続が確立している必要がある)。
/// 接続後は専用スレッドでレポートを読み続け、ボタンの押下/解放と電池残量を通知する。
/// イベントは読み取りスレッドから発火される。
/// </summary>
public sealed class WiimoteService : IWiimoteService, IAsyncDisposable
{
    private readonly IWiimoteTransport _transport;
    private readonly WiimoteServiceOptions _options;
    private readonly IWiimotePairing? _pairing;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _stateGate = new();

    private ConnectionState _state = ConnectionState.Disconnected;
    private int _batteryPercent;
    private IWiimoteConnection? _connection;
    private Task? _readLoop;
    private volatile CancellationTokenSource? _cts;

    /// <param name="pairing">
    /// 指定すると、Wii Remote が見つからない間、スキャンの合間に OS へ自動ペアリングを試みる(Windows 向け)。
    /// </param>
    public WiimoteService(
        IWiimoteTransport transport,
        WiimoteServiceOptions? options = null,
        IWiimotePairing? pairing = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? WiimoteServiceOptions.Default;
        _pairing = pairing;
    }

    public bool IsConnected => State == ConnectionState.Connected;

    public ConnectionState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public int BatteryPercent
    {
        get
        {
            lock (_stateGate)
            {
                return _batteryPercent;
            }
        }
    }

    public event EventHandler<ButtonChangedEventArgs>? ButtonPressed;

    public event EventHandler<ButtonChangedEventArgs>? ButtonReleased;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public event EventHandler<int>? BatteryChanged;

    /// <exception cref="TimeoutException">制限時間内に Wii Remote が見つからなかった。</exception>
    public async Task ConnectAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State != ConnectionState.Disconnected)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _cts = cts;
            SetState(ConnectionState.Connecting);

            IWiimoteConnection connection;
            try
            {
                connection = await ScanAsync(cts.Token).ConfigureAwait(false);
                Initialize(connection);
            }
            catch (OperationCanceledException)
            {
                // DisconnectAsync による中断。
                FinishDisconnected(null);
                return;
            }
            catch
            {
                FinishDisconnected(null);
                throw;
            }

            lock (_stateGate)
            {
                _connection = connection;
            }

            SetState(ConnectionState.Connected);
            _readLoop = Task.Factory.StartNew(
                () => ReadLoop(connection, cts.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        // スキャン中(ConnectAsync がロックを保持している間)でも中断できるよう、ロック取得の前にキャンセルする。
        _cts?.Cancel();

        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            IWiimoteConnection? connection;
            lock (_stateGate)
            {
                connection = _connection;
            }

            // 読み取りスレッドのブロックを解くため、接続を閉じる。
            connection?.Dispose();

            if (_readLoop is { } loop)
            {
                await loop.ConfigureAwait(false);
                _readLoop = null;
            }

            FinishDisconnected(connection);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private async Task<IWiimoteConnection> ScanAsync(CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        var pairingDisabled = _pairing is null;
        var lastPairing = TimeSpan.MinValue;
        while (true)
        {
            token.ThrowIfCancellationRequested();

            // HidSharp の列挙はブロッキング呼び出しのため、スレッドプールで実行する。
            var connection = await Task.Run(_transport.TryOpen, token).ConfigureAwait(false);
            if (connection is not null)
            {
                return connection;
            }

            // 見つからない間は OS へのペアリングを試みる。試行自体が数秒かかるので、間隔を空けて繰り返す。
            if (!pairingDisabled
                && (lastPairing == TimeSpan.MinValue || timer.Elapsed - lastPairing >= _options.PairingInterval))
            {
                var result = await _pairing!.TryPairAsync(token).ConfigureAwait(false);
                lastPairing = timer.Elapsed;
                if (result == WiimotePairingResult.NotSupported)
                {
                    pairingDisabled = true;
                }
                else if (result == WiimotePairingResult.Paired)
                {
                    // ペアリング直後は HID の列挙に少し時間がかかるため、すぐ再スキャンに戻る。
                    await Task.Delay(_options.ScanInterval, token).ConfigureAwait(false);
                    continue;
                }
            }

            if (timer.Elapsed >= _options.ConnectTimeout)
            {
                throw new TimeoutException(
                    $"No Wii Remote was found within {_options.ConnectTimeout.TotalSeconds:0} seconds. " +
                    "Press 1+2 on the Wii Remote while connecting (or pair it in the OS Bluetooth settings first).");
            }

            await Task.Delay(_options.ScanInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>接続直後の初期設定: LED1 点灯、ボタンのみのレポートモード、電池残量の要求。</summary>
    private void Initialize(IWiimoteConnection connection)
    {
        try
        {
            connection.Write(WiimoteProtocol.SetLed(1));
            connection.Write(WiimoteProtocol.SetReportModeCoreButtons());
            connection.Write(WiimoteProtocol.StatusRequest());
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void ReadLoop(IWiimoteConnection connection, CancellationToken token)
    {
        var buffer = new byte[connection.InputReportLength];
        var held = new HashSet<WiimoteButton>();
        var statusTimer = Stopwatch.StartNew();

        try
        {
            while (!token.IsCancellationRequested)
            {
                var count = connection.Read(buffer);
                if (count > 0 && WiimoteReportParser.TryParse(buffer.AsSpan(0, count), out var report))
                {
                    if (report.IsStatus)
                    {
                        // 拡張機能の抜き差しでレポートモードが戻るため、ステータスを受けるたびに設定し直す。
                        connection.Write(WiimoteProtocol.SetReportModeCoreButtons());
                        UpdateBattery(report.BatteryPercent!.Value);
                    }

                    ApplyButtons(report.Buttons, held);
                }

                if (statusTimer.Elapsed >= _options.StatusInterval)
                {
                    statusTimer.Restart();
                    connection.Write(WiimoteProtocol.StatusRequest());
                }
            }
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            // 電源オフ・Bluetooth 切断などによるデバイスの喪失。
        }
        catch (Exception)
        {
            // DisconnectAsync による接続のクローズに伴う例外。
        }
        finally
        {
            // 離したイベントが届かないまま終わっても、押しっぱなしが残らないようにする。
            foreach (var button in held)
            {
                Raise(ButtonReleased, new ButtonChangedEventArgs(button));
            }

            if (!token.IsCancellationRequested)
            {
                FinishDisconnected(connection);
            }
        }
    }

    private void ApplyButtons(IReadOnlySet<WiimoteButton> current, HashSet<WiimoteButton> held)
    {
        foreach (var button in current)
        {
            if (held.Add(button))
            {
                Raise(ButtonPressed, new ButtonChangedEventArgs(button));
            }
        }

        foreach (var button in held.Where(b => !current.Contains(b)).ToList())
        {
            held.Remove(button);
            Raise(ButtonReleased, new ButtonChangedEventArgs(button));
        }
    }

    private void UpdateBattery(int percent)
    {
        lock (_stateGate)
        {
            if (_batteryPercent == percent)
            {
                return;
            }

            _batteryPercent = percent;
        }

        BatteryChanged?.Invoke(this, percent);
    }

    /// <summary>
    /// 切断状態へ遷移する。<paramref name="connection"/> が現在の接続でなければ(すでに別の処理が片付けた場合)何もしない。
    /// null の場合は接続を持たない状態からの遷移(接続失敗・スキャン中断)。
    /// </summary>
    private void FinishDisconnected(IWiimoteConnection? connection)
    {
        lock (_stateGate)
        {
            if (connection is not null && !ReferenceEquals(_connection, connection))
            {
                return;
            }

            _connection?.Dispose();
            _connection = null;
            _batteryPercent = 0;
        }

        SetState(ConnectionState.Disconnected);
    }

    private void SetState(ConnectionState state)
    {
        lock (_stateGate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        ConnectionStateChanged?.Invoke(this, state);
    }

    /// <summary>購読者の例外で読み取りループが止まらないよう、通知側で吸収する。</summary>
    private void Raise(EventHandler<ButtonChangedEventArgs>? handler, ButtonChangedEventArgs args)
    {
        try
        {
            handler?.Invoke(this, args);
        }
        catch
        {
            // 購読者の不具合でデバイスの読み取りを止めない。
        }
    }
}
