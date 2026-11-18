using static DspNetEnergy.Desktop.UiLanguage;
using System.Windows;
using System.Windows.Controls;
using DspNetEnergy.Core;

namespace DspNetEnergy.Desktop;

public partial class MainWindow
{
    private readonly HashSet<string> expandedSteps = new();
    private static readonly SprayMode[] Modes = [SprayMode.None, SprayMode.ExtraProducts, SprayMode.Speed];

    private void RenderChain(ProductionChainBalance? balance, IReadOnlySet<string>? problemSteps)
    {
        bool failed = problemSteps is not null;
        RenderOverview(balance, failed);
        ChainRows.Children.Clear();
        MaterialRows.Children.Clear();
        if (balance is null || balance.Steps.Count == 0)
        {
            StepCount.Text = T("0 STEPS", "0 SCHRITTE");
            ChainRows.Children.Add(Ui.Text(failed ? T("Open the recipe node to change the selection.", "Öffne den Rezeptknoten, um die Auswahl zu korrigieren.")
                : T("No production facilities are active in this scenario.", "Für dieses Szenario sind keine Produktionsgebäude aktiv."), "Muted"));
        }
        else
        {
            StepCount.Text = $"{balance.Steps.Count} {(balance.Steps.Count == 1 ? T("STEP", "SCHRITT") : T("STEPS", "SCHRITTE"))}";
            for (int index = 0; index < balance.Steps.Count; index++)
            {
                ProductionStepBalance step = balance.Steps[index];
                ChainRows.Children.Add(BuildStep(step, index + 1, failed, problemSteps?.Contains(step.RecipeId) == true));
            }
        }

        MaterialExpander.IsEnabled = balance is not null && !failed;
        if (balance is not null && !failed)
        {
            foreach (var (id, amount) in balance.Materials.OrderBy(entry => Inventory.Name(entry.Key), StringComparer.CurrentCultureIgnoreCase))
            {
                MaterialRows.Children.Add(BuildMaterial(id, amount));
            }
        }
    }

    private void RenderOverview(ProductionChainBalance? balance, bool failed)
    {
        string itemId = Fuel.ItemId;
        string? recipeId = settings.SuppliedItems.Contains(itemId) ? null : settings.RecipeChoices.GetValueOrDefault(itemId)
            ?? ProductionCatalog.RecipesFor(itemId).FirstOrDefault()?.Id;
        string recipeName = recipeId is null ? T("Supplied", "Bereitgestellt") : Inventory.Name(recipeId);
        string generatorName = Inventory.Name(Fuel.GeneratorIconId);
        string generatorTip = $"{generatorName}\n{Ui.Percent(Fuel.GeneratorEfficiency)} {T("efficiency", "Wirkungsgrad")}\n" +
            $"{Ui.Power(Fuel.GeneratorCapacityMw)} {T("MW per plant without spray", "MW je Kraftwerk ohne Spray")}";
        var generator = OverviewTile(Fuel.GeneratorIconId, generatorName, T("Assigned automatically", "Automatisch zugeordnet"), generatorTip,
            () => { AdvancedSettings.IsExpanded = true; GeneratorCountBox.Focus(); });
        GeneratorTile.Content = generator;

        var flow = new Grid();
        for (int index = 0; index < 5; index++)
        {
            flow.ColumnDefinitions.Add(new ColumnDefinition { Width = index % 2 == 0
                ? new GridLength(1, GridUnitType.Star) : new GridLength(16) });
        }

        string itemName = Inventory.Name(itemId);
        Add(OverviewTile(recipeId ?? itemId, recipeName, T("Recipe / source", "Rezept / Quelle"),
            Format("Select recipe for {0}", "Rezept für {0} wählen", itemName),
            () => ChooseRecipe(itemId, recipeId), failed), 0);
        Add(Arrow(), 1);
        Add(OverviewTile(itemId, itemName, T("Fuel", "Brennstoff"), $"{FuelEnergy(Fuel)} {T("without spray", "ohne Spray")}",
            () => OnFuelPicker(this, new RoutedEventArgs())), 2);
        Add(Arrow(), 3);
        string generation = balance is null || failed ? T("Power plant", "Kraftwerk")
            : Format("{0} × installed", "{0} × installiert", balance.Energy.GeneratorCount);
        Add(OverviewTile(Fuel.GeneratorIconId, generatorName, generation, generatorTip,
            () => { AdvancedSettings.IsExpanded = true; GeneratorCountBox.Focus(); }), 4);
        FlowOverview.Content = flow;

        void Add(UIElement element, int column)
        {
            Grid.SetColumn(element, column);
            flow.Children.Add(element);
        }
    }

