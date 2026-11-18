using static DspNetEnergy.Desktop.UiLanguage;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace DspNetEnergy.Desktop;

internal sealed record PickOption(string Key, string Name, string Category, string? IconId,
    string Details, bool Selected = false, bool Enabled = true, string Glyph = "—");

internal sealed class IconPicker : UserControl
{
    private readonly IReadOnlyList<PickOption> options;
    private readonly Action<string?> finish;
    private readonly TextBox search = new();
    private readonly StackPanel results = new();

    internal IconPicker(string title, IReadOnlyList<PickOption> options, Action<string?> finish)
    {
        this.options = options;
        this.finish = finish;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);

        var panel = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel();
        header.Children.Add(Ui.Text(T("DSP / INVENTORY", "DSP / INVENTAR"), "Accent", 12, true));
        var heading = Ui.Text(title, size: 21, bold: true);
        heading.Margin = new Thickness(0, 3, 0, 11);
        header.Children.Add(heading);
        header.Children.Add(Ui.Text(T("Filter by name or category", "Name oder Kategorie filtern"), "Muted", 12));
        search.Margin = new Thickness(0, 4, 0, 12);
        AutomationProperties.SetName(search, T("Filter icons", "Icons filtern"));
        search.TextChanged += (_, _) => RenderResults();
        header.Children.Add(search);
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        var cancel = new Button { Content = T("Close picker", "Auswahl schließen"), HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0) };
        cancel.Click += (_, _) => finish(null);
        DockPanel.SetDock(cancel, Dock.Bottom);
        panel.Children.Add(cancel);
        panel.Children.Add(new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = new Border { Style = (Style)Application.Current.FindResource("Panel"), Padding = new Thickness(0),
            BorderBrush = Ui.Brush("Accent"), Child = panel };
        Loaded += (_, _) => search.Focus();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                args.Handled = true;
                finish(null);
            }
        };
        RenderResults();
    }

    private void RenderResults()
    {
        results.Children.Clear();
        string query = search.Text.Trim();
        var matching = options.Where(option => Matches(option.Name, query) || Matches(option.Category, query) ||
            (option.IconId is string id && Matches(Inventory.English(id), query))).GroupBy(option => option.Category);
        foreach (var group in matching)
        {
            TextBlock title = Ui.Text(group.Key, "Accent", 12, true);
            title.Margin = new Thickness(0, 2, 0, 7);
            results.Children.Add(title);
            var tiles = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            foreach (PickOption option in group)
            {
                Button tile = Ui.IconButton(option.IconId, option.Name, $"{option.Name}\n{option.Details}",
                    option.Enabled ? () => finish(option.Key) : null, option.Selected, 38, option.Glyph);
                var content = new StackPanel();
                var picture = (FrameworkElement)tile.Content;
                tile.Content = null;
                content.Children.Add(picture);
                TextBlock caption = Ui.Text(option.Name, size: 11, bold: true);
                caption.TextAlignment = TextAlignment.Center;
                caption.Margin = new Thickness(0, 4, 0, 0);
                content.Children.Add(caption);
                tile.Content = content;
                tile.Width = 113;
                tile.MinHeight = 83;
                tile.Margin = new Thickness(0, 0, 6, 6);
                tiles.Children.Add(tile);
            }

            results.Children.Add(tiles);
        }

        if (results.Children.Count == 0)
        {
            results.Children.Add(Ui.Text(T("No matching icons found.", "Keine passenden Icons gefunden."), "Muted"));
        }
    }

    private static bool Matches(string text, string query) => text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

}
