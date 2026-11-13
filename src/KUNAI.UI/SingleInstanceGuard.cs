using System.IO.Pipes;
using System.Text;

namespace KUNAI.UI;

/// <summary>
/// 多重起動の防止。ユーザーごとに 1 つだけ KUNAI を動かし、2 つ目の起動は、動いている方のウィンドウを前面に出して終了する。
/// (同じ Wii Remote を複数のプロセスが取り合ったり、プロファイルを互いに上書きしたりするのを防ぐ。)
/// <list type="bullet">
/// <item>排他: ロックファイルを共有なしで開き続ける(OS が終了時・異常終了時に解放する)。</item>
/// <item>通知: 名前付きパイプで、動いている方へ「ウィンドウを表示して」と伝える。</item>
/// </list>
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string ShowMessage = "show";
    private const string AckMessage = "ok";

    private readonly string _lockPath;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();
    private FileStream? _lock;
    private Task? _listener;
    private bool _disposed;

    public SingleInstanceGuard(string lockPath, string pipeName)
    {
        _lockPath = lockPath ?? throw new ArgumentNullException(nameof(lockPath));
        _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
    }

    /// <summary>通常の場所(ユーザーの設定領域)とユーザー名から作る。</summary>
    public static SingleInstanceGuard CreateDefault()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KUNAI");
        var user = new string(Environment.UserName.Where(char.IsLetterOrDigit).ToArray());
        return new SingleInstanceGuard(Path.Combine(directory, "instance.lock"), $"KUNAI-{user}");
    }

    /// <summary>別のインスタンスから「ウィンドウを表示して」と求められた。リスナースレッドから発火する。</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// 最初のインスタンスになれたら true。すでに動いていれば false。
    /// ロックファイルを作れないなど、判定できない場合は、起動できなくならないよう true を返す(防止なしで起動)。
    /// </summary>
    public bool TryAcquire()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return true; // ロックの置き場所を作れない。判定できないので、防止なしで起動する。
        }

        try
        {
            _lock = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false; // 他のプロセスがロック中 = すでに動いている。
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or NotSupportedException)
        {
            return true;
        }
    }

    /// <summary>最初のインスタンスとして、他の起動からの通知を待ち始める。</summary>
    public void StartListening()
    {
        _listener ??= Task.Run(() => ListenAsync(_cts.Token));
    }

    /// <summary>
    /// 動いているインスタンスへ、ウィンドウの表示を求める。受け取った確認(ack)が返れば true。
    /// 待ち受けが次の接続を準備している最中(Unix の名前付きパイプの性質)に当たると届かないので、時間内なら繰り返す。
    /// </summary>
    public bool SignalExistingInstance(TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
                client.Connect(Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds));

                var bytes = Encoding.UTF8.GetBytes(ShowMessage + "\n");
                client.Write(bytes, 0, bytes.Length);
                client.Flush();

                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                var reader = new StreamReader(client, Encoding.UTF8);
                if (reader.ReadLineAsync(cts.Token).AsTask().GetAwaiter().GetResult() == AckMessage)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // 起動の途中でまだ待ち受けていない等。もう一度試す。
            }

            Thread.Sleep(50);
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // 待ち受けの終了に伴うキャンセル。
        }

        _lock?.Dispose();
        _lock = null;
        _cts.Dispose();
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line == ShowMessage)
                {
                    ActivationRequested?.Invoke(this, EventArgs.Empty);
                    var ack = Encoding.UTF8.GetBytes(AckMessage + "\n");
                    await server.WriteAsync(ack, token).ConfigureAwait(false);
                    await server.FlushAsync(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // 相手が途中で切れた。次の接続を待つ。
            }
            catch (Exception)
            {
                // 通知の不具合でアプリを止めない。少し待って再開する。
                try
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
