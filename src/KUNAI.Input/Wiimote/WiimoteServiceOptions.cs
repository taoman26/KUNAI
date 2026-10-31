namespace KUNAI.Input.Wiimote;

/// <summary>WiimoteService の接続待ち・電池確認の設定。</summary>
/// <param name="ConnectTimeout">Wii Remote を探し続ける時間。この間に見つからなければ接続失敗。</param>
/// <param name="ScanInterval">探す間隔。</param>
/// <param name="StatusInterval">電池残量を問い合わせる間隔。</param>
public sealed record WiimoteServiceOptions(
    TimeSpan ConnectTimeout,
    TimeSpan ScanInterval,
    TimeSpan StatusInterval)
{
    public static WiimoteServiceOptions Default { get; } =
        new(TimeSpan.FromSeconds(20), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(60));
}
