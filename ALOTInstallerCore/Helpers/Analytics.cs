using System;
using System.Collections.Generic;
using System.Text;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling analytics data. These callbacks must be set or they won't do anything. This library does not natively set these, the hosting process must set them
    /// </summary>
    public static class Analytics
    {
        /// <summary>
        /// Tracks a piece of telemetry data through the hosting process' telemetry provider, if any
        /// </summary>
        public static Action<string, Dictionary<string, string>> TrackEvent { get; set; }
        
    }

    /// <summary>
    /// Class for handling exception tracking. These callbacks must be set or they won't do anything. This library does not natively set these, the hosting process must set them
    /// </summary>
    public static class Crashes
    {
        /// <summary>
        /// Tracks an exception through the hosting process' telemetry provider, if any
        /// </summary>
        public static Action<Exception, Dictionary<string,string>> TrackError { get; set; }
    }
}
