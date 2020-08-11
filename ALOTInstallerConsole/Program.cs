﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using ALOTInstallerCore.Objects.Manifest;
using Serilog;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    class Program
    {
        private static void setWrapperLogger(ILogger logger) => Log.Logger = logger;
        static void Main(string[] args)
        {
            //Initialize ALOT Installer library
            ALOTInstallerCoreLib.Startup(setWrapperLogger);

            Application.Init();
            Locations.ME3Target.StampTextureModificationInfo(new TextureModInstallationInfo(11,1,0,0));
            var startupUI = new BuilderUI.StartupUIController();
            Program.SwapToNewView(startupUI);
        }

        /// <summary>
        /// Swaps the current top level UIController (if any) with another one.
        /// </summary>
        /// <param name="controller"></param>
        public static void SwapToNewView(UIController controller)
        {
            controller.SignalStopping();
            Application.RequestStop();
            controller.SetupUI();
            controller.BeginFlow();
            Application.Run(controller);
        }

        //static void debug()
        //{
        //    var xml = File.ReadAllText(@"C:\users\mgamerz\desktop\t.txt");
        //    XDocument x = XDocument.Parse(xml);
        //    foreach (var v in x.Root.Elements("supportedhash"))
        //    {
        //        if (v.Attribute("game").Value == "me3")
        //        {
        //            Debug.WriteLine($"[@\"{v.Value}\"] = @\"{v.Attribute("name").Value}\",");
        //        }
        //    }
        //}
    }
}
