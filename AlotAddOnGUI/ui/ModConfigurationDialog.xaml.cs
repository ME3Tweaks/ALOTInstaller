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
using System.Windows.Documents.Serialization;
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
        private readonly MainWindow mainWindowRef;
        public bool Canceled;

        public ModConfigurationDialog(AddonFile af, MainWindow mainWindow, bool continueMode)
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

            if (!continueMode)
            {
                Cancel_Button.Visibility = Visibility.Collapsed;
            }
            else
            {
                Close_Button.Content = "Continue";
            }
        }


        private async void CloseContinue_Dialog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Information("Closing mod configuration dialog");
                await mainWindowRef.HideMetroDialogAsync(this);
            }
            catch (Exception ex)
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

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Log.Information("User canceling mod configuration in continue mode");
            try
            {
                Canceled = true;
                await mainWindowRef.HideMetroDialogAsync(this);
            }
            catch (Exception ex)
            {
                Log.Error("Error canceling mod dialog:");
                Log.Error(App.FlattenException(ex));
            }
        }
    }
}
