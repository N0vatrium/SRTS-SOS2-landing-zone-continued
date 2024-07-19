using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using SRTS;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SOS2SRTSLANDINGZONE
{
    class SRTSPatches : IModPatch
    {
        public void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(CompLaunchableSRTS), "StartChoosingDestination"), prefix: new HarmonyMethod(typeof(SrtsStartPatch), "Prefix"));
            harmony.Patch(AccessTools.Method(typeof(CompLaunchableSRTS), "WorldStartChoosingDestination"), prefix: new HarmonyMethod(typeof(SrtsWorldPatch), "Prefix"));
        }

        private static class SrtsStartPatch
        {
            public static CompLaunchableSRTS instance;
            public static Caravan carr;

            public static void Prefix(CompLaunchableSRTS __instance, Caravan ___carr)
            {
                instance = __instance;
                CameraJumper.TryJump(CameraJumper.GetWorldTarget(__instance.parent));
                Find.WorldSelector.ClearSelection();
                int tile = __instance.parent.Map.Tile;
                ___carr = null;

                /* SOS2 Compatibility Section */
                if (SRTSHelper.SOS2ModLoaded)
                {
                    if (__instance.parent.Map.Parent.def.defName == "ShipOrbiting")
                    {
                        Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(ChoseWorldTarget), true, CompLaunchableSRTS.TargeterMouseAttachment, true, null, delegate (GlobalTargetInfo target)
                        {
                            if (!target.IsValid || __instance.parent.TryGetComp<CompRefuelable>() == null || __instance.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax == 1.0f)
                            {
                                return null;
                            }

                            if (target.WorldObject != null && target.WorldObject.GetType().IsAssignableFrom(SRTSHelper.SpaceSiteType))
                            {
                                /*if (this.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax >= ((SRTSHelper.SpaceSite.worldObjectClass)target.WorldObject).fuelCost / 100f)
                                    return null;
                                return "MessageShuttleNeedsMoreFuel".Translate(((SpaceSite)target.WorldObject).fuelCost);*/
                                return null;
                            }
                            return "MessageShuttleMustBeFullyFueled".Translate();
                        });
                    }
                    else if (__instance.parent.Map.Parent.GetType().IsAssignableFrom(SRTSHelper.SpaceSiteType))
                    {
                        Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(ChoseWorldTarget), true, CompLaunchableSRTS.TargeterMouseAttachment, true, null, delegate (GlobalTargetInfo target)
                        {
                            if (target.WorldObject == null || (!(target.WorldObject.def == SRTSHelper.SpaceSite) && !(target.WorldObject.def.defName == "ShipOrbiting")))
                            {
                                return "MessageOnlyOtherSpaceSites".Translate();
                            }
                            return null;
                            /*if (this.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax >= ((SpaceSite)this.parent.Map.Parent).fuelCost / 100f)
                                return null;
                            return "MessageShuttleNeedsMoreFuel".Translate(((SpaceSite)this.parent.Map.Parent).fuelCost);*/
                        });
                    }
                }
                /* -------------------------- */
                Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(ChoseWorldTarget), true, CompLaunchableSRTS.TargeterMouseAttachment, true, (() => GenDraw.DrawWorldRadiusRing(tile, instance.MaxLaunchDistance)), (target =>
                {
                    if (!target.IsValid)
                        return null;
                    int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile);
                    if (num > __instance.MaxLaunchDistance)
                    {
                        GUI.color = Color.red;
                        if (num > __instance.MaxLaunchDistanceEverPossible)
                            return "TransportPodDestinationBeyondMaximumRange".Translate();
                        return "TransportPodNotEnoughFuel".Translate();
                    }

                    if (target.WorldObject?.def?.defName == "ShipOrbiting" || (target.WorldObject?.GetType()?.IsAssignableFrom(SRTSHelper.SpaceSiteType) ?? false))
                    {
                        return null;
                    }

                    IEnumerable<FloatMenuOption> floatMenuOptionsAt = __instance.GetTransportPodsFloatMenuOptionsAt(target.Tile, (Caravan)null);
                    if (!floatMenuOptionsAt.Any<FloatMenuOption>())
                    {
                        if (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
                            return "MessageTransportPodsDestinationIsInvalid".Translate();
                        return string.Empty;
                    }
                    if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
                    {
                        if (floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
                            GUI.color = Color.red;
                        return floatMenuOptionsAt.First<FloatMenuOption>().Label;
                    }
                    MapParent worldObject = target.WorldObject as MapParent;
                    if (worldObject == null)
                        return "ClickToSeeAvailableOrders_Empty".Translate();
                    return "ClickToSeeAvailableOrders_WorldObject".Translate(worldObject.LabelCap);
                }));
            }

            private static bool ChoseWorldTarget(GlobalTargetInfo target)
            {
                if (carr == null && !instance.LoadingInProgressOrReadyToLaunch)
                {
                    return true;
                }
                if (!target.IsValid)
                {
                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                int num = Find.WorldGrid.TraversalDistanceBetween(carr != null ? carr.Tile : instance.parent.Map.Tile, target.Tile, true, int.MaxValue);
                if (num > instance.MaxLaunchDistance)
                {
                    Messages.Message("MessageTransportPodsDestinationIsTooFar".Translate(CompLaunchableSRTS.FuelNeededToLaunchAtDist((float)num, instance.BaseFuelPerTile).ToString("0.#")), MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                List<Site> sitesAtTile = Find.WorldObjects.Sites.Where(site => site.Tile == target.Tile).ToList();
                List<Map> mapsAtTile = Find.Maps.Where(m => m.Tile == target.Tile).ToList();

                if ((Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile)) && (!SRTSHelper.SOS2ModLoaded || target.WorldObject?.def?.defName != "ShipOrbiting") && mapsAtTile.Count < 1 && sitesAtTile.Count < 1)
                {

                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                if (SRTSHelper.SOS2ModLoaded && target.WorldObject?.def?.defName == "ShipOrbiting")
                {
                    if (!SRTSMod.GetStatFor<bool>(instance.parent.def.defName, StatName.spaceFaring))
                    {
                        Messages.Message("NonSpaceFaringSRTS".Translate(instance.parent.def.defName), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                }

                IEnumerable<FloatMenuOption> floatMenuOptionsAt = instance.GetTransportPodsFloatMenuOptionsAt(target.Tile, carr);
                if (!floatMenuOptionsAt.Any<FloatMenuOption>())
                {
                    if ((Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile)) && mapsAtTile.Count < 1 && sitesAtTile.Count < 1)
                    {

                        Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }

                    instance.TryLaunch(target.Tile, (TransportPodsArrivalAction)null, (Caravan)null);
                    return true;
                }
                if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
                {
                    if (!floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
                        floatMenuOptionsAt.First<FloatMenuOption>().action();
                    return false;
                }
                Find.WindowStack.Add((Window)new FloatMenu(floatMenuOptionsAt.ToList<FloatMenuOption>()));
                return false;
            }

        }

        private static class SrtsWorldPatch
        {
            public static GlobalTargetInfo target;
            public static CompLaunchableSRTS instance;
            public static Caravan carr;

            public static void Prefix(Caravan car, CompLaunchableSRTS __instance, ref Caravan ___carr)
            {
                instance = __instance;

                CameraJumper.TryJump(CameraJumper.GetWorldTarget((GlobalTargetInfo)((WorldObject)car)));
                Find.WorldSelector.ClearSelection();
                int tile = car.Tile;
                ___carr = car;
                carr = car;
                Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(ChoseWorldTarget), true,
                    CompLaunchableSRTS.TargeterMouseAttachment, false,
                    (Action)(() => GenDraw.DrawWorldRadiusRing(car.Tile, __instance.MaxLaunchDistance)),
                    (Func<GlobalTargetInfo, string>)(target =>
                    {
                        if (!target.IsValid)
                            return (string)null;
                        int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile, true, int.MaxValue);
                        if (num > __instance.MaxLaunchDistance)
                        {
                            GUI.color = Color.red;
                            if (num > __instance.MaxLaunchDistanceEverPossible)
                                return "TransportPodDestinationBeyondMaximumRange".Translate();
                            return "TransportPodNotEnoughFuel".Translate();
                        }
                        IEnumerable<FloatMenuOption> floatMenuOptionsAt = __instance.GetTransportPodsFloatMenuOptionsAt(target.Tile, car);
                        if (!floatMenuOptionsAt.Any<FloatMenuOption>())
                        {
                            if (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
                                return "MessageTransportPodsDestinationIsInvalid".Translate();
                            return string.Empty;
                        }
                        if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
                        {
                            if (floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
                                GUI.color = Color.red;
                            return floatMenuOptionsAt.First<FloatMenuOption>().Label;
                        }
                        MapParent worldObject = target.WorldObject as MapParent;
                        if (worldObject == null)
                            return "ClickToSeeAvailableOrders_Empty".Translate();
                        return "ClickToSeeAvailableOrders_WorldObject".Translate(worldObject.LabelCap);
                    }));

                return;
            }

            private static bool ChoseWorldTarget(GlobalTargetInfo target)
            {
                if (carr == null && !instance.LoadingInProgressOrReadyToLaunch)
                    return true;
                if (!target.IsValid)
                {
                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                int num = Find.WorldGrid.TraversalDistanceBetween(carr != null ? carr.Tile : instance.parent.Map.Tile, target.Tile, true, int.MaxValue);
                if (num > instance.MaxLaunchDistance)
                {
                    Messages.Message("MessageTransportPodsDestinationIsTooFar".Translate(CompLaunchableSRTS.FuelNeededToLaunchAtDist((float)num, instance.BaseFuelPerTile).ToString("0.#")), MessageTypeDefOf.RejectInput, false);
                    return false;
                }

                List<Site> sitesAtTile = Find.WorldObjects.Sites.Where(site => site.Tile == target.Tile).ToList();

                if ((Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile)) && (!SRTSHelper.SOS2ModLoaded || target.WorldObject?.def?.defName != "ShipOrbiting") && sitesAtTile.Count < 1)
                {
                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                if (SRTSHelper.SOS2ModLoaded && target.WorldObject?.def?.defName == "ShipOrbiting")
                {
                    if (!SRTSMod.GetStatFor<bool>(instance.parent.def.defName, StatName.spaceFaring))
                    {
                        Messages.Message("NonSpaceFaringSRTS".Translate(instance.parent.def.defName), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    if (SRTSMod.GetStatFor<bool>(instance.parent.def.defName, StatName.shuttleBayLanding))
                    {
                        IntVec3 shuttleBayPos = (IntVec3)AccessTools.Method(type: SRTSHelper.SOS2LaunchableType, "FirstShuttleBayOpen").Invoke(null, new object[] { (target.WorldObject as MapParent).Map });
                        if (shuttleBayPos == IntVec3.Zero)
                        {
                            Messages.Message("NeedOpenShuttleBay".Translate(), MessageTypeDefOf.RejectInput);
                            return false;
                        }
                        instance.TryLaunch(target.Tile, new TransportPodsArrivalAction_LandInSpecificCell((target.WorldObject as MapParent).Map.Parent, shuttleBayPos));
                        return true;
                    }
                }
                Find.WorldObjects.MapParentAt(target.Tile);
                IEnumerable<FloatMenuOption> floatMenuOptionsAt = instance.GetTransportPodsFloatMenuOptionsAt(target.Tile, carr);
                if (!floatMenuOptionsAt.Any<FloatMenuOption>())
                {
                    if (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
                    {
                        Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
                        return false;
                    }
                    instance.TryLaunch(target.Tile, (TransportPodsArrivalAction)null, (Caravan)null);
                    return true;
                }
                if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
                {
                    if (!floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
                        floatMenuOptionsAt.First<FloatMenuOption>().action();
                    return false;
                }
                Find.WindowStack.Add((Window)new FloatMenu(floatMenuOptionsAt.ToList<FloatMenuOption>()));
                return false;
            }
        }
    }
}
