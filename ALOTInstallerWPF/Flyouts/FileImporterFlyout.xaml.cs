using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerWPF.BuilderUI;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for FileImporterFlyout.xaml
    /// </summary>
    public partial class FileImporterFlyout : UserControl, INotifyPropertyChanged
    {
        public enum EFIDisplayMode
        {
            ManuallyOpenedView,
            ImportingView,
            UserFileSelectGameView,
            BadUserFileView,
            ImportResultsView
        }

        public ObservableCollectionExtended<object> ImportResults { get; } = new ObservableCollectionExtended<object>();

        public EFIDisplayMode CurrentDisplayMode { get; set; }
        //public bool IsUserFile { get; set; }
        //public bool UserFilesUsable { get; set; }
        //public bool Importing { get; set; }
        public string ImportStatusText { get; set; }
        public bool ProgressIndeterminate { get; set; }
        public long ProgressValue { get; set; }
        public long ProgressMax { get; set; }
        public string CurrentUserFileName { get; set; }

        public FileImporterFlyout()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public GenericCommand CloseFlyoutCommand { get; set; }
        public GenericCommand ImportManifestFolderCommand { get; set; }
        public GenericCommand ImportManifestFilesCommand { get; set; }
        public GenericCommand ImportManifestFromDownloadsCommand { get; set; }
        public GenericCommand AddUserFolderCommand { get; set; }
        public GenericCommand AddUserFilesCommand { get; set; }

        /// <summary>
        /// This command is set by the importer for each file 
        /// </summary>
        public RelayCommand GameSelectionCommand { get; set; }
        private void LoadCommands()
        {
            ImportManifestFilesCommand = new GenericCommand(ImportManifestFiles, HasAnyMissingManifestFiles);
            ImportManifestFolderCommand = new GenericCommand(ImportManifestFolder, HasAnyMissingManifestFiles);
            ImportManifestFromDownloadsCommand = new GenericCommand(ImportManifestFilesFromDownloads, HasAnyMissingManifestFiles);
            CloseFlyoutCommand = new GenericCommand(closeFlyout);
            AddUserFilesCommand = new GenericCommand(AddUserFiles);
            AddUserFolderCommand = new GenericCommand(AddUserFilesFolder);
        }

        private void AddUserFilesFolder()
        {
            CommonOpenFileDialog ofd = new CommonOpenFileDialog()
            {
                Title = "Select folder containing user files",
                IsFolderPicker = true,
                EnsurePathExists = true
            };
            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (ofd.FileName != Settings.TextureLibraryLocation)
                {
                    handleOpenFolder(ofd.FileName);
                }
                else
                {
                    // Show user message?
                }
            }
        }

        private void AddUserFiles()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Select user file to add",
                Filter =
                    $"Supported file types|{string.Join(';', TextureLibrary.ImportableFileTypes.Select(x => $"*{x}"))}",
                Multiselect = true,
                CheckPathExists = true
            };
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                handleOpenFiles(ofd.FileNames, true);
            }
        }



        private void ImportManifestFilesFromDownloads()
        {
            var downloadsFolder = KnownFolders.GetPath(KnownFolder.Downloads);
            if (Directory.Exists(downloadsFolder))
            {
                handleOpenFolder(downloadsFolder);
            }
        }


        private void ImportManifestFiles()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Title = "Select manifest files to import",
                Filter =
                    $"Supported file types|{string.Join(';', TextureLibrary.ImportableFileTypes.Select(x => $"*{x}"))}",
                Multiselect = true,
                CheckPathExists = true
            };
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                handleOpenFiles(ofd.FileNames, false);
            }
        }

        private void ImportManifestFolder()
        {
            // Select folder
            CommonOpenFileDialog ofd = new CommonOpenFileDialog()
            {
                Title = "Select folder containing downloaded manifest files",
                IsFolderPicker = true,
                EnsurePathExists = true
            };
            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (ofd.FileName != Settings.TextureLibraryLocation)
                {
                    handleOpenFolder(ofd.FileName);
                }
                else
                {
                    // Show user message?
                }
            }
        }


        private bool HasAnyMissingManifestFiles() => ManifestHandler.GetAllManifestFiles().Any(x => !x.Ready);


        private void closeFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FileImporterOpen = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void handleOpenFiles(string[] files, bool? userFileMode)
        {
            internalHandleFiles(files, userFileMode);
        }

        private void internalHandleFiles(string[] files, bool? userFileMode)
        {
            CurrentDisplayMode = EFIDisplayMode.ImportingView;
            ProgressIndeterminate = true;
            ImportStatusText = "Checking file(s)...";
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportChecker");
            nbw.DoWork += (sender, args) =>
            {
                args.Result = TextureLibrary.IngestFiles(files, userFileMode,
                    x => ImportStatusText = x,
                    (file, done, total) =>
                    {
                        ProgressMax = total;
                        ProgressValue = done;
                        ProgressIndeterminate = false;
                        ImportStatusText = $"Importing {file}";
                    },
                x =>
                {
                    var syncObj = new object();
                    CurrentDisplayMode = EFIDisplayMode.UserFileSelectGameView;
                    CurrentUserFileName = Path.GetFileName(x);
                    ApplicableGame selectedGame = ApplicableGame.None;
                    void selectedGameCallback(object o)
                    {
                        if (o is string str && Enum.TryParse<ApplicableGame>(str, out var sg))
                        {
                            selectedGame = sg;
                            lock (syncObj)
                            {
                                Monitor.Pulse(syncObj);
                            }
                        }
                    }
                    GameSelectionCommand = new RelayCommand(selectedGameCallback);
                    lock (syncObj)
                    {
                        Monitor.Wait(syncObj);
                    }

                    return selectedGame;
                },
                    instF =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            FileSelectionUIController.FSUIC.CurrentModeFiles.Add(instF);
                        });
                    });
            };

            nbw.RunWorkerCompleted += (sender, args) =>
            {
                if (args.Error == null)
                {
                    if (args.Result is List<ImportResult> results)
                    {
                        CurrentDisplayMode = EFIDisplayMode.ImportResultsView;
                        ResultsText = "Results of importing/adding files:";
                        ImportResults.ReplaceAll(results);
                    }
                }
            };

            nbw.RunWorkerAsync();
        }


        public void handleOpenFolder(string folderPath)
        {
            CurrentDisplayMode = FileImporterFlyout.EFIDisplayMode.ImportingView;
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportCheckerFolder");
            nbw.DoWork += (sender, args) =>
            {
                List<ManifestFile> importedManifestFiles = null;
                TextureLibrary.ImportFromFolder(folderPath, ManifestHandler.GetAllManifestFiles(),
                    (file, done, total) =>
                    {
                        ProgressMax = total;
                        ProgressValue = done;
                        ImportStatusText = $"Importing {file}";
                    },
                    filesDone => importedManifestFiles = filesDone
                    );
                args.Result = importedManifestFiles;
            };
            nbw.RunWorkerCompleted += (sender, args) =>
            {
                if (args.Error == null)
                {
                    if (args.Result is List<ManifestFile> importedFiles)
                    {
                        CurrentDisplayMode = FileImporterFlyout.EFIDisplayMode.ImportResultsView;
                        ResultsText = importedFiles.Any()
                            ? "The following manifest files were imported:"
                            : $"No manifest files were found in {folderPath}";
                        ImportResults.ReplaceAll(importedFiles);
                    }
                }
            };
            nbw.RunWorkerAsync();
        }

        public string ResultsText { get; set; }
    }
}
