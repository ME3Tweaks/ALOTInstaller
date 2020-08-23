using System;

namespace ALOTInstallerCore.Helpers
{
    public static class FileSizeFormatter
    {
        // Load all suffixes in an array  
        static readonly string[] suffixes =
            {" bytes", "KB", "MB", "GB", "TB", "PB"};

        public static string FormatSize(Int64 bytes)
        {
            if (bytes < 0) throw new Exception("Size of bytes to format can't be less than 0.");
            if (bytes < 1024) return $"{bytes} bytes";
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return $"{number:n1}{suffixes[counter]}";
        }

        public static string FormatSize(UInt64 bytes)
        {
            if (bytes < 1024) return $"{bytes} bytes";
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return $"{number:n1}{suffixes[counter]}";
        }
    }
}