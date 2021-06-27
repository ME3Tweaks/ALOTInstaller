using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Helpers.AppSettings;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerCore.Steps.Installer;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using LegendaryExplorerCore.Packages;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Microsoft.WindowsAPICodePack.Taskbar;
using Serilog;
using Brushes = System.Windows.Media.Brushes;

namespace ALOTInstallerWPF.InstallerUI
{
    /// <summary>
    /// Interaction logic for InstallerUIController.xaml
    /// </summary>
    public partial class InstallerUIController : UserControl, INotifyPropertyChanged
    {
        // Used to suppress further closing dialogs from showing up, if they somehow do
        private bool SignaledWindowClose;

        private bool musicOn = false;
        public bool TipsVisible { get; set; } = true;
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
        public static ImageBrush GetInstallerBackgroundImage(InstallOptionsPackage iop)
        {
            if (iop.InstallerMode == ManifestMode.MEUITM)
            {
                var meuitmFile = iop.FilesToInstall.OfType<ManifestFile>().FirstOrDefault(x => x.MEUITMSettings != null);
                if (meuitmFile != null)
                {
                    using var stream = new MemoryStream(meuitmFile.MEUITMSettings.BackgroundImageBytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return new ImageBrush(bitmap)
                    {
                        Stretch = Stretch.UniformToFill
                    };
                }
            }

            string bgPath = $"/alot_{iop.InstallTarget.Game.ToString().ToLower()}_bg"; // ALOT / FREE MODE
            if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1)
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
            MusicAvailable = File.Exists(Locations.GetInstallModeMusicFilePath(InstallOptions));
            musicOn = MusicAvailable && Settings.PlayMusic;
            setMusicIcon();
        }

        private void loadTips()
        {
            var installTipsS = ExtractInternalFileToStream("ALOTInstallerWPF.InstallerUI.installtips.xml");
            XDocument tipsDoc = XDocument.Parse(new StreamReader(installTipsS).ReadToEnd());
            codexTips.ReplaceAll(tipsDoc.Root.Element(InstallOptions.InstallTarget.Game.ToString().ToLower()).Descendants("tip").Select(x => x.Value));
            codexTips.Shuffle();
        }

        #region AIWPF
        /// <summary>
        /// Gets a resource from ALOTInstallerWPF
        /// </summary>
        /// <param name="assemblyResource"></param>
        /// <returns></returns>
        private static Stream GetResourceStream(string assemblyResource)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var res = assembly.GetManifestResourceNames();
            return assembly.GetManifestResourceStream(assemblyResource);
        }


