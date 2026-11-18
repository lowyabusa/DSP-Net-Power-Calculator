using DspNetEnergy.WebParity;
using Microsoft.JSInterop;

namespace DspNetEnergy.Web;

public static class BrowserParity
{
    [JSInvokable]
    public static string RunParityScenarios() => Scenarios.Run();

    [JSInvokable]
    public static string RoundTripScenario(string json) => SettingsStore.Write(SettingsStore.Read(json));
}
