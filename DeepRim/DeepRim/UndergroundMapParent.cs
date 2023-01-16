using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace DeepRim
{

    public class PlaceWorker_AboveGround : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null)
        {
            if (map.ParentHolder is UndergroundMapParent)
                return "Must be placed above ground.";
            return true;
        }

        public override bool ForceAllowPlaceOver(BuildableDef otherDef)
        {
            return otherDef == ThingDefOf.SteamGeyser;
        }
    }

    public class UndergroundMapParent : MapParent
	{
		public bool shouldBeDeleted = false;
        public IntVec3 holeLocation;
        
        public UndergroundManager Owner = null;

        public Map Surface => (Owner != null) ? (Owner.Surface != null ? Owner.Surface : null) : null;

        public int depth = -1;

		public bool shouldRiver = true;

		protected override bool UseGenericEnterMapFloatMenuOption => false;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref holeLocation, "holeLocation");
			Scribe_Values.Look(ref depth, "depth", -1);
			Scribe_Values.Look(ref shouldRiver, "shouldRiver", defaultValue: true);
            Scribe_References.Look(ref Owner, "Owner");
        }

		public override IEnumerable<Gizmo> GetGizmos()
		{
			IEnumerator<Gizmo> enumerator = base.GetGizmos().GetEnumerator();
			while (enumerator.MoveNext())
			{
				yield return enumerator.Current;
			}
		}

		public bool abandonLift(Thing lift)
		{
			lift.DeSpawn();
			foreach (Building item in Map.listerBuildings.allBuildingsColonist)
			{
				if (item is Building_SpawnedLift)
				{
					Log.Message("There's still remaining shafts leading to layer.");
					return false;
				}
			}
			abandon();
            return true;
		}

		public void abandon()
		{
            foreach (Building item in Map.listerBuildings.allBuildingsColonist)
                if (item is Building_SpawnedFreightElevator)
                    ((Building_FreightElevator)((Building_SpawnedFreightElevator)item).Spawner).Disconnect();
                else if (item is Building_FreightElevator)
                    ((Building_FreightElevator)item).Disconnect();
            Log.Message("Utter destruction of a layer. GG. Never going to get it back now XDD");
			shouldBeDeleted = true;
            Owner.destroyLayer(this);
        }

		public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
		{
			alsoRemoveWorldObject = false;
			bool result = false;
			if (shouldBeDeleted)
			{
				result = true;
				alsoRemoveWorldObject = true;
			}
			return result;
		}
	}
}
