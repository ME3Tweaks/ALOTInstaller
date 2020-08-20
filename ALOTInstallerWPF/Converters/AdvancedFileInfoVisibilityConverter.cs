using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerWPF.Converters
{
    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class AdvancedFileInfoVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool ready)
            {
                return (!ready || Settings.ShowAdvancedFileInfo) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
