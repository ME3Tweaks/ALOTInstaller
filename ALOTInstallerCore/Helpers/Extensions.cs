using System;
using System.Collections.Generic;
using System.Text;

namespace ALOTInstallerCore.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <returns>Printable string</returns>
        public static string Flatten(this Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }
}
