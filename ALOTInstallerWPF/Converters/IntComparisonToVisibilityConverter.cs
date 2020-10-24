using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ALOTInstallerWPF.Converters
{
    [Localizable(false)]
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class IntComparisonToVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Use parameter styling: OP_NUM_OP2_NUM2...
            if (value is int currentValue && parameter is string str)
            {
                var split = str.Split('_');
                if (split.Length % 2 != 0) return Visibility.Collapsed;
                bool visible = true;
                int i = 0;
                while (visible && i < split.Length)
                {
                    if (!int.TryParse(split[i + 1], out var compareToValue)) return Visibility.Collapsed; //Wrong value
                    switch (split[i])
                    {
                        case "GT":
                            visible &= (currentValue > compareToValue);
                            break;
                        case "GTE":
                            visible &= (currentValue >= compareToValue);
                            break;
                        case "LT":
                            visible &= (currentValue < compareToValue);
                            break;
                        case "LTE":
                            visible &= (currentValue <= compareToValue);
                            break;
                        case "E":
                            visible &= (currentValue == compareToValue);
                            break;
                        case "NE":
                            visible &= (currentValue != compareToValue);
                            break;

                    }
                    i += 2;
                }

                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
