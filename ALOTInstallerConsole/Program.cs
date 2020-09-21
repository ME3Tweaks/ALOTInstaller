using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using ALOTInstallerConsole.Telemetry;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    class Program
    {
        private static void setWrapperLogger(ILogger logger) => Log.Logger = logger;
        static void Main(string[] args)
        {
            var bufferHeight = Console.BufferHeight;
            try
            {
                Application.Init();
                //Initialize ALOT Installer library
                ALOTInstallerCoreLib.Startup(setWrapperLogger, action => { }, startTelemetry, stopTelemetry);

                var startupUI = new BuilderUI.StartupUIController();
                Program.SwapToNewView(startupUI);
            }
            catch (Exception e)
            {
                // Unhandled exception!
                Console.BufferHeight = bufferHeight; //Restore
                Console.Error.WriteLine(e.FlattenWithTrace());
            }
        }

        private static UIController _currentController;
        /// <summary>
        /// Swaps the current top level UIController (if any) with another one.
        /// </summary>
        /// <param name="controller"></param>
        public static void SwapToNewView(UIController controller)
        {
            _currentController?.SignalStopping();
            Application.RequestStop();
            controller.SetupUI();
            controller.BeginFlow();
            _currentController = controller;
            Application.Run(controller);
        }

        private static void startTelemetry()
        {
            initAppCenter();
            AppCenter.SetEnabledAsync(true);
        }

        private static void stopTelemetry()
        {
            AppCenter.SetEnabledAsync(false);
        }

        private static bool telemetryStarted = false;

        private static void initAppCenter()
        {

#if DEBUG
            if (APIKeys.HasAppCenterKey && !telemetryStarted)
            {
                Crashes.GetErrorAttachments = (ErrorReport report) =>
                {
                    var attachments = new List<ErrorAttachmentLog>();
                    // Attach some text.
                    string errorMessage = "ALOT Installer Console has crashed! This is the exception that caused the crash:\n" + report.StackTrace;
                    Log.Fatal(errorMessage);
                    Log.Error("Note that this exception may appear to occur in a follow up boot due to how appcenter works");
                    string log = LogCollector.CollectLatestLog(false);
                    if (log.Length < 1024 * 1024 * 7)
                    {
                        attachments.Add(ErrorAttachmentLog.AttachmentWithText(log, "crashlog.txt"));
                    }
                    else
                    {
                        //Compress log
                        var compressedLog = SevenZipHelper.LZMA.CompressToLZMAFile(Encoding.UTF8.GetBytes(log));
                        attachments.Add(ErrorAttachmentLog.AttachmentWithBinary(compressedLog, "crashlog.txt.lzma", "application/x-lzma"));
                    }

                    // Attach binary data.
                    //var fakeImage = System.Text.Encoding.Default.GetBytes("Fake image");
                    //ErrorAttachmentLog binaryLog = ErrorAttachmentLog.AttachmentWithBinary(fakeImage, "ic_launcher.jpeg", "image/jpeg");

                    return attachments;
                };
                AppCenter.Start(APIKeys.AppCenterKey, typeof(Analytics), typeof(Crashes));
            }
#else
            if (!APIKeys.HasAppCenterKey)
            {
                Debug.WriteLine(" >>> This build is missing an API key for AppCenter!");
            }
            else
            {
                Debug.WriteLine("This build has an API key for AppCenter");
            }
#endif
            telemetryStarted = true;

        }
    }
}
