namespace KUNAI.Core.Abstractions;

/// <summary><see cref="IWiimotePairing.TryPairAsync"/> の結果。</summary>
public enum WiimotePairingResult
{
    /// <summary>この OS ではアプリ側でのペアリングを行わない(OS 側の操作に任せる)。</summary>
    NotSupported,

    /// <summary>探索したが Wii Remote が見つからなかった(1+2 が押されていない等)。</summary>
    NoDeviceFound,

    /// <summary>ペアリング済み、または今回ペアリングに成功し、HID として使える状態にした。</summary>
    Paired,

    /// <summary>Wii Remote は見つかったが、ペアリングに失敗した。</summary>
    Failed,
}

/// <summary>
/// Wii Remote を OS の Bluetooth スタックへ自動でペアリングする。
/// Windows では PIN が Wii Remote の Bluetooth アドレスのバイナリ値となり手入力できないため、アプリが代わりに行う。
/// </summary>
public interface IWiimotePairing
{
    /// <summary>
    /// 1 回分の探索とペアリングを試みる(数秒かかる)。Wii Remote 側は 1 と 2 を同時押しして探索可能な状態にしておく。
    /// 失敗は例外ではなく戻り値で返す。経過は ILogService に記録する。
    /// </summary>
    Task<WiimotePairingResult> TryPairAsync(CancellationToken cancellationToken);
}
