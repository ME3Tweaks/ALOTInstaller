using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF.BuilderUI;

namespace ALOTInstallerWPF.Converters
{
    [Localizable(false)]
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class ToggleEnabledVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ManifestFile mf)
            {
                if (mf.Recommendation == RecommendationType.Required) return Visibility.Collapsed;
                if (mf.ForceDisabled) return Visibility.Collapsed; // Cannot enable this file
            }

            if (FileSelectionUIController.FSUIC.IsStaging) return Visibility.Collapsed; //Do not allow toggling files while staging
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
