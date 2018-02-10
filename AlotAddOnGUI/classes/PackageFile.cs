using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    public class PackageFile
    {
        public string SourceName { get; set; }
        public string DestinationName { get; set; }
        public bool MoveDirectly { get; set; }
        public bool CopyDirectly { get; set; }
        public bool Delete { get; set; }
        public string TPFSource { get; set; }
        public bool ME1 { get; set; }
        public bool ME2 { get; set; }
        public bool ME3 { get; set; }
        public bool Processed { get; set; }
        public string ChoiceTitle { get; internal set; }

        internal bool AppliesToGame(int game)
        {
            if (game == 1)
            {
                return ME1;
            }
            if (game == 2)
            {
                return ME2;
            }
            if (game == 3)
            {
                return ME3;
            }
            return false;
        }
    }
}
