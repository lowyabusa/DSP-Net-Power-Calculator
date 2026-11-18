using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

internal static class Ui
{
    internal static Brush Brush(string key) => (Brush)Application.Current.FindResource(key + "Brush");
    internal static string Amount(decimal value) => value.ToString("#,0.##", UiLanguage.Culture);
    internal static string Power(decimal value) => value.ToString("#,0.00#", UiLanguage.Culture);
    internal static string Precise(decimal value) => value.ToString("#,0.######", UiLanguage.Culture);
    internal static string Percent(decimal ratio) => ratio.ToString("0.#%", UiLanguage.Culture);
    internal static string Edit(decimal value) => value.ToString("0.############################", UiLanguage.Culture);

    internal static decimal? DefaultOilRate(RecipeDefinition recipe, string machineId) => recipe.Id == "crude-oil-seep"
        ? recipe.Outputs["crude-oil"] * ProductionCatalog.Machine(machineId).Speed / recipe.CycleSeconds
        : null;

    internal static TextBlock Text(string value, string brush = "Text", double size = 13, bool bold = false) => new()
    {
        Text = value,
        Foreground = Brush(brush),
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
    };

    internal static Button IconButton(string? id, string name, string tooltip, Action? action,
        bool selected = false, double iconSize = 32, string glyph = "—", string? fallbackLabel = null)
    {
        var button = new Button { Content = Inventory.Picture(id, iconSize, glyph, fallbackLabel), Padding = new Thickness(5),
            ToolTip = tooltip, IsEnabled = action is not null };
        AutomationProperties.SetName(button, name);
        if (selected)
        {
            button.BorderBrush = Brush("Accent");
            button.Background = Brush("Selection");
            button.BorderThickness = new Thickness(2);
        }

        if (action is not null)
        {
            button.Click += (_, _) => action();
        }

        return button;
    }

    internal static Grid Metric(string label, string value, string brush = "Text")
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock key = Text(label, "Muted", 12);
        key.Margin = new Thickness(0, 0, 8, 0);
        grid.Children.Add(key);
        TextBlock number = Text(value, brush, 12, true);
        Grid.SetColumn(number, 1);
        grid.Children.Add(number);
        return grid;
    }
}
