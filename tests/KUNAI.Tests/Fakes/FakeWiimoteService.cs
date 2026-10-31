using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Tests.Fakes;

public sealed class FakeWiimoteService : IWiimoteService
{
    public bool IsConnected => State == ConnectionState.Connected;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public int BatteryPercent { get; private set; }

    public int ConnectCalls { get; private set; }

    public int DisconnectCalls { get; private set; }

    /// <summary>設定すると、<see cref="ConnectAsync"/> がこの例外で失敗する。</summary>
    public Exception? ConnectException { get; set; }

    public Task ConnectAsync()
    {
        ConnectCalls++;
        if (ConnectException is { } ex)
        {
            throw ex;
        }

        SetState(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectCalls++;
        SetState(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    public event EventHandler<ButtonChangedEventArgs>? ButtonPressed;

    public event EventHandler<ButtonChangedEventArgs>? ButtonReleased;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public event EventHandler<int>? BatteryChanged;

    public int PressedSubscriberCount => ButtonPressed?.GetInvocationList().Length ?? 0;

    public void Press(WiimoteButton button) => ButtonPressed?.Invoke(this, new ButtonChangedEventArgs(button));

    public void Release(WiimoteButton button) => ButtonReleased?.Invoke(this, new ButtonChangedEventArgs(button));

    public void SetState(ConnectionState state)
    {
        State = state;
        ConnectionStateChanged?.Invoke(this, state);
    }

    public void SetBattery(int percent)
    {
        BatteryPercent = percent;
        BatteryChanged?.Invoke(this, percent);
    }
}
