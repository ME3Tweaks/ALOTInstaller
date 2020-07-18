using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole
{
    public abstract class UIController : Window
    {
        /// <summary>
        /// Indicates that this UI has been closed and disposed of and should no longer be used.
        /// </summary>
        public bool Disposed;
        public abstract void SetupUI();
        public abstract void BeginFlow();
    }
}
