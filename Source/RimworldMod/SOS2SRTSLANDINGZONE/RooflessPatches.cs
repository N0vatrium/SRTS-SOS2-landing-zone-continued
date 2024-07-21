using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse.AI;
using Verse;

namespace SOS2SRTSLANDINGZONE
{
    internal class RooflessPatches : IModPatch
    {
        public void Patch(Harmony harmony)
        {
            harmony.Patch(AccessTools.PropertyGetter(typeof(Room), "OpenRoofCount"), postfix: new HarmonyMethod(typeof(RoofCountPatch), "Postfix"));
            harmony.Patch(AccessTools.Method(typeof(WeatherEvent_VacuumDamage), "FireEvent"), prefix: new HarmonyMethod(typeof(VaccumPatch), "Prefix"));
            harmony.Patch(AccessTools.Method(typeof(RoomTempTracker), "EqualizeTemperature"), postfix: new HarmonyMethod(typeof(VacuumTempPatch), "Postfix"));
        }

        public static class RoofCountPatch
        {
            public static int Postfix(int __result, Room __instance, ref int ___cachedOpenRoofCount, bool __state)
            {
                // if we are in space
                if (__instance.Map.IsSpace() && !__instance.TouchesMapEdge && !__instance.IsDoorway && ___cachedOpenRoofCount > 0)
                {
                    int total = 0;
                    RoofDef roofDef = DefDatabase<RoofDef>.GetNamed("RoofShip");

                    Map currentMap = __instance.Map;
                    if (currentMap != null)
                    {
                        RoofGrid roofGrid = currentMap.roofGrid;

                        foreach (IntVec3 v in __instance.Cells)
                        {
                            // get the roof at the current cell
                            RoofDef roofAt = roofGrid.RoofAt(v);

                            // get all things
                            List<Thing> thingsAtCell = v.GetThingList(__instance.Map);

                            // if there is no roof and no hangar tile then the cell is really missing a roof
                            if (roofAt == null)
                            {
                                Thing thing = thingsAtCell.FirstOrDefault(t => t.def.defName.Contains("ShipHangarTile"));

                                if (thing != default(Thing) && thing is ThingWithComps comps)
                                {
                                    if (comps.GetComp<CompPowerTrader>().PowerOn)
                                    {
                                        continue;
                                    }
                                }

                                total++;
                            }
                            else if (roofAt != roofDef)
                            {
                                total++;
                            }
                        }
                    }

                    ___cachedOpenRoofCount = total;
                }

                return ___cachedOpenRoofCount;
            }
        }


        public static class VaccumPatch
        {
            public static HediffDef ArchoLung = HediffDef.Named("SoSArchotechLung");
            public static HediffDef ArchoSkin = HediffDef.Named("SoSArchotechSkin");

