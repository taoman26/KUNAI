using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Core.Services;

/// <summary>
/// ボタン入力 → レイヤー更新 → マッピング解決 → アクション実行 を仲介する。
/// <list type="bullet">
/// <item>B と Home はレイヤー切替専用で、アクションには割り当てない(割り当てても無視する)。</item>
/// <item>アクションは入力スレッドを塞がないよう別タスクで、押された順に 1 つずつ実行する。</item>
/// <item>実行するレイヤーは「押した時点」のレイヤーで決める(実行待ちの間にレイヤーが変わっても影響しない)。</item>
/// </list>
/// </summary>
public sealed class InputOrchestrator : IInputOrchestrator, IDisposable
{
    private readonly IWiimoteService _wiimote;
    private readonly ILayerManager _layers;
    private readonly IActionExecutor _actions;
    private readonly IMacroExecutor _macros;
    private readonly ILogService _log;

    private readonly object _gate = new();
    private volatile Profile? _profile;
    private bool _started;
    private CancellationTokenSource? _cts;
    private Task _tail = Task.CompletedTask;

    public InputOrchestrator(
        IWiimoteService wiimote,
        ILayerManager layers,
        IActionExecutor actions,
        IMacroExecutor macros,
        ILogService log)
    {
        _wiimote = wiimote ?? throw new ArgumentNullException(nameof(wiimote));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _macros = macros ?? throw new ArgumentNullException(nameof(macros));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void SetProfile(Profile profile)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _cts = new CancellationTokenSource();
            _wiimote.ButtonPressed += OnButtonPressed;
            _wiimote.ButtonReleased += OnButtonReleased;
            _wiimote.ConnectionStateChanged += OnConnectionStateChanged;
            _layers.LayerChanged += OnLayerChanged;
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _wiimote.ButtonPressed -= OnButtonPressed;
            _wiimote.ButtonReleased -= OnButtonReleased;
            _wiimote.ConnectionStateChanged -= OnConnectionStateChanged;
            _layers.LayerChanged -= OnLayerChanged;
            cts = _cts;
            _cts = null;
        }

        cts?.Cancel();
        cts?.Dispose();
    }

    public void Dispose() => Stop();

    /// <summary>実行待ちのアクションがすべて終わったときに完了するタスク(テスト用)。</summary>
    internal Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _tail;
        }
    }

    private static bool IsLayerButton(WiimoteButton button) => button is WiimoteButton.B or WiimoteButton.Home;

    private void OnButtonPressed(object? sender, ButtonChangedEventArgs e)
    {
        var button = e.Button;

        // レイヤー更新を先に行う。LayerChanged(ログ "Layer1")は "A Pressed" より前に出る。
        _layers.OnButtonPressed(button);
        if (IsLayerButton(button))
        {
            return;
        }

        _log.Add($"{button} Pressed");

        var layer = _layers.ActiveLayer;
        var mapping = _profile?.FindMapping(layer, button);
        if (mapping is null)
        {
            return;
        }

        // 実行待ちの間にプロファイルが編集されても影響しないよう、ここで値を確定させる。
        IReadOnlyList<string>? macro = mapping.IsMacro ? mapping.Macro!.ToArray() : null;
        var action = mapping.Action;
        if (macro is null && string.IsNullOrWhiteSpace(action))
        {
            return;
        }

        CancellationToken token;
        lock (_gate)
        {
            token = _cts?.Token ?? CancellationToken.None;
            _tail = _tail.ContinueWith(
                _ => RunAsync(macro, action, token),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();
        }
    }

    private void OnButtonReleased(object? sender, ButtonChangedEventArgs e) => _layers.OnButtonReleased(e.Button);

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        _log.Add(state.ToString());

        if (state == ConnectionState.Disconnected)
        {
            // 切断で「離した」イベントが届かないことがあるため、押下状態を破棄して Layer0 に戻す。
            _layers.Reset();
        }
    }

    private void OnLayerChanged(object? sender, int layer) => _log.Add($"Layer{layer}");

    private async Task RunAsync(IReadOnlyList<string>? macro, string action, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        try
        {
            if (macro is not null)
            {
                await _macros.ExecuteAsync(macro, token).ConfigureAwait(false);
            }
            else
            {
                await _actions.ExecuteAsync(action).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Stop による中断。
        }
        catch (Exception ex)
        {
            // 1 つの失敗が後続のアクションを止めないよう、ここで吸収してログに残す。
            _log.Add($"Error: {ex.Message}");
        }
    }
}
