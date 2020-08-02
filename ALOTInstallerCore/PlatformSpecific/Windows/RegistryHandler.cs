using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace ALOTInstallerCore.PlatformSpecific.Windows
{
    public static class RegistryHandler
    {
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

        internal static void WriteRegistryKey(RegistryKey subkey, string subpath, string value, object data)
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

        internal static void WriteRegistrySettingBool(string keyname, bool value)
        {
            WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon",keyname, value.ToString());
        }

        internal static void WriteRegistrySettingString(string keyname, string value)
        {
            WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        }

        internal static void WriteRegistrySettingInt(string keyname, int value)
        {
            WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        }

        internal static void WriteRegistrySettingFloat(string keyname, float value)
        {
            WriteRegistryKey(Registry.CurrentUser, "Software\\ALOTAddon", keyname, value);
        }

        /// <summary>
        /// Gets an ALOT registry setting string.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistrySettingString(string name)
        {
            string softwareKey = @"HKEY_CURRENT_USER\Software\ALOTAddon";
            return (string)Registry.GetValue(softwareKey, name, null);
        }

        /// <summary>
        /// Gets a string value frmo the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetRegistrySettingString(string key, string name)
        {

            return (string)Registry.GetValue(key, name, null);
        }

        public static bool? GetRegistrySettingBool(string name)
        {

            string softwareKey = @"HKEY_CURRENT_USER\Software\ALOTAddon";

            int? value = (int?)Registry.GetValue(softwareKey, name, null);
            if (value != null)
            {
                return value > 0;
            }
            return null;
        }
    }
}