            static bool Prefix(WeatherEvent_VacuumDamage __instance, Map ___map)
            {
                List<Pawn> allPawns = (List<Pawn>)___map.mapPawns.AllPawnsSpawned;
                List<Pawn> pawnsToDamage = new List<Pawn>();
                List<Pawn> pawnsToSuffocate = new List<Pawn>();
                foreach (Pawn thePawn in allPawns)
                {
                    if (thePawn.RaceProps.IsFlesh && !thePawn.CanSurviveVacuum())
                    {
                        Room theRoom = thePawn.Position.GetRoom(___map);
                        bool hasLandingPoint = true;

                        foreach (IntVec3 cell in theRoom.Cells)
                        {
                            List<Thing> landingPointsAtCell =
                                cell.GetThingList(theRoom.Map).Where(t => t.def.defName.Contains("ShipHangarTile")).ToList();
                            if (landingPointsAtCell.Count < 1)
                            {
                                if (!cell.Roofed(theRoom.Map))
                                {
                                    hasLandingPoint = false;
                                    break;
                                }
                            }
                            foreach (Thing thang in landingPointsAtCell)
                            {
                                try
                                {
                                    if (!((ThingWithComps)thang).GetComp<CompPowerTrader>().PowerOn)
                                    {
                                        hasLandingPoint = false;
                                        break;
                                    }
                                }
                                catch (Exception e)
                                {
                                    //Do nothing
                                }
                            }
                        }

                        if (theRoom.FirstRegion.type != RegionType.Portal)
                        {
                            if ((theRoom == null || theRoom.OpenRoofCount > 0 || theRoom.TouchesMapEdge)
                                && thePawn.health.hediffSet.GetFirstHediffOfDef(ArchoSkin) == null && hasLandingPoint == false)
                            {
                                pawnsToDamage.Add(thePawn);
                                //find first nonvac area and run to it
                                if (thePawn.Faction != Faction.OfPlayer && !thePawn.Downed && thePawn.CurJob.def != DefDatabase<JobDef>.GetNamed("FleeVacuum"))
                                {
                                    Predicate<Thing> otherValidator = delegate (Thing t)
                                    {
                                        return t is Building_ShipAirlock && !((Building_ShipAirlock)t).Outerdoor();
                                    };
                                    Thing b = GenClosest.ClosestThingReachable(thePawn.Position, thePawn.Map, ThingRequest.ForDef(ThingDef.Named("ShipAirlock")), PathEndMode.Touch, TraverseParms.For(thePawn), 99f, otherValidator);
                                    Job Flee = new Job(DefDatabase<JobDef>.GetNamed("FleeVacuum"), b);
                                    thePawn.jobs.StartJob(Flee, JobCondition.InterruptForced);
                                }
                            }
                            else if (!hasLifeSupport(theRoom) && thePawn.health.hediffSet.GetFirstHediffOfDef(ArchoLung) == null)
                            {
                                pawnsToSuffocate.Add(thePawn);
                            }
                        }
                    }
                }
                foreach (Pawn thePawn in pawnsToDamage)
                {
                    int damage = 1;
                    thePawn.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("VacuumDamage"), damage));
                    HealthUtility.AdjustSeverity(thePawn, HediffDef.Named("SpaceHypoxia"), 0.025f);
                }
                foreach (Pawn thePawn in pawnsToSuffocate)
                {
                    HealthUtility.AdjustSeverity(thePawn, HediffDef.Named("SpaceHypoxia"), 0.0125f);
                }

                return false;
            }
            static bool pawnInAirlock(Pawn thePawn)
            {
                foreach (Thing t in thePawn.Map.thingGrid.ThingsAt(thePawn.Position))
                {
                    if (t is Building_ShipAirlock)
                        return true;
                }
                return false;
            }

            static bool hasLifeSupport(Room theRoom)
            {
                return theRoom.Map.spawnedThings.Any(t => (t.def.defName.Equals("Ship_LifeSupport") || t.def.defName.Equals("Ship_LifeSupport_Small")) && ((ThingWithComps)t).GetComp<CompFlickable>().SwitchIsOn && ((ThingWithComps)t).GetComp<CompPowerTrader>().PowerOn);
            }
        }


        public static class VacuumTempPatch
        {
            public static void Postfix(RoomTempTracker __instance)
            {
                //This is literally just SOS2's Harmony Postfix but with our added code. 
                Room room = (Room)typeof(RoomTempTracker)
                    .GetField("room", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);

                if (room.Map.IsSpace())
                {
                    bool foundHangarHull = false;
                    foreach (IntVec3 cell in room.Cells)
                    {
                        List<Thing> thingsAtCell = new List<Thing>();

                        // if there is a ShipHangarTile

                        foreach (Thing thang in cell.GetThingList(room.Map))
                        {
                            if (thang.def.defName.Contains("ShipHangarTile"))
                            {
                                foundHangarHull = true;
                                break;
                            }
                        }

                        if (foundHangarHull)
                        {
                            break;
                        }
                    }

                    if (foundHangarHull && room.OpenRoofCount == 0)
                    {
                        __instance.Temperature = 21f;
                    }


                    //Added code ends

                    if (room.Map.terrainGrid.TerrainAt(IntVec3.Zero).defName != "EmptySpace")
                        return;
                    if (room.Role != RoomRoleDefOf.None && room.OpenRoofCount > 0)
                        __instance.Temperature = -100f;

                }
            }
        }
    }
}
