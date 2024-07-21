using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace SOS2SRTSLANDINGZONE
{
    [StaticConstructorOnStartup]
    public class SOS2SRTSLANDINGZONE_PATCHER
    {
        static SOS2SRTSLANDINGZONE_PATCHER()
        {

            Helper.Log("Initiating core....");

            ModLoader modLoader = new ModLoader();
            if (!modLoader.IsSOS2Activated())
            {
                Helper.Log("SOS2 is not activated, aborting", true);

                return;
            }

            var harmony = new Harmony("rimworld.glasses.SOS2SRTSHangar");

            new RooflessPatches().Patch(harmony);

            Helper.Log("Core loaded, now checking for supported mods");

            List<ModInfo> mods = modLoader.GetSupportedMods();

            foreach (ModInfo mod in mods)
            {
                Helper.Log("Found " + mod.Name + ", patching...");

                IModPatch patcher = (IModPatch)System.Activator.CreateInstance(mod.Type);

                patcher.Patch(harmony);
            }

            if(mods.Count == 0)
            {
                Helper.Log("No supported mods found, the list is: " + ModLoader.SupportedMods.Join(m => m.Name));
            }
        }
    }
}