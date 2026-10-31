using KUNAI.Core.Domain;

namespace KUNAI.Input.Wiimote;

/// <summary>入力レポートの解析結果。</summary>
/// <param name="Buttons">押されているボタンの集合。</param>
/// <param name="BatteryPercent">ステータスレポートの場合のみ、電池残量(0〜100)。</param>
/// <param name="IsStatus">ステータスレポート(0x20)か。</param>
public sealed record WiimoteReport(IReadOnlySet<WiimoteButton> Buttons, int? BatteryPercent, bool IsStatus);

/// <summary>Wii Remote の入力レポートを解析する。</summary>
public static class WiimoteReportParser
{
    private const int ButtonReportMinLength = 3;
    private const int StatusReportLength = 7;

    /// <summary>
    /// 先頭 1 バイトがレポート ID、続く 2 バイトがボタン、の形式を解析する。
    /// ボタンを含まないレポート(0x00〜0x1F)や、短すぎるレポートは false。
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> report, out WiimoteReport result)
    {
        result = null!;

        if (report.Length < ButtonReportMinLength)
        {
            return false;
        }

        var id = report[0];
        if (id is < WiimoteProtocol.FirstButtonReportId or > WiimoteProtocol.LastButtonReportId)
        {
            return false;
        }

        var buttons = new HashSet<WiimoteButton>();
        foreach (var (mask, button) in WiimoteProtocol.FirstByteButtons)
        {
            if ((report[1] & mask) != 0) buttons.Add(button);
        }

        foreach (var (mask, button) in WiimoteProtocol.SecondByteButtons)
        {
            if ((report[2] & mask) != 0) buttons.Add(button);
        }

        var isStatus = id == WiimoteProtocol.StatusReportId;
        int? battery = null;
        if (isStatus)
        {
            if (report.Length < StatusReportLength)
            {
                return false;
            }

            battery = ToBatteryPercent(report[6]);
        }

        result = new WiimoteReport(buttons, battery, isStatus);
        return true;
    }

    /// <summary>生の電池値(0〜200)を 0〜100% に変換する。範囲外は丸める。</summary>
    public static int ToBatteryPercent(byte raw)
    {
        var percent = (int)Math.Round(raw * 100.0 / WiimoteProtocol.BatteryRawMax);
        return Math.Clamp(percent, 0, 100);
    }
}
