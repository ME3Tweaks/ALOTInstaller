using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using ALOTInstallerCore.Objects.Manifest;
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

        public ObservableCollectionExtended<ManifestFile> ImportResults { get; } = new ObservableCollectionExtended<ManifestFile>();    

        public EFIDisplayMode CurrentDisplayMode { get; set; }
        //public bool IsUserFile { get; set; }
        //public bool UserFilesUsable { get; set; }
        //public bool Importing { get; set; }
        public string ImportStatusText { get; set; }
        public bool ProgressIndeterminate { get; set; }
        public long ProgressValue { get; set; }
        public long ProgressMax { get; set; }
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

        private void LoadCommands()
        {
            ImportManifestFilesCommand = new GenericCommand(ImportManifestFiles, HasAnyMissingManifestFiles);
            ImportManifestFolderCommand = new GenericCommand(ImportManifestFolder, HasAnyMissingManifestFiles);
            ImportManifestFromDownloadsCommand = new GenericCommand(ImportManifestFilesFromDownloads, HasAnyMissingManifestFiles);
            CloseFlyoutCommand = new GenericCommand(closeFlyout);
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
                handleOpenFiles(ofd.FileNames);
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

        public void handleOpenFiles(string[] files)
        {

            internalHandleFiles(files);
        }

        private void internalHandleFiles(string[] files)
        {
            CurrentDisplayMode = EFIDisplayMode.ImportingView;
            ProgressIndeterminate = true;
            ImportStatusText = "Checking file(s)...";
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("ImportChecker");
            nbw.DoWork += (sender, args) =>
            {
                foreach (var v in files)
                {
                    //TextureLibrary.ImportFromFolder();

                }
            };
            nbw.RunWorkerAsync();
        }

        public void handleOpenFolder(string folderPath)
        {
            CurrentDisplayMode = EFIDisplayMode.ImportingView;
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
                        CurrentDisplayMode = EFIDisplayMode.ImportResultsView;
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
