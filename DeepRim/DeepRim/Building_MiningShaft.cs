using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;

namespace DeepRim
{
    [StaticConstructorOnStartup]
    public class Building_MiningShaft : Building
    {
        private static Texture2D UI_Send;
        private static Texture2D UI_BringUp;
        private static Texture2D UI_Start;
        private static Texture2D UI_Pause;
        private static Texture2D UI_Abandon;
        private static Texture2D UI_DrillDown;
        private static Texture2D UI_DrillUp;
        private static Texture2D UI_Option;

        private const int updateEveryXTicks = 50;

        private int ticksCounter = 0;

        private CompPowerTrader m_Power;

        private int mode = 0;

        public bool drillNew = true;

        public int targetedLevel = 0;

        private int ChargeLevel;

        private UndergroundMapParent connectedMapParent = null;

        private Thing connectedLift = null;

        public float ConnectedMapMarketValue
        {
            get
            {
                if (isConnected)
                {
                    if (Current.ProgramState != ProgramState.Playing)
                        return 0f;
                    return connectedLift.Map.wealthWatcher.WealthTotal;
                }
                return 0f;
            }
        }

        public bool isConnected => connectedMapParent != null && connectedLift != null && connectedLift.Spawned ;

        public int curMode => mode;

        public UndergroundMapParent linkedMapParent => connectedMapParent;

        static Building_MiningShaft()
        {
            UI_Send = ContentFinder<Texture2D>.Get("UI/sendDown");
            UI_BringUp = ContentFinder<Texture2D>.Get("UI/bringUp");
            UI_Start = ContentFinder<Texture2D>.Get("UI/Start");
            UI_Pause = ContentFinder<Texture2D>.Get("UI/Pause");
            UI_Abandon = ContentFinder<Texture2D>.Get("UI/Abandon");
            UI_DrillUp = ContentFinder<Texture2D>.Get("UI/drillup");
            UI_DrillDown = ContentFinder<Texture2D>.Get("UI/drilldown");
            UI_Option = ContentFinder<Texture2D>.Get("UI/optionsIcon");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ChargeLevel, "ChargeLevel", 0);
            Scribe_Values.Look(ref mode, "mode", 0);
            Scribe_Values.Look(ref targetedLevel, "targetedLevel", 0);
            Scribe_Values.Look(ref drillNew, "drillNew", defaultValue: true);

            Scribe_References.Look(ref connectedMapParent, "m_ConnectedMapParent");
            Scribe_References.Look(ref connectedLift, "m_ConnectedLift");
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerator<Gizmo> enumerator = base.GetGizmos().GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
            Command_TargetLayer command = new Command_TargetLayer
            {
                thing = this,
                manager = (Map.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager),
                action = delegate
                {
                },
                defaultLabel = "ChangeTarget.Label".Translate()
            };

            command.defaultDesc = "ChangeTarget.Desc".Translate();
            if (drillNew)
                command.defaultDesc += "ChangeTarget.Desc_New".Translate();
            else
                command.defaultDesc += string.Format("ChangeTarget.Desc_Old".Translate(), targetedLevel);
            command.icon = UI_Option;
            yield return command;

            if (isConnected)
            {
                yield return new Command_Action
                {
                    action = Send,
                    defaultLabel = "SendDown.Label".Translate(),
                    defaultDesc = "SendDown.Desc".Translate(),
                    icon = UI_Send
                };
                yield return new Command_Action
                {
                    action = BringUp,
                    defaultLabel = "BringUp.Label".Translate(),
                    defaultDesc = "BringUp.Desc".Translate(),
                    icon = UI_BringUp
                };
                yield return new Command_Action
                {
                    action = delegate
                    {
                        connectedLift.DeSpawn();
                        connectedLift.Destroy();
                        connectedLift = null;
                        mode = 0;
                    },
                    defaultLabel = "Disconnect.Label".Translate(),
                    defaultDesc = "Disconnect.Desc".Translate(),
                    icon = UI_Abandon
                };
            }

