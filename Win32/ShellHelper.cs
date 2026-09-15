using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace RiftVault.Win32
{
    public static class ShellHelper
    {
        public static BitmapSource? GetThumbnail(string filePath, int width, int height)
        {
            IntPtr hBitmap = IntPtr.Zero;
            NativeMethods.IShellItem? shellItem = null;

            try
            {
                NativeMethods.SHCreateItemFromParsingName(
                    filePath, 
                    IntPtr.Zero, 
                    typeof(NativeMethods.IShellItem).GUID, 
                    out shellItem);

                if (shellItem is NativeMethods.IShellItemImageFactory imageFactory)
                {
                    var size = new NativeMethods.SIZE(width, height);
                    imageFactory.GetImage(size, NativeMethods.SIIGBF.RESIZETOFIT | NativeMethods.SIIGBF.BIGGERSIZEOK, out hBitmap);

                    if (hBitmap != IntPtr.Zero)
                    {
                        var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                }
            }
            catch
            {
                // Fallback or ignore on failure
            }
            finally
            {
                if (hBitmap != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(hBitmap);
                }
                if (shellItem != null)
                {
                    Marshal.ReleaseComObject(shellItem);
                }
            }

            return null;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        public static bool ShowFileProperties(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                var info = new SHELLEXECUTEINFO();
                info.cbSize = Marshal.SizeOf(info);
                info.lpVerb = "properties";
                info.lpFile = path;
                info.nShow = 5; // SW_SHOW
                info.fMask = 0x0000000C; // SEE_MASK_INVOKEIDLIST
                return ShellExecuteEx(ref info);
            }
            catch
            {
                return false;
            }
        }
    }
}