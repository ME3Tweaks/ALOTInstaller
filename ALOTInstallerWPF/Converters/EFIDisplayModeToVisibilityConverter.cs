using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ALOTInstallerWPF.Flyouts;

namespace ALOTInstallerWPF.Converters
{
    [Localizable(false)]
    [ValueConversion(typeof(FileImporterFlyout.EFIDisplayMode), typeof(Visibility))]
    public class EFIDisplayModeToVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileImporterFlyout.EFIDisplayMode dm && parameter is string str &&
                Enum.TryParse<FileImporterFlyout.EFIDisplayMode>(str, out var neededMode))
            {
                return dm == neededMode ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Don't need any convert back
            return null;
        }
    }
}
