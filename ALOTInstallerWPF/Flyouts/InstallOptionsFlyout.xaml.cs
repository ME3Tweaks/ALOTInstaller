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
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for InstallOptionsFlyout.xaml
    /// </summary>
    public partial class InstallOptionsFlyout : UserControl, INotifyPropertyChanged
    {
        public bool DeterminingOptionsVisible { get; set; } = true;
        public string TitleText { get; } = "Select install options";
        public InstallOptionsFlyout(GameTarget target, List<UserFile> userFiles)
        {
            DataContext = this;
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("InstallOptionsWorker");
            nbw.DoWork += (a, b) =>
            {
                var files = ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode);
                files.AddRange(userFiles);
                b.Result = InstallOptionsStep.CalculateInstallOptions(target, ManifestHandler.CurrentMode, files);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    var optionsDictionary = b.Result as Dictionary<InstallOptionsStep.InstallOption, (InstallOptionsStep.OptionState state, string reasonForState)>;
                    foreach (var v in optionsDictionary)
                    {
                        CheckBox cb = new CheckBox()
                        {
                            Content = v.Key.ToString(),
                            IsEnabled = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible || v.Value.state == InstallOptionsStep.OptionState.UncheckedVisible,
                            IsChecked = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible || v.Value.state == InstallOptionsStep.OptionState.ForceCheckedVisible,
                            ToolTip = v.Value.reasonForState,
                            Margin = new Thickness(5, 5, 5, 5)
                        };
                        optionsList.Children.Add(cb);
                    }
                }

                DeterminingOptionsVisible = false;
            };
            nbw.RunWorkerAsync();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
