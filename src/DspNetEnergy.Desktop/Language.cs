using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace DspNetEnergy.Desktop;

internal enum AppLanguage { English, German, Chinese }

internal sealed class UiLanguage : INotifyPropertyChanged
{
    internal static UiLanguage Current { get; } = new();
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(Current.Selected switch
    {
        AppLanguage.German => "de-DE",
        AppLanguage.Chinese => "zh-CN",
        _ => "en-US",
    });
    private static readonly Lazy<Dictionary<string, string>> Chinese = new(LoadChinese);

    public AppLanguage Selected { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    internal static string T(string english, string german) => Translate(Current.Selected, english, german);

    internal static string Format(string english, string german, params object[] values) =>
        string.Format(Culture, T(english, german), values);

    internal static string Translate(AppLanguage language, string english, string german) => language switch
    {
        AppLanguage.German => german,
        AppLanguage.Chinese => Chinese.Value.GetValueOrDefault(english, english),
        _ => english,
    };

    internal void Select(AppLanguage language)
    {
        if (Selected != language)
        {
            Selected = language;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    private static Dictionary<string, string> LoadChinese()
    {
        using var stream = Application.GetResourceStream(new Uri("Assets/zh-CN.json", UriKind.Relative)).Stream;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException("Missing Chinese translations.");
    }
}

public sealed class TextExtension(string english, string german) : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding(nameof(UiLanguage.Selected))
    {
        Source = UiLanguage.Current,
        Mode = BindingMode.OneWay,
        Converter = this,
    }.ProvideValue(serviceProvider);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AppLanguage language ? UiLanguage.Translate(language, english, german) : english;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
