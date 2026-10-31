using System.Globalization;
using Avalonia.Data;
using Avalonia.Media;
using KUNAI.Core.Domain;
using KUNAI.UI.Converters;

namespace KUNAI.Tests.UI;

public class ConnectionStateToBrushConverterTests
{
    private readonly ConnectionStateToBrushConverter _converter = new();

    private Color ColorOf(ConnectionState state)
    {
        var result = _converter.Convert(state, typeof(IBrush), null, CultureInfo.InvariantCulture);
        return Assert.IsAssignableFrom<ISolidColorBrush>(result).Color;
    }

    [Fact]
    public void Disconnected_IsRed() => Assert.Equal(Color.Parse("#EB5757"), ColorOf(ConnectionState.Disconnected));

    [Fact]
    public void Connecting_IsYellow() => Assert.Equal(Color.Parse("#F2C94C"), ColorOf(ConnectionState.Connecting));

    [Fact]
    public void Connected_IsGreen() => Assert.Equal(Color.Parse("#3ECF6E"), ColorOf(ConnectionState.Connected));

    [Fact]
    public void ThreeStates_HaveDistinctColors()
    {
        var colors = Enum.GetValues<ConnectionState>().Select(ColorOf).Distinct().ToList();

        Assert.Equal(Enum.GetValues<ConnectionState>().Length, colors.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Connected")]
    [InlineData(42)]
    public void UnexpectedValue_LeavesTheBindingUntouched(object? value)
    {
        var result = _converter.Convert(value, typeof(IBrush), null, CultureInfo.InvariantCulture);

        Assert.Same(BindingOperations.DoNothing, result);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(Brushes.Red, typeof(ConnectionState), null, CultureInfo.InvariantCulture));
    }
}
