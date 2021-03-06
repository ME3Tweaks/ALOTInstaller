﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls.Dialogs;
using Notifications.Wpf.Core;
using Serilog;
using Serilog.Sinks.File;
using Application = System.Windows.Application;
using ZipFile = System.IO.Compression.ZipFile;

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

        private static bool _showOptionaFiles = true;

        public static bool ShowOptionalFiles
        {
            get => _showOptionaFiles;
            set
            {
                if (SetProperty(ref _showOptionaFiles, value))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FSUIC.DisplayedFilesView.Refresh();
                    });
                }
            }
        }

        private static bool _showNonReadyFiles = true;
        public static bool ShowNonReadyFiles
        {
            get => _showNonReadyFiles;
            set
            {
                if (SetProperty(ref _showNonReadyFiles, value))
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

        public string BackgroundTaskText { get; set; }

        public void OnBackgroundTaskTextChanged()
        {
            ProgressIndeterminate = BackgroundTaskText != null;
            if (BackgroundTaskText == null)
            {
                // Trigger update.
                manifestFileReadyStateChanged(null);
            }
        }

        public bool IsStaging { get; set; }

        public void OnIsStagingChanged()
        {
            ShowNonReadyFiles = !IsStaging;
            if (!IsStaging)
            {
                ProgressIndeterminate = false;
                TextureLibrary.SetupLibraryWatcher(ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode).OfType<ManifestFile>().ToList(), manifestFileReadyStateChanged);
                ShownSpecificFileSet = null;
            }
            else
            {
                TextureLibrary.StopLibraryWatcher();
            }
            DisplayedFilesView.Refresh();
        }
        public string StagingStatusText { get; set; }

        public ModeHeader SelectedHeader { get; set; }
        public ObservableCollectionExtended<ModeHeader> AvailableModes { get; } = new ObservableCollectionExtended<ModeHeader>();
        public string AppTopText { get; set; } =
            "Add files to install by dragging and dropping their files onto the interface. Make sure you do not extract or rename any files you download, or the installer will not recognize them.";
        public ObservableCollectionExtended<InstallerFile> CurrentModeFiles { get; } = new ObservableCollectionExtended<InstallerFile>();
        public ICollectionView DisplayedFilesView { get; }

        public FileSelectionUIController()
        {
            DisplayedFilesView = CollectionViewSource.GetDefaultView(CurrentModeFiles);

            DataContext = this;
            FSUIC = this;
            LoadCommands();
            InitializeComponent();
            ShowME1Files = Locations.ME1Target != null;
            ShowME2Files = Locations.ME2Target != null;
            ShowME3Files = Locations.ME3Target != null;


            AvailableModes.AddRange(ManifestHandler.MasterManifest.ManifestModePackageMappping.Select(x => new ModeHeader(x.Key, getModeDirections(x.Key), getModeDescription(x.Key))));

            OnManifestModeChanged(ManifestHandler.CurrentMode);
            ManifestHandler.OnManifestModeChanged = OnManifestModeChanged; //Setup change subscription

            //Group items by Author
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Author");
            DisplayedFilesView.GroupDescriptions.Add(groupDescription);
            manifestFileReadyStateChanged(null); //Trigger progressbar update

            // Add filtering
            DisplayedFilesView.Filter = FilterShownFilesByGame;

            startPostStartup();


        }

        private void startPostStartup()
        {
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("PostStartup");
            nbw.DoWork += async (sender, args) =>
            {
                try
                {
                    if (ManifestHandler.MasterManifest == null || ManifestHandler.MasterManifest.MusicPackMirrors.Count == 0)
                    {
                        // Nothing wae can do.
                        return;
                    }
                    string me1ogg = Path.Combine(Locations.MusicDirectory, "me1.mp3");
                    string me2ogg = Path.Combine(Locations.MusicDirectory, "me2.mp3");
                    string me3ogg = Path.Combine(Locations.MusicDirectory, "me3.mp3");

                    if (!File.Exists(me1ogg) || !File.Exists(me2ogg) || !File.Exists(me3ogg))
                    {
                        BackgroundTaskText = "Downloading installer music pack";
                        bool writtenFile = false;
                        string outpath = null;
                        foreach (var v in ManifestHandler.MasterManifest.MusicPackMirrors)
                        {
                            outpath = Path.Combine(Locations.TempDirectory(), "MusicPack" + Path.GetExtension(v.URL));
                            var memoryItem = await OnlineContent.DownloadToMemory(v.URL, (done, total) =>
                            {
                                ProgressIndeterminate = false;
                                ProgressMax = total;
                                ProgressValue = done;
                            }, hash: v.Hash);
                            if (memoryItem.errorMessage == null)
                            {
                                memoryItem.result.WriteToFile(outpath);
                                writtenFile = true;
                                break;
                            }
                        }

                        if (!writtenFile) return;
                        ProgressIndeterminate = true;
                        BackgroundTaskText = "Extracting music pack";
                        MEMIPCHandler.RunMEMIPCUntilExit($"--unpack-archive --input \"{outpath}\" --output \"{Locations.MusicDirectory}\" --ipc");
                        File.Delete(outpath);
                        BackgroundTaskText = null;
                    }

                }
                catch (Exception e)
                {
                    Log.Error($"Error fetching music pack: {e.Message}");
                }
            };
            nbw.RunWorkerAsync();
        }

        /// <summary>
        /// List of files that will be shown in the list. Set to null to allow normal filtering
        /// </summary>
        internal List<InstallerFile> ShownSpecificFileSet;

        private bool FilterShownFilesByGame(object obj)
        {
            if (obj is InstallerFile ifx)
            {
                if (ShownSpecificFileSet != null) return ShownSpecificFileSet.Contains(ifx); //Show only files in the specifically set UI list
                if (!ShowNonReadyFiles && (!ifx.Ready || ifx.Disabled)) return false; // Show only ready to install files
                if (!ShowOptionalFiles && ifx is ManifestFile mf && mf.Recommendation == RecommendationType.Optional) return false; //Optional file not shown.
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
        public RelayCommand OpenModWebpageCommand { get; set; }
        public RelayCommand OpenFileOnDiskCommand { get; set; }
        public GenericCommand ImportAssistantCommand { get; set; }

        private void LoadCommands()
        {
            OpenModWebpageCommand = new RelayCommand(OpenModWebpage, CanOpenWebpage);
            OpenFileOnDiskCommand = new RelayCommand(OpenFileOnDisk, CanOpenFileOnDisk);
            OpenSettingsCommand = new GenericCommand(OpenSettings, CanOpenSettings);
            InstallTexturesCommand = new GenericCommand(BeginInstallTextures, CanInstallTextures);
            ImportAssistantCommand = new GenericCommand(OpenImportAssistant, () => !IsStaging);
        }

        private bool CanOpenWebpage(object obj) => obj is ManifestFile mf && mf.DownloadLink != null;

        private void OpenImportAssistant()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FileImporterFlyoutContent.CurrentDisplayMode = FileImporterFlyout.EFIDisplayMode.ManuallyOpenedView;
                mw.FileImporterOpen = true;
            }
        }

        private bool CanOpenFileOnDisk(object obj) => !IsStaging && obj is InstallerFile ifx && File.Exists(ifx.GetUsedFilepath());

        private void OpenFileOnDisk(object obj)
        {
            if (obj is InstallerFile ifx && File.Exists(ifx.GetUsedFilepath()))
            {
                Utilities.OpenAndSelectFileInExplorer(ifx.GetUsedFilepath());
            }
        }

        private void OpenModWebpage(object obj)
        {
            if (obj is ManifestFile mf)
            {
                Utilities.OpenWebPage(mf.DownloadLink);
            }
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
            return (Locations.ME1Target != null || Locations.ME2Target != null || Locations.ME3Target != null) && CurrentModeFiles.Any(x => x.Ready && !x.Disabled); //a game must be installed
        }

        private async void BeginInstallTextures()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                //InstallerUIController iuic = new InstallerUIController();
                //mw.OpenInstallerUI(iuic, InstallerUIController.GetInstallerBackgroundImage(MEGame.ME1, ManifestHandler.CurrentMode));
                //return;
                var buttons = new List<Button>();
                var mp = ManifestHandler.MasterManifest.ManifestModePackageMappping[ManifestHandler.CurrentMode];
                var targets = Locations.GetAllAvailableTargets().Where(
                    g => mp.UserFiles.Any(x => x.ApplicableGames.HasFlag(g.Game.ToApplicableGame())) ||
                         mp.ManifestFiles.Any(x => x.ApplicableGames.HasFlag(g.Game.ToApplicableGame()))).ToList();

                if (!targets.Any())
                {
                    // There are no targets that can be installed to
                    await mw.ShowMessageAsync("No files are ready for any installed game",
                        "There are no files that are ready to install to any currently installed games. Add files that can be installed to a game you current have installed and try again.");
                    return;
                }

                List<MEGame> availableGames = new List<MEGame>();
                foreach (var game in targets)
                {
                    {
                        Image image = null;
                        try
                        {
                            image = new Image()
                            {
                                Height = 45,
                                Source = new BitmapImage(new Uri(
                                    $"pack://application:,,,/ALOTInstallerWPF;component/Images/logo_{game.Game.ToString().ToLower()}.png")),
                            };
                            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                        }
                        catch (Exception e)
                        {
                            // Really weird Windows 2004 issue
                            Log.Error($"[AIWPF] Error loading image for game selection: {e.Message}");
                        }

                        buttons.Add(new Button()
                        {
                            ToolTip = game.TargetPath,
                            Margin = new Thickness(5),
                            Content = image != null ? (object)image : (object)game.Game.ToString(),
                            Padding = new Thickness(4),
                            Style = (Style)FindResource("MahApps.Styles.Button.Square.Accent")
                        });
                        availableGames.Add(game.Game);
                    }
                }

                buttons.Add(new Button()
                {
                    Height = 50,
                    Content = "Abort install"
                });
                int chosenOption = -1;

                if (availableGames.Count > 1)
                {

                    chosenOption = await mw.GetFlyoutResponse("Select which game to install textures for",
                        buttons.ToArray());
                    if (chosenOption == buttons.Count - 1)
                    {
                        // ABORT
                        return;
                    }
                }
                else
                {
                    chosenOption = 0;
                }
                // USER CHOSE OPTION OR ONLY WAS ONE OPTION
                mw.ShowBottomDialog(new InstallOptionsFlyout(targets[chosenOption],
                    CurrentModeFiles.OfType<UserFile>().ToList()));
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
                    return $"Install MEUITM using MEUITM mode defaults. Can also install user files.\nManifest version: {manifestVersion}";
                case ManifestMode.ALOT:
                    return
                        $"Install ALOT and MEUITM (if applicable) with ALOT defaults. Can also install user files. This is the default mode.\nManifest version: {manifestVersion}";
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
            var newFileSet = ManifestHandler.GetManifestFilesForMode(newMode, true);
            using (DisplayedFilesView.DeferRefresh())
            {
                CurrentModeFiles.ReplaceAll(newFileSet);
            }

            TextureLibrary.SetupLibraryWatcher(newFileSet.OfType<ManifestFile>().ToList(), manifestFileReadyStateChanged);
            UpdateReadyness();
        }

        public void OnSelectedHeaderChanged()
        {
            ManifestHandler.SetCurrentMode(SelectedHeader.Mode);
        }

        private void manifestFileReadyStateChanged(ManifestFile changedManifestFile)
        {
            UpdateReadyness();
        }

        private void UpdateReadyness()
        {
            var readyness = ManifestHandler.GetNonOptionalReadyness();
            ProgressValue = readyness.ready;
            ProgressMax = Math.Max(1, readyness.recommendedCount);
        }

        public bool ProgressIndeterminate { get; set; }
        public long ProgressMax { get; set; }
        public long ProgressValue { get; set; }
        public MEGame StagingGame { get; set; }

        /// <summary>
        /// Gets if this is a file or directory. Returns null if path doesn't exist. False if it's a file. True if it's a directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool? getPathType(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return null;
            FileAttributes attr = File.GetAttributes(path);

            //detect whether its a directory or file
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                return true;
            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private async void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Source is Hyperlink hl && hl.Tag is ManifestFile mf)
            {
                try
                {
                    Log.Information("Opening URL: " + e.Uri.ToString());
                    Utilities.OpenWebPage(e.Uri.ToString());
                    var message = $"Download the file titled '{mf.Tooltipname}'.\n\nDo not extract the file. Drag the download onto ALOT Installer to import it or use Import Assistant.";
                    var messageType = NotificationType.Information;
                    if (mf.ExtraInstructions != null)
                    {
                        message += $"\n\n{mf.ExtraInstructions}";
                        messageType = NotificationType.Warning;
                    }
                    Toaster.ShowNotification("Directions", message, -1, messageType);
                }
                catch (Exception other)
                {
                    Log.Error("Exception opening browser - handled. The error was " + other.Message);
                    System.Windows.Clipboard.SetText(e.Uri.ToString());
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        await mw.ShowMessageAsync("Unable to open web browser",
                            "Unable to open your default web browser. Open your browser and paste the link (already copied to clipboard) into your URL bar. " +
                            $"Download the file named {mf.Tooltipname}, then drag and drop it onto this program's interface.");

                    }
                }
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            e.Handled = true; //we handle all drag drops.

            if (IsStaging)
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                {
                    // We support multi drop. I'm not going to bother checking all the extensions, the importer will handle this
                    e.Effects = DragDropEffects.Copy;
                }
                else
                {
                    var f = files[0];
                    var pathType = getPathType(f);
                    if (pathType.HasValue)
                    {
                        if (pathType.Value)
                        {
                            // It's a directory.
                            // We support this for dropping, I guess, technically.
                            e.Effects = DragDropEffects.Copy;
                        }
                        else
                        {
                            // It's a file
                            if (!TextureLibrary.ImportableFileTypes.Contains(Path.GetExtension(f), StringComparer.InvariantCultureIgnoreCase))
                            {
                                // Not supported.
                                e.Effects = DragDropEffects.None;
                            }
                            else
                            {
                                // Supported
                                e.Effects = DragDropEffects.Copy;
                            }
                        }
                    }
                }
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            e.Handled = true; //we handle all drag drops.

            if (IsStaging)
            {
                // We don't allow drops in staging mode
                return;
            }
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1 && files.All(x =>
                    {
                        var type = getPathType(x);
                        return type != null && type.Value == false;
                    }))
                {
                    // We support multi drop. I'm not going to bother checking all the extensions, the importer will handle this
                    attemptImportFiles(files, ManifestHandler.CurrentMode == ManifestMode.Free ? (bool?)true : null);
                }
                else
                {
                    var f = files[0];
                    var pathType = getPathType(f);
                    if (pathType.HasValue)
                    {
                        if (pathType.Value)
                        {
                            // It's a directory.
                            // We support this for dropping, I guess, technically.
                            attemptImportFolder(f);
                        }
                        else
                        {
                            // It's a file

                            if (Path.GetFileName(f) == "InstallerSupportPackage.zip")
                            {
                                IngestSupportPackage(f);
                            }
                            else if (TextureLibrary.ImportableFileTypes.Contains(Path.GetExtension(f), StringComparer.InvariantCultureIgnoreCase))
                            {
                                // Not supported.
                                e.Effects = DragDropEffects.None;
                                attemptImportFiles(files, ManifestHandler.CurrentMode == ManifestMode.Free ? (bool?)true : null);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Installs an archive of supporting files that are typically downloaded
        /// </summary>
        /// <param name="packagePath"></param>
        private async void IngestSupportPackage(string packagePath)
        {
            Log.Information(@"[AIWPF] Installing support package for the installer");
            using ZipArchive za = ZipFile.OpenRead(packagePath);
            za.ExtractToDirectory(Locations.AppDataFolder(), true);
            if (Application.Current.MainWindow is MainWindow mw)
            {
                await mw.ShowMessageAsync("Support package installed",
                    "The support package has been installed. The installer should be restarted.");
            }
            //// MEM, manifest
            //za.Entries.FirstOrDefault(x => x.Name == "MassEffectModderNoGui.exe").ExtractToFile(Locations.MEMPath());
            //za.Entries.FirstOrDefault(x => x.Name == "manifest.xml").ExtractToFile(Locations.GetCachedManifestPath());

            //// Music pack
            //var me1Mp3Entry = za.Entries.FirstOrDefault(x => x.Name == "me1.mp3");
            //var me2Mp3Entry = za.Entries.FirstOrDefault(x => x.Name == "me2.mp3");
            //var me3Mp3Entry = za.Entries.FirstOrDefault(x => x.Name == "me3.mp3");
            //me1Mp3Entry?.ExtractToFile(Path.Combine(Locations.MusicDirectory, "me1.mp3"));
            //me2Mp3Entry?.ExtractToFile(Path.Combine(Locations.MusicDirectory, "me2.mp3"));
            //me3Mp3Entry?.ExtractToFile(Path.Combine(Locations.MusicDirectory, "me3.mp3"));

        }

        private void attemptImportFolder(string folderPath)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.OpenFileImporterFolders(folderPath);
            }
        }

        private void attemptImportFiles(string[] files, bool? userFileMode)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.OpenFileImporterFiles(files, userFileMode);
            }
        }
    }
}
