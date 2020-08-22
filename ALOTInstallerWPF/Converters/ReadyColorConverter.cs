using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Converters
{
    class ReadyColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is InstallerFile ifx)
            {
                if (ifx.IsProcessing) return Brushes.Yellow;
                if (ifx.IsWaiting) return Brushes.LightSkyBlue;
                if (ifx.Disabled) return Brushes.Gray;
                if (ifx.Ready) return Brushes.LimeGreen;
                return Brushes.Red;
            }

            return Brushes.DeepPink;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
