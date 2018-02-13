using AlotAddOnGUI.classes;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
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

namespace AlotAddOnGUI
{
    /// <summary>
    /// Interaction logic for UpdateAvailableDialog.xaml
    /// </summary>
    public partial class UpdateAvailableDialog : CustomDialog
    {
        private MainWindow mainWindowRef;
        private bool _updateAccepted = false;
        public UpdateAvailableDialog(String headertext, String changelog, MainWindow mainWindow)
        {
            InitializeComponent();
            mainWindowRef = mainWindow;
            Textblock_UpdateText.Text = headertext;
            Textblock_ChangelogText.Text = changelog;

        }

        private async void Update_Button_Click(object sender, RoutedEventArgs e)
        {
            _updateAccepted = true;
            await mainWindowRef.HideMetroDialogAsync(this);
        }

        internal bool wasUpdateAccepted()
        {
            return _updateAccepted;
        }

        private async void Later_Button_Click(object sender, RoutedEventArgs e)
        {
            _updateAccepted = false;
            await mainWindowRef.HideMetroDialogAsync(this);
        }
    }
}
