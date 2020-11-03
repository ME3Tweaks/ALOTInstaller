using System.IO;
using System.Threading.Tasks;
using ALOTInstallerCore.Helpers;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using ME3ExplorerCore.Helpers;
using Serilog;
using Application = System.Windows.Application;
using Exception = System.Exception;

namespace ALOTInstallerWPF.Controllers
{
    class MEMGUILaunchController
    {
        /// <summary>
        /// Launches Mass Effect Modder GUI update + launch flow. MUST BE CALLED FROM THE MAIN UI THREAD.
        /// </summary>
        public static async void LaunchMEMGUI()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.SettingsOpen = false;
                if (!App.CheckedForMEMUpdates)
                {
                    var pd = await mw.ShowProgressAsync("Checking for updates to Mass Effect Modder", "Please wait");
                    await Task.Run(() =>
                     {
                         return MEMGUIUpdater.UpdateMEMGUI(title => pd.SetTitle(title),
                             message => pd.SetMessage(message),
                             (done, total) => pd.SetProgress(total != 0 ? (done * 1.0 / total) : 0));
                     }).ContinueWith(result =>
                     {
                         Application.Current.Invoke(async () =>
                         {
                             await pd.CloseAsync();
                             LaunchMEMGUINoUpdate();
                         });
                         App.CheckedForMEMUpdates = true;
                     });
                }
                else
                {
                    LaunchMEMGUINoUpdate();
                }
            }
        }

        public static async void LaunchMEMGUINoUpdate()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var memGuiPath = Locations.GetCachedExecutable("MassEffectModder", true);
                if (File.Exists(memGuiPath))
                {
                    try
                    {
                        ALOTInstallerCore.Utilities.RunProcess(memGuiPath, "", noWindow: false);
                    }
                    catch (Exception e)
                    {
                        Log.Error($@"[AIWPF] Unable to launch Mass Effect Modder: {e.Message}");
                        await mw.ShowMessageAsync("Unable to launch Mass Effect Modder", $"An error occurred launching Mass Effect Modder: {e.Message}");
                    }
                }
            }
        }
    }
}