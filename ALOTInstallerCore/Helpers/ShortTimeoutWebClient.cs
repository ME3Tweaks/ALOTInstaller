using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ALOTInstallerCore.Helpers
{
    [System.ComponentModel.DesignerCategory("Code")]
    public class ShortTimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = 5000; //short timeout
            return w;
        }
    }
}
