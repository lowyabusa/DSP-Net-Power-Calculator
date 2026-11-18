using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DspNetEnergy.Desktop;

internal static partial class WindowTheme
{
    internal static void UseDarkCaption(Window window) => window.SourceInitialized += (_, _) =>
    {
        int enabled = 1;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref enabled, sizeof(int));
    };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
