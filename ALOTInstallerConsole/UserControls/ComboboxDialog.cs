using System;
using System.Collections.Generic;
using System.Text;
using Terminal.Gui;

namespace ALOTInstallerConsole.UserControls
{
    /// <summary>
    /// Handles diagnostics information
    /// </summary>
    public class ComboboxDialog : Dialog
    {
        private ComboBox cb;

        public ComboboxDialog(string title, string aboveComboMessage, string belowComboMessage, params Button[] buttons) : base(title, buttons)
        {
            Add(new Label(aboveComboMessage));
            cb = new ComboBox();
            Add(cb);
            Add(new Label(belowComboMessage));
        }
    }
}
