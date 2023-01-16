using Verse;
using RimWorld;

namespace DeepRim
{
    public class GenStep_RocksFromGrid_Deep : GenStep
    {
        private const int MinRoofedCellsPerGroup = 20;

        public override int SeedPart => 8204671;

        public static ThingDef RockDefAt(IntVec3 c)
        {
            ThingDef thingDef = null;
            float num = -999999f;
            for (int i = 0; i < RockNoises.rockNoises.Count; i++)
            {
                float value = RockNoises.rockNoises[i].noise.GetValue(c);
                if (value > num)
                {
                    thingDef = RockNoises.rockNoises[i].rockDef;
                    num = value;
                }
            }
            if (thingDef == null)
            {
                Log.ErrorOnce("Did not get rock def to generate at " + c, 50812);
                thingDef = ThingDefOf.Sandstone;
            }
            return thingDef;
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            map.regionAndRoomUpdater.Enabled = false;
            foreach (IntVec3 allCell in map.AllCells)
            {
                ThingDef def = GenStep_RocksFromGrid.RockDefAt(allCell);
                if (((UndergroundMapParent)map.info.parent).holeLocation.DistanceTo(allCell) > 5f)
                    GenSpawn.Spawn(def, allCell, map);
                map.roofGrid.SetRoof(allCell, RoofDefOf.RoofRockThick);
            }
            GenStep_ScatterLumpsMineable genStep_ScatterLumpsMineable = new GenStep_ScatterLumpsMineable();
            float num = 16f;
            genStep_ScatterLumpsMineable.countPer10kCellsRange = new FloatRange(num, num);
            genStep_ScatterLumpsMineable.Generate(map, default(GenStepParams));
            map.regionAndRoomUpdater.Enabled = true;
        }

        private bool IsNaturalRoofAt(IntVec3 c, Map map)
        {
            return c.Roofed(map) && c.GetRoof(map).isNatural;
        }
    }


    public class GenStep_FindDrillLocation : GenStep
	{
		public override int SeedPart => 820815231;

		public override void Generate(Map map, GenStepParams parms)
		{
			DeepProfiler.Start("RebuildAllRegions");
			map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
			DeepProfiler.End();
			MapGenerator.PlayerStartSpot = ((UndergroundMapParent)map.info.parent).holeLocation;
		}
	}
}
