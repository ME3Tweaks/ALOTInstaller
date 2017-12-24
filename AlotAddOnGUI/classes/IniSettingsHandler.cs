using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlotAddOnGUI.classes
{
    class IniSettingsHandler
    {
        public static string GetConfigIniPath(int game)
        {
            switch (game)
            {
                case 1:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect\Config\BIOEngine.ini";
                case 2:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 2\BIOGame\Config\GamerSettings.ini";
                case 3:
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\BioWare\Mass Effect 3\BIOGame\Config\GamerSettings.ini";
            }
            return null;
        }

        /// <summary>
        /// Updates LOD settings to be high res
        /// </summary>
        /// <param name="gameId">Game to update</param>
        /// <param name="limitME1Lods"></param>
        static public void updateLOD(int gameId, bool limitME1Lods = false)
        {
            string engineConfPath = GetConfigIniPath(gameId);
            if (File.Exists(engineConfPath))
            {
                IniFile engineConf = new IniFile(engineConfPath);
                if (gameId == 1)
                {
                    engineConf.Write("TEXTUREGROUP_World", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_WorldNormalMap", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_AmbientLightMap", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_LightAndShadowMap", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_64", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_128", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_256", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_512", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_1024", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_64", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_128", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_256", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_512", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_1024", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_128", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_256", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_512", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_1024", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_GUI", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Promotional", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    if (limitME1Lods)
                    {
                        engineConf.Write("TEXTUREGROUP_Character_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Diff", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Norm", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Spec", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    }
                    else
                    {
                        engineConf.Write("TEXTUREGROUP_Character_1024", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Diff", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Norm", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                        engineConf.Write("TEXTUREGROUP_Character_Spec", "(MinLODSize=4096,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    }
                }
                else if (gameId == 2)
                {
                    engineConf.Write("TEXTUREGROUP_World", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_WorldNormalMap", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_AmbientLightMap", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_LightAndShadowMap", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_RenderTarget", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_64", "(MinLODSize=128,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_128", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_256", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_512", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_64", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_128", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_256", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_512", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_1024", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_128", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_256", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_512", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_UI", "(MinLODSize=64,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Promotional", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Diff", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Norm", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Spec", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                }
                else if (gameId == 3)
                {
                    engineConf.Write("TEXTUREGROUP_World", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_WorldSpecular", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_WorldNormalMap", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_AmbientLightMap", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_LightAndShadowMap", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_RenderTarget", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_64", "(MinLODSize=128,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_128", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_256", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_512", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_64", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_128", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_256", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_512", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_1024", "(MinLODSize=32,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_128", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_256", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_512", "(MinLODSize=1024,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_APL_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_UI", "(MinLODSize=64,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Promotional", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_1024", "(MinLODSize=2048,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Diff", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Norm", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Spec", "(MinLODSize=512,MaxLODSize=4096,LODBias=0)", "SystemSettings");
                }
                else
                {
                    throw new Exception("Invalid Game ID: " + gameId);
                }
            }
        }

        /// <summary>
        /// Removes LOD settings and reverts them back to normal settings
        /// </summary>
        /// <param name="gameId">Game to revert settings for</param>
        static public void removeLOD(int gameId)
        {
            Log.Information("Reverting LOD settings for Mass Effect " + gameId);
            //string exe = MainWindow.BINARY_DIRECTORY + MainWindow.MEM_EXE_NAME;
            //string args = "-remove-lods " + gameId;
            //Utilities.runProcess(exe, args, true);
            string engineConfPath = GetConfigIniPath(gameId);
            if (File.Exists(engineConfPath))
            {
                IniFile engineConf = new IniFile(engineConfPath);

                if (gameId == 1)
                {
                    engineConf.Write("TEXTUREGROUP_World", "(MinLODSize=16,MaxLODSize=4096,LODBias=2)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_WorldNormalMap", "(MinLODSize=16,MaxLODSize=4096,LODBias=2)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_AmbientLightMap", "(MinLODSize=32,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_LightAndShadowMap", "(MinLODSize=256,MaxLODSize=4096,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_64", "(MinLODSize=32,MaxLODSize=64,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_128", "(MinLODSize=32,MaxLODSize=128,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_256", "(MinLODSize=32,MaxLODSize=256,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_512", "(MinLODSize=32,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Environment_1024", "(MinLODSize=32,MaxLODSize=1024,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_64", "(MinLODSize=8,MaxLODSize=64,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_128", "(MinLODSize=8,MaxLODSize=128,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_256", "(MinLODSize=8,MaxLODSize=256,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_512", "(MinLODSize=8,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_VFX_1024", "(MinLODSize=8,MaxLODSize=1024,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_128", "(MinLODSize=32,MaxLODSize=128,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_256", "(MinLODSize=32,MaxLODSize=256,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_512", "(MinLODSize=32,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_APL_1024", "(MinLODSize=32,MaxLODSize=1024,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_GUI", "(MinLODSize=8,MaxLODSize=1024,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Promotional", "(MinLODSize=32,MaxLODSize=2048,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Character_1024", "(MinLODSize=32,MaxLODSize=1024,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Diff", "(MinLODSize=32,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Norm", "(MinLODSize=32,MaxLODSize=512,LODBias=0)", "TextureLODSettings");
                    engineConf.Write("TEXTUREGROUP_Character_Spec", "(MinLODSize=32,MaxLODSize=256,LODBias=0)", "TextureLODSettings");
                }
                else if (gameId == 2)
                {
                    engineConf.DeleteKey("TEXTUREGROUP_World", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_WorldNormalMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_AmbientLightMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_LightAndShadowMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_RenderTarget", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_64", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_64", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_UI", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Promotional", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Diff", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Norm", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Spec", "SystemSettings");
                }
                else if (gameId == 3)
                {
                    engineConf.DeleteKey("TEXTUREGROUP_World", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_WorldSpecular", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_WorldNormalMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_AmbientLightMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_LightAndShadowMap", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_RenderTarget", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_64", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Environment_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_64", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_VFX_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_128", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_256", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_512", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_APL_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_UI", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Promotional", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_1024", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Diff", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Norm", "SystemSettings");
                    engineConf.DeleteKey("TEXTUREGROUP_Character_Spec", "SystemSettings");
                }
                else
                {
                    throw new Exception("Invalid Game ID: " + gameId);
                }
            }
        }
        
        public static void updateGFXSettings(int gameId)
        {

            string engineConfPath = GetConfigIniPath(gameId);
            if (File.Exists(engineConfPath))
            {
                IniFile engineConf = new IniFile(engineConfPath);
                if (gameId == 1)
                {
                    engineConf.Write("MaxShadowResolution", "4096", "Engine.GameEngine");
                    engineConf.Write("MinShadowResolution", "64", "Engine.GameEngine");
                    engineConf.Write("DynamicShadows", "True", "SystemSettings");
                    engineConf.Write("ShadowFilterQualityBias", "2", "SystemSettings");
                    engineConf.Write("ShadowFilterRadius", "5", "Engine.GameEngine");
                    engineConf.Write("bEnableBranchingPCFShadows", "True", "Engine.GameEngine");
                    engineConf.Write("MaxAnisotropy", "16", "SystemSettings");
                    engineConf.Write("DisplayGamma", "2.4", "WinDrv.WindowsClient");
                    engineConf.Write("TextureLODLevel", "3", "WinDrv.WindowsClient");
                    engineConf.Write("FilterLevel", "2", "WinDrv.WindowsClient");
                    engineConf.Write("Trilinear", "True", "SystemSettings");
                    engineConf.Write("MotionBlur", "True", "SystemSettings");
                    engineConf.Write("DepthOfField", "True", "SystemSettings");
                    engineConf.Write("Bloom", "True", "SystemSettings");
                    engineConf.Write("QualityBloom", "True", "SystemSettings");
                    engineConf.Write("ParticleLODBias", "0", "SystemSettings");
                    engineConf.Write("SkeletalMeshLODBias", "0", "SystemSettings");
                    engineConf.Write("DetailMode", "2", "SystemSettings");
                }
                else if (gameId == 2)
                {
                    engineConf.Write("MaxShadowResolution", "4096", "SystemSettings");
                    engineConf.Write("MinShadowResolution", "64", "SystemSettings");
                    engineConf.Write("ShadowFilterQualityBias", "2", "SystemSettings");
                    engineConf.Write("ShadowFilterRadius", "5", "SystemSettings");
                    engineConf.Write("bEnableBranchingPCFShadows", "True", "SystemSettings");
                    engineConf.Write("MaxAnisotropy", "16", "SystemSettings");
                    engineConf.Write("Trilinear", "True", "SystemSettings");
                    engineConf.Write("MotionBlur", "True", "SystemSettings");
                    engineConf.Write("DepthOfField", "True", "SystemSettings");
                    engineConf.Write("Bloom", "True", "SystemSettings");
                    engineConf.Write("QualityBloom", "True", "SystemSettings");
                    engineConf.Write("ParticleLODBias", "0", "SystemSettings");
                    engineConf.Write("SkeletalMeshLODBias", "0", "SystemSettings");
                    engineConf.Write("DetailMode", "2", "SystemSettings");
                }
                else if (gameId == 3)
                {
                    engineConf.Write("MaxShadowResolution", "4096", "SystemSettings");
                    engineConf.Write("MinShadowResolution", "64", "SystemSettings");
                    engineConf.Write("ShadowFilterQualityBias", "2", "SystemSettings");
                    engineConf.Write("ShadowFilterRadius", "5", "SystemSettings");
                    engineConf.Write("bEnableBranchingPCFShadows", "True", "SystemSettings");
                    engineConf.Write("MaxAnisotropy", "16", "SystemSettings");
                    engineConf.Write("MotionBlur", "True", "SystemSettings");
                    engineConf.Write("DepthOfField", "True", "SystemSettings");
                    engineConf.Write("Bloom", "True", "SystemSettings");
                    engineConf.Write("QualityBloom", "True", "SystemSettings");
                    engineConf.Write("ParticleLODBias", "0", "SystemSettings");
                    engineConf.Write("SkeletalMeshLODBias", "0", "SystemSettings");
                    engineConf.Write("DetailMode", "2", "SystemSettings");
                }
                else
                {
                    throw new Exception("Invalid Game ID: " + gameId);
                }

            }
        }
    }
}
