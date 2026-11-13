using System.Globalization;
using System.Windows.Data;
using Trimx.Core.Documents;

namespace Trimx.UI;

/// <summary>
/// Converts a <see cref="Layer"/> to a short label describing its kind.
/// </summary>
public sealed class LayerKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ImageLayer => "画像",
            TextLayer => "テキスト",
            ShapeLayer => "図形",
            _ => string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
