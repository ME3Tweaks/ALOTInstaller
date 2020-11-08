using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Converters
{
    class InstallerFileBackingFileConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is InstallerFile ifx)
            {
                try
                {
                    return $"{Path.GetFileName(ifx.GetUsedFilepath())} ({FileSizeFormatter.FormatSize(ifx.FileSize)})";
                }
                catch
                {
                    // If the file is moved and this is invoked it may cause an exception
                    return ifx.Filename;
                }
            }

            return "ERROR";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
