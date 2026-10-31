using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>Wii Remote との接続とボタン入力を提供する。</summary>
public interface IWiimoteService
{
    bool IsConnected { get; }

    /// <summary>接続状態(Connecting を含む)。</summary>
    ConnectionState State { get; }

    /// <summary>電池残量(0〜100)。未接続時は 0。</summary>
    int BatteryPercent { get; }

    Task ConnectAsync();

    Task DisconnectAsync();

    event EventHandler<ButtonChangedEventArgs> ButtonPressed;

    event EventHandler<ButtonChangedEventArgs> ButtonReleased;

    event EventHandler<ConnectionState> ConnectionStateChanged;

    /// <summary>電池残量が変化したとき。引数は新しい残量(%)。</summary>
    event EventHandler<int> BatteryChanged;
}
