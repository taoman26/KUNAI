using KUNAI.Input.Wiimote;

namespace KUNAI.Platform.Linux.Bluetooth;

/// <summary>
/// 自動ペアリングで確立した L2CAP 接続を優先し、無ければ従来どおり hidraw(HidSharp)から Wii Remote を探す。
/// </summary>
public sealed class LinuxWiimoteTransport : IWiimoteTransport
{
    private readonly IWiimoteTransport _inner;
    private readonly LinuxWiimoteLink _link;

    public LinuxWiimoteTransport(IWiimoteTransport inner, LinuxWiimoteLink link)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _link = link ?? throw new ArgumentNullException(nameof(link));
    }

    public IWiimoteConnection? TryOpen() => _link.Take() ?? _inner.TryOpen();
}
