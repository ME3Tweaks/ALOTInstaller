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
using ALOTInstallerWPF.Controllers;
using ALOTInstallerWPF.Flyouts;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Controls;

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
                FlyoutOptionDialog.IsOpen = false;
            });
            FlyoutOptionDialog.Content = null; //clear
            FlyoutOptionDialog.Content = content;
            FlyoutOptionDialog.IsOpen = true;
            return tcs.Task;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
