using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using ALOTInstallerCore.Objects.Manifest;

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
            CurrentlyDisplayedFiles.ReplaceAll(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode, true));
            OnManifestModeChanged(ManifestHandler.CurrentMode);
            ManifestHandler.OnManifestModeChanged = OnManifestModeChanged; //Setup change subscription

            //Group items by Author
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(InstallerFilesListBox.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            view.GroupDescriptions.Add(groupDescription);
        }

        private void OnManifestModeChanged(ManifestMode newMode)
        {
            TextureLibrary.SetupLibraryWatcher(ManifestHandler.GetManifestFilesForMode(newMode).OfType<ManifestFile>().ToList(), manifestFileReadyStateChanged);
        }

        private void manifestFileReadyStateChanged(ManifestFile changedManifestFile)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DataContext = null;
                DataContext = this;
            });
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {

        }
    }
}
