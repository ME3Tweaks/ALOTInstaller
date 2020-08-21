using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using ALOTInstallerCore.Objects;
using MahApps.Metro.IconPacks;

namespace ALOTInstallerWPF.Converters
{
    class FileCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count)
            {
                if (count != 1)
                {
                    return $"{count} files";
                }

                return "1 file";
            }

            return "ERROR!";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
