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
using ALOTInstallerCore.Objects;

namespace ALOTInstallerWPF.BuilderUI
{
    /// <summary>
    /// Interaction logic for FileSelectionUIController.xaml
    /// </summary>
    public partial class FileSelectionUIController : UserControl, INotifyPropertyChanged
    {
        public ObservableCollectionExtended<InstallerFile> CurrentlyDisplayedFiles { get; } = new ObservableCollectionExtended<InstallerFile>();
        public FileSelectionUIController()
        {
            DataContext = this;
            InitializeComponent();
            CurrentlyDisplayedFiles.ReplaceAll(
                ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode, true));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
