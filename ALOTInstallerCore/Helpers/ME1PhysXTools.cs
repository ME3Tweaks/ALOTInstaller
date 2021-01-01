using System.IO;
using ALOTInstallerCore.ModManager.Objects;
using Serilog;

namespace ALOTInstallerCore.Helpers
{
    public class ME1PhysXTools
    {
        /// <summary>
        /// Patches the ME1 PhysxLoader to always use local. This will make it so ME1 will run regardless if it can access the registry or not with the enableLocalPhysXCore key.
        /// </summary>
        /// <param name="me1Target"></param>
        /// <returns></returns>
        public static bool PatchPhysXLoaderME1(GameTarget me1Target)
        {
            Log.Information(@"[AICORE] Patching PhysXLoader.dll to force loading local PhysXCore");
            var loaderPath = Path.Combine(me1Target.TargetPath, @"Binaries", @"PhysXLoader.dll");
            if (File.Exists(loaderPath) && new FileInfo(loaderPath).Length == 68688) //Make sure it's same size so it's not like some other build
            {
                using var pls = File.Open(loaderPath, FileMode.Open, FileAccess.ReadWrite);
                pls.Seek(0x1688, SeekOrigin.Begin);

                var jzByte1 = pls.ReadByte();
                var jzByte2 = pls.ReadByte();
                Log.Information($@"[AICORE] Byte 1 @ 0x1688: 0x{jzByte1:X2}");
                Log.Information($@"[AICORE] Byte 2 @ 0x1689: 0x{jzByte2:X2}");
                if (jzByte1 == 0x75 && jzByte2 == 0x19)
                {
                    // It's a jz instruction. Change to nop
                    Log.Information(@"[AICORE] This file is has the original PhysXLoader.dll jump instruction for allowing system PhysX. Patching out to force local PhysX codepath");
                    pls.Seek(-2, SeekOrigin.Current);
                    pls.WriteByte(0x90); //nop
                    pls.WriteByte(0x90); //nop
                    Log.Information(@"[AICORE] PhysXLoader.dll has been patched");
                }
                else if (jzByte1 == 0x90 && jzByte2 == 0x90)
                {
                    Log.Information(@"[AICORE] This file appears to have already been patched to force local use of PhysX. Not patching file.");
                }
                else
                {
                    Log.Warning(@"[AICORE] Bytes are not expected values. We will not patch this file.");
                    return false;
                }
            }
            return true;
        }

        public static bool IsPhysXLoaderPatchedLocalOnly(GameTarget me1Target)
        {
            Log.Information(@"[AICORE] Checking if PhysXLoader.dll is patched for local only");
            var loaderPath = Path.Combine(me1Target.TargetPath, @"Binaries", @"PhysXLoader.dll");
            if (File.Exists(loaderPath) && new FileInfo(loaderPath).Length == 68688) //Make sure it's same size so it's not like some other build
            {
                using var pls = File.Open(loaderPath, FileMode.Open, FileAccess.Read);
                pls.Seek(0x1688, SeekOrigin.Begin);

                var jzByte1 = pls.ReadByte();
                var jzByte2 = pls.ReadByte();
                return jzByte1 == 0x90 && jzByte2 == 0x90;
            }

            return false; // File doesn't exist or is wrong size
        }
    }
}