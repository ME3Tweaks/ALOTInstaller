using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using ALOTInstallerCore.Steps;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using MahApps.Metro.IconPacks;

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
            bgPath = $"pack://application:,,,/ALOTInstallerWPF;component/Images{bgPath}";
            return new ImageBrush(new BitmapImage(new Uri(bgPath)))
            {
                Stretch = Stretch.UniformToFill
            };
        }

        public InstallerUIController(InstallOptionsPackage installOptions)
        {
            DataContext = this;
            InstallOptions = installOptions;
            LoadCommands();
            InitializeComponent();
            MusicAvailable = File.Exists(getMusicPath(InstallOptions.InstallTarget.Game));
            musicOn = MusicAvailable && Settings.PlayMusic;
            setMusicIcon();
        }

        public GenericCommand ToggleMusicCommand { get; set; }
        public GenericCommand CloseInstallerCommand { get; set; }
        public InstallOptionsPackage InstallOptions { get; set; }

        private void LoadCommands()
        {
            ToggleMusicCommand = new GenericCommand(ToggleMusic, CanToggleMusic);
            CloseInstallerCommand = new GenericCommand(CloseInstaller);
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

        private void setMusicIcon()
        {
            MusicIcon = musicOn ? PackIconIoniconsKind.VolumeHighMD : PackIconIoniconsKind.VolumeOffMD;
        }

        internal void StartInstall(bool isOpeningDebug = false)
        {
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
            };
            installerWorker.WorkerReportsProgress = true;
            installerWorker.DoWork += ss.InstallTextures;
            installerWorker.RunWorkerCompleted += (a, b) =>
            {
                ContinueButtonVisible = true;
                if (MusicAvailable)
                {
                    musicButton.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(musicButton.Opacity, 0, TimeSpan.FromSeconds(1)));
                    audioPlayer.BeginAnimation(MediaElement.VolumeProperty, new DoubleAnimation(audioPlayer.Volume, 0, TimeSpan.FromSeconds(3)));
                }
                // Installation has completed
                if (b.Error == null)
                {
                    if (b.Result is InstallStep.InstallResult ir)
                    {
                        if (ir == InstallStep.InstallResult.InstallOK)
                        {
                            BigIconKind = PackIconIoniconsKind.CheckmarkCircleMD;
                            BigIconForeground = Brushes.Green;
                            InstallerTextTop = installString;
                        }
                        else
                        {
                            Debug.WriteLine($"Unhandled exit code: {ir}");
                        }
                    }
                    else
                    {
                        BigIconKind = PackIconIoniconsKind.CloseCircleMD;
                        BigIconForeground = Brushes.Red;
                        InstallerTextTop = "Failed to install textures";
                        InstallerTextMiddle = "Installer exited without success or failure code";
                        InstallerTextBottom = "Check installer log for more info";
                    }

                }
                else
                {
                    BigIconKind = PackIconIoniconsKind.CloseCircleMD;
                    BigIconForeground = Brushes.Red;
                    InstallerTextTop = "Failed to install textures";
                    InstallerTextMiddle = b.Error.Message;
                    InstallerTextBottom = "Check installer log for more info";
                }

                BigIconVisible = true;

                //PostInstallUIController bui = new PostInstallUIController();
                //bui.setInstalledString(installString);
                //Program.SwapToNewView(bui);
            };
            if (!isOpeningDebug)
            {
                installerWorker.RunWorkerAsync();
            }
            else
            {
                ContinueButtonVisible = true;
                CommonUtil.Run(() =>
                {
                    var musicButtonFadeoutAnim = new DoubleAnimation(musicButton.Opacity, 0, TimeSpan.FromSeconds(2));
                    musicButtonFadeoutAnim.Completed += (sender, args) =>
                    {
                        MusicAvailable = false; // will collapse button.
                    };
                    var volumeFadeoutAnim = new DoubleAnimation(audioPlayer.Volume, 0, TimeSpan.FromSeconds(4.5));
                    volumeFadeoutAnim.EasingFunction = new QuadraticEase();
                    volumeFadeoutAnim.Completed += (sender, args) =>
                    {
                        //audioPlayer.Stop();
                        //audioPlayer.Close();
                    };
                    musicButton.BeginAnimation(UIElement.OpacityProperty, musicButtonFadeoutAnim);
                    audioPlayer.BeginAnimation(MediaElement.VolumeProperty, volumeFadeoutAnim);
                }, TimeSpan.FromSeconds(5));
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
