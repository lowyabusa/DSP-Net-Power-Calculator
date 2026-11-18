using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DspNetEnergy.Desktop;

internal static class Inventory
{
    private sealed record Entry(string Name, string English, string Chinese, string Texture, bool Shared, string? Badge)
    {
        internal GameTexture GameTexture => new(Texture, Shared);
    }

    private static readonly Dictionary<string, Entry> Entries = LoadEntries();
    private static readonly HashSet<GameTexture> Wanted = Entries.Values.Select(entry => entry.GameTexture).ToHashSet();
    private static Dictionary<string, ImageSource> sprites = new();
    internal static int IconCount => Entries.Count;

    internal static string Name(string id) => Entries.TryGetValue(id, out Entry? entry) ? UiLanguage.Current.Selected switch
    {
        AppLanguage.German => entry.Name,
        AppLanguage.Chinese => entry.Chinese,
        _ => entry.English,
    } : id;
    internal static string English(string id) => Entries.GetValueOrDefault(id)?.English ?? id;

    internal static Dictionary<string, ImageSource> LoadGameIcons(string folder)
    {
        Dictionary<GameTexture, BitmapSource> textures = DspGameIcons.Load(folder, Wanted);
        var result = new Dictionary<string, ImageSource>();
        foreach ((string id, Entry entry) in Entries)
        {
            if (Find(entry) is not BitmapSource icon)
            {
                continue;
            }

            if (entry.Badge is string badge && Find(Entries[badge]) is BitmapSource badgeImage)
            {
                var drawing = new DrawingGroup();
                drawing.Children.Add(new ImageDrawing(icon, new Rect(0, 0, 80, 80)));
                drawing.Children.Add(new ImageDrawing(badgeImage, new Rect(48, 48, 32, 32)));
                var composite = new DrawingImage(drawing);
                composite.Freeze();
                result.Add(id, composite);
            }
            else
            {
                result.Add(id, icon);
            }
        }

        return result;

        BitmapSource? Find(Entry entry) => textures.GetValueOrDefault(entry.GameTexture)
            ?? textures.GetValueOrDefault(entry.GameTexture with { Shared = !entry.Shared });
    }

    internal static void SetIcons(Dictionary<string, ImageSource> icons) => sprites = icons;

    internal static FrameworkElement Picture(string? id, double size, string glyph = "—", string? fallbackLabel = null)
    {
        if (id is not null && sprites.TryGetValue(id, out ImageSource? sprite))
        {
            return new Image { Source = sprite, Width = size, Height = size, Stretch = Stretch.Uniform };
        }

        if (fallbackLabel is not null)
        {
            return new TextBlock { Text = fallbackLabel, FontSize = 9, Width = size + 10,
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = size + 5,
                TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        return new TextBlock { Text = glyph, FontSize = size * 0.66, Width = size, Height = size,
            TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    private static Dictionary<string, Entry> LoadEntries()
    {
        using var stream = Application.GetResourceStream(new Uri("Assets/inventory.json", UriKind.Relative)).Stream;
        return JsonSerializer.Deserialize<Dictionary<string, Entry>>(stream) ?? throw new InvalidDataException("Empty icon index.");
    }
}
