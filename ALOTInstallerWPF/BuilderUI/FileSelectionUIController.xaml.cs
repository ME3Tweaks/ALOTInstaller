using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Objects;
using Octokit;
using Application = System.Windows.Application;

namespace ALOTInstallerWPF.BuilderUI
{
    /// <summary>
    /// Interaction logic for FileSelectionUIController.xaml
    /// </summary>
    public partial class FileSelectionUIController : UserControl, INotifyPropertyChanged
    {
        public ModeHeader SelectedHeader { get; set; }
        public ObservableCollectionExtended<ModeHeader> AvailableModes { get; } = new ObservableCollectionExtended<ModeHeader>();
        public string AppTopText { get; set; } =
            "Add files to install by dragging and dropping their files onto the interface. Make sure you do not extract or rename any files you download, or the installer will not recognize them.";
        public ObservableCollectionExtended<InstallerFile> CurrentModeFiles { get; } = new ObservableCollectionExtended<InstallerFile>();
        public ICollectionView DisplayedFilesView => CollectionViewSource.GetDefaultView(CurrentModeFiles);

        public FileSelectionUIController()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            AvailableModes.AddRange(ManifestHandler.MasterManifest.ManifestModePackageMappping.Select(x => new ModeHeader(x.Key, getModeDescription(x.Key))));
            OnManifestModeChanged(ManifestHandler.CurrentMode);
            ManifestHandler.OnManifestModeChanged = OnManifestModeChanged; //Setup change subscription

            //Group items by Author
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            DisplayedFilesView.GroupDescriptions.Add(groupDescription);
            manifestFileReadyStateChanged(null); //Trigger progerssbar update
        }

        #region COMMANDS

        public ICommand InstallTexturesCommand { get; set; }
        public ICommand OpenSettingsCommand { get; set; }

        private void LoadCommands()
        {
            OpenSettingsCommand = new GenericCommand(OpenSettings, CanOpenSettings);
            InstallTexturesCommand = new GenericCommand(BeginInstallTextures, CanInstallTextures);
        }

        private bool CanOpenSettings()
        {
            return true;
        }

        private void OpenSettings()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.SettingsOpen = true;
            }
        }

        #endregion

        private bool CanInstallTextures()
        {
            return CurrentModeFiles.Any(x => x.Ready);
        }

        private async void BeginInstallTextures()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var buttons = new List<Button>();
                var targets = Locations.GetAllAvailableTargets();
                foreach (var game in targets)
                {
                    var image = new Image()
                    {
                        Height = 45,
                        Source = new BitmapImage(new Uri(
                            $"pack://application:,,,/ALOTInstallerWPF;component/Images/logo_{game.Game.ToString().ToLower()}.png")),
                    };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                    buttons.Add(new Button()
                    {
                        ToolTip = game.TargetPath,
                        Margin = new Thickness(5),
                        Content = image,
                        Padding = new Thickness(4),
                        Style = (Style)FindResource("MahApps.Styles.Button.Square.Accent")
                    });
                }

                buttons.Add(new Button()
                {
                    Height = 50,
                    Content = "Abort install"
                });

                var chosenOption = await mw.GetFlyoutResponse("Select which game to install textures for", buttons.ToArray());
                if (chosenOption == buttons.Count - 1)
                {
                    // ABORT
                }
                else
                {
                    // USER CHOSE OPTION
                    GameTarget selectedTarget = targets[chosenOption];

                }
            }
        }


        private string getModeDescription(ManifestMode argKey)
        {
            switch (argKey)
            {
                case ManifestMode.Free:
                    return
                        "Drag and drop files to install onto this window, then select Install Textures to begin installation";
                case ManifestMode.MEUITM:
                    return
                        "Drag and drop files to install onto this window, then select Install Textures to begin installation. Do not extract or modify any files you download or the installer will not properly recognize them as files for this mode.";
                case ManifestMode.ALOT:
                    return
                        "Drag and drop files to install onto this window, then select Install Textures to begin installation. Do not extract or modify any files you download or the installer will not properly recognize them as files for this mode.";
                default:
                    return null;
            }
        }

        private void OnManifestModeChanged(ManifestMode newMode)
        {
            SelectedHeader = AvailableModes.FirstOrDefault(x => x.Mode == newMode); //Change header. This won't retrigger this since mode should already be set
            var currentUserFiles = CurrentModeFiles.Where(x => x is UserFile);
            var newFileSet = ManifestHandler.GetManifestFilesForMode(newMode);
            CurrentModeFiles.ReplaceAll(newFileSet);
            CurrentModeFiles.AddRange(currentUserFiles);
            TextureLibrary.SetupLibraryWatcher(newFileSet.OfType<ManifestFile>().ToList(), manifestFileReadyStateChanged);
        }

        public void OnSelectedHeaderChanged()
        {
            ManifestHandler.SetCurrentMode(SelectedHeader.Mode);
        }

        private void manifestFileReadyStateChanged(ManifestFile changedManifestFile)
        {
            var readyness = ManifestHandler.GetNonOptionalReadyness();
            ProgressValue = readyness.ready;
            ProgressMax = Math.Max(1, readyness.recommendedCount);
        }

        public bool ProgresssIndeterminate { get; set; }
        public long ProgressMax { get; set; }
        public long ProgressValue { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Utilities.OpenWebPage(e.Target);
        }
    }
}
