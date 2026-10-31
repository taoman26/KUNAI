namespace KUNAI.Core.Domain;

/// <summary>デバイスの現在状態。</summary>
public class DeviceStatus
{
    public bool IsConnected { get; set; }

    public int BatteryPercent { get; set; }

    public int ActiveLayer { get; set; }

    public ConnectionState State { get; set; } = ConnectionState.Disconnected;
}
