﻿using System;

namespace ALOTInstallerCore.Helpers
{
    public static class FileSizeFormatter
    {
        // Load all suffixes in an array  
        static readonly string[] suffixes =
            {"Bytes", "KB", "MB", "GB", "TB", "PB"};

        public static string FormatSize(Int64 bytes)
        {
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