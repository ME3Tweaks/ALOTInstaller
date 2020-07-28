using System;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Attempts to convert a string to the specific type. If it fails, a default value of 0 is returned.
    /// </summary>
    internal class TryConvert
    {
        internal static short ToInt16(string v)
        {
            if (Int16.TryParse(v, out var res))
            {
                return res;
            }
            return 0;
        }

        internal static byte ToByte(string v)
        {
            if (byte.TryParse(v, out var res))
            {
                return res;
            }
            return 0;
        }

        internal static int ToInt32(string v)
        {
            if (int.TryParse(v, out var res))
            {
                return res;
            }
            return 0;
        }
    }
}