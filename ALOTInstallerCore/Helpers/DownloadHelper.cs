using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Threading;

namespace ALOTInstallerCore.Helpers
{
    public class DownloadHelper
    {
        /// <summary>
        /// Asynchronously downloads a file, but blocks the calling thread until the download completes. This will allow you to subscribe to the progress notification
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="destination"></param>
        /// <param name="progressChanged"></param>
        public static void DownloadFile(Uri uri, string destination, Action<long, long> progressChanged = null)
        {
            void HandleDownloadComplete(object sender, AsyncCompletedEventArgs args)
            {
                lock (args.UserState)
                {
                    //releases blocked thread
                    Monitor.Pulse(args.UserState);
                }
            }


            void HandleDownloadProgress(object sender, DownloadProgressChangedEventArgs args)
            {
                //Process progress updates here
                progressChanged?.Invoke(args.BytesReceived, args.TotalBytesToReceive);
            }

            using (var wc = new WebClient())
            {
                wc.DownloadProgressChanged += HandleDownloadProgress;
                wc.DownloadFileCompleted += HandleDownloadComplete;
                var syncObject = new Object();
                lock (syncObject)
                {
                    wc.DownloadFileAsync(uri, destination, syncObject);
                    //This would block the thread until download completes
                    Monitor.Wait(syncObject);
                }
            }
        }
    }
}
