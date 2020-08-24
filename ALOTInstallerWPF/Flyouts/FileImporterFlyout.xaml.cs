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
using ALOTInstallerWPF.Objects;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for FileImporterFlyout.xaml
    /// </summary>
    public partial class FileImporterFlyout : UserControl, INotifyPropertyChanged
    {
        public bool UserFilesUsable { get; set; }
        public FileImporterFlyout()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public GenericCommand CloseFlyoutCommand { get; set; }

        private void LoadCommands()
        {
            CloseFlyoutCommand = new GenericCommand(closeFlyout);
        }


        private void closeFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.FileImporterOpen = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
