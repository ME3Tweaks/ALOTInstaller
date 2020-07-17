using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    public static class Extensions
    {
        public static bool IsDefault<T>(this T value) where T : struct
        {
            bool isDefault = value.Equals(default(T));

            return isDefault;
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static bool RepresentsPackageFilePath(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(".pcc", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(".sfm", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(".u", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(".upk", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }

    }
}
