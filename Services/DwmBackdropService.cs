using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MDViewer.Services;

public sealed class DwmBackdropService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaMicaEffect = 1029;
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
            GradientColor = ToAbgr(0x58, 0x10, 0x15, 0x20)
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
            DisableModernBackdrop(hwnd);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private static void DisableModernBackdrop(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        try
        {
            var none = 1;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref none, sizeof(int));

            var disabled = 0;
            DwmSetWindowAttribute(hwnd, DwmwaMicaEffect, ref disabled, sizeof(int));
        }
        catch
        {
        }
    }

    private static int ToAbgr(byte alpha, byte red, byte green, byte blue)
    {
        return unchecked((int)((uint)(alpha << 24) | (uint)(blue << 16) | (uint)(green << 8) | red));
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
