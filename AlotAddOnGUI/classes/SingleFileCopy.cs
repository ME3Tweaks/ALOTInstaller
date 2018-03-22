using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class SingleFileCopy
    {
        public delegate void ProgressHandlerDel(DownloadProgressChangedEventArgs args);
        private ProgressHandlerDel progressDelegate;

        public delegate void ProgressCompleteDel(AsyncCompletedEventArgs args);
        private ProgressCompleteDel completedDelegate;

        public void DownloadFile(string source, string destination, ProgressHandlerDel progressDelegate, ProgressCompleteDel completedDelegate)
        {
            this.progressDelegate = progressDelegate;
            this.completedDelegate = completedDelegate;

            using (var wc = new WebClient())
            {
                wc.Headers["user-agent"] = "ALOTInstaller";
                wc.Proxy = null;
                wc.DownloadProgressChanged += HandleDownloadProgress;
                wc.DownloadFileCompleted += HandleDownloadComplete;

                var syncObject = new Object();
                lock (syncObject)
                {
                    wc.DownloadFileAsync(new Uri(source), destination, syncObject);
                    //This would block the thread until download completes
                    Monitor.Wait(syncObject);
                }
            }

            //Do more stuff after download was complete
        }

        public void HandleDownloadComplete(object sender, AsyncCompletedEventArgs e)
        {
            lock (e.UserState)
            {
                if (completedDelegate != null)
                {
                    completedDelegate(e);
                }
                //releases blocked thread
                Monitor.Pulse(e.UserState);
            }
        }

        public void HandleDownloadProgress(object sender, DownloadProgressChangedEventArgs args)
        {
            //Process progress updates here
            if (progressDelegate != null)
            {
                progressDelegate(args);
            }
        }
    }
}
