using System.Text.Json;
using DspNetEnergy.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using static DspNetEnergy.Web.UiLanguage;
using static DspNetEnergy.Web.Display;

namespace DspNetEnergy.Web;

public partial class App
{
    private static readonly ProliferatorTier?[] Tiers = [null, ProliferatorTier.MkI, ProliferatorTier.MkII, ProliferatorTier.MkIII];
    private static readonly EnergyBalance Coal = EnergyCalculator.Calculate(new DirectCombustionScenario(FuelKind.Coal, 120m));
    private static readonly EnergyBalance Graphite = EnergyCalculator.Calculate(new CoalToGraphiteScenario(120m));

    private ScenarioSettings Settings = new();
    private readonly Dictionary<string, string> Fields = new();
    private bool ready;
    private bool transient;
    private ProductionChainBalance? Balance;
    private ProductionChainBalance? lastBalance;
    private FuelKind lastBalancedFuel;
    private ProductionChainBalance? Topology;
    private readonly HashSet<string> ProblemSteps = new();
    private string? editedStep;
    private string? Status;
    private string? Failure;
    private string? InvalidField;
    private PickerState? Picker;
    private string Search = "";
    private string? focusDialog;
    private readonly List<Variant> Variants = new();

    private FuelDefinition Fuel => ProductionCatalog.Fuels.Single(fuel => fuel.Kind == Settings.Fuel);
    private string GeneratorTip => $"{Name(Fuel.GeneratorIconId)}\n{Percent(Fuel.GeneratorEfficiency)} {T("efficiency", "Wirkungsgrad")}\n{Power(Fuel.GeneratorCapacityMw)} {T("MW per plant without spray", "MW je Kraftwerk ohne Spray")}";
    private IEnumerable<Choice> FilteredChoices => (Picker?.Choices ?? []).Where(choice =>
        new[] { choice.Title, choice.Group, choice.Tooltip, choice.Icon is null ? "" : English(choice.Icon) }
            .Any(value => Culture.CompareInfo.IndexOf(value, Search.Trim(), System.Globalization.CompareOptions.IgnoreCase) >= 0));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var saved = await JS.InvokeAsync<SavedState>("dsp.load");
                transient = saved.Transient;
                Current.Select(saved.Language switch { "de" => AppLanguage.German, "zh-CN" => AppLanguage.Chinese, _ => AppLanguage.English });
                if (saved.Language is { } code) await JS.InvokeVoidAsync("dsp.setDocumentLanguage", code);
                if (!string.IsNullOrEmpty(saved.Scenario))
                {
                    Settings = SettingsStore.Read(saved.Scenario);
                }
            }
            catch (Exception error) when (error is JSException or JsonException || ScenarioError(error))
            {
                Status = T("Could not load the saved scenario. Defaults are active.", "Das gespeicherte Szenario konnte nicht geladen werden. Standardwerte sind aktiv.");
            }
            await RestoreIcons();
            SetFields();
            Calculate();
            ready = true;
            StateHasChanged();
        }
        if (focusDialog is { } selector)
        {
            focusDialog = null;
            await JS.InvokeVoidAsync("dsp.dialog", selector);
        }
    }

    private void SetFields()
    {
        Fields["rate"] = Edit(Settings.Rate);
        Fields["plants"] = Edit(Settings.GeneratorCount);
        Fields["infrastructure"] = Edit(Settings.InfrastructureMw);
        Fields["coaters"] = Edit(Settings.CoaterCount);
        Fields["coater-rate"] = Edit(Settings.CoaterRate);
    }

    private void Calculate()
    {
        Failure = InvalidField = null;
        Balance = null;
        Topology = lastBalancedFuel == Settings.Fuel ? lastBalance : null;
        ProblemSteps.Clear();
        Variants.Clear();
        try
        {
            Settings = Settings with
            {
                Rate = DecimalField("rate", T("Target throughput", "Der Zieldurchsatz"))!.Value,
                GeneratorCount = CountField("plants", T("Power plant count", "Die Kraftwerksanzahl")),
                InfrastructureMw = DecimalField("infrastructure", T("Infrastructure power", "Die Infrastrukturleistung"), optional: true),
                CoaterCount = Settings.Tier.HasValue ? CountField("coaters", T("Spray Coater count", "Die Coater-Anzahl")) : Settings.CoaterCount,
                CoaterRate = Settings.Tier.HasValue ? DecimalField("coater-rate", T("Spray Coater throughput", "Der Coater-Durchsatz"), positive: true)!.Value : Settings.CoaterRate,
            };
        }
        catch (InputError error)
        {
            InvalidField = error.Field;
            Failure = error.Message;
            return;
        }

        var scenario = Settings.ToScenario(Settings.Tier);
        try
        {
            Topology = Balance = lastBalance = EnergyCalculator.Calculate(scenario);
            lastBalancedFuel = Settings.Fuel;
        }
        catch (Exception error) when (ScenarioError(error))
        {
            Failure = ErrorMessage(error);
            Diagnose(scenario);
        }

        foreach (var tier in Tiers)
        {
            try
            {
                Variants.Add(new(tier, tier == Settings.Tier ? Balance : EnergyCalculator.Calculate(Settings.ToScenario(tier)), null));
            }
            catch (Exception error) when (ScenarioError(error))
            {
                Variants.Add(new(tier, null, ErrorMessage(error)));
            }
        }
        editedStep = null;
    }

    private void Diagnose(ProductionScenario scenario)
    {
        if (editedStep is not null) ProblemSteps.Add(editedStep);
        if (!scenario.Steps.Values.Any(step => step.MachineCount.HasValue)) return;
        var diagnostic = scenario with
        {
            Steps = scenario.Steps.ToDictionary(pair => pair.Key, pair => pair.Value with { MachineCount = null }),
        };
        try
        {
            Topology = EnergyCalculator.Calculate(diagnostic);
            foreach (var step in Topology.Steps)
            {
                if (scenario.Steps.GetValueOrDefault(step.RecipeId)?.MachineCount < step.MachineCount)
                    ProblemSteps.Add(step.RecipeId);
            }
        }
        catch (Exception error) when (ScenarioError(error))
        {
            // Keep the previous topology available for correction.
        }
    }

    private async Task Update(ScenarioSettings next)
    {
        Settings = next;
        Status = null;
        Calculate();
        var compare = RefreshRoutes();
        await Save();
        await compare;
    }

    private async Task Save()
    {
        if (transient || Balance is null) return;
        try { await JS.InvokeVoidAsync("dsp.saveScenario", SettingsStore.Write(Settings)); }
        catch (JSException) { Status = T("Could not save the scenario settings locally.", "Die Szenarioeinstellungen konnten lokal nicht gespeichert werden."); }
    }

    private async Task SelectLanguage(AppLanguage language)
    {
        Current.Select(language);
        foreach (string field in Fields.Keys.ToArray()) Reformat(field);
        Calculate();
        string code = language switch { AppLanguage.German => "de", AppLanguage.Chinese => "zh-CN", _ => "en" };
        try { await JS.InvokeVoidAsync("dsp.language", code); }
        catch (JSException) { Status = T("Could not save language locally.", "Die Sprache konnte lokal nicht gespeichert werden."); }
    }

    private async Task Import(InputFileChangeEventArgs args)
    {
        try
        {
            using var reader = new StreamReader(args.File.OpenReadStream(args.File.Size));
            var imported = SettingsStore.Read(await reader.ReadToEndAsync());
            Settings = imported;
            SetFields();
            Calculate();
            Status = $"{T("Scenario imported", "Szenario importiert")} · {args.File.Name}";
            await Save();
            await RefreshRoutes();
        }
        catch (Exception error) when (error is IOException or JsonException || ScenarioError(error))
        {
            Status = $"{T("Could not import the scenario.", "Szenario konnte nicht importiert werden.")} {error.Message}";
        }
    }

    private async Task Export()
    {
        if (Balance is null) return;
        string name = T("dsp-scenario.json", "dsp-szenario.json");
        try
        {
            await JS.InvokeVoidAsync("dsp.download", name, SettingsStore.Write(Settings));
            Status = $"{T("Scenario exported", "Szenario exportiert")} · {name}";
        }
        catch (JSException error) { Status = $"{T("Could not export the scenario.", "Szenario konnte nicht exportiert werden.")} {error.Message}"; }
    }

    private async Task SelectFuel(FuelKind fuel) => await Update(Settings with { Fuel = fuel });
    private async Task SelectTier(ProliferatorTier? tier) => await Update(Settings with { Tier = tier });
    private async Task FocusGenerator() => await JS.InvokeVoidAsync("dsp.focusGenerator");
    private void OpenFuelPicker() => OpenPicker(T("Select fuel", "Brennstoff wählen"), ProductionCatalog.Fuels.Select(fuel =>
        new Choice(fuel.ItemId, Name(fuel.ItemId), Name(fuel.GeneratorIconId), fuel.ItemId, FuelTip(fuel), Settings.Fuel == fuel.Kind)),
        id => SelectFuel(ProductionCatalog.Fuels.Single(fuel => fuel.ItemId == id).Kind));

    private void OpenPicker(string title, IEnumerable<Choice> choices, Func<string, Task> select)
    {
        ContextRecipe = null;
        Search = "";
        Picker = new(title, choices.ToArray(), select);
        focusDialog = "#picker-search";
    }

    private async Task Choose(string id)
    {
        var selected = Picker;
        await Dismiss();
        if (selected is not null) await selected.Select(id);
    }

    private async Task Dismiss()
    {
        Picker = null;
        CloseEditor();
        await JS.InvokeVoidAsync("dsp.closeDialog");
    }

    private void Reformat(string field)
    {
        if (NumericInput.TryDecimal(Fields[field].Trim(), out decimal value))
            Fields[field] = Edit(value);
    }

    private int? CountField(string key, string label) => NumericInput.Count(key, Fields[key], label);
    private decimal? DecimalField(string key, string label, bool optional = false, bool positive = false) =>
        NumericInput.Decimal(key, Fields[key], label, optional, positive);

    private static string? TierIcon(ProliferatorTier? tier) => tier.HasValue ? ProductionCatalog.Proliferator(tier.Value).ItemId : null;
    private static string FuelTip(FuelDefinition fuel) => $"{Name(fuel.ItemId)}\n{Precise(fuel.EnergyMj)} {T("MJ per item", "MJ pro Stück")}";
    private static string TierTip(ProliferatorTier? tier)
    {
        if (tier is null) return T("No Proliferator", "Ohne Proliferation");
        var rule = ProductionCatalog.Proliferator(tier.Value);
        return $"{Name(rule.ItemId)}\n{rule.SpraysPerUnit} {T("sprays", "Sprühvorgänge")} · +" +
            $"{Percent(rule.ExtraProductsMultiplier - 1m)} {T("Extra Products", "Zusatzprodukte")}\n+" +
            $"{Percent(rule.SpeedMultiplier - 1m)} {T("Production Speedup", "Produktionsbeschleunigung")} · +" +
            $"{Percent(rule.WorkingPowerMultiplier - 1m)} {T("working power", "Arbeitsleistung")}";
    }

    private static string Comparison(ProductionChainBalance balance, Func<decimal, string> format) =>
        $"{format(balance.Energy.NetPowerMw)} {T("MW net", "MW netto")} · {format(balance.Energy.TotalConsumptionMw)} {T("MW used", "MW Verbrauch")}";

    private sealed record SavedState(bool Transient, string? Language, string? Scenario);
    private sealed record Variant(ProliferatorTier? Tier, ProductionChainBalance? Balance, string? Error);
    private sealed record Choice(string Id, string Title, string Group, string? Icon, string Tooltip, bool Selected, bool Enabled = true, string Glyph = "—");
    private sealed record PickerState(string Title, Choice[] Choices, Func<string, Task> Select);
}
