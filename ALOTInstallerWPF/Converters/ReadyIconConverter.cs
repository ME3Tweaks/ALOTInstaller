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
    class ReadyIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is InstallerFile ifx)
            {
                if (ifx.IsProcessing) return PackIconIoniconsKind.CogiOS;
                if (ifx.IsWaiting) return PackIconIoniconsKind.HourglassMD;
                if (ifx.Disabled) return PackIconIoniconsKind.RemoveCircleMD;
                if (ifx.Ready) return PackIconIoniconsKind.CheckmarkCircleMD;
                return PackIconIoniconsKind.CloseCircleMD;
            }

            return PackIconIoniconsKind.JetMD;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
