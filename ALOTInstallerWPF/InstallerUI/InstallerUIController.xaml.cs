using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Xml.Linq;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerCore.Steps.Installer;
using ALOTInstallerWPF.BuilderUI;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using Microsoft.WindowsAPICodePack.Taskbar;
using Serilog;

namespace ALOTInstallerWPF.InstallerUI
{
    /// <summary>
    /// Interaction logic for InstallerUIController.xaml
    /// </summary>
    public partial class InstallerUIController : UserControl, INotifyPropertyChanged
    {
        private bool musicOn = false;
        public PackIconIoniconsKind MusicIcon { get; private set; }
        public PackIconIoniconsKind BigIconKind { get; private set; }
        public bool BigIconVisible { get; private set; }
        public bool ShowTriangleBackground { get; private set; }
        public bool ShowCircleBackground { get; private set; }
        public void OnBigIconVisibleChanged()
        {
            updateIconBackgroundVisibility();
        }

        public void OnBigIconKindChanged()
        {
            updateIconBackgroundVisibility();
        }

        private void updateIconBackgroundVisibility()
        {
            // Sets the background for the icon canvas to make it look filled in.
            ShowCircleBackground = ShowTriangleBackground = false;
            if (BigIconKind == PackIconIoniconsKind.CloseCircleMD || BigIconKind == PackIconIoniconsKind.CheckmarkCircleMD)
            {
                ShowCircleBackground = BigIconVisible;
            }
            else if (BigIconKind == PackIconIoniconsKind.WarningMD)
            {
                ShowTriangleBackground = BigIconVisible;
            }
        }
#if DEBUG
        public ObservableCollectionExtended<InstallStep.InstallResult> DebugAllResultCodes { get; } = new ObservableCollectionExtended<InstallStep.InstallResult>();
        /// <summary>
        /// Used when debugging the overlay
        /// </summary>
        public bool DebugMode { get; private set; }
#else
        public bool DebugMode => false;
#endif
        public bool ContinueButtonVisible { get; private set; }
        public bool MusicAvailable { get; private set; }
        public SolidColorBrush BigIconForeground { get; private set; }
        public string InstallerTextTop { get; set; }
        public string InstallerTextMiddle { get; set; }
        public string InstallerTextBottom { get; set; }
        public Visibility InstallerTextTopVisibility { get; private set; } = Visibility.Visible;
        public Visibility InstallerTextMiddleVisibility { get; private set; } = Visibility.Visible;
        public Visibility InstallerTextBottomVisibility { get; private set; } = Visibility.Visible;
        public static ImageBrush GetInstallerBackgroundImage(Enums.MEGame game, ManifestMode mode)
        {
            string bgPath = $"/alot_{game.ToString().ToLower()}_bg"; // ALOT / FREE MODE
            if (mode == ManifestMode.MEUITM)
            {
                bgPath = $"/meuitm_{game.ToString().ToLower()}_bg";
            }
            else if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
            {
                bgPath += "_alt";
            }
            bgPath += ".jpg";
            bgPath = $"pack://application:,,,/ALOTInstallerWPF;component/Images{bgPath}"; //IF ASSEMBLY CHANGES THIS MUST BE UPDATED!!
            return new ImageBrush(new BitmapImage(new Uri(bgPath)))
            {
                Stretch = Stretch.UniformToFill
            };
        }

        private List<string> codexTips = new List<string>();

        public InstallerUIController(InstallOptionsPackage installOptions)
        {
            DataContext = this;
            InstallOptions = installOptions;
            LoadCommands();
            loadTips();
            CurrentTip = "This may take a while, enjoy some lore while you wait.";
            InitializeComponent();
            MusicAvailable = File.Exists(getMusicPath(InstallOptions.InstallTarget.Game));
            musicOn = MusicAvailable && Settings.PlayMusic;
            setMusicIcon();
        }

        private void loadTips()
        {
            var installTipsFile = Path.Combine(Locations.ResourcesDir, "InstallerUI", "installtips.xml");
            Debug.WriteLine(installTipsFile);
            if (File.Exists(installTipsFile))
            {
                XDocument tipsDoc = XDocument.Load(installTipsFile);
                codexTips.ReplaceAll(tipsDoc.Root.Element(InstallOptions.InstallTarget.Game.ToString().ToLower()).Descendants("tip").Select(x => x.Value));
                codexTips.Shuffle();
            }
        }