            if (mode == 0)
            {
                Command_Action command_ActionStart = new Command_Action
                {
                    action = delegate { mode = 1; },
                    defaultLabel = "StartDrilling.Label".Translate()
                };
                if (drillNew)
                    command_ActionStart.defaultDesc = "StartDrilling.Desc_New".Translate();
                else
                    command_ActionStart.defaultDesc = "StartDrilling.Desc_Old".Translate();
                command_ActionStart.icon = UI_Start;
                yield return command_ActionStart;
            }

            else if (mode == 1)
            {
                yield return new Command_Action
                {
                    action = delegate { mode = 0; },
                    defaultLabel = "PauseDrilling.Label".Translate(),
                    defaultDesc = "PauseDrilling.Desc".Translate(),
                    icon = UI_Pause
                };
            }
            else if (mode == 2)
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        mode = 3;
                        Messages.Message("AbandonLayer.Message".Translate(), MessageTypeDefOf.RejectInput);
                    },
                    defaultLabel = "AbandonLayer.Label".Translate(),
                    defaultDesc = "AbandonLayer.Desc".Translate(),
                    icon = UI_Abandon
                };
            }
            else if (mode == 3)
            {
                yield return new Command_Action
                {
                    action = delegate { mode = 2; },
                    defaultLabel = "CancelAbandon.Label".Translate(),
                    defaultDesc = "CancelAbandon.Desc".Translate(),
                    icon = UI_Abandon
                };
                yield return new Command_Action
                {
                    action = Abandon,
                    defaultLabel = "ConfirmAbandon.Label".Translate(),
                    defaultDesc = "ConfirmAbandon.Desc".Translate(),
                    icon = UI_Abandon
                };
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            //Abandon();
            mode = 0;
            if (connectedLift != null)
                connectedLift.Destroy();

            connectedMapParent = null;
            connectedLift = null;

            base.Destroy(mode);
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (drillNew)
                stringBuilder.AppendLine("Target".Translate() + "NewLayer".Translate());
            else
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "Target".Translate(),
                    string.Format("LayerAtDepth".Translate(),targetedLevel)
                }));
            if (mode < 2)
            {
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "Progress".Translate(),
                    ChargeLevel,
                    "%"
                }));
                stringBuilder.Append(base.GetInspectString());
            }
            else
            {
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "DrillingComplete".Translate(),
                    "Depth".Translate(),
                    connectedMapParent.depth
                }));
                stringBuilder.Append(base.GetInspectString());
            }
            return stringBuilder.ToString();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            m_Power = GetComp<CompPowerTrader>();
        }

        private void Abandon()
        {
            mode = 0;
            if (connectedMapParent != null)
                if (!connectedMapParent.abandonLift(connectedLift))
                    drillNew = true;
            if (connectedLift != null)
                connectedLift.Destroy();

            connectedMapParent = null;
            connectedLift = null;
        }

        private void DrillNewLayer()
        {
            Messages.Message("DrillingComplete".Translate(), MessageTypeDefOf.PositiveEvent);

            UndergroundManager undergroundManager = Map.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
            connectedMapParent = undergroundManager.CreateNewLayer(Tile, this.OccupiedRect());

            connectedLift = GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("undergroundlift"), base.Stuff), connectedMapParent.holeLocation, connectedMapParent.Map);
            connectedLift.SetFaction(Faction.OfPlayer);

            if (connectedLift is Building_SpawnedLift)
            {
                ((Building_SpawnedLift)connectedLift).depth = connectedMapParent.depth;
                ((Building_SpawnedLift)connectedLift).Spawner = this;
            }
            else
                Log.Warning("Spawned lift isn't deeprim's lift. Someone's editing this mod! And doing it badly!!! Very badly.");
        }

        private void FinishedDrill()
        {
            if (drillNew)
                DrillNewLayer();
            else
                DrillToOldLayer();
        }

        private void DrillToOldLayer()
        {
            UndergroundManager undergroundManager = base.Map.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
            var connectedMap = (connectedMapParent = undergroundManager.layersState[targetedLevel]).Map;
            CellRect cellRect = this.OccupiedRect();
            IntVec3 intVec = new IntVec3(cellRect.minX + 1, 0, cellRect.minZ + 1);
            connectedLift = GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("undergroundlift"), base.Stuff), intVec, connectedMap);
            connectedLift.SetFaction(Faction.OfPlayer);
            FloodFillerFog.FloodUnfog(intVec, connectedMap);
            if (connectedLift is Building_SpawnedLift)
            {
                ((Building_SpawnedLift)connectedLift).depth = connectedMapParent.depth;
                ((Building_SpawnedLift)connectedLift).Spawner = this;
            }
            else
                Log.Warning("Spawned lift isn't deeprim's lift. Someone's editing this mod! And doing it badly!!! Very badly.");
        }

        private void Send()
        {
            if (!m_Power.PowerOn)
            {
                Messages.Message("NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            Messages.Message("SendingDown".Translate(), MessageTypeDefOf.PositiveEvent);
            IEnumerable<IntVec3> cells = this.OccupiedRect().Cells;
            foreach (IntVec3 item in cells)
            {
                List<Thing> thingList = item.GetThingList(base.Map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (thingList[i] is Pawn || (thingList[i] is ThingWithComps && !(thingList[i] is Building)))
                    {
                        Thing thing = thingList[i];
                        thing.DeSpawn();
                        GenSpawn.Spawn(thing, item, connectedLift.Map);
                    }
                }
            }
        }

        private void BringUp()
        {
            if (!m_Power.PowerOn)
            {
                Messages.Message("NoPower".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            Messages.Message("BringingUp".Translate(), MessageTypeDefOf.PositiveEvent);
            IEnumerable<IntVec3> cells = connectedLift.OccupiedRect().Cells;
            foreach (IntVec3 item in cells)
            {
                List<Thing> thingList = item.GetThingList(connectedLift.Map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Log.Warning("Test " + i + " " + thingList[i]);
                    if (thingList[i] is Pawn || (thingList[i] is ThingWithComps && !(thingList[i] is Building)))
                    {
                        Thing thing = thingList[i];
                        thing.DeSpawn();
                        GenSpawn.Spawn(thing, item, base.Map);
                    }
                }
            }
        }

        public override void Tick()
        {
            base.Tick();
            ticksCounter++;
            if (m_Power.PowerOn && ticksCounter >= 50 && mode == 1)
            {
                ticksCounter = 0;
                if (DebugSettings.unlimitedPower)
                    ChargeLevel += 20;
                else
                    ChargeLevel++;
                if (ChargeLevel >= 100)
                {
                    ChargeLevel = 0;
                    mode = 2;
                    FinishedDrill();
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public class Building_FreightElevator : Building
    {
        public int curMode => mode;
        public bool Receive => isConnected && 
            //往上送 且 深度小于SpawnedLift
            ((mode == 2 && Depth < ((Building_SpawnedFreightElevator)connectedLift).depth) ||
            //往下送 且 深度大于SpawnedLift
            ((mode == 3 && Depth > ((Building_SpawnedFreightElevator)connectedLift).depth)));

        public int Depth => m_depth;

        public bool isConnected => connectedLift != null && connectedLift.Spawned;

        public int targetedLevel = 0;

        private static Texture2D UI_Send;
        private static Texture2D UI_BringUp;
        private static Texture2D UI_Start;
        private static Texture2D UI_Pause;
        private static Texture2D UI_Cancel;
        private static Texture2D UI_Option;
        private static bool Loaded = false;

        private CompPowerTrader m_Power;
        private int ticksCounter = 0;
        private int mode = 0;
        //0 未开采
        //1 正在开采
        //2 往上
        //3 往下

        private int m_depth = -1;

        private int ChargeLevel;
        private Thing connectedLift = null;
        private UndergroundManager undergroundManager;

        static Building_FreightElevator()
        {
            if(!Loaded)
            {
                UI_Send = ContentFinder<Texture2D>.Get("UI/sendDown");
                UI_BringUp = ContentFinder<Texture2D>.Get("UI/bringUp");
                UI_Cancel = ContentFinder<Texture2D>.Get("UI/Abandon");
                UI_Option = ContentFinder<Texture2D>.Get("UI/optionsIcon");
                UI_Pause = ContentFinder<Texture2D>.Get("UI/Pause");
                UI_Start = ContentFinder<Texture2D>.Get("UI/Start");
                Loaded = true;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerator<Gizmo> enumerator = base.GetGizmos().GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }

            Command_TargetLayer command = new Command_TargetLayer
            {
                thing = this,
                manager = undergroundManager,
                action = delegate
                {
                },
                defaultLabel = "ChangeTarget.Label".Translate()
            };

            command.defaultDesc = "ChangeTarget.Desc".Translate();
            if(targetedLevel == 0)
                command.defaultDesc += "Yan.Target.Surface".Translate();
            else
                command.defaultDesc += string.Format("ChangeTarget.Desc_Old".Translate(), targetedLevel);
            command.icon = UI_Option;

            yield return command;

            if (isConnected)
            {
                if(mode == 3)
                    //往上送
                    yield return new Command_Action
                    {
                        action = delegate 
                        {
                            mode = 2;
                        },
                        defaultLabel = "BringUp.Label".Translate(),
                        defaultDesc = "BringUp.Desc".Translate(),
                        icon = UI_BringUp
                    };
                else if(mode == 2)
                    //往下送
                    yield return new Command_Action
                    {
                        action = delegate 
                        {
                            mode = 3;
                        },
                        defaultLabel = "SendDown.Label".Translate(),
                        defaultDesc = "SendDown.Desc".Translate(),
                        icon = UI_Send
                    };

                yield return new Command_Action
                {
                    action = Disconnect,
                    defaultLabel = "Disconnect.Label".Translate(),
                    defaultDesc = "Disconnect.Desc".Translate(),
                    icon = UI_Cancel
                };
            }

            if (mode == 0)
            {
                Command_Action command_ActionStart = new Command_Action
                {
                    action = delegate { mode = 1; },
                    defaultLabel = "StartDrilling.Label".Translate()
                };
                command_ActionStart.defaultDesc = "StartDrilling.Desc_Old".Translate();
                command_ActionStart.icon = UI_Start;
                yield return command_ActionStart;
            }

            else if (mode == 1)
            {
                yield return new Command_Action
                {
                    action = delegate { mode = 0; },
                    defaultLabel = "PauseDrilling.Label".Translate(),
                    defaultDesc = "PauseDrilling.Desc".Translate(),
                    icon = UI_Pause
                };
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            m_Power = GetComp<CompPowerTrader>();


            //获取地层
            undergroundManager = Map.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
            if(!(Map.ParentHolder is UndergroundMapParent))
                m_depth = 0;
            else if(((UndergroundMapParent)Map.Parent).Surface != null)
            {
                undergroundManager = ((UndergroundMapParent)Map.Parent).Surface.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
                foreach (var _map in undergroundManager.layersState)
                    if (_map.Value.Map.uniqueID == Map.uniqueID)
                        m_depth = _map.Key;
            }

            if (Depth == -1)
                Log.Error("Yan.SpawnedFE.Error.DontFindLayer".Translate());
            else
                Log.Message(this.ThingID + " Depth : " + Depth);
        }

        public override void Tick()
        {
            base.Tick();

            if (ticksCounter >= 30)
            {
                if(m_Power.PowerOn)
                    if (mode == 1)
                    {
                        ticksCounter = 0;
                        if (DebugSettings.unlimitedPower)
                            ChargeLevel += 20;
                        else
                            ChargeLevel++;
                        if (ChargeLevel >= 100)
                        {
                            ChargeLevel = 0;
                            DrillToLayer();
                            mode = targetedLevel > Depth ? 3 : 2;
                        }
                        else if (ChargeLevel >= 75 && !CanSpawnLift())
                        {
                            mode = 0;
                            ChargeLevel = 0;
                            Messages.Message("DontSpawnLift.Message".Translate(), MessageTypeDefOf.RejectInput);
                        }
                    }
                    else if (mode >= 2 && mode < 5)
                    {
                        ticksCounter = 0;
                        TransportThings();
                    }

                if(undergroundManager == null)
                    if (Map.ParentHolder is UndergroundMapParent)
                        undergroundManager = ((UndergroundMapParent)Map.Parent).Surface.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
                    else
                        undergroundManager = Map.components.Find((MapComponent item) => item is UndergroundManager) as UndergroundManager;
            }

            ticksCounter++;
        }

        private void TransportThings()
        {
            Map Target_map;
            Map Sender_map;
            CellRect cellRect = this.OccupiedRect();

            if (!Receive)
            {
                Target_map = connectedLift.Map;
                Sender_map = Map;
            }else
            {
                Target_map = Map;
                Sender_map = connectedLift.Map;
            }

            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++)
                {
                    IntVec3 cell = new IntVec3(cellRect.minX + x - 1, 0, cellRect.minZ + z - 1);
                    List<Thing> thingList = Sender_map.thingGrid.ThingsListAt(cell);

                    for (int i = 0; i < thingList.Count; i++)
                    {
                        Thing thing = thingList[i];
                        if (thing is Thing && !(thing is Building || thing is Pawn || thing is Plant || thing is Blueprint))
                        {
                            List<Thing> Target_map_thingList = cell.GetThingList(Target_map);
                            bool IsBuilding = false;

                            foreach (Thing _thing in Target_map_thingList)
                                if (_thing is Building && ((Building)_thing).def.passability == Traversability.Impassable)
                                {
                                    IsBuilding = true;
                                    break;
                                }

                            if (!IsBuilding)
                            {
                                thing.DeSpawn();
                                GenSpawn.Spawn(thing, cell, Target_map);
                            }
                        }
                    }

                }
        }

        private void DrillToLayer()
        {
            Map connectedMap;
            if (targetedLevel == 0)
                connectedMap = undergroundManager.Surface;
            else
                connectedMap = undergroundManager.layersState[targetedLevel].Map;

            CellRect cellRect = this.OccupiedRect();
            IntVec3 intVec = new IntVec3(cellRect.minX, 0, cellRect.minZ);
            connectedLift = GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("FreightElevatorSpawned"), base.Stuff), intVec, connectedMap);
            connectedLift.SetFaction(Faction.OfPlayer);
            FloodFillerFog.FloodUnfog(intVec, connectedMap);

            if (connectedLift is Building_SpawnedFreightElevator)
            {
                ((Building_SpawnedFreightElevator)connectedLift).depth = targetedLevel;
                ((Building_SpawnedFreightElevator)connectedLift).Spawner = this;
            }
        }

        public void Disconnect()
        {
            if(isConnected)
                connectedLift.DeSpawn();
            connectedLift = null;
            mode = 0;
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Concat(new object[]
            {
                    "Target".Translate(),
                    targetedLevel == 0 ? "Yan.Target.Surface".Translate() : string.Format("LayerAtDepth".Translate(),targetedLevel),
            }));

            if (mode < 2)
            {
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "Progress".Translate(),
                    ChargeLevel,
                    "%"
                }));
                stringBuilder.Append(base.GetInspectString());
            }
            else
            {
                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "Yan.Mode".Translate(),
                    mode == 2 ? "Yan.Mode.Up".Translate() :"Yan.Mode.Down".Translate(),
                    "    ",
                    Receive ? "Yan.Mode.Receive".Translate():"Yan.Mode.Send".Translate()
                }));

                stringBuilder.Append(base.GetInspectString());
            }
            return stringBuilder.ToString();
        }

        private bool CanSpawnLift()
        {
            CellRect cellRect = this.OccupiedRect();
            IntVec3 intVec = new IntVec3(cellRect.minX, 0, cellRect.minZ);

            foreach (IntVec3 item in cellRect)
            {
                List<Thing> thingList;

                if (targetedLevel != 0) // 目标不是地表
                    thingList = item.GetThingList(undergroundManager.layersState[targetedLevel].Map);
                else
                    thingList = item.GetThingList(undergroundManager.Surface);

                for (int i = 0; i < thingList.Count; i++)
                    if (thingList[i] is Building)
                        return false;
            }
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ChargeLevel, "ChargeLevel", 0);
            Scribe_Values.Look(ref m_depth, "m_Depth");
            Scribe_Values.Look(ref targetedLevel, "targetedLevel");
            Scribe_Values.Look(ref mode, "Mode");

            Scribe_References.Look(ref connectedLift, "m_ConnectedLift");
            Scribe_References.Look(ref undergroundManager, "m_UndergroundManager");
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if(isConnected)
            {
                connectedLift.DeSpawn();
                connectedLift.Destroy();
                connectedLift = null;
            }
            base.Destroy(mode);
        }
    }
}
