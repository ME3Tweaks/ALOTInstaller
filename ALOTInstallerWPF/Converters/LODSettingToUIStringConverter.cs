using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ALOTInstallerCore.Helpers;
using System.Windows.Data;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Converters
{
    public class LODSettingToUIStringConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LodSetting ls)
            {
                string str = "Unknown";
                if (ls.HasFlag(LodSetting.Vanilla)) str = "Vanilla";
                if (ls.HasFlag(LodSetting.TwoK)) str = "2K";
                if (ls.HasFlag(LodSetting.FourK)) str = "4K";
                if (ls.HasFlag(LodSetting.SoftShadows)) str += ", soft shadows";
                return $"Current setting: {str}";
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}