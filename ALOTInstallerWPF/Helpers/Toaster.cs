using System;
using System.Collections.Generic;
using System.Text;
using Notifications.Wpf.Core;

namespace ALOTInstallerWPF.Helpers
{
    public static class Toaster
    {
        private static NotificationManager notificationManager;

        /// <summary>
        /// Shows a toast notification with the specified title/message and an optional time.
        /// </summary>
        /// <param name="title">Title of the toast</param>
        /// <param name="message">Message of the toast</param>
        /// <param name="time">How long the toast should last, in seconds. The default is 10 seconds.</param
        /// <param name="notificationType">The type of notification to show. The default is Information.</param>
        public static async void ShowNotification(string title, string message, int time = 10, NotificationType notificationType = NotificationType.Information)
        {
            if (notificationManager == null)
                notificationManager = new NotificationManager();

            await notificationManager.ShowAsync(new NotificationContent
            {
                Title = title,
                Message = message,
                Type = notificationType
            }, expirationTime: TimeSpan.MaxValue);
        }

    }
}
