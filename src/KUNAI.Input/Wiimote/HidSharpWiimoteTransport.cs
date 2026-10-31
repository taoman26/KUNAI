using HidSharp;

namespace KUNAI.Input.Wiimote;

/// <summary>HidSharp による Wii Remote の検出・オープン(Windows / Linux 共通)。</summary>
public sealed class HidSharpWiimoteTransport : IWiimoteTransport
{
    /// <summary>レポートが来ない間も切断や終了要求を検知できるよう、読み取りに付けるタイムアウト。</summary>
    private const int ReadTimeoutMilliseconds = 500;

    public IWiimoteConnection? TryOpen()
    {
        foreach (var productId in WiimoteProtocol.ProductIds)
        {
            foreach (var device in DeviceList.Local.GetHidDevices(WiimoteProtocol.NintendoVendorId, productId))
            {
                if (device.TryOpen(out var stream))
                {
                    stream.ReadTimeout = ReadTimeoutMilliseconds;
                    return new HidSharpConnection(device, stream);
                }
            }
        }

        return null;
    }

    private sealed class HidSharpConnection : IWiimoteConnection
    {
        private readonly HidStream _stream;
        private readonly int _outputLength;

        public HidSharpConnection(HidDevice device, HidStream stream)
        {
            _stream = stream;
            InputReportLength = Math.Max(device.GetMaxInputReportLength(), 22);
            _outputLength = device.GetMaxOutputReportLength();
        }

        public int InputReportLength { get; }

        public int Read(byte[] buffer)
        {
            try
            {
                return _stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                return 0;
            }
        }

        public void Write(byte[] report)
        {
            // Windows では出力レポート長ぴったりのバッファが必要なため、不足分を 0 で埋める。
            var buffer = report;
            if (_outputLength > report.Length)
            {
                buffer = new byte[_outputLength];
                Array.Copy(report, buffer, report.Length);
            }

            _stream.Write(buffer);
        }

        public void Dispose() => _stream.Dispose();
    }
}
