using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Data;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Converters
{
    public class ApplicableGamesVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is ApplicableGame ag && parameter is string gameshortname)
            {
                ApplicableGame gsnag = Enum.Parse<ApplicableGame>(gameshortname);
                return ag.HasFlag(gsnag) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null; //don't care
            //if (value is bool)
            //{
            //    if ((bool)value == true)
            //        return "yes";
            //    else
            //        return "no";
            //}
            //return "no";
        }
    }
}
