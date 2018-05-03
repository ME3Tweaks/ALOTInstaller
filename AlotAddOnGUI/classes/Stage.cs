using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    [DebuggerDisplay("{StageName} ME1Scaling = {ME1Scaling}, ME2Scaling = {ME2Scaling}, ME3Scaling = {ME3Scaling}, Weight: {Weight}")]
    public class Stage
    {
        public double ME1Scaling = 1;
        public double ME2Scaling = 1;
        public double ME3Scaling = 1;
        public double Weight = 0;
        public string StageName = "STAGE_PLACEHOLDER"; //Used by IPC
        public string TaskName = "Unknown task"; //Removing empty mipmaps
        public List<StageFailure> FailureInfos;

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
        public StageFailure getDefaultFailure()
        {
            return FailureInfos.Where(x => x.FailureIPCTrigger == null).FirstOrDefault();
        }
    }

    public class StageFailure
    {
        public string FailureHeaderText;
        public string FailureTopText;
        public string FailureBottomText;
        public string FailureIPCTrigger;
        public int FailureResultCode;
    }
}
