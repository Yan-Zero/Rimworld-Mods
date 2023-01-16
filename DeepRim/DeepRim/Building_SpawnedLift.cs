using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace DeepRim
{
	public class Building_SpawnedLift : Building
	{
		public int depth;
        public Thing Spawner;

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref depth, "depth", 0);
			Scribe_References.Look(ref Spawner, "Spawner");
		}

		public override string GetInspectString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(string.Concat(new object[]
			{
                "Depth".Translate(),
				depth == 0 ? "Yan.Target.Surface".Translate() : depth + "0m",
            }));
			stringBuilder.Append(base.GetInspectString());
			return stringBuilder.ToString();
		}
	}

    public class Building_SpawnedFreightElevator : Building
    {
        public int depth;
        public Thing Spawner;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref depth, "depth", 0);
            Scribe_References.Look(ref Spawner, "Spawner");
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Concat(new object[]
            {
                "Yan.Mode".Translate(),
                !((Building_FreightElevator)Spawner).Receive ? "Yan.Mode.Receive".Translate() : "Yan.Mode.Send".Translate()
            }));
            stringBuilder.AppendLine(string.Concat(new object[]
            {
                "Depth".Translate(),
                depth,
                "0m"
            }));
            stringBuilder.Append(base.GetInspectString());
            return stringBuilder.ToString();
        }
    }
}
