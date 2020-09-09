using System;
using System.Collections.Generic;

namespace ALOTInstallerCore.Helpers
{
    /// <summary>
    /// Class for handling analytics data. These callbacks must be set or they won't do anything. This library does not natively set these, the hosting process must set them
    /// </summary>
    public static class CoreAnalytics
    {
        /// <summary>
        /// Tracks a piece of telemetry data through the hosting process' telemetry provider, if any
        /// </summary>
        public static Action<string, Dictionary<string, string>> TrackEvent { get; set; }

        public static Action StartTelemetryCallback { get; set; }
        public static Action StopTelemetryCallback { get; set; }
    }

    /// <summary>
    /// Class for handling exception tracking. These callbacks must be set or they won't do anything. This library does not natively set these, the hosting process must set them
    /// </summary>
    public static class CoreCrashes
    {
        /// <summary>
        /// Tracks an exception through the hosting process' telemetry provider, if any. This only takes the exceptions
        /// </summary>
        public static Action<Exception> TrackError { get; set; }
        /// <summary>
        /// Tracks an exception through the hosting process' telemetry provider, if any. This takes the exception and the properties
        /// </summary>
        public static Action<Exception, Dictionary<string, string>> TrackError2 { get; set; }
        /// <summary>
        /// Tracks an exception through the hosting process' telemetry provider, if any. This takes the exception, the properties, and the attachments
        /// </summary>
        public static Action<Exception, Dictionary<string, string>, ErrorAttachmentLog[]> TrackError3 { get; set; }

        public class ErrorAttachmentLog
        {
            public string text;
            public string filename;

            public ErrorAttachmentLog(string text, string filename)
            {
                this.text = text;
                this.filename = filename;
            }

            public static ErrorAttachmentLog AttachmentWithText(string log, string filename)
            {
                return new ErrorAttachmentLog(log, filename);
            }
        }
    }
}
