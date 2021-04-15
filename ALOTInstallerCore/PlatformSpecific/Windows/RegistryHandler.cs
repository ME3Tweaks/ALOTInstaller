#if WINDOWS
using System;
using System.Security.AccessControl;
using Microsoft.Win32;
using Serilog;

namespace ALOTInstallerCore.PlatformSpecific.Windows
{
    public static class RegistryHandler
    {
        public static string RegistrySettingsPath = @"HKEY_CURRENT_USER\SOFTWARE\ALOTAddon";
        public static string CurrentUserRegistrySubpath = @"Software\ALOTAddon";

        /// <summary>
        /// Tests if a registry key is writable by writing a randomly named value into it and then attempting to delete it.
        /// </summary>
        /// <param name="subkey"></param>
        /// <param name="subpath"></param>
        /// <returns></returns>
        public static bool TestKeyWritable(RegistryKey subkey, string subpath)
        {
            var guid = Guid.NewGuid().ToString();
            try
            {
                subkey = subkey.OpenSubKey(subpath, true);
                subkey.SetValue(guid, "testwrite");
                subkey.DeleteValue(guid);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, string data)
        {

            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }
            subkey.SetValue(value, data);
        }

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, bool data)
        {

            WriteRegistryKey(subkey, subpath, value, data ? 1 : 0);
        }

        /// <summary>
        /// Writes the specified data to the lsited key/subpath/value.
        /// </summary>
        /// <param name="subkey"></param>
        /// <param name="subpath"></param>
        /// <param name="value"></param>
        /// <param name="data"></param>
        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, object data)
        {

            int i = 0;
            string[] subkeys = subpath.Split('\\');
            while (i < subkeys.Length)
            {
                subkey = subkey.CreateSubKey(subkeys[i]);
                i++;
            }

            if (data is long l)
            {
                subkey.SetValue(value, data, RegistryValueKind.QWord);
            }
            else
            {
                subkey.SetValue(value, data);
            }
        }

        public static void WriteRegistrySettingBool(string keyname, bool value)
        {
            WriteRegistryKey(Registry.CurrentUser, CurrentUserRegistrySubpath, keyname, value);
        }

        internal static void WriteRegistrySettingString(string keyname, string value)
        {
            WriteRegistryKey(Registry.CurrentUser, CurrentUserRegistrySubpath, keyname, value);
        }

        internal static void WriteRegistrySettingInt(string keyname, int value)
        {
            WriteRegistryKey(Registry.CurrentUser, CurrentUserRegistrySubpath, keyname, value);
        }

        internal static void WriteRegistrySettingLong(string keyname, long value)
        {
            WriteRegistryKey(Registry.CurrentUser, CurrentUserRegistrySubpath, keyname, value);
        }

        internal static void WriteRegistrySettingFloat(string keyname, float value)
        {
            WriteRegistryKey(Registry.CurrentUser, CurrentUserRegistrySubpath, keyname, value);
        }

        /// <summary>
        /// Gets an ALOT registry setting string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistryString(string name)
        {
            return (string)Registry.GetValue(RegistrySettingsPath, name, null);
        }

        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueName"></param>
        /// <returns></returns>
        public static string GetRegistryString(string key, string valueName)
        {

            return (string)Registry.GetValue(key, valueName, null);
        }

        /// <summary>
        /// Gets a DWORD value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueName"></param>
        /// <returns></returns>
        public static int? GetRegistryInt(string key, string valueName)
        {
            return (int?)Registry.GetValue(key, valueName, -1);
        }

        /// <summary>
        /// Gets a settings value for ALOT Installer from the registry
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool? GetRegistrySettingBool(string name)
        {
            int? value = (int?)Registry.GetValue(RegistrySettingsPath, name, null);
            if (value != null)
            {
                return value > 0;
            }
            return null;
        }

        /// <summary>
        /// Deletes a registry key from the registry. USE WITH CAUTION
        /// </summary>
        /// <param name="primaryKey"></param>
        /// <param name="subkey"></param>
        /// <param name="valuename"></param>
        public static bool DeleteRegistryValue(RegistryKey primaryKey, string subkey, string valuename)
        {
            using RegistryKey key = primaryKey.OpenSubKey(subkey, true);
            try
            {
                key?.DeleteValue(valuename, true);
                Log.Information($@"[AICORE] Deleted registry value {primaryKey.Name}\{subkey} {valuename}");
                return true;
            }
            catch (Exception e)
            {
                Log.Information($@"[AICORE] Error deleting registry value {primaryKey.Name}\{subkey} {valuename}: {e.Message}");
                return false;
            }
        }

        public static long? GetRegistrySettingLong(string name)
        {
            return (long?)Registry.GetValue(RegistrySettingsPath, name, null);
        }

        /// <summary>
        /// Removes all non-inherited ACLs from the specified key and path.
        /// </summary>
        /// <param name="localMachine"></param>
        /// <param name="softwareWow6432nodeAgeiaTechnologies"></param>
        public static void RemoveFullControlNonInheritedACLs(RegistryKey key, string subkey, Action successfullyRevoked = null, Action error = null)
        {
            try
            {
                using var keyToOperateOn = key.OpenSubKey(subkey, true);
                var acl = keyToOperateOn.GetAccessControl();
                string currentUser = $"{Environment.UserDomainName}\\{Environment.UserName}";
                RegistryAccessRule rar = new RegistryAccessRule(currentUser, RegistryRights.FullControl, AccessControlType.Allow);
                acl.RemoveAccessRuleAll(rar);
                keyToOperateOn.SetAccessControl(acl);
                successfullyRevoked?.Invoke();
            }
            catch { error?.Invoke(); }// We don't care. If there's an issue, we can't do anything about it, so don't bother throwing the error.
        }
    }
}
#endif