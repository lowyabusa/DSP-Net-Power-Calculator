using System.Text.Json;
using DspNetEnergy.Core;
using static DspNetEnergy.Web.UiLanguage;

namespace DspNetEnergy.Web;

internal static class Display
{
    private static readonly Dictionary<string, ItemEntry> Items = LoadItems();

    internal static string Name(string id) => Items.TryGetValue(id, out var item) ? Current.Selected switch
    {
        AppLanguage.German => item.Name,
        AppLanguage.Chinese => item.Chinese,
        _ => item.English,
    } : id;

    internal static string English(string id) => Items.GetValueOrDefault(id)?.English ?? id;
    internal static string? Badge(string id) => Items.GetValueOrDefault(id)?.Badge;
    internal static IReadOnlyDictionary<string, ItemEntry> Inventory => Items;
    internal static string Amount(decimal value) => value.ToString("#,0.##", Culture);
    internal static string Power(decimal value) => value.ToString("#,0.00#", Culture);
    internal static string Precise(decimal value) => value.ToString("#,0.######", Culture);
    internal static string Percent(decimal value) => value.ToString("0.#%", Culture);
    internal static string Edit(decimal? value) => value?.ToString("0.############################", Culture) ?? "";
    internal static bool ScenarioError(Exception error) => error is ArgumentException or InvalidOperationException or OverflowException;

    internal static string ErrorMessage(Exception error) => error switch
    {
        OverflowException => T("The target throughput exceeds the supported range.", "Der Zieldurchsatz überschreitet den berechenbaren Bereich."),
        InvalidOperationException when error.Message.Contains("spray coaters", StringComparison.Ordinal) =>
            T("The installed Spray Coaters cannot handle this throughput.", "Die installierten Spray Coater reichen für diesen Durchsatz nicht aus."),
        InvalidOperationException => T("The selected recipes and facilities cannot meet the target throughput.", "Die gewählten Rezepte und Anlagen können den Zieldurchsatz nicht bereitstellen."),
        _ => Format("Invalid scenario setting. {0}", "Szenarioeinstellung ungültig. {0}", error.Message),
    };

    internal static string TierName(ProliferatorTier? tier) => tier switch
    {
        ProliferatorTier.MkI => T("Mk.I", "Typ I"),
        ProliferatorTier.MkII => T("Mk.II", "Typ II"),
        ProliferatorTier.MkIII => T("Mk.III", "Typ III"),
        _ => T("None", "Ohne"),
    };

    private static Dictionary<string, ItemEntry> LoadItems()
    {
        using var stream = typeof(Display).Assembly.GetManifestResourceStream("inventory.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, ItemEntry>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    internal sealed record ItemEntry(string Name, string English, string Chinese, string Texture, bool Shared, string? Badge);
}
