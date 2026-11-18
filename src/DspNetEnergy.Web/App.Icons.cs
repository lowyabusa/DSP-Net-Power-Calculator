using Microsoft.JSInterop;
using static DspNetEnergy.Web.UiLanguage;
using static DspNetEnergy.Web.Display;

namespace DspNetEnergy.Web;

public partial class App
{
    private bool ShowIcons;
    private bool ReadingIcons;
    private string? IconStatus;

    private async Task RestoreIcons()
    {
        if (transient)
        {
            ShowIcons = true;
            return;
        }
        try
        {
            var saved = await JS.InvokeAsync<IconState>("dsp.loadIcons");
            GameIcon.IconImages.Clear();
            foreach (var (id, source) in saved.Images) GameIcon.IconImages[id] = source;
            ShowIcons = !saved.Decided;
        }
        catch (JSException)
        {
            ShowIcons = true;
        }
    }

    private async Task LoadIcons()
    {
        if (ReadingIcons) return;
        ReadingIcons = true;
        IconStatus = T("Reading game icons …", "Spiel-Icons werden gelesen …");
        await Task.Delay(30);
        try
        {
            var js = (IJSInProcessRuntime)JS;
            var sizes = js.Invoke<Dictionary<string, long>>("dsp.assetSizes");
            using var resources = Open("resources.assets");
            using var shared = Open("sharedassets0.assets");
            var wanted = Inventory.Values.Where(entry => !string.IsNullOrEmpty(entry.Texture))
                .Select(entry => new GameTexture(entry.Texture, entry.Shared)).ToHashSet();
            var textures = DspGameIcons.Load(resources, shared, wanted);
            var images = new Dictionary<string, string>();
            foreach (var (id, entry) in Inventory)
            {
                var key = new GameTexture(entry.Texture, entry.Shared);
                if ((textures.GetValueOrDefault(key) ?? textures.GetValueOrDefault(key with { Shared = !key.Shared })) is { } image)
                    images[id] = "data:image/png;base64," + Convert.ToBase64String(image);
            }

            if (images.Count == 0)
            {
                IconStatus = T("No supported icons were found. Choose your Dyson Sphere Program game folder.",
                    "Keine unterstützten Icons gefunden. Wähle den Installationsordner von Dyson Sphere Program.");
                return;
            }

            GameIcon.IconImages.Clear();
            foreach (var (id, source) in images) GameIcon.IconImages[id] = source;
            try { if (!transient) await JS.InvokeVoidAsync("dsp.saveIcons", images); }
            catch (JSException)
            {
                IconStatus = T("Icons loaded for this session. Browser storage is unavailable.",
                    "Icons für diese Sitzung geladen. Der Browserspeicher ist nicht verfügbar.");
                return;
            }
            IconStatus = Format("{0} game icons loaded.", "{0} Spiel-Icons geladen.", images.Count);

            BrowserAssetStream? Open(string name) => sizes.TryGetValue(name, out long length) ? new(js, name, length) : null;
        }
        catch (Exception error) when (error is JSException or IOException || ScenarioError(error))
        {
            IconStatus = T("Could not read game icons. Check the selected DSP installation.",
                "Spiel-Icons konnten nicht gelesen werden. Prüfe die gewählte DSP-Installation.");
        }
        finally
        {
            ReadingIcons = false;
            await JS.InvokeVoidAsync("dsp.releaseAssets");
        }
    }

    private async Task SkipIcons()
    {
        GameIcon.IconImages.Clear();
        try { if (!transient) await JS.InvokeVoidAsync("dsp.saveIcons", new Dictionary<string, string>()); }
        catch (JSException)
        {
            IconStatus = T("Could not save icon settings locally.", "Die Icon-Einstellungen konnten lokal nicht gespeichert werden.");
            return;
        }
        IconStatus = null;
        ShowIcons = false;
    }

    private sealed record IconState(bool Decided, Dictionary<string, string> Images);
}
