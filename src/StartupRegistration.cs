using System;
using Microsoft.Win32;

namespace WindowsFish
{
    internal static class StartupRegistration
    {
        internal const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "Moyu";
        internal static string Command(string path) { return "\"" + path + "\" --silent"; }

        internal static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunPath))
                    return key != null && !string.IsNullOrEmpty(key.GetValue(ValueName) as string);
            }
            catch { return false; }
        }

        internal static bool SetEnabled(bool enabled, string path, out string error)
        {
            error = null;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunPath))
                {
                    if (enabled) key.SetValue(ValueName, Command(path), RegistryValueKind.String);
                    else key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
