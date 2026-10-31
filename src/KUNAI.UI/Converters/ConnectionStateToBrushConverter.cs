using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using KUNAI.Core.Domain;

namespace KUNAI.UI.Converters;

/// <summary>接続状態を状態色に変換する。Disconnected = 赤、Connecting = 黄、Connected = 緑。</summary>
public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public static readonly IBrush Disconnected = new ImmutableSolidColorBrush(Color.Parse("#EB5757"));
    public static readonly IBrush Connecting = new ImmutableSolidColorBrush(Color.Parse("#F2C94C"));
    public static readonly IBrush Connected = new ImmutableSolidColorBrush(Color.Parse("#3ECF6E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ConnectionState.Connected => Connected,
            ConnectionState.Connecting => Connecting,
            ConnectionState.Disconnected => Disconnected,
            _ => BindingOperations.DoNothing
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
