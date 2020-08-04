using System.ComponentModel;
using System.Threading;

namespace ALOTInstallerCore.Helpers
{
    [System.ComponentModel.DesignerCategory("Code")]
    public class NamedBackgroundWorker : BackgroundWorker
    {
        public NamedBackgroundWorker(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            if (Thread.CurrentThread.Name == null) // Can only set it once
                Thread.CurrentThread.Name = Name;

            base.OnDoWork(e);
        }
    }
}
