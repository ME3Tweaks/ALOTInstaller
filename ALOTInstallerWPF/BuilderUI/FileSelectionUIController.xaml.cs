using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using AlotAddOnGUI;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.InstallerUI;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Controls.Dialogs;
using Application = System.Windows.Application;

namespace ALOTInstallerWPF.BuilderUI
{
    /// <summary>
    /// Interaction logic for FileSelectionUIController.xaml
    /// </summary>
    public partial class FileSelectionUIController : UserControl, INotifyPropertyChanged
    {

        #region Static Property Changed

        private static bool Loaded = false;
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        #endregion

        private static bool _showME1Files = true;
        private static bool _showME2Files = true;
        private static bool _showME3Files = true;
        public static bool ShowME1Files
        {
            get => _showME1Files;
            set
            {
                if (SetProperty(ref _showME1Files, value))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FSUIC.DisplayedFilesView.Refresh();
                    });
                }
            }
        }
        public static bool ShowME2Files
        {
            get => _showME2Files;
            set
            {
                if (SetProperty(ref _showME2Files, value))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FSUIC.DisplayedFilesView.Refresh();
                    });
                }
            }
        }
        public static bool ShowME3Files
        {
            get => _showME3Files;
            set
            {
                if (SetProperty(ref _showME3Files, value))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FSUIC.DisplayedFilesView.Refresh();
                    });
                }
            }
        }

        /// <summary>
        /// Way for the filter static props to access the current instance of this
        /// </summary>
        internal static FileSelectionUIController FSUIC;

        public bool IsStaging { get; set; }

        public void OnIsStagingChanged()
        {
            DisplayedFilesView.Refresh();
            if (!IsStaging)
            {
                TextureLibrary.SetupLibraryWatcher(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode).OfType<ManifestFile>().ToList(), manifestFileReadyStateChanged);
            }
            else
            {
                TextureLibrary.StopLibraryWatcher();
            }
        }
        public string StagingStatusText { get; set; }

        public ModeHeader SelectedHeader { get; set; }
        public ObservableCollectionExtended<ModeHeader> AvailableModes { get; } = new ObservableCollectionExtended<ModeHeader>();
        public string AppTopText { get; set; } =
            "Add files to install by dragging and dropping their files onto the interface. Make sure you do not extract or rename any files you download, or the installer will not recognize them.";
        public ObservableCollectionExtended<InstallerFile> CurrentModeFiles { get; } = new ObservableCollectionExtended<InstallerFile>();
        public ICollectionView DisplayedFilesView => CollectionViewSource.GetDefaultView(CurrentModeFiles);
        public FileSelectionUIController()
        {
            DataContext = this;
            FSUIC = this;
            LoadCommands();
            InitializeComponent();
            AvailableModes.AddRange(ManifestHandler.MasterManifest.ManifestModePackageMappping.Select(x => new ModeHeader(x.Key, getModeDirections(x.Key), getModeDescription(x.Key))));
            OnManifestModeChanged(ManifestHandler.CurrentMode);
            ManifestHandler.OnManifestModeChanged = OnManifestModeChanged; //Setup change subscription

            //Group items by Author
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            DisplayedFilesView.GroupDescriptions.Add(groupDescription);
            manifestFileReadyStateChanged(null); //Trigger progressbar update

            // Add filtering
            DisplayedFilesView.Filter = FilterShownFilesByGame;
        }

        private bool FilterShownFilesByGame(object obj)
        {
            if (obj is InstallerFile ifx)
            {
                if (IsStaging && !ifx.Ready) return false; // Show only ready files
                if (ifx.ApplicableGames.HasFlag(ApplicableGame.ME1) && ShowME1Files) return true;
                if (ifx.ApplicableGames.HasFlag(ApplicableGame.ME2) && ShowME2Files) return true;
                if (ifx.ApplicableGames.HasFlag(ApplicableGame.ME3) && ShowME3Files) return true;
                return false;
            }
            // Default to true so I don't get angry debugging missing items
            return true;
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
            return !IsStaging;
        }

        private async void OpenSettings()
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
                //InstallerUIController iuic = new InstallerUIController();
                //mw.OpenInstallerUI(iuic, InstallerUIController.GetInstallerBackgroundImage(Enums.MEGame.ME1, ManifestHandler.CurrentMode));
                //return;
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
                    mw.ShowBottomDialog(new InstallOptionsFlyout(targets[chosenOption], CurrentModeFiles.OfType<UserFile>().ToList()));
                }
            }
        }

        private string getModeDescription(ManifestMode argKey)
        {
            var manifestVersion = ManifestHandler.MasterManifest.ManifestModePackageMappping[argKey].ManifestVersion;
            switch (argKey)
            {
                case ManifestMode.Free:
                    return
                        "Install whatever you want. Has no file requirements, however files will not be specifically parsed like in other modes";
                case ManifestMode.MEUITM:
                    return $"Install MEUITM using MEUITM mode defaults. Can also install user files\n\nManifest version: {manifestVersion}";
                case ManifestMode.ALOT:
                    return
                        $"Install ALOT and MEUITM (if applicable) with ALOT defaults. Can also install user files. This is the default mode\n\nManifest version: {manifestVersion}";
                default:
                    return null;
            }
        }

        private string getModeDirections(ManifestMode argKey)
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

        public bool ProgressIndeterminate { get; set; }
        public long ProgressMax { get; set; }
        public long ProgressValue { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Utilities.OpenWebPage(e.Target);
        }
    }
}
