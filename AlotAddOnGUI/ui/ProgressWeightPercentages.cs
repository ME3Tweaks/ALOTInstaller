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
        private const double WEIGHT_UNPACKED = 0.11004021318;
        private const double WEIGHT_SCAN = 0.12055272684;
        private const double WEIGHT_REMOVE = 0.19155062326;
        private const double WEIGHT_INSTALL = 0.31997680866;
        private const double WEIGHT_SAVE = 0.25787962804;
        private const double WEIGHT_REPACK = 0.0800000;
        private const double WEIGHT_INSTALLMARKERS = 0.0400000;

        public const int JOB_UNPACK = 0;
        public const int JOB_SCAN = 1;
        public const int JOB_REMOVE = 2;
        public const int JOB_INSTALLMARKERS = 3;
        public const int JOB_INSTALL = 4;
        public const int JOB_SAVE = 5;
        public const int JOB_REPACK = 6;

        private static double TOTAL_ACTIVE_WEIGHT = 0;
        private static List<MutableKeyValuePair<int, double>> jobWeightList = new List<MutableKeyValuePair<int, double>>();
        public static void ClearTasks()
        {
            TOTAL_ACTIVE_WEIGHT = 0;
            jobWeightList.Clear();
        }

        /// <summary>
        /// Adds a task to the progress tracker. These tasks must be submitted in order that the program will execute them in. Tasks add to the weight pool and will allocate a progress slot.
        /// </summary>
        /// <param name="task">Job Type. Use one of this classes constants.</param>
        public static void AddTask(int task)
        {
            switch (task)
            {
                case JOB_UNPACK:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_UNPACKED));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_UNPACKED;
                    break;
                case JOB_SCAN:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_SCAN));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_SCAN;
                    break;
                case JOB_REMOVE:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_REMOVE));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_REMOVE;
                    break;
                case JOB_INSTALL:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_INSTALL));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_INSTALL;
                    break;
                case JOB_INSTALLMARKERS:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_INSTALLMARKERS));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_INSTALLMARKERS;
                    break;
                case JOB_SAVE:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_SAVE));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_SAVE;
                    break;
                case JOB_REPACK:
                    jobWeightList.Add(new MutableKeyValuePair<int, double>(0, WEIGHT_REPACK));
                    TOTAL_ACTIVE_WEIGHT += WEIGHT_REPACK;
                    break;
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
            Log.Information("Setting progress for stage " + (stage+1) + " to " + newProgressValue + ". Overall progress for this task accounts for " + (jobWeightList[stage].Key * jobWeightList[stage].Value));
            jobWeightList[stage].Key = newProgressValue;
            return GetOverallProgress();
        }

        /// <summary>
        /// Call this method before your first progress submission. This will update the weights so they all add up to 1.
        /// </summary>
        public static void ScaleWeights()
        {
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
                Log.Information("Overall Progress: " + progress);
                return progress;
            }
            return 0;
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
