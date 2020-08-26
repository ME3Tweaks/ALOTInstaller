using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ALOTInstallerCore;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.ModManager.Objects;
using ALOTInstallerCore.Objects;
using ALOTInstallerWPF.Objects;

namespace ALOTInstallerWPF.Flyouts
{
    /// <summary>
    /// Interaction logic for LODSwitcherFlyout.xaml
    /// </summary>
    public partial class LODSwitcherFlyout : UserControl, INotifyPropertyChanged
    {
        public bool ShowMoreInfo { get; set; }
        public LODSwitcherFlyout()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }
        public GenericCommand CloseFlyoutCommand { get; set; }
        public GenericCommand ShowMoreInfoCommand { get; set; }

        private void LoadCommands()
        {
            ShowMoreInfoCommand = new GenericCommand(() => ShowMoreInfo = true);
            CloseFlyoutCommand = new GenericCommand(CloseFlyout);
        }


        private void CloseFlyout()
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.LODSwitcherOpen = false;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollectionExtended<LODGame> LODGames { get; } = new ObservableCollectionExtended<LODGame>();

        public void UpdateGameStatuses()
        {
            LODGames.ClearEx();
            NamedBackgroundWorker nbw = new NamedBackgroundWorker("LODFetcherThread");
            nbw.DoWork += (sender, args) =>
            {
                List<LODGame> lgames = new List<LODGame>();
                foreach (var target in Locations.GetAllAvailableTargets())
                {
                    lgames.Add(new LODGame(target));
                }
                args.Result = lgames;
            };
            nbw.RunWorkerCompleted += (sender, args) =>
            {
                if (args.Error == null && args.Result is List<LODGame> lgs)
                {
                    LODGames.ReplaceAll(lgs);
                    CommandManager.InvalidateRequerySuggested();
                }
            };
            nbw.RunWorkerAsync();
        }

        public class LODGame : INotifyPropertyChanged
        {
            public string TexturesInstalledString { get; }
            public Enums.MEGame Game { get; set; }
            public LodSetting CurrentSetting { get; set; }
            public bool ShowHigherLODs { get; private set; }
            public ObservableCollectionExtended<LodSetting> AvailableSettings { get; set; }
            public RelayCommand ApplyLODSettingCommand { get; }
            public bool ApplyingLODs { get; set; }

            public LODGame(GameTarget target)
            {
                Game = target.Game;
                var availableLODs = LODHelper.GetAvailableLODs(target);
                ShowHigherLODs = target.GetInstalledALOTInfo() != null;
                TexturesInstalledString = ShowHigherLODs ? "Texture mod installed" : "Texture mod not installed";
                ApplyLODSettingCommand = new RelayCommand(applyLODSetting, canExecute: canApplyLODLevel);
                refreshLODSetting();
                AvailableSettings = new ObservableCollectionExtended<LodSetting>(availableLODs.Select(x => x.Item2));
            }

            private bool canApplyLODLevel(object obj)
            {
                if (ApplyingLODs) return false;
                if (obj is string str && Enum.TryParse<LodSetting>(str, out var ls))
                {
                    if ((ls == LodSetting.TwoK || ls == LodSetting.FourK) && !ShowHigherLODs)
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }

            private void refreshLODSetting()
            {
                var lods = MEMIPCHandler.GetLODs(Game);
                CurrentSetting = lods == null ? LodSetting.Vanilla : LODHelper.GetLODSettingFromLODs(Game, lods);
            }

            private async void applyLODSetting(object obj)
            {
                if (obj is string str && Enum.TryParse<LodSetting>(str, out var ls))
                {
                    if (Game == Enums.MEGame.ME1)
                    {
                        var target = Locations.GetTarget(Enums.MEGame.ME1);
                        if (target.GetInstalledALOTInfo()?.MEUITMVER > 0)
                        {
                            //detect soft shadows/meuitm
                            var branchingPCFCommon = Path.Combine(target.TargetPath, @"Engine", @"Shaders", @"BranchingPCFCommon.usf");
                            if (File.Exists(branchingPCFCommon))
                            {
                                if (Utilities.CalculateMD5(branchingPCFCommon) == @"10db76cb98c21d3e90d4f0ffed55d424")
                                {
                                    ls |= LodSetting.SoftShadows;
                                }
                            }
                        }
                    }
                    
                    NamedBackgroundWorker nbw = new NamedBackgroundWorker("LODSetterThread");
                    nbw.DoWork += (sender, args) =>
                    {
                        MEMIPCHandler.SetLODs(Game, ls);
                        refreshLODSetting();
                    };
                    nbw.RunWorkerCompleted += (sender, args) =>
                    {
                        ApplyLODSettingCommand.RaiseCanExecuteChanged();
                        ApplyingLODs = false;
                    };
                    ApplyingLODs = true;
                    nbw.RunWorkerAsync();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
