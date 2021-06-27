using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using ALOTInstallerCore.Objects;
using LegendaryExplorerCore.Packages;

namespace ALOTInstallerWPF.Converters
{
    public class GameToLogoConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MEGame game && game != MEGame.Unknown)
            {
                return $"/Images/logo_{game.ToString().ToLower()}.png";
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}