using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using MahApps.Metro.Actions;
using MahApps.Metro.Controls;

namespace ALOTInstallerWPF.Flyouts
{
    public abstract class FlyoutController : UserControl
    {
        internal Action CloseFlyout;
    }
}
