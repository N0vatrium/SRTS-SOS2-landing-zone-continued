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
        // create a list of mods that are supported by this patch, add sos2 to the list
        public static List<ModInfo> SupportedMods = new List<ModInfo>
        {
            new ModInfo
            {
                Name = "SRTS Expanded (local)",
                Id = "smashphil.srtsexpanded",
                Type = typeof(SRTSPatches)
            },
            new ModInfo
            {
                Name = "SRTS Expanded (steam)",
                Id = "smashphil.srtsexpanded_steam",
                Type = typeof(SRTSPatches)
            }
        };

        public bool IsSOS2Activated()
        {
            return ModLister.AllInstalledMods.Any(m => m.Active && m.PackageId == "kentington.saveourship2");
        }

        public List<ModInfo> GetSupportedMods()
        {
            List<ModInfo> supportedMods = new List<ModInfo>();

            List<ModMetaData> mods = ModLister.AllInstalledMods.ToList();
            string[] ids = SupportedMods.Select(m => m.Id).ToArray();

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
