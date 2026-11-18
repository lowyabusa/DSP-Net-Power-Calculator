# DSP Net-Energy Calculator

[English](README.md) | [Deutsch](README.de.md) | [简体中文](README.zh-CN.md)

> This project is vibe-coded.

A browser-based calculator for the net electrical output of fuel production chains in Dyson Sphere Program, built with .NET 10 Blazor WebAssembly. The C# engine runs locally in the browser and covers all 24 fuels accepted by the Thermal Power Plant, Mini Fusion Power Plant, and Artificial Star in the base game. Items, recipes, machines, power plants, and Proliferator rules are stored in separate JSON datasets.

Unlike general-purpose production calculators, DSP Net-Energy Calculator determines the **net electrical output** remaining after subtracting the production chain's own power consumption, including resource extraction, Proliferator production, and configured logistics costs. Costs outside the selected calculation scope are explicitly marked as excluded.

## Use the website without installing .NET

Once the calculator has been deployed to a static web host, open its website URL in a browser. Deployment from this repository is described under [Development: build and publish](#development-build-and-publish). **Users do not need to install .NET, the .NET SDK, or a desktop application.** The browser automatically downloads the application and its WebAssembly runtime on first use. All calculations run locally in the browser; no calculation backend is required.

Scenario, language, and imported icons are stored in the current browser profile for that site address, provided browser storage is available. Clearing the site's browser data also removes these saved settings and icons; use JSON export to back up scenarios.

## Use offline

The published website caches its application files through a service worker, which requires HTTPS or localhost. Once caching is complete and the service worker is active, reopen the same site address in the same browser profile to reload and use the calculator offline, including its cached runtime. The offline cache must remain available; clearing the site data also removes it. No separate .NET installation is needed for offline use either. The development server described below does not provide this offline cache.

## Using the app

- On first use, the app opens in English. Use the language buttons in the upper right to switch between English, German, and Simplified Chinese. The app saves the language preference separately from the scenario.
- Choose a fuel and a Proliferator tier on the left. Enter the target fuel throughput in the center. Gross output, consumption, net output, and the calculation scope update on the right.
- **All fuels** opens the searchable icon picker, grouped by power plant type. Log, Plant Fuel, and Energy Shard are treated as supplied items. The balance marks their collection or acquisition costs as excluded.
- Click recipe, material, and machine icons to choose alternatives. Right-click a production step or use its action button to set machine count, utilization, or power. The spray icons select **Extra Products** or **Production Speedup** where supported. Tooltips explain unavailable options.
- The Coal and Energetic Graphite reference compares both routes at 120 supplied Coal/min, excluding extraction, Sorters, and logistics. The Proliferator comparison uses the current scenario. Expand material flows to see per-step throughput, working power, and idle power.
- **Import JSON** and **Export JSON** are in the scenario header. Exports contain the current valid recipe, machine, spray, and logistics settings. Scenario files use schema version 1. A failed import leaves the current scenario in place.
- **Alternative recipe chains** ranks feasible recipe combinations by net output at the same fuel throughput, including upstream Proliferator production. The power plant limit, supplied materials, factory settings, and logistics parameters are held constant. The view flags externally supplied materials and counts infeasible combinations. Select **Apply** to use a ranked route.
- Valid scenario changes are saved automatically in the browser. Add `?transient=1` to the site URL, for example `http://localhost:5080/?transient=1`, to start an English session with default settings and without loading or saving the stored scenario, language, or icons.

## Game icons

In Steam, right-click **Dyson Sphere Program** and select **Manage > Browse local files**. In the calculator, select **Game icons > Choose game folder** and choose that installation folder containing `DSPGAME.exe` and `DSPGAME_Data`. Custom Steam library locations are supported. The browser requires manual folder selection; the **Steam** button opens the game's Steam library page.

After folder selection, the browser reads only `DSPGAME_Data/resources.assets` and `DSPGAME_Data/sharedassets0.assets` locally to extract supported icons. Decoded icons are stored in the browser's IndexedDB for later visits and offline use. Game files and icons are not uploaded. Select **Continue without icons** to use text labels, or reopen **Game icons** in the header to change the selection. Unavailable icons fall back to text.

The icon reader targets the Windows assets checked with DSP 0.10.35.29151 and Unity 2022.3.62. Game artwork is excluded from the repository and published website and belongs to its respective rights holders. DSP Net-Energy is an independent fan tool. The reader uses [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) under its [MIT license](src/DspNetEnergy.Desktop/Assets/AssetsTools.NET.LICENSE.txt).

## Development: run from source

The **.NET 10 SDK is only required for building the source code, running the development server, and running the .NET tests**. It is not a requirement for users of the published website.

With the SDK installed, open PowerShell in the project folder and run:

```powershell
dotnet run --project src/DspNetEnergy.Web --configuration Release -- --urls http://localhost:5080
```

Open [http://localhost:5080/](http://localhost:5080/) in the browser. Keep the command running while using this development server.

## Development: build and publish

With the .NET 10 SDK installed, build the static website with:

```powershell
dotnet publish src/DspNetEnergy.Web --configuration Release
```

Serve `src/DspNetEnergy.Web/bin/Release/net10.0/publish/wwwroot` with a static web server. The static web server does not need .NET installed. Use HTTPS or localhost and open the site at a URL ending in a slash. Opening `index.html` directly from the filesystem is not supported.

The repository includes the manually triggered **Publish calculator to GitHub Pages** workflow. Setup and browser verification commands are documented in [Browser edition](docs/blazor-web.md).

## Tests

Run the solution tests from the project folder on Windows with the .NET 10 SDK. The solution also contains the WPF desktop application.

```powershell
dotnet test DspNetEnergy.slnx --configuration Release
```

See [Browser edition](docs/blazor-web.md#verification) for browser parity, persistence, JSON import/export, offline reload, and optional local icon import tests.

## Logistics costs

Open **Edit logistics costs** under **Advanced scenario settings**. For Sorters and Logistics Stations, enter the count, average utilization, and active and idle power per unit. Station power covers station operation and excludes vehicle charging, which is entered separately for Drones and Vessels. For Logistics Drones and Interstellar Logistics Vessels, multiply round trips per minute by kJ per round trip and divide by 60,000 to get average charging power in MW. Include the return leg in the energy per round trip.

Blank categories appear as excluded under **Calculation scope**. Enter a count or trip rate of zero to include an unused category explicitly. When itemized costs are configured, the infrastructure total is reserved for additional costs. The recipe comparison uses the same logistics parameters for every route.

## Calculating a production chain

```csharp
using DspNetEnergy.Core;

var scenario = new ProductionScenario(FuelKind.EnergeticGraphite, FuelPerMinute: 60m)
{
    Proliferation = new(ProliferatorTier.MkIII, SprayFuel: true, SelfSpray: true),
    Steps = new Dictionary<string, ProductionStepSettings>
    {
        ["energetic-graphite"] = new("arc-smelter", Spray: SprayMode.ExtraProducts),
    },
};
ProductionChainBalance result = EnergyCalculator.Calculate(scenario);
```

- `FuelPerMinute` sets the target rate in items per minute. The engine resolves the required inputs back to their sources. `RecipeChoices` selects alternative recipes by material. Usable by-products and recirculated inputs share the same material balance. Cycles such as X-Ray Cracking assume an initial stock.
- `Steps` sets the machine, count, maximum utilization, throughput multiplier, optional power overrides, and spray mode by recipe. If a fixed machine count cannot meet the target, the scenario reports an error. A power plant capacity limit can leave fuel unburned. A steady-state factory needs an outlet for that surplus.
- `SuppliedItems` defines the materials supplied at the calculation boundary. Material flows list their quantities and the energy balance flags their upstream costs as excluded. Dark Fog drops are treated as supplied items. `Logistics` holds the four itemized cost categories. If it is unset, `OtherInfrastructureMw` is the lump sum for Sorters and logistics. With both unset, those costs are excluded.
- Each of the five consumption categories accounts for a separate share of demand. The engine allocates the additional production and extraction needed for spray to Proliferator production. Idle draw for installed machines and Spray Coaters is accounted for separately.
- The default pooled capacity is 1,800 items/min per Spray Coater. Spray costs include coated production inputs, fuel actually burned, and self-spraying. Self-sprayed Proliferator yields 13, 28, or 75 charges per item for Mk.I, Mk.II, or Mk.III. After the charge used for self-spraying, 12, 27, or 74 charges are available for other items. `ExternalDysonPowerMw` reports the energy carried by Critical Photons separately.

The original `DirectCombustionScenario` and `CoalToGraphiteScenario` use supplied fuel or Coal. Their reference tests exclude resource extraction, Sorters, and logistics.

## Game data

The embedded recipe catalog is based on the MIT-licensed FactorioLab dataset marked `version.DSP = 0.10.29.21950`. The published source still reported that version on November 7, 2026. The game version used for the cross-check is **0.10.35.29151**, released on October 15, 2026. [FactorioLab dataset](https://github.com/factoriolab/factoriolab/blob/main/public/data/dsp/data.json) · [DSP patch notes](https://steamcommunity.com/app/1366540/announcements/?l=english)

The fuel list and exact heating values were checked against the installed game item prototypes for Steam build 25955035. The 24 fuels comprise 21 chemical fuels, one fusion fuel, and two Antimatter fuels. Charged Accumulators belong to the Energy Exchanger and lenses belong to the Ray Receiver. Item and recipe names use the English, German, and Simplified Chinese localization tables shipped with that game build.

The catalog includes the Dark Fog Lens variant of the Ray Receiver. The patch that changed lens buffer capacity affects startup. The calculator models steady-state operation. [FactorioLab MIT license](src/DspNetEnergy.Core/GameData/FactorioLab.LICENSE.txt)

Defaults assume six veins per Mining Machine, 1 Crude Oil/s per Oil Extractor, and 1,800 unstacked Hydrogen passes/min per Fractionator. Click the rate shown on a **Crude Oil Seep** step to enter the Oil Extractor's current Collecting Speed at full utilization. Use an average per extractor if the sources differ. The material flow below the step reports the combined output in items/min. Orbital Collector rates use the FactorioLab gas compositions after the collector's internal 30 MW demand. Ray Receiver rates assume continuous reception and consumption of 0.1 lenses/min for the lens variants.
