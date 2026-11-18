using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DspNetEnergy.Desktop;

internal static partial class SteamGame
{
    private const string GameName = "Dyson Sphere Program";
    private const string SteamAppId = "1366540";

    internal static bool IsGameFolder(string folder) =>
        File.Exists(Path.Combine(folder, "DSPGAME.exe")) &&
        File.Exists(Path.Combine(folder, "DSPGAME_Data", "resources.assets"));

    internal static IEnumerable<string> FindInstallations() => SteamRoots().SelectMany(Libraries)
        .Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(GameFolders)
        .Where(IsGameFolder).Distinct(StringComparer.OrdinalIgnoreCase);

    internal static string? BrowseStartFolder() => SteamRoots()
        .Select(path => Path.Combine(path, "steamapps", "common")).FirstOrDefault(Directory.Exists);

    private static IEnumerable<string> SteamRoots() =>
        new[]
        {
            SteamPath(@"HKEY_CURRENT_USER\Software\Valve\Steam"),
            SteamPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"),
            SteamPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
        }.Where(Path.IsPathFullyQualified).Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> Libraries(string steam)
    {
        yield return steam;
        string manifest = ReadText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
        foreach (Match match in LibraryPath().Matches(manifest))
        {
            string folder = match.Groups[1].Value.Replace(@"\\", @"\");
            if (Path.IsPathFullyQualified(folder))
            {
                yield return folder;
            }
        }
    }

    private static IEnumerable<string> GameFolders(string library)
    {
        string steamApps = Path.Combine(library, "steamapps");
        string manifest = ReadText(Path.Combine(steamApps, $"appmanifest_{SteamAppId}.acf"));
        string installDir = InstallDir().Match(manifest).Groups[1].Value;
        if (installDir.Length > 0 && installDir is not ("." or "..") &&
            installDir.IndexOfAny(Path.GetInvalidFileNameChars()) < 0)
        {
            yield return Path.Combine(steamApps, "common", installDir);
        }

        yield return Path.Combine(steamApps, "common", GameName);
    }

    private static string SteamPath(string key)
    {
        try
        {
            return Registry.GetValue(key, "SteamPath", null) as string
                ?? Registry.GetValue(key, "InstallPath", null) as string ?? "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            return "";
        }
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return "";
        }
    }

    [GeneratedRegex("(?im)^\\s*\"path\"[ \\t]+\"([^\"\\r\\n]+)\"")]
    private static partial Regex LibraryPath();

    [GeneratedRegex("(?im)^\\s*\"installdir\"[ \\t]+\"([^\"\\r\\n]+)\"")]
    private static partial Regex InstallDir();
}
