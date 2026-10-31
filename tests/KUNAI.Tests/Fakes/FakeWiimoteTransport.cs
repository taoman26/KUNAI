using System.Collections.Concurrent;
using KUNAI.Input.Wiimote;

namespace KUNAI.Tests.Fakes;

/// <summary>Wii Remote の代わり。受信レポートを <see cref="Enqueue"/> で流し込み、書き込まれた出力レポートを記録する。</summary>
public sealed class FakeWiimoteConnection : IWiimoteConnection
{
    private readonly BlockingCollection<object> _incoming = new();
    private volatile bool _disposed;

    public int InputReportLength => 22;

    public List<byte[]> Written { get; } = new();

    public bool IsDisposed => _disposed;

    public void Enqueue(params byte[] report) => _incoming.Add(report);

    /// <summary>次の Read で例外を投げる(デバイスの喪失を再現)。</summary>
    public void FailNextRead(Exception exception) => _incoming.Add(exception);

    public int Read(byte[] buffer)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FakeWiimoteConnection));
        }

        if (!_incoming.TryTake(out var item, 20))
        {
            return 0; // タイムアウト
        }

        if (item is Exception ex)
        {
            throw ex;
        }

        var report = (byte[])item;
        report.CopyTo(buffer, 0);
        return report.Length;
    }

    public void Write(byte[] report)
    {
        lock (Written)
        {
            Written.Add(report);
        }
    }

    public void Dispose() => _disposed = true;
}

public sealed class FakeWiimoteTransport : IWiimoteTransport
{
    private int _attempts;

    /// <summary>この回数目の試行から見つかる(1 なら初回から)。0 以下なら見つからない。</summary>
    public int AvailableFromAttempt { get; set; } = 1;

    public FakeWiimoteConnection Connection { get; } = new();

    public int Attempts => _attempts;

    public IWiimoteConnection? TryOpen()
    {
        var attempt = Interlocked.Increment(ref _attempts);
        return AvailableFromAttempt > 0 && attempt >= AvailableFromAttempt ? Connection : null;
    }
}
