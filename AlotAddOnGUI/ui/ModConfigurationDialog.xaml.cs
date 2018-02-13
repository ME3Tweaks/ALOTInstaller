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
    /// Interaction logic for ModConfigurationDialog.xaml
    /// </summary>
    public partial class ModConfigurationDialog : CustomDialog
    {
        private MainWindow mainWindowRef;

        public ModConfigurationDialog(AddonFile af, MainWindow mainWindow)
        {
            InitializeComponent();
            Title = af.FriendlyName + " configuration";
            mainWindowRef = mainWindow;
            DataContext = af;
            ListView_ChoiceFiles.ItemsSource = af.ChoiceFiles;
        }


        private async void Close_Dialog(object sender, RoutedEventArgs e)
        {
            foreach (ChoiceFile cf in ListView_ChoiceFiles.Items)
            {
                var row = (System.Windows.Controls.ListViewItem)ListView_ChoiceFiles.ItemContainerGenerator.ContainerFromItem(cf);


            }

            await mainWindowRef.HideMetroDialogAsync(this);
        }

        private void Combobox_DropdownClosed(object sender, EventArgs e)
        {
            if (sender is ComboBox)
            {
                ComboBox cb = (ComboBox)sender;
                ChoiceFile choisefile = (ChoiceFile)cb.DataContext;
                choisefile.SelectedIndex = cb.SelectedIndex;
            }
        }
    }
}