    private static TextBlock Arrow() => new() { Text = "›", FontSize = 22, Foreground = Ui.Brush("Muted"),
        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

    private static Button OverviewTile(string id, string title, string subtitle, string tooltip, Action action, bool problem = false)
    {
        Button button = Ui.IconButton(id, $"{subtitle} · {title}", tooltip, action, iconSize: 28);
        var icon = (FrameworkElement)button.Content;
        button.Content = null;
        var panel = new DockPanel { LastChildFill = true };
        icon.Margin = new Thickness(0, 0, 7, 0);
        panel.Children.Add(icon);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Text(title, problem ? "Error" : "Text", 12, true));
        text.Children.Add(Ui.Text(subtitle, "Muted", 10));
        panel.Children.Add(text);
        button.Content = panel;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.MinHeight = 48;
        if (problem)
        {
            button.BorderBrush = Ui.Brush("Error");
        }

        return button;
    }

    private Border BuildStep(ProductionStepBalance step, int number, bool failed, bool problem)
    {
        RecipeDefinition recipe = ProductionCatalog.Recipe(step.RecipeId);
        string itemId = PrimaryOutput(recipe);
        ProductionStepSettings? configured = settings.Steps.GetValueOrDefault(recipe.Id);
        string machineId = configured?.MachineId ?? step.MachineId;
        var content = new StackPanel();
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        string name = Inventory.Name(recipe.Id);
        Button recipeButton = Ui.IconButton(recipe.Id, $"{T("Change recipe", "Rezept ändern")} · {name}", RecipeTip(recipe),
            () => ChooseRecipe(itemId, recipe.Id), iconSize: 33);
        recipeButton.Margin = new Thickness(0, 0, 9, 0);
        header.Children.Add(recipeButton);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Text($"{number:00}  {name}", problem ? "Error" : "Text", 14, true));
        string summary = problem ? T("Check capacity or settings", "Kapazität oder Einstellung prüfen") : failed ? T("Calculation pending", "Bilanz ausstehend")
            : $"{step.MachineCount} × {Inventory.Name(machineId)}  ·  {Ui.Percent(step.Utilization)} {T("utilization", "Auslastung")}";
        text.Children.Add(Ui.Text(summary, problem ? "Error" : "Muted", 11));
        decimal? defaultOilRate = Ui.DefaultOilRate(recipe, machineId);
        if (defaultOilRate is decimal defaultRate)
        {
            decimal multiplier = configured?.RateMultiplier ?? 1m;
            string caption = $"{Ui.Precise(defaultRate * multiplier)} {T("Crude Oil/s per extractor", "Rohöl/s je Extraktor")}";
            if (multiplier == 1m)
            {
                caption += T(" · assumed default", " · Standardannahme");
            }

            var oilRate = new Button
            {
                Content = Ui.Text(caption + "  ›", "Blue", 11),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = T("Edit the current flow per Oil Extractor.", "Aktuelle Förderrate je Ölextraktion einstellen."),
            };
            oilRate.Click += (_, _) => EditMachine(recipe, machineId);
            text.Children.Add(oilRate);
        }
        Grid.SetColumn(text, 1);
        header.Children.Add(text);

        Button machine = Ui.IconButton(machineId, $"{T("Select machine", "Maschine wählen")} · {Inventory.Name(machineId)}", MachineTip(machineId),
            () => ChooseMachine(recipe, machineId), iconSize: 29);
        machine.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(machine, 2);
        header.Children.Add(machine);
        content.Children.Add(header);

