using System.Threading;

namespace AlotAddOnGUI
{
    internal class ThreadCommandDialogOptions
    {
        public EventWaitHandle signalHandler;
        public string title;
        public string message;
        public string AffirmativeButtonText;
        public string NegativeButtonText;

    }
}