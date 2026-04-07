using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BetterGenshinImpact.Service.Interface;

namespace BetterGenshinImpact.View.Converters;

[ValueConversion(typeof(string), typeof(string))]
public sealed class TrConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
        {
            return value ?? string.Empty;
        }

        var translator = App.GetService<ITranslationService>();
        var source = parameter is MissingTextSource sourceParam ? sourceParam : MissingTextSource.UiDynamicBinding;
        return translator?.Translate(s, TranslationSourceInfo.From(source)) ?? s;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value ?? string.Empty;
    }

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = values.FirstOrDefault(static value => value is string { Length: > 0 }) as string;
        return Convert(text, targetType, parameter, culture);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        return targetTypes.Select(_ => Binding.DoNothing).ToArray();
    }
}
