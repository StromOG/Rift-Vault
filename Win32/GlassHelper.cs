using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RiftVault.Win32
{
    public static class GlassHelper
    {
        public static bool EnableMica(Window window, bool isDarkTheme)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return false;

            int trueValue = 0x01;
            int falseValue = 0x00;
            
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
            int darkModeValue = isDarkTheme ? trueValue : falseValue;
            DwmSetWindowAttribute(hwnd, 20, ref darkModeValue, sizeof(int));

            // Windows 11 22H2+ Mica: DWMWA_SYSTEMBACKDROP_TYPE = 38, DWMSBT_MAINWINDOW = 2
            int backdropType = 2;
            int hr = DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
            if (hr == 0) return true;

            // Older Windows 11 Mica: DWMWA_MICA_EFFECT = 1029
            hr = DwmSetWindowAttribute(hwnd, 1029, ref trueValue, sizeof(int));
            if (hr == 0) return true;

            return false;
        }

        public static bool EnableAcrylic(Window window, bool isDarkTheme)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return false;

            var policy = new NativeMethods.AccentPolicy
            {
                AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = isDarkTheme ? 0x99000000 : 0x99FFFFFF
            };

            int sizeOfPolicy = Marshal.SizeOf(policy);
            IntPtr policyPtr = Marshal.AllocHGlobal(sizeOfPolicy);
            Marshal.StructureToPtr(policy, policyPtr, false);

            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = NativeMethods.WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = policyPtr,
                SizeOfData = sizeOfPolicy
            };

            int result = NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(policyPtr);

            return result == 0;
        }

        public static bool EnableRoundedCorners(Window window, bool round = true)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return false;

                // DWMWA_WINDOW_CORNER_PREFERENCE = 33
                // DWMWCP_ROUND = 2, DWMWCP_DONOTROUND = 1
                int cornerPreference = round ? 2 : 1;
                int hr = DwmSetWindowAttribute(hwnd, 33, ref cornerPreference, sizeof(int));
                return hr == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void ApplyBackdrop(Window window, string backdropType, bool isDarkTheme)
        {
            try
            {
                EnableRoundedCorners(window, true);

                if (string.Equals(backdropType, "Mica", StringComparison.OrdinalIgnoreCase))
                {
                    EnableMica(window, isDarkTheme);
                }
                else if (string.Equals(backdropType, "Acrylic", StringComparison.OrdinalIgnoreCase))
                {
                    EnableAcrylic(window, isDarkTheme);
                }
                else
                {
                    // Default / Solid: ensure immersive dark mode is set
                    var hwnd = new WindowInteropHelper(window).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        int darkModeValue = isDarkTheme ? 1 : 0;
                        DwmSetWindowAttribute(hwnd, 20, ref darkModeValue, sizeof(int));
                        int noneBackdrop = 1; // DWMSBT_NONE
                        DwmSetWindowAttribute(hwnd, 38, ref noneBackdrop, sizeof(int));
                    }
                }
            }
            catch { }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}