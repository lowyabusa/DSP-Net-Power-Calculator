# Browser edition

The standalone .NET 10 Blazor WebAssembly application runs the same `DspNetEnergy.Core` engine and embedded game catalog as the Windows application.

## Run and publish

```powershell
dotnet run --project src/DspNetEnergy.Web
dotnet publish src/DspNetEnergy.Web -c Release
```

Serve `src/DspNetEnergy.Web/bin/Release/net10.0/publish/wwwroot` as static files. Use HTTPS or localhost for the service worker. Open the site at a URL ending in a slash. Offline reloads become available after the first visit finishes caching the published files. Scenario, language and decoded icons are stored in the browser for that site. Add `?transient=1` to start an English session with default settings and with saving disabled.

For GitHub Pages, choose **GitHub Actions** in **Settings → Pages** and run **Publish calculator to GitHub Pages** after the workflow has landed on the default branch. The relative base works at the repository subpath. GitHub requires a supported plan for Pages on a private repository. Pages availability and site visibility should be checked in the repository settings before running the deployment.

## Steam icons

In Steam, right-click **Dyson Sphere Program** and select **Manage → Browse local files**. In the web calculator select **Game icons → Choose game folder** and pick that installation folder. The browser reads the two supported Unity files under `DSPGAME_Data` after selection and keeps the decoded icons in its local storage. The selection works with custom Steam library locations. Text labels are available if icon extraction is skipped or some icons are absent. Game artwork is not bundled or uploaded. The reader targets the same Unity 2022.3.62 Texture2D layout as the desktop reader.

## Verification

```powershell
dotnet test DspNetEnergy.slnx -c Release
dotnet publish src/DspNetEnergy.Web -c Release -p:DspBrowserTests=true
dotnet run --project tests/DspNetEnergy.WebParity -c Release -- "$PWD/tests/browser/native-parity.json"
cd tests/browser
npm install
npx playwright install chromium
npm test
```

The Playwright suite serves published files at the GitHub Pages subpath. It compares the full native and browser results, tests scenario schema compatibility and exercises offline reload, browser persistence and JSON import and export. A normal publish excludes the test endpoints. To also verify icon import end to end, set `DSP_GAME_FOLDER` to the path of a local Steam installation of Dyson Sphere Program before running the browser suite. The optional test verifies that icons are still visible after an offline reload.