        public GenericCommand ToggleMusicCommand { get; set; }
        public GenericCommand CloseInstallerCommand { get; set; }
        public InstallOptionsPackage InstallOptions { get; set; }
#if DEBUG
        public RelayCommand DebugHandleFailureCommand { get; set; }
#endif
        private void LoadCommands()
        {
            ToggleMusicCommand = new GenericCommand(ToggleMusic, CanToggleMusic);
            CloseInstallerCommand = new GenericCommand(CloseInstaller);
#if DEBUG
            DebugHandleFailureCommand = new RelayCommand(DebugHandleFailure);

#endif
        }

        private void DebugHandleFailure(object obj)
        {
            if (obj is InstallStep.InstallResult res)
            {
                handleInstallResult(res, res.ToString());
            }
        }

        private void CloseInstaller()
        {


            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.CloseInstallerUI();
            }
        }

        private bool CanToggleMusic() => MusicAvailable;

        private void ToggleMusic()
        {
            if (musicOn)
            {
                audioPlayer.Pause();
            }
            else
            {
                audioPlayer.Play();
            }
            musicOn = !musicOn;
            setMusicIcon();
        }

        /// <summary>
        /// Current index of tip
        /// </summary>
        private int tipIndex = 0;
        private void setMusicIcon()
        {
            MusicIcon = musicOn ? PackIconIoniconsKind.VolumeHighMD : PackIconIoniconsKind.VolumeOffMD;
        }

        internal void StartInstall(bool debugMode = false)
        {
#if DEBUG
            DebugMode = debugMode;
            foreach (var v in ProgressHandler.DefaultStages)
            {
                DebugAllResultCodes.AddRange(v.FailureInfos.Where(x => !x.Warning).Select(x => x.FailureResultCode));
            }
            DebugAllResultCodes.AddRange(Enum.GetValues(typeof(InstallStep.InstallResult)).Cast<InstallStep.InstallResult>());

#endif
            TipTimer = new System.Windows.Threading.DispatcherTimer();
            TipTimer.Tick += (sender, args) =>
            {
                TipTimer.Interval = new TimeSpan(0, 0, 20);
                if (codexTips.Count > 1)
                {
                    CurrentTip = codexTips[tipIndex++ % codexTips.Count];
                }
            };
            tipIndex = 0;
            TipTimer.Interval = new TimeSpan(0, 0, 6); //Initial tip time
            TipTimer.Start();
            string installString = null;
            NamedBackgroundWorker installerWorker = new NamedBackgroundWorker("InstallerWorker");
            InstallStep ss = new InstallStep(InstallOptions)
            {
                SetInstallString = x => installString = x,
                SetTopTextCallback = x => InstallerTextTop = x,
                SetMiddleTextCallback = x => InstallerTextMiddle = x,
                SetBottomTextCallback = x => InstallerTextBottom = x,
                SetTopTextVisibilityCallback = x =>
                    InstallerTextTopVisibility = x ? Visibility.Visible : Visibility.Collapsed,
                SetMiddleTextVisibilityCallback = x =>
                    InstallerTextMiddleVisibility = x ? Visibility.Visible : Visibility.Collapsed,
                SetBottomTextVisibilityCallback = x =>
                    InstallerTextBottomVisibility = x ? Visibility.Visible : Visibility.Collapsed,
                ShowStorefrontDontClickUpdateCallback = showStorefrontNoUpdateUI,
                SetOverallProgressCallback = x=> TaskbarHelper.SetProgress(x),
                SetProgressStyle = x => TaskbarHelper.SetProgressState(progressStyleToProgressState(x))
            };
            installerWorker.WorkerReportsProgress = true;
            installerWorker.DoWork += ss.InstallTextures;
            installerWorker.RunWorkerCompleted += (a, b) =>
            {
                ContinueButtonVisible = true;
                fadeoutMusic();
                // Installation has completed
                if (b.Error == null)
                {
                    if (b.Result is InstallStep.InstallResult ir)
                    {
                        Log.Information($"[AIWPF] Installation result: {b.Result}");
                        handleInstallResult(ir, installString);
                    }
                    else
                    {
                        Log.Error("[AIWPF] Installer thread exited with no exception but did not set result code");
                        BigIconKind = PackIconIoniconsKind.CloseCircleMD;
                        BigIconForeground = Brushes.Red;
                        InstallerTextTop = "Failed to install textures";
                        InstallerTextMiddle = "Installer exited without success or failure code";
                        InstallerTextBottom = "Check installer log for more info";
                    }

                }
                else
                {

                    Log.Error("[AIWPF] Installation step threw an exception:");
                    b.Error.WriteToLog("[AIWPF] ");
                    BigIconKind = PackIconIoniconsKind.CloseCircleMD;
                    BigIconForeground = Brushes.Red;
                    InstallerTextTop = "Failed to install textures";
                    InstallerTextMiddle = b.Error.Message;
                    InstallerTextBottom = "Check installer log for more info";
                }
            };

            if (!DebugMode)
            {
                installerWorker.RunWorkerAsync();
            }
            else
            {
                ContinueButtonVisible = true;
                CommonUtil.Run(fadeoutMusic, TimeSpan.FromSeconds(5));
            }

            #region MUSIC

            if (InstallOptions.InstallALOT || InstallOptions.InstallALOTUpdate || InstallOptions.InstallMEUITM)
            {
                audioPlayer.Source = new Uri(getMusicPath(InstallOptions.InstallTarget.Game));
                if (musicOn)
                {
                    audioPlayer.Play();
                }
            }

            #endregion
        }

