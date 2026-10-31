using KUNAI.Core.Domain;
using KUNAI.Input.Wiimote;

namespace KUNAI.Tests.Input;

public class WiimoteReportParserTests
{
    [Theory]
    [InlineData(1, 0x01, WiimoteButton.Left)]
    [InlineData(1, 0x02, WiimoteButton.Right)]
    [InlineData(1, 0x04, WiimoteButton.Down)]
    [InlineData(1, 0x08, WiimoteButton.Up)]
    [InlineData(1, 0x10, WiimoteButton.Plus)]
    [InlineData(2, 0x01, WiimoteButton.Two)]
    [InlineData(2, 0x02, WiimoteButton.One)]
    [InlineData(2, 0x04, WiimoteButton.B)]
    [InlineData(2, 0x08, WiimoteButton.A)]
    [InlineData(2, 0x10, WiimoteButton.Minus)]
    [InlineData(2, 0x80, WiimoteButton.Home)]
    public void EachButtonBit_MapsToExactlyOneButton(int byteIndex, int mask, WiimoteButton expected)
    {
        var report = new byte[] { 0x30, 0, 0 };
        report[byteIndex] = (byte)mask;

        Assert.True(WiimoteReportParser.TryParse(report, out var result));

        Assert.Equal(new[] { expected }, result.Buttons);
        Assert.False(result.IsStatus);
        Assert.Null(result.BatteryPercent);
    }

    [Fact]
    public void NoBits_MeansNoButtonsPressed()
    {
        Assert.True(WiimoteReportParser.TryParse(new byte[] { 0x30, 0, 0 }, out var result));
        Assert.Empty(result.Buttons);
    }

    [Fact]
    public void MultipleBits_AreAllReported()
    {
        // B + Home + A + Up
        Assert.True(WiimoteReportParser.TryParse(new byte[] { 0x30, 0x08, 0x04 | 0x80 | 0x08 }, out var result));

        Assert.Equal(
            new[] { WiimoteButton.A, WiimoteButton.B, WiimoteButton.Home, WiimoteButton.Up }.OrderBy(b => b),
            result.Buttons.OrderBy(b => b));
    }

    [Theory]
    [InlineData(0x30)]
    [InlineData(0x31)]
    [InlineData(0x37)]
    [InlineData(0x3E)]
    [InlineData(0x3F)]
    [InlineData(0x21)]
    public void ButtonsAreReadFromAllDataReportIds(byte id)
    {
        Assert.True(WiimoteReportParser.TryParse(new byte[] { id, 0, 0x08, 0, 0, 0, 0 }, out var result));
        Assert.Equal(new[] { WiimoteButton.A }, result.Buttons);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x10)]
    [InlineData(0x1F)]
    [InlineData(0x40)]
    [InlineData(0xFF)]
    public void ReportIdsOutsideTheDataRange_AreRejected(byte id)
    {
        Assert.False(WiimoteReportParser.TryParse(new byte[] { id, 0xFF, 0xFF, 0, 0, 0, 0 }, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TooShortReports_AreRejected(int length)
    {
        var report = new byte[length];
        if (length > 0) report[0] = 0x30;

        Assert.False(WiimoteReportParser.TryParse(report, out _));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 50)]
    [InlineData(200, 100)]
    [InlineData(255, 100)] // 範囲外は 100 に丸める
    [InlineData(2, 1)]
    public void StatusReport_ConvertsBatteryToPercent(byte raw, int expectedPercent)
    {
        var report = new byte[] { 0x20, 0, 0, 0, 0, 0, raw };

        Assert.True(WiimoteReportParser.TryParse(report, out var result));

        Assert.True(result.IsStatus);
        Assert.Equal(expectedPercent, result.BatteryPercent);
    }

    [Fact]
    public void StatusReport_AlsoCarriesButtons()
    {
        Assert.True(WiimoteReportParser.TryParse(new byte[] { 0x20, 0, 0x08, 0, 0, 0, 100 }, out var result));
        Assert.Equal(new[] { WiimoteButton.A }, result.Buttons);
    }

    [Fact]
    public void TruncatedStatusReport_IsRejected()
    {
        Assert.False(WiimoteReportParser.TryParse(new byte[] { 0x20, 0, 0, 0, 0 }, out _));
    }

    [Fact]
    public void OutputReports_AreBuiltAsDocumented()
    {
        Assert.Equal(new byte[] { 0x15, 0x00 }, WiimoteProtocol.StatusRequest());
        Assert.Equal(new byte[] { 0x12, 0x00, 0x30 }, WiimoteProtocol.SetReportModeCoreButtons());
        Assert.Equal(new byte[] { 0x11, 0x10 }, WiimoteProtocol.SetLed(1));
        Assert.Equal(new byte[] { 0x11, 0x80 }, WiimoteProtocol.SetLed(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => WiimoteProtocol.SetLed(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WiimoteProtocol.SetLed(5));
    }
}
