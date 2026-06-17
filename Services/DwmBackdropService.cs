using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MDViewer.Services;

public sealed class DwmBackdropService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int AccentEnableAcrylicblurbehind = 4;
    private const int WcaAccentPolicy = 19;

    public void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            PrepareTransparentClientArea(window);
            ApplyAcrylicBlur(new WindowInteropHelper(window).Handle);
            return;
        }

        window.SourceInitialized += (_, _) =>
        {
            PrepareTransparentClientArea(window);
            ApplyAcrylicBlur(new WindowInteropHelper(window).Handle);
        };
    }

    public void ApplyRoundedCorners(Window window, bool rounded)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var preference = rounded ? 2 : 1;
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
        }
    }

    public void ApplyDarkCaption(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var value = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
        }
        catch
        {
        }
    }

    private static void PrepareTransparentClientArea(Window window)
    {
        window.Background = Brushes.Transparent;
        if (PresentationSource.FromVisual(window) is HwndSource source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }
    }

    private static void ApplyAcrylicBlur(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0))
        {
            return;
        }

        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicblurbehind,
            AccentFlags = 2,
            GradientColor = unchecked((int)0x55101520)
        };

        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                SizeOfData = accentSize,
                Data = accentPtr
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
}