        private TaskbarProgressBarState progressStyleToProgressState(InstallStep.ProgressStyle progressStyle)
        {
            switch (progressStyle)
            {
                case InstallStep.ProgressStyle.None:
                    return TaskbarProgressBarState.NoProgress;
                case InstallStep.ProgressStyle.Indeterminate:
                    return TaskbarProgressBarState.Indeterminate;
                case InstallStep.ProgressStyle.Determinate:
                    return TaskbarProgressBarState.Normal;
                default:
                    return TaskbarProgressBarState.NoProgress;
            }
        }

        private void showStorefrontNoUpdateUI(Enums.MEGame obj)
        {
            Application.Current.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.OpenOriginFlyout(obj);
                }
            });
        }

        public DispatcherTimer TipTimer { get; set; }

        private void handleInstallResult(InstallStep.InstallResult ir, string installString)
        {
            TipTimer?.Stop(); //Stop the tip rotation
            if (ir == InstallStep.InstallResult.InstallOK)
            {
                BigIconKind = PackIconIoniconsKind.CheckmarkCircleMD;
                BigIconForeground = Brushes.Green;
                //InstallerTextTop = $"Installed {installString}";
                //InstallerTextBottomVisibility = InstallerTextMiddleVisibility = Visibility.Collapsed;
                CurrentTip = $"Texture installation succeeded. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
            }
            else if (ir == InstallStep.InstallResult.InstallOKWithWarning)
            {
                BigIconKind = PackIconIoniconsKind.WarningMD;
                BigIconForeground = Brushes.Yellow;
                InstallerTextTop = $"Installed {installString}";
                InstallerTextMiddle = "Installation completed with warnings";
                CurrentTip = $"Texture installation succeeded with warnings. Check the installer log for more information on these warnings. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
            }
            else
            {
                // Is this a stage failure?
                StageFailure sf = null;
                foreach (var stage in ProgressHandler.DefaultStages)
                {
                    var failure = stage.FailureInfos.FirstOrDefault(x => x.FailureResultCode == ir);
                    if (failure != null)
                    {
                        sf = failure;
                        break;
                    }
                }
                if (sf != null)
                {
                    BigIconKind = PackIconIoniconsKind.CloseCircleMD;
                    BigIconForeground = Brushes.Red;
                    InstallerTextTop = sf.FailureTopText;
                    InstallerTextMiddle = sf.FailureBottomText;
                    CurrentTip = sf.FailureHeaderText;
                }
            }
            InstallerTextBottomVisibility = Visibility.Collapsed;
            InstallerTextMiddleVisibility = InstallerTextTopVisibility = Visibility.Visible;
            BigIconVisible = true;
        }

        public string CurrentTip { get; set; }

        private void fadeoutMusic()
        {
            var musicButtonFadeoutAnim = new DoubleAnimation(musicButton.Opacity, 0, TimeSpan.FromSeconds(2));
            musicButtonFadeoutAnim.Completed += (sender, args) =>
            {
                MusicAvailable = false; // will collapse button.
            };
            var volumeFadeoutAnim = new DoubleAnimation(audioPlayer.Volume, 0, TimeSpan.FromSeconds(4));
            volumeFadeoutAnim.EasingFunction = new QuadraticEase();
            volumeFadeoutAnim.Completed += (sender, args) =>
            {
                audioPlayer.Close();
            };
            musicButton.BeginAnimation(UIElement.OpacityProperty, musicButtonFadeoutAnim);
            audioPlayer.BeginAnimation(MediaElement.VolumeProperty, volumeFadeoutAnim);
        }

        private string getMusicPath(Enums.MEGame game)
        {
            return Path.Combine(Locations.MusicDirectory, game.ToString().ToLower() + ".mp3");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void AudioPlayer_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            audioPlayer.Position = TimeSpan.Zero;
            audioPlayer.Play();
        }
    }
}
