using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ALOTInstallerCore.Helpers;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace ALOTInstallerWPF.Helpers
{
    /// <summary>
    /// Maps the internal telemetry calls to Microsoft AppCenter calls
    /// </summary>
    public static class TelemetryController
    {
        public static void TrackEvent(string eventName, Dictionary<string, string> eventData)
        {
            Analytics.TrackEvent(eventName, eventData);
        }

        public static void TrackError(Exception exception)
        {
            Crashes.TrackError(exception);
        }

        public static void TrackError2(Exception exception, Dictionary<string, string> data)
        {
            Crashes.TrackError(exception, data);
        }

        public static void TrackError3(Exception exception, Dictionary<string, string> data, CoreCrashes.ErrorAttachmentLog[] attachment)
        {
            Crashes.TrackError(exception, data, attachment.Select(x => ErrorAttachmentLog.AttachmentWithText(x.text, x.filename)).ToArray());
        }
    }
}
