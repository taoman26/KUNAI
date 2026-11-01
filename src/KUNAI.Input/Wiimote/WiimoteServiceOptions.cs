namespace KUNAI.Input.Wiimote;

/// <summary>WiimoteService の接続待ち・電池確認の設定。</summary>
/// <param name="ConnectTimeout">Wii Remote を探し続ける時間。この間に見つからなければ接続失敗。</param>
/// <param name="ScanInterval">探す間隔。</param>
/// <param name="StatusInterval">電池残量を問い合わせる間隔。</param>
/// <param name="PairingInterval">自動ペアリングを試みる間隔(Wii Remote が見つからない間)。</param>
public sealed record WiimoteServiceOptions(
    TimeSpan ConnectTimeout,
    TimeSpan ScanInterval,
    TimeSpan StatusInterval,
    TimeSpan PairingInterval)
{
    public WiimoteServiceOptions(TimeSpan connectTimeout, TimeSpan scanInterval, TimeSpan statusInterval)
        : this(connectTimeout, scanInterval, statusInterval, TimeSpan.FromSeconds(2))
    {
    }

    public static WiimoteServiceOptions Default { get; } =
        new(TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(2));
}
