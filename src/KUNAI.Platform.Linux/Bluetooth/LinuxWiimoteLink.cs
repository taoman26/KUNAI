using KUNAI.Input.Wiimote;

namespace KUNAI.Platform.Linux.Bluetooth;

/// <summary>
/// 自動ペアリング(<see cref="LinuxWiimotePairing"/>)が確立した接続を、
/// 接続待ち中の WiimoteService へ渡す受け渡し場所(<see cref="LinuxWiimoteTransport"/> が受け取る)。
/// </summary>
public sealed class LinuxWiimoteLink
{
    private readonly object _gate = new();
    private IWiimoteConnection? _pending;

    internal bool HasPending
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }

    internal void Set(IWiimoteConnection connection)
    {
        IWiimoteConnection? old;
        lock (_gate)
        {
            old = _pending;
            _pending = connection;
        }

        old?.Dispose();
    }

    internal IWiimoteConnection? Take()
    {
        lock (_gate)
        {
            var connection = _pending;
            _pending = null;
            return connection;
        }
    }

    internal void Clear() => Take()?.Dispose();
}
