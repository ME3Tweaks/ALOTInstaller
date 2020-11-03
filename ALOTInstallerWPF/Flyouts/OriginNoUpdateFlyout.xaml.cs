using System;
using System.Collections.Generic;
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
using ALOTInstallerWPF.Objects;
using ME3ExplorerCore.Packages;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for OriginNoUpdateFlyout.xaml
    /// </summary>
    public partial class OriginNoUpdateFlyout : UserControl
    {
        public string ImagePath { get; }
        public OriginNoUpdateFlyout(MEGame game)
        {
            DataContext = this;
            ImagePath = $"pack://application:,,,/ALOTInstallerWPF;component/Images/origin/{game.ToString().ToLower()}update.png"; //IF ASSEMBLY CHANGES THIS MUST BE UPDATED!!
            CloseFlyoutCommand = new GenericCommand(closeFlyout);
            InitializeComponent();
        }

        private void closeFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.CloseOriginFlyoutUI();
            }
        }

        public GenericCommand CloseFlyoutCommand { get; set; }
    }
}
