using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class DiskTypeDetector
    {
        public static int GetPartitionDiskBackingType(string partitionLetter)
        {
            using (var partitionSearcher = new ManagementObjectSearcher(
        @"\\localhost\ROOT\Microsoft\Windows\Storage",
        $"SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter='{partitionLetter}'"))
            {
                try
                {
                    var partition = partitionSearcher.Get().Cast<ManagementBaseObject>().Single();
                    using (var physicalDiskSearcher = new ManagementObjectSearcher(
                                @"\\localhost\ROOT\Microsoft\Windows\Storage",
                                $"SELECT Size, Model, MediaType FROM MSFT_PhysicalDisk WHERE DeviceID='{ partition["DiskNumber"] }'"))
                    {
                        var physicalDisk = physicalDiskSearcher.Get().Cast<ManagementBaseObject>().Single();
                        return
                            (UInt16)physicalDisk["MediaType"];/*||
                        SSDModelSubstrings.Any(substring => result.Model.ToLower().Contains(substring)); ;*/


                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error reading partition type on " + partitionLetter + ": " + e.Message);
                    return -1;
                }
            }
        }
    }
}
