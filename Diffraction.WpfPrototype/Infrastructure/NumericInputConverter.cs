using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Diffraction.WpfPrototype.Infrastructure;

public sealed class NumericInputConverter : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new EditorConverter();

    private sealed class EditorConverter : IValueConverter
    {
        private object? _lastValue;
        private string? _lastValidText;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Keep this editor's valid spelling, including trailing decimal zeros.
            if (_lastValidText is not null && Equals(value, _lastValue))
                return _lastValidText;
            _lastValue = null;
            _lastValidText = null;
            return System.Convert.ToString(value, culture) ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string ?? string.Empty;
            object parsed;
            if (targetType == typeof(int) && int.TryParse(text, NumberStyles.Integer, culture, out int integer))
                parsed = integer;
            else if (targetType == typeof(double) && double.TryParse(text, NumberStyles.Float, culture, out double number))
                parsed = number;
            else
                return DependencyProperty.UnsetValue;

            _lastValue = parsed;
            _lastValidText = text;
            return parsed;
        }
    }
}
