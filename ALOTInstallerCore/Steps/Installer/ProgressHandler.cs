using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ALOTInstallerCore.Helpers;
using ALOTInstallerCore.Objects;
using Serilog;

namespace ALOTInstallerCore.Steps.Installer
{
    /// <summary>
    /// Handles progress for the installation step
    /// </summary>
    public class ProgressHandler : INotifyPropertyChanged
    {
        /// <summary>
        /// The list of all default stages. This information is populated from the manifest and is copied into a local progress handler object
        /// </summary>
        public static List<Stage> DefaultStages = new List<Stage>();

        /// <summary>
        /// Stages that applicable for this handler
        /// </summary>
        public ObservableCollectionExtended<Stage> Stages { get; } = new ObservableCollectionExtended<Stage>();
        private double TOTAL_ACTIVE_WEIGHT => Stages.Sum(x => x.Weight);
        public int TOTAL_PROGRESS { get; private set; } = -1;
        public Stage CurrentStage { get; private set; }


        /// <summary>
        /// Adds a task to the progress tracker. These tasks must be submitted in the order that the program will execute them in. Tasks add to the weight pool and will allocate a progress slot.
        /// </summary>
        /// <param name="stagename">Name of stage.</param>
        public void AddStage(string stagename, Enums.MEGame game = Enums.MEGame.Unknown)
        {
            Stage pw = DefaultStages.FirstOrDefault(x => x.StageName == stagename);
            if (pw != null)
            {
                // Generate a local stage based on the global default ones.
                Stage localStage = new Stage(pw)
                {
                    StageUIIndex = Stages.Count + 1 //+1 cause these are only really used for the UI
                };
                pw.reweightStageForGame(game);
                Stages.Add(localStage);
            }
            else
            {
                Log.Error("Error adding stage for progress: " + stagename + ". Could not find stage in weighting system.");
            }
        }

        /// <summary>
        /// Marks the current stage as completed (100% progress) and moves to the next stage, as specified by the stage name. Returns true if the stage transition is the indicator of completion.
        /// </summary>
        /// <param name="stageName"></param>
        public bool CompleteAndMoveToStage(string stageName)
        {
            if (stageName == "STAGE_DONE")
            {
                // We've finished!
                if (CurrentStage != null)
                {
                    CurrentStage.Progress = 100;
                }

                return true;
            }
            Log.Information("Transitioning to " + stageName);
            if (CurrentStage != null)
            {
                CurrentStage.Progress = 100;
            }

            CurrentStage = Stages.FirstOrDefault(x => x.StageName == stageName);
            if (CurrentStage == null)
            {
                Log.Error("Unknown stage: " + stageName);
            }

            return false;
        }

        /// <summary>
        /// Submits a new progress value for the current stage.
        /// </summary>
        /// <param name="newProgressValue">The new value of progress</param>
        /// <returns>Newly calculated overall progress integer (from 0-100, rounded down).</returns>
        public int SubmitProgress(int newProgressValue)
        {
            if (CurrentStage != null)
            {
                CurrentStage.Progress = newProgressValue;
            }

            return GetOverallProgress();
        }

        /// <summary>
        /// Call this method before your first progress submission. This will update the weights so they all add up to 1.
        /// </summary>
        public void ScaleWeights()
        {
            //recalculate total weight
            //foreach (MutableKeyValuePair<int, double> job in jobWeightList)
            //{
            //    TOTAL_ACTIVE_WEIGHT += job.Value;
            //}
            ////calculate each job's value
            //foreach (MutableKeyValuePair<int, double> job in jobWeightList)
            //{
            //    job.Value = job.Value / TOTAL_ACTIVE_WEIGHT;
            //}
        }

        /// <summary>
        /// Gets the total percentage done. The value returned will be between 0 and 100, rounded
        /// </summary>
        /// <returns></returns>
        public int GetOverallProgress()
        {
            double currentFinishedWeight = Stages.Sum(x => x.Weight * x.Progress) / 100.0; //progress is 0 to 100 so we must divide by 100 to get accurate result
            double totalWeight = Stages.Sum(x => x.Weight);
            if (totalWeight > 0)
            {
                // have to cast this...? what?
                return (int)Math.Round((currentFinishedWeight * 100 / totalWeight));
            }
            return 0;
        }

        internal void ScaleStageWeight(string stagename, double scale)
        {
            var stage = Stages.FirstOrDefault(x => x.StageName == stagename);
            if (stage != null)
            {
                stage.Weight *= scale;
            }
            ScaleWeights();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static void UseBuiltinDefaultStages()
        {
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_PRESCAN"
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_UNPACKDLC",
                Weight = 0.11004021318
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_SCAN",
                Weight = 0.12055272684
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_REMOVE",
                Weight = 0.19155062326,
                ME1Scaling = 2.3
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_INSTALL",
                Weight = 0.31997680866
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_SAVE",
                Weight = 0.25787962804
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_REPACK",
                Weight = 0.0800000
            });
            DefaultStages.Add(new Stage()
            {
                StageName = "STAGE_INSTALLMARKERS",
                Weight = 0.0400000
            });
        }
    }
}
