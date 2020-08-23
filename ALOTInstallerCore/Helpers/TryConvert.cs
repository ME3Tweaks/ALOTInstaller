using System;
using System.Diagnostics;
using System.Globalization;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Attempts to convert a string to the specific type. If it fails, a default value is returned.
    /// </summary>
    internal class TryConvert
    {
        internal static short ToInt16(string v, short defaultValue)
        {
            if (Int16.TryParse(v, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        internal static byte ToByte(string v, byte defaultValue)
        {
            if (byte.TryParse(v, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        internal static int ToInt32(string v, int defaultValue)
        {
            if (int.TryParse(v, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        public static bool ToBool(string value, bool defaultValue)
        {
            if (bool.TryParse(value, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        public static double ToDouble(string value, double defaultValue)
        {
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        public static long ToInt64(string value, long defaultValue)
        {
            if (Int64.TryParse(value, out var res))
            {
                return res;
            }
            return defaultValue;
        }

        public static TEnum ToEnum<TEnum>(string value, TEnum defaultValue)
        {
            if (Enum.TryParse(typeof(TEnum), value, out var res))
            {
                return (TEnum) res;
            }
            return defaultValue;
        }
    }
}