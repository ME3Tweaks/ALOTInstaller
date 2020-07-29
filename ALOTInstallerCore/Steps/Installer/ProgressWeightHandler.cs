﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Serilog;

namespace ALOTInstallerCore.Steps.Installer
{
    public class ProgressWeightHandler
    {
        public List<Stage> Stages;
        private double TOTAL_ACTIVE_WEIGHT = 0;
        private List<MutableKeyValuePair<int, double>> jobWeightList = new List<MutableKeyValuePair<int, double>>();

        private int OVERALL_PROGRESS = -1;

        public void ClearTasks()
        {
            TOTAL_ACTIVE_WEIGHT = 0;
            jobWeightList.Clear();
        }

        /// <summary>
        /// Adds a task to the progress tracker. These tasks must be submitted in the order that the program will execute them in. Tasks add to the weight pool and will allocate a progress slot.
        /// </summary>
        /// <param name="task">Name of stage.</param>
        public void AddTask(string stagename, int game = 0)
        {
            Stage pw = Stages.FirstOrDefault(x => x.StageName == stagename);
            if (pw != null)
            {
                pw.reweightStageForGame(game);
                jobWeightList.Add(new MutableKeyValuePair<int, double>(0, pw.Weight));
                TOTAL_ACTIVE_WEIGHT += pw.Weight;
            }
            else
            {
                Log.Error("Error adding stage for progress: " + stagename + ". Could not find stage in weighting system.");
            }
        }

        /// <summary>
        /// Submits a new progress value to the weighted percent class
        /// </summary>
        /// <param name="stage">Which stage (index of weight list) that the progress should be assigned to</param>
        /// <param name="newProgressValue">The new value of progress</param>
        /// <returns>Newly calculated overall progress integer (from 0-100, rounded down).</returns>
        public int SubmitProgress(int stage, int newProgressValue)
        {
            //if (newProgressValue == 100)
            //    Debug.WriteLine("BREAK");
            stage--; //stages are the display value so they will start at 1.
            if (stage < 0 || stage >= jobWeightList.Count)
            {
                return 0;
            }
            //Log.Information("Setting progress for stage " + (stage+1) + " to " + newProgressValue + ". Overall progress for this task accounts for " + (jobWeightList[stage].Key * jobWeightList[stage].Value));
            jobWeightList[stage].Key = newProgressValue;
            return GetOverallProgress();
        }

        /// <summary>
        /// Call this method before your first progress submission. This will update the weights so they all add up to 1.
        /// </summary>
        public void ScaleWeights()
        {
            TOTAL_ACTIVE_WEIGHT = 0;
            //recalculate total weight
            foreach (MutableKeyValuePair<int, double> job in jobWeightList)
            {
                TOTAL_ACTIVE_WEIGHT += job.Value;
            }
            //calculate each job's value
            foreach (MutableKeyValuePair<int, double> job in jobWeightList)
            {
                job.Value = job.Value / TOTAL_ACTIVE_WEIGHT;
            }
        }

        public int GetOverallProgress()
        {
            double currentFinishedWeight = 0;
            foreach (MutableKeyValuePair<int, double> job in jobWeightList)
            {
                currentFinishedWeight += job.Key * job.Value; //progress * weight
            }
            if (TOTAL_ACTIVE_WEIGHT > 0)
            {
                int progress = (int)currentFinishedWeight;
                if (OVERALL_PROGRESS != progress)
                {
                    Log.Information("Overall Progress: " + progress + "%");
                    OVERALL_PROGRESS = progress;
                }
                return progress;
            }
            return 0;
        }

        internal void ScaleCurrentTaskWeight(int index, double scale)
        {
            jobWeightList[index].Value *= scale;
            ScaleWeights();
        }

        internal void SetDefaultWeights()
        {
            Stages = new List<Stage>();
            Stages.Add(new Stage()
            {
                StageName = "STAGE_PRESCAN"
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_UNPACKDLC",
                Weight = 0.11004021318
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_SCAN",
                Weight = 0.12055272684
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_REMOVE",
                Weight = 0.19155062326,
                ME1Scaling = 2.3
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_INSTALL",
                Weight = 0.31997680866
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_SAVE",
                Weight = 0.25787962804
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_REPACK",
                Weight = 0.0800000
            });
            Stages.Add(new Stage()
            {
                StageName = "STAGE_INSTALLMARKERS",
                Weight = 0.0400000
            });
        }
    }

    public class MutableKeyValuePair<TKey, TValue>
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }

        public MutableKeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}
