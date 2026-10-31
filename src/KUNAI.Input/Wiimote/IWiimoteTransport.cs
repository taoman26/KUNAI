namespace KUNAI.Input.Wiimote;

/// <summary>開いている Wii Remote との接続。</summary>
public interface IWiimoteConnection : IDisposable
{
    /// <summary>1 レポートの最大長(読み取りバッファのサイズ)。</summary>
    int InputReportLength { get; }

    /// <summary>1 レポート読む。一定時間レポートが来なければ 0 を返す。切断された場合は <see cref="IOException"/>。</summary>
    int Read(byte[] buffer);

    /// <summary>出力レポート(先頭がレポート ID)を送る。</summary>
    void Write(byte[] report);
}

/// <summary>Wii Remote の検出とオープン。HID ライブラリへの依存をここに閉じ込める。</summary>
public interface IWiimoteTransport
{
    /// <summary>接続されている Wii Remote を探して開く。見つからなければ null。</summary>
    IWiimoteConnection? TryOpen();
}