        public static MemoryStream ExtractInternalFileToStream(string internalResourceName)
        {
            Log.Information("[AIWPF] Extracting embedded file: " + internalResourceName + " to memory");
#if DEBUG
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
#endif
            using Stream stream = GetResourceStream(internalResourceName);
            MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }
        #endregion

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
                SetOverallProgressCallback = x => TaskbarHelper.SetProgress(x / 100.0f),
                SetProgressStyle = x => TaskbarHelper.SetProgressState(progressStyleToProgressState(x)),
                NotifyClosingWillBreakGame = notifyClosingWillBreakGame,
            };
            installerWorker.WorkerReportsProgress = true;
            installerWorker.DoWork += ss.InstallTextures;
            installerWorker.RunWorkerCompleted += (a, b) =>
            {
                TaskbarHelper.SetProgress(0);
                TaskbarHelper.SetProgressState(TaskbarProgressBarState.NoProgress);
                notifyClosingWillBreakGame(false);
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
                var musPath = Locations.GetInstallModeMusicFilePath(InstallOptions);
                if (File.Exists(musPath))
                {
                    audioPlayer.Source = new Uri(musPath);
                    if (musicOn)
                    {
                        audioPlayer.Play();
                    }
                }
            }

            #endregion
        }

        private void notifyClosingWillBreakGame(bool closingWillBreakGame)
        {
            Application.Current?.Invoke(() =>
            {
                if (Application.Current?.MainWindow is MainWindow mw)
                {
                    if (closingWillBreakGame)
                    {
                        // Ensure we don't add a duplicate by removing any previous one that was added
                        mw.Closing -= ShowClosingWillBreakGamePrompt;
                        mw.Closing += ShowClosingWillBreakGamePrompt;
                    }
                    else
                    {
                        mw.Closing -= ShowClosingWillBreakGamePrompt;
                    }
                }
            });
        }

        private async void ShowClosingWillBreakGamePrompt(object sender, CancelEventArgs e)
        {
            if (SignaledWindowClose) return; // Nothing to handle here
            Log.Error(@"[AIWPF] User trying to close installer while critical install is in progress. Prompting user to NOT do this.");
            if (Application.Current.MainWindow is MainWindow mw)
            {
                e.Cancel = true; //Cancel the close request. The dialog will re-throw the close
                var closeResult = await mw.ShowMessageAsync("Closing the installer now will break the game", "The installer is currently in a state where closing it will leave the game in a broken state. Your game will have to be restored to vanilla, either through a restore operation (if you made a backup) or a complete deletion of the game directory and then a reinstall.\n\n" +
                                                                                                             "If you're having issues with the installer, please come to the ALOT Discord, which can be found in the settings.\n\n" +
                                                                                                             "Close the installer?", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings()
                                                                                                             {
                                                                                                                 AffirmativeButtonText = "Close installer",
                                                                                                                 NegativeButtonText = "Keep installer open",
                                                                                                                 DefaultButtonFocus = MessageDialogResult.Negative
                                                                                                             }, 75);
                if (closeResult == MessageDialogResult.Affirmative)
                {
                    Log.Error(@"[AIWPF] User has chosen to close the installer while critical install is still in progress. Game will likely be in broken state");
                    SignaledWindowClose = true;
                    mw.Closing -= ShowClosingWillBreakGamePrompt;
                    mw.Close(); //Rethrow the close
                }
                else
                {
                    Log.Information(@"[AIWPF] User didn't close the installer");
                    e.Cancel = true;
                }
            }
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

        private void showStorefrontNoUpdateUI(MEGame obj)
        {
            if (obj != MEGame.ME3)
            {
                Application.Current.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.OpenOriginFlyout(obj);
                    }
                });
            }
        }

        public DispatcherTimer TipTimer { get; set; }

        private void handleInstallResult(InstallStep.InstallResult ir, string installString)
        {
            CoreAnalytics.TrackEvent?.Invoke("Install Step Finished", new Dictionary<string, string>()
            {
                {"Result", ir.ToString()},
                {"Game", InstallOptions.InstallTarget.Game.ToString()},
                {"LOD setting", InstallOptions.Limit2K ? "2K" : "4K"}
            });
            TipTimer?.Stop(); //Stop the tip rotation
            var installedInfo = InstallOptions.InstallTarget.GetInstalledALOTInfo();
            var installedTextures = InstallOptions.FilesToInstall.Any(x => !(x is PreinstallMod)); //Debug mode will not have files to install set

            bool showBottomText = false;
            if (ir == InstallStep.InstallResult.InstallOK)
            {
                BigIconKind = PackIconIoniconsKind.CheckmarkCircleMD;
                BigIconForeground = Brushes.Green;
                //InstallerTextTop = $"Installed {installString}";
                //InstallerTextBottomVisibility = InstallerTextMiddleVisibility = Visibility.Collapsed;
                if (installedTextures)
                {
                    CurrentTip = $"Texture installation succeeded. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
                }
                else if (installedInfo != null)
                {
                    CurrentTip = $"This installation has been texture modded. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
                }
                else
                {
                    TipsVisible = false;
                }
            }
            else if (ir == InstallStep.InstallResult.InstallOKWithWarning)
            {
                BigIconKind = PackIconIoniconsKind.WarningMD;
                BigIconForeground = System.Windows.Media.Brushes.Yellow;
                InstallerTextTop = $"Installed {installString}";
                InstallerTextMiddle = "Installation completed with warnings";
                if (installedTextures)
                {
                    CurrentTip = $"Texture installation succeeded with warnings. Check the installer log for more information on these warnings. Ensure you do not install package files (files ending in .pcc, .u, .upk, .sfm) outside of {Utilities.GetAppPrefixedName()} Installer to this game, or you will corrupt it.";
                }
                else
                {
                    CurrentTip = $"Installation succeeded with warnings. Check the installer log for more information on these warnings.";
                }
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
                    showBottomText = sf.ShowBottomText;
                }
            }
            InstallerTextBottomVisibility = showBottomText ? Visibility.Visible : Visibility.Collapsed;
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void AudioPlayer_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            audioPlayer.Position = TimeSpan.Zero;
            audioPlayer.Play();
        }
    }
}
