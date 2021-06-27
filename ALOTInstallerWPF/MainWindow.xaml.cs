using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.BuilderUI;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.InstallerUI;
using ALOTInstallerWPF.Objects;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls;
using Octokit;

namespace ALOTInstallerWPF
{
    /// <summary>
    /// Main window for ALOT Installer
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        /// <summary>
        /// Sets the open/close status of the settings panel
        /// </summary>
        public bool SettingsOpen { get; set; }
        public bool DiagnosticsOpen { get; set; }
        public bool FileImporterOpen { get; set; }
        public bool LODSwitcherOpen { get; set; }

        #region CONTENTS (so we can load these after startup)
        public FileImporterFlyout FileImporterFlyoutContent { get; internal set; }
        public SettingsFlyout SettingsFlyoutContent { get; set; }

        internal LODSwitcherFlyout LODSwitcherFlyoutContent;
        #endregion

        private void OnSettingsOpenChanged()
        {
            if (SettingsOpen)
            {
                // Opening
                // Update the status of textures
                SettingsFlyoutContent.UpdateGameStatuses();
            }
        }

        public void OnLODSwitcherOpenChanged()
        {
            if (LODSwitcherOpen)
            {
                LODSwitcherFlyoutContent.UpdateGameStatuses();
            }
        }

        public MainWindow()
        {
            DataContext = this;
            Title = $"ALOT Installer {Utilities.GetAppVersion()}";
            InitializeComponent();
        }


        private void OnFileImporterFlyoutContentChanged()
        {
            FileImporterFlyoutControl.Content = FileImporterFlyoutContent;
        }

        private void MainWindow_OnContentRendered(object? sender, EventArgs e)
        {
            StartupUIController.BeginFlow(this);
        }

        /// <summary>
        /// Opens the botttom flyout with the specified buttons and top text and returns the index of the selected button
        /// </summary>
        /// <param name="topText"></param>
        /// <param name="buttons"></param>
        /// <returns></returns>
        public Task<int> GetFlyoutResponse(string topText, params Button[] buttons)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            var content = new FlyoutDialogPanel(topText, buttons, selectedOption =>
            {
                // If user clicks too fast this can cause an exception.
                try
                {
                    tcs.SetResult(selectedOption);
                }
                catch
                {
                }

                BottomBasicDialog.IsOpen = false;
            });
            BottomBasicDialog.Content = null; //clear
            BottomBasicDialog.Content = content;
            BottomBasicDialog.IsOpen = true;
            return tcs.Task;
        }

        public void ShowBottomDialog(FlyoutController control)
        {
            control.CloseFlyout = () => BottomBasicDialog2.IsOpen = false;
            BottomBasicDialog2.Content = control;
            BottomBasicDialog2.IsOpen = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void CloseFlyout2()
        {
            BottomBasicDialog2.IsOpen = false;
        }

        private void InstallingOverlayFlyout_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Allow installing UI overlay to be window drag
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void InstallingOverlayoutFlyout_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double click changes maximize status
            this.WindowState =
                this.WindowState ==
                System.Windows.WindowState.Normal ?
                    System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
        }

        public void OpenInstallerUI(InstallerUIController controller, ImageBrush background, bool isOpeningDebug = false)
        {
            BorderThickness = new Thickness(0);
            InstallingOverlayFlyout.Content = null; //Lose the old reference
            InstallingOverlayFlyout.Content = controller;
            InstallingOverlayFlyout.Background = background;
            InstallingOverlayFlyout.IsOpen = true;
            controller.StartInstall(isOpeningDebug);
        }

        public void OpenOriginFlyout(MEGame game)
        {
            var content = new OriginNoUpdateFlyout(game);
            OriginFlyout.Content = content;
            OriginFlyout.IsOpen = true;
        }

        public void CloseOriginFlyoutUI()
        {
            OriginFlyout.IsOpen = false;
            CommonUtil.Run(() =>
            {
                OriginFlyout.Content = null; //Remove this so it doesn't keep running. GC will remove it
            }, TimeSpan.FromSeconds(3));
        }


        public void CloseInstallerUI()
        {
            BorderThickness = new Thickness(1);
            InstallingOverlayFlyout.IsOpen = false;
            FileSelectionUIController.FSUIC.IsStaging = false;
            CommonUtil.Run(() =>
            {
                foreach (var v in ManifestHandler.GetAllManifestFiles())
                {
                    if (v.MEUITMSettings != null)
                    {
                        v.MEUITMSettings.BackgroundImageBytes = null; //Clean this out of memory
                    }
                }
                InstallingOverlayFlyout.Content = null; //Remove this so it doesn't keep running. GC will remove it
            }, TimeSpan.FromSeconds(3));
        }

        public void OpenFileImporterFolders(string folderPath)
        {
            FileImporterFlyoutContent.handleOpenFolder(folderPath);
            FileImporterOpen = true;
        }

        public void OpenFileImporterFiles(string[] files, bool? userFileMode)
        {
            FileImporterFlyoutContent.handleOpenFiles(files, userFileMode);
            FileImporterOpen = true;
        }
    }
}
