using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ALOTInstallerCore.Steps.Installer
{
    [DebuggerDisplay("{StageName} ME1Scaling = {ME1Scaling}, ME2Scaling = {ME2Scaling}, ME3Scaling = {ME3Scaling}, Weight: {Weight}")]
    public class Stage
    {
        /// <summary>
        /// Scaling of the weight for this stage for ME1
        /// </summary>
        public double ME1Scaling { get; set; } = 1;
        /// <summary>
        /// Scaling of the weight for this stage for ME2
        /// </summary>
        public double ME2Scaling { get; set; } = 1;
        /// <summary>
        /// Scaling of the weight for this stage for ME3
        /// </summary>
        public double ME3Scaling { get; set; } = 1;
        /// <summary>
        /// The 'heaviness' of this task.
        /// </summary>
        public double Weight { get; set; } = 0;
        /// <summary>
        /// The stage indicator that MEM will produce to transition to this stage.
        /// </summary>
        public string StageName { get; set; } = "STAGE_PLACEHOLDER"; //Used by IPC
        /// <summary>
        /// The UI string that the installer shell will display
        /// </summary>
        public string TaskName { get; set; } = "Unknown task"; //Removing empty mipmaps
        /// <summary>
        /// List of failures that can occur at this stage.
        /// </summary>
        public List<StageFailure> FailureInfos { get; set; } = new List<StageFailure>();
        /// <summary>
        /// Index of this stage in the installation. This value is indexed from 0
        /// </summary>
        public int StageIndex { get; set; }
        /// <summary>
        /// The amount of progress this individual stage has accomplished
        /// </summary>
        public int Progress { get; set; }

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

        /// <summary>
        /// Gets the default stage failure information, which is used when MEM exits but we have no IPC trigger saying why it exits, which is almost always a crash.
        /// </summary>
        /// <returns>Default failure info</returns>
        public StageFailure getDefaultFailure()=>FailureInfos.FirstOrDefault(x => x.FailureIPCTrigger == null);

        public Stage()
        {

        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="original"></param>
        public Stage(Stage original) => new Stage()
        {
            ME1Scaling = original.ME1Scaling,
            ME2Scaling = original.ME2Scaling,
            ME3Scaling = original.ME3Scaling,
            Weight = original.ME1Scaling,
            StageName = original.StageName,
            TaskName = original.TaskName,
            FailureInfos = original.FailureInfos //These are never modified so we can just pass them through
        };
    }

    public class StageFailure
    {
        public string FailureHeaderText { get; }
        public string FailureTopText { get; }
        public string FailureBottomText { get; }
        public string FailureIPCTrigger { get; }
        public int FailureResultCode { get; }
        public bool Warning { get; }
    }
}