using System;
using System.IO;
using Microsoft.Win32;

namespace RiftVault.Win32
{
    public static class SystemIntegrationHelper
    {
        public static string GetDefaultInstallDirectory()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", "RiftVault");
        }

        public static bool CreateDesktopShortcut(string? exePath = null, string shortcutName = "Rift Vault", string description = "Rift Vault - Modern High-Performance Glassmorphic File Explorer")
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;

                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, $"{shortcutName}.lnk");

                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                shortcut.Description = description;
                shortcut.IconLocation = $"{exePath},0";
                shortcut.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RegisterExplorerContextMenu(string? exePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;

                // 1. Directory Context Menu (Right click on a folder)
                using (var dirKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\RiftVault"))
                {
                    if (dirKey != null)
                    {
                        dirKey.SetValue("", "Open in Rift Vault");
                        dirKey.SetValue("Icon", $"\"{exePath}\",0");
                        using (var cmdKey = dirKey.CreateSubKey("command"))
                        {
                            cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }
                }

                // 2. Directory Background Context Menu (Right click inside an open folder)
                using (var bgKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\Background\shell\RiftVault"))
                {
                    if (bgKey != null)
                    {
                        bgKey.SetValue("", "Open in Rift Vault");
                        bgKey.SetValue("Icon", $"\"{exePath}\",0");
                        using (var cmdKey = bgKey.CreateSubKey("command"))
                        {
                            cmdKey?.SetValue("", $"\"{exePath}\" \"%V\"");
                        }
                    }
                }

                // 3. Drive Context Menu (Right click on a drive letter C:, D:, etc.)
                using (var driveKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Drive\shell\RiftVault"))
                {
                    if (driveKey != null)
                    {
                        driveKey.SetValue("", "Open in Rift Vault");
                        driveKey.SetValue("Icon", $"\"{exePath}\",0");
                        using (var cmdKey = driveKey.CreateSubKey("command"))
                        {
                            cmdKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool UnregisterExplorerContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\shell\RiftVault", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Directory\Background\shell\RiftVault", false);
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Drive\shell\RiftVault", false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsContextMenuRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\shell\RiftVault");
                return key != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
