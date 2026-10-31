using KUNAI.Core.Domain;

namespace KUNAI.Input.Wiimote;

/// <summary>Wii Remote の HID プロトコル定数と、出力レポートの組み立て。</summary>
public static class WiimoteProtocol
{
    public const int NintendoVendorId = 0x057E;

    /// <summary>RVL-CNT-01(初代)。</summary>
    public const int WiimoteProductId = 0x0306;

    /// <summary>RVL-CNT-01-TR(Wii Remote Plus)。</summary>
    public const int WiimotePlusProductId = 0x0330;

    public static readonly IReadOnlyList<int> ProductIds = new[] { WiimoteProductId, WiimotePlusProductId };

    // 入力レポート ID
    public const byte StatusReportId = 0x20;
    public const byte FirstButtonReportId = 0x20;
    public const byte LastButtonReportId = 0x3F;

    // 出力レポート ID
    private const byte OutputLedReportId = 0x11;
    private const byte OutputReportModeId = 0x12;
    private const byte OutputStatusRequestId = 0x15;

    /// <summary>電池残量の最大値(ステータスレポートの値は 0〜200)。</summary>
    public const int BatteryRawMax = 200;

    /// <summary>ボタンのみを送る基本レポートモード(0x30)。</summary>
    private const byte CoreButtonsMode = 0x30;

    /// <summary>ステータスレポートの要求(電池残量の取得)。</summary>
    public static byte[] StatusRequest() => new byte[] { OutputStatusRequestId, 0x00 };

    /// <summary>LED を点灯する。<paramref name="led"/> は 1〜4。</summary>
    public static byte[] SetLed(int led)
    {
        if (led is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(led), "LED must be 1 to 4.");
        }

        return new byte[] { OutputLedReportId, (byte)(0x10 << (led - 1)) };
    }

    /// <summary>レポートモードを「ボタンのみ(0x30)」に設定する。拡張機能の抜き差しでモードが戻るため、都度送る。</summary>
    public static byte[] SetReportModeCoreButtons() => new byte[] { OutputReportModeId, 0x00, CoreButtonsMode };

    // ボタンのビット(レポートのバイト 1 / バイト 2)
    internal static readonly (byte Mask, WiimoteButton Button)[] FirstByteButtons =
    {
        (0x01, WiimoteButton.Left),
        (0x02, WiimoteButton.Right),
        (0x04, WiimoteButton.Down),
        (0x08, WiimoteButton.Up),
        (0x10, WiimoteButton.Plus)
    };

    internal static readonly (byte Mask, WiimoteButton Button)[] SecondByteButtons =
    {
        (0x01, WiimoteButton.Two),
        (0x02, WiimoteButton.One),
        (0x04, WiimoteButton.B),
        (0x08, WiimoteButton.A),
        (0x10, WiimoteButton.Minus),
        (0x80, WiimoteButton.Home)
    };
}
