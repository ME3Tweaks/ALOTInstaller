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
        public ProgressHandlerDel progressDelegate;
        public void DownloadFile(string source, string destination, ProgressHandlerDel progressDelegate)
        {
            this.progressDelegate = progressDelegate;
            using (var wc = new WebClient())
            {
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
                //releases blocked thread
                Monitor.Pulse(e.UserState);
            }
        }

        public void HandleDownloadProgress(object sender, DownloadProgressChangedEventArgs args)
        {
            //Process progress updates here
            progressDelegate(args);
        }
    }
}
