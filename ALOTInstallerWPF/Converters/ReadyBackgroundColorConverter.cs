using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;

namespace ALOTInstallerWPF.Converters
{
    class ReadyBackgroundColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush DisabledBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#25545050"));
        private static readonly SolidColorBrush ReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3549c70a"));
        private static readonly SolidColorBrush WaitingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35009dff"));
        private static readonly SolidColorBrush RequiredNotReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35d91600"));
        private static readonly SolidColorBrush RecommendedNotReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35b85000"));
        private static readonly SolidColorBrush ProcessingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#35ffe900"));
        private static readonly SolidColorBrush OptionalNotReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#350045a6"));
        private static readonly SolidColorBrush UserNotReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3503e3fc"));
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is InstallerFile ifx)
            {
                if (ifx.Disabled) return DisabledBrush;
                if (ifx.IsProcessing) return ProcessingBrush;
                if (ifx.IsWaiting) return WaitingBrush;
                if (ifx.Ready) return ReadyBrush;
                if (ifx is ManifestFile mf)
                {
                    if (mf.Recommendation == RecommendationType.Required) return RequiredNotReadyBrush;
                    if (mf.Recommendation == RecommendationType.Recommended) return RecommendedNotReadyBrush;
                }
                else if (ifx is UserFile uf)
                {
                    return UserNotReadyBrush;
                }

                return OptionalNotReadyBrush;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
