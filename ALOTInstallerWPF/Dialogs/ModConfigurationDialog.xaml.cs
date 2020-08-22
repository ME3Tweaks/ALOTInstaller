using MahApps.Metro.Controls.Dialogs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF;
using ALOTInstallerWPF.Objects;

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for ModConfigurationDialog.xaml
    /// </summary>
    public partial class ModConfigurationDialog : CustomDialog
    {
        /// <summary>
        /// Callback that will be issued when the dialog closes. True for accept, False for cancel. This must be set or the app will crash
        /// </summary>
        public Action<bool> closeDialogWithResult;
        public string DialogTitle { get; }
        public string ModeText { get; }
        public ManifestFile ConfiguringFile { get; set; }
        public ObservableCollectionExtended<IConfigurableMod> ConfigurableItems { get; } = new ObservableCollectionExtended<IConfigurableMod>();
        public ModConfigurationDialog(ManifestFile mf, ManifestMode mode)
        {
            DataContext = this;
            LoadCommands();
            ConfiguringFile = mf;
            DialogTitle = $"{mf.FriendlyName} options";
            ModeText = $"Using {mode} mode defaults";
            InitializeComponent();
            ConfigurableItems.AddRange(mf.ChoiceFiles);
            ConfigurableItems.AddRange(mf.CopyFiles.Where(s => s.Optional));
            ConfigurableItems.AddRange(mf.ZipFiles.Where(s => s.Optional));
        }

        private void LoadCommands()
        {
            OpenComparisonsPageCommand = new GenericCommand(() => Utilities.OpenWebPage(ConfiguringFile.ComparisonsLink));
            AbortInstallCommand = new GenericCommand(AbortInstall);
            InstallWithOptionsCommand = new GenericCommand(InstallWithOptions);
        }

        public GenericCommand OpenComparisonsPageCommand { get; set; }

        private void InstallWithOptions()
        {
            closeDialogWithResult(true);
        }

        public GenericCommand InstallWithOptionsCommand { get; set; }

        private void AbortInstall()
        {
            closeDialogWithResult(false);
        }

        public GenericCommand AbortInstallCommand { get; set; }


        //private void Combobox_DropdownClosed(object sender, EventArgs e)
        //{
        //    if (sender is ComboBox)
        //    {
        //        ComboBox cb = (ComboBox)sender;
        //        ConfigurableModInterface choicefile = (ConfigurableModInterface)cb.DataContext;
        //        choicefile.SelectedIndex = cb.SelectedIndex;
        //    }
        //}

        private void Comparisons_Click(object sender, RoutedEventArgs e)
        {
            Utilities.OpenWebPage(ConfiguringFile.ComparisonsLink);
        }
    }
}
