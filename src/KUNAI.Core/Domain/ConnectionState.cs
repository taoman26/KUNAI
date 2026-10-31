namespace KUNAI.Core.Domain;

/// <summary>接続状態。UI の状態色(赤/黄/緑)に対応する。</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected
}
