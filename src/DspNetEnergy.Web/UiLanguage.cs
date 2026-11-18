using System.Globalization;
using System.Text.Json;

namespace DspNetEnergy.Web;

internal enum AppLanguage { English, German, Chinese }

internal sealed class UiLanguage
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
        }
    }

    private static Dictionary<string, string> LoadChinese()
    {
        using var stream = typeof(UiLanguage).Assembly.GetManifestResourceStream("zh-CN.json")!;
        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException("Missing Chinese translations.");
        using var web = typeof(UiLanguage).Assembly.GetManifestResourceStream("WebTranslations.json")!;
        foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(web)!)
            translations[key] = value;
        return translations;
    }
}

