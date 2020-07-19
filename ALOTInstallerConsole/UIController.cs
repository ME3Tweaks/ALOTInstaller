using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    public abstract class UIController : Window
    {
        /// <summary>
        /// Sets up the controller's UI. This should be called before swapping to it
        /// </summary>
        public abstract void SetupUI();
        /// <summary>
        /// This method will be called right before the UI is attached, but after the prevoius one has been removed.
        /// </summary>
        public abstract void BeginFlow();
    }
}
