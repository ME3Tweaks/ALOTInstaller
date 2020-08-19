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
using ALOTInstallerCore.Helpers;

namespace ALOTInstallerWPF.Objects
{
    /// <summary>
    /// Interaction logic for FlyoutDialogPanel.xaml
    /// </summary>
    public partial class FlyoutDialogPanel : UserControl, INotifyPropertyChanged
    {
        public string TopText { get; private set; }
        public ObservableCollectionExtended<Button> Items { get; } = new ObservableCollectionExtended<Button>();

        public FlyoutDialogPanel()
        {
            DataContext = this;
            InitializeComponent();
        }

        public FlyoutDialogPanel(string topText, Button[] buttons, Action<int> notifyOptionChosen) : this()
        {
            TopText = topText;
            Items.ReplaceAll(buttons);
            int option = 0;
            foreach (var b in buttons)
            {
                b.Tag = option;
                b.Click += (_a, _b) =>
                {
                    var usedOption = (int)(_a as FrameworkElement).Tag; //Recapture variable
                    notifyOptionChosen?.Invoke(usedOption);
                };
                option++;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
