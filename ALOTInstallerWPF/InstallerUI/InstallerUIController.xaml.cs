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
using System.Windows.Shapes;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
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
        public SolidColorBrush BigIconForeground { get; private set; }
        public string InstallerTextTop { get; set; }
        public string InstallerTextMiddle { get; set; }
        public string InstallerTextBottom { get; set; }

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

        public InstallerUIController()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            setMusicIcon();
        }
        public GenericCommand ToggleMusicCommand { get; set; }
        public GenericCommand CloseInstallerCommand { get; set; }

        private void LoadCommands()
        {
            ToggleMusicCommand = new GenericCommand(ToggleMusic, CanToggleMusic);
            CloseInstallerCommand = new GenericCommand(CloseInstaller);
        }

        private void CloseInstaller()
        {

        }

        private bool CanToggleMusic()
        {
            return true;
        }


        private void ToggleMusic()
        {
            musicOn = !musicOn;
            setMusicIcon();
        }

        private void setMusicIcon()
        {
            MusicIcon = musicOn ? PackIconIoniconsKind.VolumeHighMD : PackIconIoniconsKind.VolumeOffMD;
        }



        public event PropertyChangedEventHandler PropertyChanged;
    }
}