        var tools = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 7, 0, 0) };
        var modeButtons = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (SprayMode mode in Modes)
        {
            (string title, string glyph, string? reason) = SprayChoice(recipe, mode);
            SprayMode configuredMode = configured?.Spray ?? SprayMode.None;
            bool selected = settings.Tier is null ? mode == SprayMode.None : configuredMode == mode;
            Button button = Ui.IconButton(null, $"{title} · {name}", reason ?? title,
                reason is null ? () => ChangeStep(recipe.Id, old => old with { Spray = mode }) : null, selected, 21, glyph);
            button.Margin = new Thickness(0, 0, 4, 0);
            button.Padding = new Thickness(3, 1, 3, 1);
            modeButtons.Children.Add(button);
        }

        tools.Children.Add(modeButtons);
        string power = failed ? "— MW" : $"{Ui.Power(step.WorkingPowerMw + step.IdlePowerMw)} MW";
        TextBlock consumption = Ui.Text(power, problem ? "Error" : "Blue", 13, true);
        consumption.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(consumption, Dock.Right);
        tools.Children.Add(consumption);
        content.Children.Add(tools);

        var expander = new Expander
        {
            Header = T("Material flows and machine power", "Stoffflüsse und Maschinenverbrauch"),
            Content = BuildStepDetails(step, failed, defaultOilRate.HasValue),
            IsExpanded = expandedSteps.Contains(recipe.Id),
            Margin = new Thickness(0, 7, 0, 0),
        };
        expander.Expanded += (_, _) => expandedSteps.Add(recipe.Id);
        expander.Collapsed += (_, _) => expandedSteps.Remove(recipe.Id);
        content.Children.Add(expander);

        var menu = new ContextMenu();
        AddMenu(T("Select recipe or source", "Rezept oder Quelle wählen"), recipe.Id, () => ChooseRecipe(itemId, recipe.Id));
        AddMenu(T("Select machine", "Maschine wählen"), machineId, () => ChooseMachine(recipe, machineId));
        AddMenu(T("Machine settings", "Maschinenparameter"), machineId, () => EditMachine(recipe, machineId));
        AddMenu(T("Proliferator mode", "Proliferator-Modus"), settings.Tier is ProliferatorTier tier ? ProductionCatalog.Proliferator(tier).ItemId : null,
            () => ChooseSprayMode(recipe));
        return new Border
        {
            Child = content,
            ContextMenu = menu,
            Background = Ui.Brush("Inset"),
            BorderBrush = Ui.Brush(problem ? "Error" : "Outline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9),
            Margin = new Thickness(0, 0, 0, 6),
        };

        void AddMenu(string label, string? id, Action action)
        {
            var entry = new MenuItem { Header = label, Icon = Inventory.Picture(id, 20), Foreground = Ui.Brush("Text") };
            entry.Click += (_, _) => action();
            menu.Items.Add(entry);
        }
    }

    private StackPanel BuildStepDetails(ProductionStepBalance step, bool failed, bool oilExtraction)
    {
        var panel = new StackPanel { Margin = new Thickness(2, 6, 0, 0) };
        AddItems(T("Inputs", "Eingang"), step.InputsPerMinute);
        AddItems(oilExtraction ? T("Outputs · all extractors", "Ausgang · alle Extraktoren") : T("Outputs", "Ausgang"),
            step.OutputsPerMinute);
        if (!failed)
        {
            panel.Children.Add(Ui.Metric(T("Working power", "Arbeitsleistung"), $"{Ui.Precise(step.WorkingPowerMw)} MW"));
            panel.Children.Add(Ui.Metric(T("Idle power", "Leerlauf"), $"{Ui.Precise(step.IdlePowerMw)} MW"));
        }

        return panel;

        void AddItems(string caption, IReadOnlyDictionary<string, decimal> amounts)
        {
            if (amounts.Count == 0)
            {
                return;
            }

            panel.Children.Add(Ui.Text(caption, "Muted", 11, true));
            var row = new WrapPanel { Margin = new Thickness(0, 3, 0, 4) };
            foreach (var (item, amount) in amounts)
            {
                string title = Inventory.Name(item);
                string value = failed ? "—" : $"{Ui.Amount(amount)} /min";
                var button = OverviewTile(item, title, value, $"{title}\n{value}", () => ChooseRecipe(item, null));
                button.MaxWidth = 194;
                button.MinHeight = 38;
                button.Margin = new Thickness(0, 0, 5, 4);
                row.Children.Add(button);
            }

            panel.Children.Add(row);
        }
    }

    private Button BuildMaterial(string id, MaterialBalance amount)
    {
        string name = Inventory.Name(id);
        bool supplied = amount.SuppliedPerMinute > 0m;
        string suppliedLabel = T("Supplied", "Bereitgestellt");
        string surplus = T("Surplus", "Überschuss");
        string tooltip = $"{name} · {T("items/min", "Stück/min")}\n" +
            $"{T("Produced", "Produziert")} {Ui.Precise(amount.ProducedPerMinute)}\n" +
            $"{suppliedLabel} {Ui.Precise(amount.SuppliedPerMinute)}\n" +
            $"{T("Used in production", "Verarbeitet")} {Ui.Precise(amount.UsedInProductionPerMinute)}\n" +
            $"{T("Burned", "Verbrannt")} {Ui.Precise(amount.BurnedPerMinute)}\n{surplus} {Ui.Precise(amount.SurplusPerMinute)}";
        string summary = supplied ? $"{suppliedLabel} · {Ui.Amount(amount.SuppliedPerMinute)} /min" :
            $"{Ui.Amount(amount.ProducedPerMinute)} {T("produced", "produziert")} · {Ui.Amount(amount.SurplusPerMinute)} {surplus} /min";
        Button tile = OverviewTile(id, name, summary, tooltip, () => ChooseRecipe(id, null));
        tile.Margin = new Thickness(0, 0, 0, 4);
        if (supplied)
        {
            tile.BorderBrush = Ui.Brush("Warning");
        }

        return tile;
    }

    private string PrimaryOutput(RecipeDefinition recipe)
    {
        var products = recipe.Outputs.Keys.Where(item => recipe.Outputs[item] > recipe.Inputs.GetValueOrDefault(item)).ToArray();
        return products.FirstOrDefault(item => settings.RecipeChoices.GetValueOrDefault(item) == recipe.Id)
            ?? products.FirstOrDefault(item => ProductionCatalog.RecipesFor(item).FirstOrDefault()?.Id == recipe.Id)
            ?? products[0];
    }

    private void ChooseRecipe(string itemId, string? shownRecipeId)
    {
        IReadOnlyList<RecipeDefinition> recipes = ProductionCatalog.RecipesFor(itemId);
        string? explicitRecipe = settings.RecipeChoices.GetValueOrDefault(itemId);
        bool supplied = settings.SuppliedItems.Contains(itemId) || recipes.Count == 0;
        var options = recipes.Select(recipe => new PickOption(recipe.Id, Inventory.Name(recipe.Id),
            RecipeGroup(recipe.Kind), recipe.Id, RecipeTip(recipe),
            !supplied && (explicitRecipe ?? shownRecipeId ?? recipes[0].Id) == recipe.Id)).ToList();
        if (recipes.Count > 0)
        {
            options.Add(new PickOption("@auto", T("Automatic", "Automatisch"), T("Calculation scope", "Bilanzgrenze"), itemId,
                $"{T("Default recipe", "Standardrezept")} · {Inventory.Name(recipes[0].Id)}", explicitRecipe is null && !supplied));
        }

        options.Add(new PickOption("@supplied", T("Supplied", "Bereitgestellt"), T("Calculation scope", "Bilanzgrenze"), itemId,
            T("Upstream costs for this material are excluded.", "Die vorgelagerten Kosten dieses Materials sind ausgeschlossen."), supplied));
        Pick(Format("Recipe for {0}", "Rezept für {0}", Inventory.Name(itemId)), options, choice => SetRecipeChoice(itemId, choice));
    }

    private void SetRecipeChoice(string itemId, string choice)
    {
        var choices = new Dictionary<string, string>(settings.RecipeChoices);
        var suppliedItems = new HashSet<string>(settings.SuppliedItems);
        choices.Remove(itemId);
        suppliedItems.Remove(itemId);
        if (choice == "@supplied")
        {
            suppliedItems.Add(itemId);
        }
        else if (choice != "@auto")
        {
            choices[itemId] = choice;
            editedStep = choice;
        }

        settings = settings with { RecipeChoices = choices, SuppliedItems = suppliedItems };
        Refresh();
    }

    private void ChooseMachine(RecipeDefinition recipe, string selected)
    {
        var options = recipe.MachineIds.Select(id => new PickOption(id, Inventory.Name(id), T("Production facilities", "Produktionsgebäude"), id,
            MachineTip(id), id == selected)).ToArray();
        Pick(Format("Machine for {0}", "Maschine für {0}", Inventory.Name(recipe.Id)), options,
            choice => ChangeStep(recipe.Id, old => old with { MachineId = choice }));
    }

    private void EditMachine(RecipeDefinition recipe, string machine)
    {
        var original = settings.Steps.GetValueOrDefault(recipe.Id) ?? new ProductionStepSettings();
        ShowInspector(new MachineSettingsDialog(recipe, machine, original, changed =>
        {
            CloseInspector();
            if (changed is not null)
            {
                ChangeStep(recipe.Id, _ => changed);
            }
        }));
    }

    private void ChooseSprayMode(RecipeDefinition recipe)
    {
        var options = Modes.Select(mode =>
        {
            (string name, string glyph, string? reason) = SprayChoice(recipe, mode);
            bool selected = (settings.Tier is null ? SprayMode.None : settings.Steps.GetValueOrDefault(recipe.Id)?.Spray ?? SprayMode.None) == mode;
            return new PickOption(mode.ToString(), name, T("Spray mode", "Sprühmodus"), null, reason ?? T("Apply to this step", "Für diesen Schritt anwenden"),
                selected, reason is null, glyph);
        }).ToArray();
        Pick(Format("Proliferator for {0}", "Proliferator für {0}", Inventory.Name(recipe.Id)), options,
            choice => ChangeStep(recipe.Id, old => old with { Spray = Enum.Parse<SprayMode>(choice) }));
    }

    private (string Name, string Glyph, string? Reason) SprayChoice(RecipeDefinition recipe, SprayMode mode)
    {
        if (mode == SprayMode.None)
        {
            return (T("No spray", "Ohne Spray"), "∅", null);
        }

        bool allowed = mode == SprayMode.ExtraProducts ? recipe.AllowsExtraProducts : recipe.AllowsSpeed;
        string? reason = !allowed ? T("This recipe does not support this mode.", "Dieser Modus ist für das Rezept unzulässig.")
            : settings.Tier is null ? T("Select a Proliferator tier first.", "Wähle zuerst eine Proliferator-Stufe.") : null;
        return mode == SprayMode.ExtraProducts ? (T("Extra Products", "Zusatzprodukte"), "+", reason) : (T("Production Speedup", "Produktionsbeschleunigung"), "»", reason);
    }

    private void ChangeStep(string recipeId, Func<ProductionStepSettings, ProductionStepSettings> change)
    {
        var steps = new Dictionary<string, ProductionStepSettings>(settings.Steps);
        steps[recipeId] = change(steps.GetValueOrDefault(recipeId) ?? new ProductionStepSettings());
        settings = settings with { Steps = steps };
        editedStep = recipeId;
        Refresh();
    }

    private static string MachineTip(string machineId)
    {
        MachineDefinition machine = ProductionCatalog.Machine(machineId);
        return $"{Inventory.Name(machineId)}\n{T("Speed", "Tempo")} ×{Ui.Amount(machine.Speed)}\n" +
            $"{T("Working power", "Arbeitsleistung")} {Ui.Precise(machine.WorkingPowerMw)} MW · " +
            $"{T("Idle power", "Leerlauf")} {Ui.Precise(machine.IdlePowerMw)} MW";
    }

    private static string RecipeTip(RecipeDefinition recipe) => $"{Inventory.Name(recipe.Id)}\n" +
        $"{Describe(recipe.Inputs)} → {Describe(recipe.Outputs)}\n{Ui.Amount(recipe.CycleSeconds)} {T("s per base cycle", "s je Grundzyklus")}";

    private static string Describe(IReadOnlyDictionary<string, decimal> materials) => materials.Count == 0 ? T("Source", "Quelle")
        : string.Join(" + ", materials.Select(entry => $"{Ui.Precise(entry.Value)} {Inventory.Name(entry.Key)}"));

    private static string RecipeGroup(RecipeKind kind) => kind switch
    {
        RecipeKind.Extraction => T("Resource extraction", "Rohstoffgewinnung"),
        RecipeKind.PhotonCapture => T("Dyson Sphere power", "Dyson-Energie"),
        _ => T("Processing", "Verarbeitung"),
    };
}
