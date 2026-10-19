using System;
using System.IO;
using Microsoft.Win32;

namespace SeleccionTraductor
{
    internal static class StartupSettings
    {
        internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "Lexora";

        internal static string Command(string executable)
        {
            return "\"" + Path.GetFullPath(executable) + "\" --tray";
        }

        internal static bool IsEnabled(string executable, string keyPath = RunKey)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                return key != null && string.Equals(key.GetValue(ValueName) as string, Command(executable), StringComparison.OrdinalIgnoreCase);
        }

        internal static void SetEnabled(string executable, bool enabled, string keyPath = RunKey)
        {
            if (enabled)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                    key.SetValue(ValueName, Command(executable), RegistryValueKind.String);
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                    if (key != null && string.Equals(key.GetValue(ValueName) as string, Command(executable), StringComparison.OrdinalIgnoreCase))
                        key.DeleteValue(ValueName, false);
            }
        }
    }
}
