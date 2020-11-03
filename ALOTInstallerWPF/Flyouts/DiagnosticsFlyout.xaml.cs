using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.ME3Tweaks;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.Helpers;
using ALOTInstallerWPF.Objects;
using ME3ExplorerCore.Packages;
using Serilog;
using Path = System.IO.Path;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for DiagnosticsFlyout.xaml
    /// </summary>
    public partial class DiagnosticsFlyout : UserControl, INotifyPropertyChanged
    {
        #region chosen options
        private bool? IsBothChosen = null;
        private MEGame? GameChosen = null;
        private LogItem LogChosen;
        private bool FullDiagChosen;
        #endregion

        /// <summary>
        /// Controls what page of the diagnostics is shown
        /// </summary>
        public int Step { get; set; }

        private const int FIRST_STEP = 0;
        private const int LOG_STEP = 1;
        private const int GAMEDIAG_STEP = 2;
        private const int FULLDIAG_STEP = 3;

        /// <summary>
        /// When this value is assigned to Step it will start the diagnostic.
        /// </summary>
        private const int FINAL_STEP = 10;

        public LogItem UISelectedLogItem { get; set; }
        public bool DiagnosticComplete { get; set; }
        public bool DiagnosticInProgress { get; set; }
        public bool ProgressIndeterminate { get; set; }
        public long ProgressValue { get; set; }
        /// <summary>
        /// Result of the diagnostic. This on success will be a link, on failure will be text (not a link)
        /// </summary>
        public string DiagnosticResultText { get; set; }
        public string DiagnosticStatusText { get; set; }
        public string LogSelectorWatermark { get; set; }

        public void OnUISelectedLogItemChanged()
        {
            if (UISelectedLogItem == null)
            {
                LogSelectorWatermark = "No log selected";
                return;
            }

            if (LogFiles.IndexOf(UISelectedLogItem) == 0)
            {
                LogSelectorWatermark = "Latest log";
                return;
            }

            LogSelectorWatermark = "Older log";
        }

        public DiagnosticsFlyout()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public RelayCommand IssueButtonCommand { get; set; }
        public RelayCommand SelectGameCommand { get; set; }
        public GenericCommand ContinueFromLogSelectorCommand { get; set; }
        public GenericCommand BackCommand { get; set; }
        public GenericCommand CopyLinkCommand { get; set; }
        public GenericCommand CloseDiagnosticsPanel { get; set; }
        public GenericCommand ViewLogCommand { get; set; }
        public RelayCommand SetFullTextureCheckCommand { get; set; }

        public ObservableCollectionExtended<LogItem> LogFiles { get; } = new ObservableCollectionExtended<LogItem>();

        private void LoadCommands()
        {
            IssueButtonCommand = new RelayCommand(SelectIssueType, CanSelectIssueType);
            SelectGameCommand = new RelayCommand(SelectGameForDiag, CanSelectGame);
            ContinueFromLogSelectorCommand = new GenericCommand(ContinuePastLogStep, CanContinueFromLogStep);
            BackCommand = new GenericCommand(GoBack, () => Step > 0 && Step != FINAL_STEP);
            CloseDiagnosticsPanel = new GenericCommand(CloseFlyout, () => !DiagnosticInProgress);
            SetFullTextureCheckCommand = new RelayCommand(ContinuePastFullTextureCheckStep);
            CopyLinkCommand = new GenericCommand(CopyLink);
            ViewLogCommand = new GenericCommand(ViewLink, LinkIsValid);
        }

        private void ViewLink()
        {
            if (DiagnosticResultText.StartsWith("http"))
                Utilities.OpenWebPage(DiagnosticResultText);
        }

        private void CopyLink()
        {
            try
            {
                Clipboard.SetText(DiagnosticResultText);
            }
            catch (Exception e)
            {
                Log.Error($"Can't set text to clipboard: {e.Message}");
            }
        }

        private bool LinkIsValid()
        {
            return DiagnosticResultText != null && DiagnosticResultText.StartsWith("http");
        }

        private void ContinuePastFullTextureCheckStep(object obj)
        {
            if (obj is string str && bool.TryParse(str, out var hasFullTextureCheck))
            {
                FullDiagChosen = hasFullTextureCheck;
                Step = FINAL_STEP; // Full texture check is last step before we upload
            }
        }


        private void CloseFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.DiagnosticsOpen = false;
                CommonUtil.Run(() => ResetDiagnostics(), TimeSpan.FromSeconds(1.5));
            }
        }

        private void ResetDiagnostics()
        {
            UISelectedLogItem = null;
            LogChosen = null;
            DiagnosticResultText = null;
            Step = 0;
            DiagnosticInProgress = DiagnosticComplete = false;
            LogFiles.ClearEx();
            IsBothChosen = false;
            FullDiagChosen = false;
            GameChosen = null;
            ProgressValue = 0;
            ProgressIndeterminate = true;
        }

        private void GoBack()
        {
            if (Step == LOG_STEP)
            {
                IsBothChosen = null;
                Step = FIRST_STEP;
            }
            else if (Step == GAMEDIAG_STEP)
            {
                if (IsBothChosen.HasValue && IsBothChosen.Value)
                {
                    // Is Both Chosen -> Back to log
                    LogChosen = null;
                    Step = LOG_STEP;
                }
                else
                {
                    // Back to start
                    IsBothChosen = null;
                    Step = FIRST_STEP;
                }
            }
            else if (Step == FULLDIAG_STEP)
            {
                FullDiagChosen = false; //Unselect
                Step = GAMEDIAG_STEP;
            }
        }


        private bool CanContinueFromLogStep() => UISelectedLogItem != null;

        private void ContinuePastLogStep()
        {
            LogChosen = UISelectedLogItem;
            if (IsBothChosen.HasValue && IsBothChosen.Value)
            {
                Step = GAMEDIAG_STEP;
            }
            else
            {
                Step = FINAL_STEP;
            }
        }

        private void SelectGameForDiag(object obj)
        {
            if (obj is string str && Enum.TryParse<MEGame>(str, out var game))
            {
                GameChosen = game;
                Step = FULLDIAG_STEP;
            }
        }

        private bool CanSelectGame(object obj) => obj is string str && Enum.TryParse<MEGame>(str, out var game) && Locations.GetTarget(game) != null;

        private bool CanSelectIssueType(object obj)
        {
            if (obj is string str)
            {
                if (str == "Both" || str == "Game")
                {
                    return Locations.GetAllAvailableTargets().Any(); //If no game don't allow user to choose 
                }

                return true;
            }

            return false;
        }

        private void SelectIssueType(object obj)
        {
            if (obj is string str)
            {
                switch (str)
                {
                    case "Installer":
                        IsBothChosen = false;
                        Step = LOG_STEP;
                        return;
                    case "Game":
                        IsBothChosen = false;
                        Step = GAMEDIAG_STEP;
                        return;
                    case "Both":
                        IsBothChosen = true;
                        Step = LOG_STEP;
                        return;

                }
            }
        }

        public void OnStepChanged()
        {
            UISelectedLogItem = null;
            if (Step == LOG_STEP)
            {
                ReloadLogsList();
            }
            else if (Step == FINAL_STEP)
            {
                // START DIAGNOSTIC
                StartDiagnostic();
            }
        }

        private async void StartDiagnostic()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                NamedBackgroundWorker nbw = new NamedBackgroundWorker("DiagnosticsWorker");
                nbw.DoWork += (a, b) =>
                {
                    ProgressIndeterminate = true;
                    GameTarget target = GameChosen != null ? Locations.GetTarget(GameChosen.Value) : null;
                    StringBuilder logUploadText = new StringBuilder();

                    string logText = "";
                    if (target != null)
                    {
                        logUploadText.Append("[MODE]diagnostics\n"); //do not localize
                        logUploadText.Append(LogCollector.PerformDiagnostic(target, FullDiagChosen,
                                x => DiagnosticStatusText = x,
                                x =>
                                {
                                    ProgressIndeterminate = false;
                                    ProgressValue = x;
                                },
                                () => ProgressIndeterminate = true));
                        logUploadText.Append("\n"); //do not localize
                    }

                    if (LogChosen != null)
                    {
                        logUploadText.Append("[MODE]logs\n"); //do not localize
                        logUploadText.AppendLine(LogCollector.CollectLogs(LogChosen.FilePath));
                        logUploadText.Append("\n"); //do not localize
                    }

                    DiagnosticStatusText = "Uploading to log viewing service";
                    ProgressIndeterminate = true;
                    var response = LogUploader.UploadLog(logUploadText.ToString(), "https://me3tweaks.com/alot/logupload3");

                    DiagnosticResultText = response;
                    if (response.StartsWith("http"))
                    {
                        Utilities.OpenWebPage(response);
                    }
                    DiagnosticComplete = true;
                    DiagnosticInProgress = false;
                };
                DiagnosticInProgress = true;
                nbw.RunWorkerAsync();
            }

        }

        private void ReloadLogsList()
        {
            var directory = new DirectoryInfo(LogCollector.LogDir);
            LogFiles.ReplaceAll(directory.GetFiles("*.txt").OrderByDescending(f => f.LastWriteTime).Select(x => new LogItem(x.FullName)));
            UISelectedLogItem = LogFiles.FirstOrDefault();
        }

        public class LogItem
        {
            public string FilePath { get; set; }
            public string ShortName { get; set; }
            public string FileSize { get; set; }

            public LogItem(string filepath)
            {
                this.FilePath = filepath;
                ShortName = Path.GetFileName(FilePath);
                FileSize = FileSizeFormatter.FormatSize(new FileInfo(FilePath).Length);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
