using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Timers;
using ALOTInstallerCore.ModManager.Services;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Periodically refreshes various variables such as backup status
    /// </summary>
    public class PeriodicRefresh
    {
        private static Timer periodicTimer;

        public static void StartPeriodicRefresh()
        {
            if (periodicTimer != null)
            {
                periodicTimer.Stop();
                periodicTimer.Elapsed -= periodicRefresh;
                periodicTimer.Close();
            }
            periodicTimer = new Timer(60000)
            {
                AutoReset = true
            };
            periodicTimer.Elapsed += periodicRefresh;
            periodicTimer.Start();
        }

        private static void periodicRefresh(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("Periodic refresh");
            BackupService.RefreshBackupStatus(null, false);
        }
    }
}
