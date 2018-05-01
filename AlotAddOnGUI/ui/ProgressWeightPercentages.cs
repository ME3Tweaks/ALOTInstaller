using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.ui
{
    class ProgressWeightPercentages
    {
        public static List<ProgressWeight> Weights;
        public const int JOB_PRESCAN = 7;
        public const int JOB_UNPACK = 0;
        public const int JOB_SCAN = 1;
        public const int JOB_REMOVE = 2;
        public const int JOB_INSTALLMARKERS = 3;
        public const int JOB_INSTALL = 4;
        public const int JOB_SAVE = 5;
        public const int JOB_REPACK = 6;

        private static double TOTAL_ACTIVE_WEIGHT = 0;
        private static List<MutableKeyValuePair<int, double>> jobWeightList = new List<MutableKeyValuePair<int, double>>();

        private static int OVERALL_PROGRESS = -1;

        public static void ClearTasks()
        {
            TOTAL_ACTIVE_WEIGHT = 0;
            jobWeightList.Clear();
        }

        /// <summary>
        /// Adds a task to the progress tracker. These tasks must be submitted in the order that the program will execute them in. Tasks add to the weight pool and will allocate a progress slot.
        /// </summary>
        /// <param name="task">Name of stage.</param>
        public static void AddTask(string stagename, int game = 0)
        {
            ProgressWeight pw = Weights.Where(x => x.StageName == stagename).FirstOrDefault();
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
        public static int SubmitProgress(int stage, int newProgressValue)
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
        public static void ScaleWeights()
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

        public static int GetOverallProgress()
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

        internal static void ScaleCurrentTaskWeight(int index, double scale)
        {
            jobWeightList[index].Value *= scale;
            ScaleWeights();
        }

        internal static void SetDefaultWeights()
        {
            Weights = new List<ProgressWeight>();
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_PRESCAN"
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_UNPACKDLC",
                Weight = 0.11004021318
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_SCAN",
                Weight = 0.12055272684
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_REMOVE",
                Weight = 0.19155062326,
                ME1Scaling = 2.3
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_INSTALL",
                Weight = 0.31997680866
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_SAVE",
                Weight = 0.25787962804
            });
            Weights.Add(new ProgressWeight()
            {
                StageName = "STAGE_REPACK",
                Weight = 0.0800000
            });
            Weights.Add(new ProgressWeight()
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

    [DebuggerDisplay("{StageName} ME1Scaling = {ME1Scaling}, ME2Scaling = {ME2Scaling}, ME3Scaling = {ME3Scaling}, Weight: {Weight}")]
    public class ProgressWeight
    {
        public double ME1Scaling = 1;
        public double ME2Scaling = 1;
        public double ME3Scaling = 1;
        public double Weight = 0;
        public string StageName = "Stage";

        public void reweightStageForGame(int game)
        {
            double scalingVal = 1;
            switch (game)
            {
                case 1:
                    scalingVal = ME1Scaling;
                    break;
                case 2:
                    scalingVal = ME2Scaling;
                    break;
                case 3:
                    scalingVal = ME3Scaling;
                    break;
            }
            Weight *= scalingVal;
        }
    }
}
