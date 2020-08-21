﻿using System;
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
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Actions;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for InstallOptionsFlyout.xaml
    /// </summary>
    public partial class InstallOptionsFlyout : FlyoutController, INotifyPropertyChanged
    {
        private Dictionary<InstallOptionsStep.InstallOption, ToggleSwitch> checkboxMapping = new Dictionary<InstallOptionsStep.InstallOption, ToggleSwitch>();
        public bool DeterminingOptionsVisible { get; set; } = true;
        public string TitleText { get; } = "Select install options";
        public string ModeText { get; } = "Mode Text";
        public string SpinnerText { get; set; } = "Calculating install options";
        public string InstallOptionsTopText { get; set; }
        public string CurrentLodsDescText { get; set; }
        public bool Use4KLODs { get; set; } = true; //Default to TRUE
        public bool OptimizeTextureLibrary { get; set; } = true; //Default to TRUE
#if DEBUG
        public bool DebugPerformMainInstallation { get; set; } = true; //Default to TRUE
#else
        public bool DebugPerformMainInstallation => true; //ALWAYS TRUE IN RELEASE
#endif

        private static string fourKLodsStr = "Uses best quality textures, uses more memory than 2K";
        private static string twoKLodsStr = "Uses good quality textures, uses less memory than 4K";
        public void OnUse4KLODsChanged()
        {
            CurrentLodsDescText = Use4KLODs ? fourKLodsStr : twoKLodsStr;
        }

        public InstallOptionsFlyout(GameTarget target, List<UserFile> userFiles)
        {
            DataContext = this;
            InstallTarget = target;
            TitleText = $"Select install options for {target.Game.ToGameName()}";
            ModeText = $"Installer mode: {ManifestHandler.CurrentMode} Mode";
            OnUse4KLODsChanged(); //Set the default text.
            LoadCommands();
            InitializeComponent();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("InstallOptionsWorker");
            var files = ManifestHandler.GetManifestFilesForMode(ManifestHandler.CurrentMode);
            files.AddRange(userFiles);
            nbw.DoWork += (a, b) =>
            {
                string prefix = "Existing texture installation info: ";
                InstallOptionsTopText = prefix + "No textures installed";
                var ii = target.GetInstalledALOTInfo();
                if (ii != null)
                {
                    InstallOptionsTopText = prefix + ii.ToString();
                }
                b.Result = InstallOptionsStep.CalculateInstallOptions(target, ManifestHandler.CurrentMode, files);
            };
            nbw.RunWorkerCompleted += (a, b) =>
            {
                if (b.Error == null)
                {
                    var optionsDictionary = b.Result as Dictionary<InstallOptionsStep.InstallOption, (InstallOptionsStep.OptionState state, string reasonForState)>;
                    foreach (var v in optionsDictionary)
                    {
                        ToggleSwitch cb = new ToggleSwitch()
                        {
                            Content = getUIString(v.Key, files),
                            IsEnabled = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible || v.Value.state == InstallOptionsStep.OptionState.UncheckedVisible,
                            IsOn = v.Value.state == InstallOptionsStep.OptionState.CheckedVisible || v.Value.state == InstallOptionsStep.OptionState.ForceCheckedVisible,
                            ToolTip = v.Value.reasonForState,
                        };
                        optionsList.Children.Add(cb);
                        checkboxMapping[v.Key] = cb;
                    }
                }

                DeterminingOptionsVisible = false;
            };
            nbw.RunWorkerAsync();
        }

        public GameTarget InstallTarget { get; set; }

        private string getUIString(InstallOptionsStep.InstallOption option, List<InstallerFile> installerFiles)
        {
            if (option == InstallOptionsStep.InstallOption.ALOT) return (installerFiles.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER == 0)?.FriendlyName ?? "ALOT");
            if (option == InstallOptionsStep.InstallOption.ALOTUpdate) return (installerFiles.FirstOrDefault(x => x.AlotVersionInfo.ALOTVER > 0 && x.AlotVersionInfo.ALOTUPDATEVER != 0)?.FriendlyName ?? "ALOT update");
            if (option == InstallOptionsStep.InstallOption.Addon) return "ALOT Addon";
            if (option == InstallOptionsStep.InstallOption.MEUITM) return "MEUITM";
            if (option == InstallOptionsStep.InstallOption.UserFiles) return "User files";
            return "UNKNOWN OPTION";
        }

        public ICommand InstallTexturesCommand { get; set; }
        public ICommand AbortInstallCommand { get; set; }
        private void LoadCommands()
        {
            AbortInstallCommand = new GenericCommand(() => CloseFlyout()); //Can't just pass as it seems to cause exception.
            InstallTexturesCommand = new GenericCommand(BeginTextureInstallFlow, CanInstallTextures);
        }

        private async void BeginTextureInstallFlow()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                SpinnerText = "Performing installation precheck";
                DeterminingOptionsVisible = true;
                var answer = await mw.ShowMessageAsync("YOU WILL BE UNABLE TO INSTALL FURTHER MODS/DLC/FILES AFTER THIS POINT", "Once textures are installed, you will be unable to add or change files in your game as all files will be modified. Ensure all mods and DLC are installed now, as you will not be able to change these after this point. DO NOT ATTEMPT TO INSTALL FILES OUTSIDE OF ALOT INSTALLER AFTER THIS POINT or you will have to completely delete and reinstall the game.",
                    MessageDialogStyle.AffirmativeAndNegative);

                if (answer == MessageDialogResult.Negative)
                {
                    CloseFlyout();
                }
                else
                {
                    InstallOptionsPackage iop = new InstallOptionsPackage()
                    {
                        InstallTarget = ,
                        ImportNewlyUnpackedFiles = OptimizeTextureLibrary,
                        InstallALOT = checkboxMapping.ContainsKey(InstallOptionsStep.InstallOption.ALOT) ? checkboxMapping[InstallOptionsStep.InstallOption.ALOT].IsOn : false,
                        DebugNoInstall = !DebugPerformMainInstallation
                    };
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker("InstallPrecheckWorker");
                    nbw.DoWork += (a, b) =>
                    {
                        if (InstallTarget.Game == Enums.MEGame.ME1 && ManifestHandler.CurrentMode == ManifestMode.ALOT)
                        {
                            //void 
                            //Prxecheck.CheckMEUITM()
                        }
                    };
                    nbw.RunWorkerCompleted += (a, b) =>
                    {

                    };
                    nbw.RunWorkerAsync();
                }
            }
        }

        private bool CanInstallTextures()
        {
            return checkboxMapping.Any(x => x.Value.IsOn);
        }

        private void AbortInstall()
        {
            CloseFlyout();
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
