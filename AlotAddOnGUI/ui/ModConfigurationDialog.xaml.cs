using AlotAddOnGUI.classes;
using MahApps.Metro.Controls.Dialogs;
using Serilog;
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
            Title = af.FriendlyName;
            mainWindowRef = mainWindow;
            DataContext = af;
            List<ConfigurableModInterface> configurableItems = new List<ConfigurableModInterface>();
            configurableItems.AddRange(af.ChoiceFiles);
            configurableItems.AddRange(af.CopyFiles.Where(s => s.Optional));
            configurableItems.AddRange(af.ZipFiles.Where(s => s.Optional));

            ListView_ChoiceFiles.ItemsSource = configurableItems;
            if (af.ComparisonsLink == null)
            {
                Comparison_Button.Visibility = Visibility.Collapsed;
            }
        }


        private async void Close_Dialog_Click(object sender, RoutedEventArgs e)
        {
            //foreach (ChoiceFile cf in ListView_ChoiceFiles.Items)
            //{
            //    var row = (System.Windows.Controls.ListViewItem)ListView_ChoiceFiles.ItemContainerGenerator.ContainerFromItem(cf);


            //}
            try
            {
                await mainWindowRef.HideMetroDialogAsync(this);
            } catch (Exception ex)
            {
                Log.Error("Error closing mod dialog:");
                Log.Error(App.FlattenException(ex));
            }
        }

        private void Combobox_DropdownClosed(object sender, EventArgs e)
        {
            if (sender is ComboBox)
            {
                ComboBox cb = (ComboBox)sender;
                ConfigurableModInterface choicefile = (ConfigurableModInterface)cb.DataContext;
                choicefile.SelectedIndex = cb.SelectedIndex;
            }
        }

        private void Comparisons_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.openWebPage(((AddonFile)DataContext).ComparisonsLink);
        }
    }
}
