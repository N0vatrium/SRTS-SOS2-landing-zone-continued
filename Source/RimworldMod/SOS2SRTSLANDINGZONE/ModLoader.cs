using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SOS2SRTSLANDINGZONE
{
    struct ModInfo
    {
        public string Name;
        public string Id;
        public Type Type;
    }

    interface IModPatch
    {
        void Patch(Harmony harmony);
    }

    internal class ModLoader
    {
        public static bool SOS2ModLoaded = false;

        // create a list of mods that are supported by this patch, add sos2 to the list
        public static List<ModInfo> SupportedMods = new List<ModInfo>
        {
            new ModInfo
            {
                Name = "SRTS Expanded",
                Id = "smashphil.srtsexpanded",
                Type = typeof(SRTSPatches)
            }
        };

        public List<ModInfo> GetSupportedMods()
        {
            List<ModInfo> supportedMods = new List<ModInfo>();

            List<ModMetaData> mods = ModLister.AllInstalledMods.ToList();
            string[] ids = mods.Select(m => m.PackageId).ToArray();

            foreach (ModMetaData mod in mods)
            {
                if (mod.Active && ids.Contains(mod.PackageId))
                {
                    ModInfo modInfo = SupportedMods.Find(m => m.Id == mod.PackageId);
                    if (modInfo.Type != null)
                    {
                        supportedMods.Add(modInfo);
                    }
                }
            }

            return supportedMods;
        }
    }
}
