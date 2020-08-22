using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;

namespace ALOTInstallerWPF.Helpers
{
    // From https://stackoverflow.com/questions/3756038/c-sharp-execute-action-after-x-seconds?answertab=active#tab-top
    public static class ActionExtensions
    {
        public static void RunAfter(this Action action, TimeSpan span)
        {
            var dispatcherTimer = new DispatcherTimer {Interval = span};
            dispatcherTimer.Tick += (sender, args) =>
            {
                var timer = sender as DispatcherTimer;
                timer?.Stop();
                action();
            };
            dispatcherTimer.Start();
        }
    }

    //<Namespace>.Utilities
    public static class CommonUtil
    {
        public static void Run(Action action, TimeSpan afterSpan)
        {
            action.RunAfter(afterSpan);
        }
    }
}