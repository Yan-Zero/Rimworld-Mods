using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace DeepRim
{
	public class UndergroundManager : MapComponent, ILoadReferenceable
    {
		private static Version Version = Main.CurrentVer;
        private int m_NextKey = 0;
        private int spawned = 0;

        public Map Surface => map;

        public Dictionary<int, UndergroundMapParent> layersState = new Dictionary<int, UndergroundMapParent>();

		private List<int> list2;

		private List<UndergroundMapParent> list3;

		public UndergroundManager(Map map)
			: base(map)
		{
            m_NextKey = 1;
        }

        [Obsolete("这是用来做向后兼容的。")]
		public int getNextEmptyLayer(int starting = 1)
		{
			int i;
			for (i = starting; layersState.ContainsKey(i); i++)
			{
			}
			return i;
		}

		public void insertLayer(UndergroundMapParent mp)
		{
            layersState.Add(m_NextKey, mp);
			mp.depth = m_NextKey;
            m_NextKey++;
		}

        public UndergroundMapParent CreateNewLayer(int tile, CellRect cellRect)
        {
            MapParent mapParent = (MapParent)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("UndergroundMapParent"));
            mapParent.Tile = tile;
            Find.WorldObjects.Add(mapParent);

            var createdMap = (UndergroundMapParent)mapParent;
            createdMap.holeLocation = new IntVec3(cellRect.minX + 1, 0, cellRect.minZ + 1);
            string seedString = Find.World.info.seedString;
            Find.World.info.seedString = new System.Random().Next(0, 2147483646).ToString();
            var connectedMap = MapGenerator.GenerateMap(Find.World.info.initialMapSize, mapParent, mapParent.MapGeneratorDef, mapParent.ExtraGenStepDefs);
            Find.World.info.seedString = seedString;
            createdMap.Owner = this;
            insertLayer(createdMap);

            return createdMap;
        }

        public void pinAllUnderground()
		{
			int i = 1;
			foreach (Building item in map.listerBuildings.allBuildingsColonist)
			{
				Building_MiningShaft building_MiningShaft;
				if ((building_MiningShaft = (item as Building_MiningShaft)) != null && building_MiningShaft.isConnected)
				{
					UndergroundMapParent linkedMapParent = building_MiningShaft.linkedMapParent;
					if (linkedMapParent.depth == -1)
					{
						for (; layersState.ContainsKey(i); i++)
						{
						}
						layersState.Add(i, linkedMapParent);
						linkedMapParent.depth = i;
					}
				}
			}
		}

        public void CheckAllLayer(bool ShowMessage = false,bool log = false)
        {
            try
            {
                foreach (var a in layersState)
                {
                    if (a.Value != null)
                    {
                        if (a.Value.Owner == null || a.Value.Owner != this)
                        {
                            a.Value.Owner = this;
                            if (ShowMessage)
                                Messages.Message(string.Format("Yan.Version.Compatibility.LayerOwner.Null".Translate(), a.Key), MessageTypeDefOf.RejectInput);
                        }
                    }
                    else
                        layersState.Remove(a.Key);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

		public void destroyLayer(UndergroundMapParent layer)
		{
			int depth = layer.depth;
			if (depth == -1)
				Log.Error("Destroyed layer doesn't have correct depth");
			layersState[depth] = null;
            layersState.Remove(depth);
		}

		public override void ExposeData()
		{
            string strVersion = Version.ToStringSafe();

            base.ExposeData();
			Scribe_Values.Look(ref spawned, "spawned", 0);
			Scribe_Values.Look(ref m_NextKey, "m_NextKey", 0);
			Scribe_Values.Look(ref strVersion, "Version", "0.0.0.0");
			Scribe_Collections.Look(ref layersState, "layers", LookMode.Value, LookMode.Reference, ref list2, ref list3);
			Scribe_References.Look(ref map, "map");

            Version = new Version(strVersion);

        }

		public override void MapComponentTick()
		{
            try
            {
                if (1 != spawned && spawned == 0)
                {
                    pinAllUnderground();
                    spawned = 1;
                }
            }
            catch
            {
                spawned = 1;
            }
            if (Surface != null || !(Surface.ParentHolder is UndergroundMapParent))
            {
                if (Version == null || Version < Main.CurrentVer)
                {
                    if(Version == null)
                        Version = new Version(0, 0, 0, 0);
                    Messages.Message("Yan.Version.Low".Translate(), MessageTypeDefOf.RejectInput);
                    /*  
                     *  第一次做版本兼容之前的版本
                     *  1、修复Owner
                     *  2、绑定m_Next
                     *  3、清理null地层
                     */


                    //Position 4.0.0
                    if (Version < new Version(0, 4, 0, 0))
                    {
                        try
                        {
                            if (m_NextKey < 1)
                                m_NextKey = getNextEmptyLayer();
                        }
                        catch(Exception ex)
                        {
                            m_NextKey = getNextEmptyLayer();
                            Messages.Message(string.Format("Yan.Version.Error_Fixed".Translate(), "Position 4.0.0", ex.Message), MessageTypeDefOf.RejectInput);
                        }

                        CheckAllLayer(true);
                        Messages.Message(string.Format("Yan.Version.Compatibility.Finished".Translate(), new Version(0, 4, 1, 0).ToString()), MessageTypeDefOf.RejectInput);
                    }

                    Version = Main.CurrentVer;
                }
                else if (Version > Main.CurrentVer && !Main.Warned)
                {
                    Main.Warned = true;
                    Messages.Message("Yan.Version.High".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
        }

        public string GetUniqueLoadID()
        {
            return "Underground_Manager_" + Surface.uniqueID;
        }
    }
}
