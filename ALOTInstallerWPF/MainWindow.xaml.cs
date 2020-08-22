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
using ALOTInstallerWPF.BuilderUI;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.InstallerUI;
using ALOTInstallerWPF.Objects;
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

        private void OnSettingsOpenChanged()
        {
            if (SettingsOpen)
            {
                // Opening
                // Update the status of textures
                settingsFlyout.UpdateGameStatuses();
            }
        }
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
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
                tcs.SetResult(selectedOption);
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
            if (this.WindowState == System.Windows.WindowState.Normal)
            {
                this.WindowState = System.Windows.WindowState.Maximized;
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Normal;
            }
        }

        public void OpenInstallerUI(InstallerUIController controller, ImageBrush background)
        {
            InstallingOverlayFlyout.Content = null; //Lose the old reference
            InstallingOverlayFlyout.Content = controller;
            InstallingOverlayFlyout.Background = background;
            InstallingOverlayFlyout.IsOpen = true;
            controller.StartInstall();
        }

        public void CloseInstallerUI()
        {
            InstallingOverlayFlyout.IsOpen = false;
            CommonUtil.Run(() =>
            {
                InstallingOverlayFlyout.Content = null; //Remove this so it doesn't keep running. GC will remove it
            }, TimeSpan.FromSeconds(3));
        }
    }
}
