using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Converters
{
    class RecommendationColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush RequiredBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88c40a00"));
        private static readonly SolidColorBrush RecommendedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88c45f00"));
        private static readonly SolidColorBrush OptionalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#88007fc4"));
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is RecommendationType r)
            {
                switch (r)
                {
                    case RecommendationType.Recommended:
                        return RecommendedBrush;
                    case RecommendationType.Required:
                        return RequiredBrush;
                    case RecommendationType.Optional:
                        return OptionalBrush;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null; //don't care
        }
    }
}
