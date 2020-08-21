using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for SettingsFlyout.xaml
    /// </summary>
    public partial class SettingsFlyout : UserControl, INotifyPropertyChanged
    {
        public bool ME1Available => Locations.GetTarget(Enums.MEGame.ME1) != null;
        public bool ME2Available => Locations.GetTarget(Enums.MEGame.ME2) != null;
        public bool ME3Available => Locations.GetTarget(Enums.MEGame.ME3) != null;
        public string ME1TextureInstallInfo { get; private set; }
        public string ME2TextureInstallInfo { get; private set; }
        public string ME3TextureInstallInfo { get; private set; }

        public SettingsFlyout()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private bool isDecidingBetaMode;
        private async void Checkbox_BetaMode_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!isDecidingBetaMode)
            {
                isDecidingBetaMode = true;

                isDecidingBetaMode = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void UpdateGameStatuses()
        {
            var me1Target = Locations.ME1Target;
            if (me1Target == null || me1Target.GetInstalledALOTInfo() == null)
            {
                ME1TextureInstallInfo = "ME1: No textures installed";
            }
            else
            {
                ME1TextureInstallInfo = $"ME1: {me1Target.GetInstalledALOTInfo().ToString()}";
            }

            var me2Target = Locations.ME2Target;
            if (me2Target == null || me2Target.GetInstalledALOTInfo() == null)
            {
                ME2TextureInstallInfo = "ME2: No textures installed";
            }
            else
            {
                ME2TextureInstallInfo = $"ME2: {me2Target.GetInstalledALOTInfo().ToString()}";
            }

            var me3Target = Locations.ME3Target;
            if (me3Target == null || me3Target.GetInstalledALOTInfo() == null)
            {
                ME3TextureInstallInfo = "ME3: No textures installed";
            }
            else
            {
                ME3TextureInstallInfo = $"ME3: {me3Target.GetInstalledALOTInfo().ToString()}";
            }
        }
    }
}
