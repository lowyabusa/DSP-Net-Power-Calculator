using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using static DspNetEnergy.Desktop.UiLanguage;

namespace DspNetEnergy.Desktop;

internal sealed record GameIconPreference(bool Disabled = false, string? Folder = null);

public partial class MainWindow
{
    private enum IconState { Searching, Ready, MissingGame, UnsupportedGame, Partial, Disabled, InvalidFolder }

    private GameIconPreference iconPreference = new();
    private IconState iconState = IconState.Searching;

    private async void OnGameIconsLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= OnGameIconsLoaded;
        iconPreference = SettingsStore.LoadGameIcons();
        if (iconPreference.Disabled)
        {
            iconState = IconState.Disabled;
            RenderGameIconStatus();
            return;
        }

        await FindGameIconsAsync(iconPreference.Folder, searchSteam: true);
    }

    private void OnGameIconsSettings(object sender, RoutedEventArgs args)
    {
        GameIconsPanel.Visibility = GameIconsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void OnChooseGameFolder(object sender, RoutedEventArgs args)
    {
        var dialog = new OpenFolderDialog
        {
            Title = T("Choose the Dyson Sphere Program folder", "Dyson-Sphere-Program-Ordner wählen"),
        };
        string? startFolder = Directory.Exists(iconPreference.Folder) ? iconPreference.Folder : SteamGame.BrowseStartFolder();
        if (startFolder is not null)
        {
            dialog.InitialDirectory = startFolder;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!SteamGame.IsGameFolder(dialog.FolderName))
        {
            iconState = IconState.InvalidFolder;
            RenderGameIconStatus();
            return;
        }

        await FindGameIconsAsync(dialog.FolderName, searchSteam: false);
    }

    private async void OnSearchSteam(object sender, RoutedEventArgs args) =>
        await FindGameIconsAsync(folder: null, searchSteam: true);

    private void OnSkipGameIcons(object sender, RoutedEventArgs args)
    {
        iconState = IconState.Disabled;
        GameIconsPanel.Visibility = Visibility.Collapsed;
        Inventory.SetIcons(new());
        Refresh(rebuildTiles: true);
        SaveIconPreference(iconPreference with { Disabled = true });
        RenderGameIconStatus();
    }

    private async Task FindGameIconsAsync(string? folder, bool searchSteam)
    {
        iconState = IconState.Searching;
        RenderGameIconStatus();
        GameIconsActions.IsEnabled = false;
        var result = await Task.Run(() => FindGameIcons(folder, searchSteam));
        GameIconsActions.IsEnabled = true;

        if (result.Icons is { Count: > 0 } icons)
        {
            Inventory.SetIcons(icons);
            Refresh(rebuildTiles: true);
            SaveIconPreference(new(Folder: result.Folder));
            iconState = icons.Count == Inventory.IconCount ? IconState.Ready : IconState.Partial;
        }
        else
        {
            iconState = result.Folder is null ? IconState.MissingGame : IconState.UnsupportedGame;
        }

        GameIconsPanel.Visibility = iconState == IconState.Ready ? Visibility.Collapsed : Visibility.Visible;
        RenderGameIconStatus();
    }

    private static (string? Folder, Dictionary<string, ImageSource>? Icons) FindGameIcons(string? folder, bool searchSteam)
    {
        IEnumerable<string> candidates = searchSteam ? SteamGame.FindInstallations() : [];
        if (folder is not null && SteamGame.IsGameFolder(folder))
        {
            candidates = candidates.Prepend(folder);
        }

        (string? Folder, Dictionary<string, ImageSource>? Icons) best = (null, null);
        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var icons = Inventory.LoadGameIcons(candidate);
            if (icons.Count == Inventory.IconCount)
            {
                return (candidate, icons);
            }
            if (best.Icons is null || icons.Count > best.Icons.Count)
            {
                best = (candidate, icons);
            }
        }

        return best;
    }

    private void SaveIconPreference(GameIconPreference value)
    {
        if (iconPreference == value)
        {
            return;
        }

        iconPreference = value;
        try
        {
            SettingsStore.SaveGameIcons(value);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = T("Could not save the game icon preference locally.",
                "Die Einstellung für Spiel-Icons konnte lokal nicht gespeichert werden.");
        }
    }

    private void RenderGameIconStatus()
    {
        GameIconsStatus.Text = iconState switch
        {
            IconState.Searching => T("Looking for Dyson Sphere Program…", "Dyson Sphere Program wird gesucht…"),
            IconState.Ready => T("Showing icons from your local game.", "Icons aus der lokalen Spielinstallation werden angezeigt."),
            IconState.MissingGame => T("Dyson Sphere Program was not found.", "Dyson Sphere Program wurde nicht gefunden."),
            IconState.UnsupportedGame => T("The game was found, but its icons could not be read.",
                "Das Spiel wurde gefunden, seine Icons konnten aber nicht gelesen werden."),
            IconState.Partial => T("Some game icons could not be read. The remaining entries use text.",
                "Einige Spiel-Icons konnten nicht gelesen werden. Die übrigen Einträge zeigen Text."),
            IconState.InvalidFolder => T("Choose the folder that contains DSPGAME.exe.",
                "Wähle den Ordner, der DSPGAME.exe enthält."),
            _ => T("Game icons are turned off.", "Spiel-Icons sind ausgeschaltet."),
        };
    }
}
