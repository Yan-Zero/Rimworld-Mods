using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace DeepRim
{
	[StaticConstructorOnStartup]
	public static class HarmonyPatches
	{
		private static readonly Type patchType;
        public static HarmonyInstance harmonyInstance;

        static HarmonyPatches()
		{
			patchType = typeof(HarmonyPatches);
            harmonyInstance = HarmonyInstance.Create("Yan.DeepRim.com");
			Log.Message("DeepRim: Adding Harmony patch ");
			harmonyInstance.Patch(AccessTools.Property(typeof(Thing), "MarketValue").GetGetMethod(nonPublic: false), null, new HarmonyMethod(patchType, "MarketValuePostfix"));
			harmonyInstance.Patch(AccessTools.Property(typeof(Map), "Biome").GetGetMethod(nonPublic: false), null, new HarmonyMethod(patchType, "MapBiomePostfix"));

            LongEventHandler.QueueLongEvent((Action)Patch, "Initializing", true, (Action<Exception>)null);

        }

        private static void MarketValuePostfix(Thing __instance, ref float __result)
		{
			if (__instance is Building_MiningShaft)
			{
				Building_MiningShaft building_MiningShaft = __instance as Building_MiningShaft;
				__result += building_MiningShaft.ConnectedMapMarketValue;
			}
		}

        public static void Patch()
        {
            if (DefDatabase<PawnKindDef>.GetNamed("AIRobot_Hauler") != null)
            {
                var original = typeof(AIRobot.X2_Building_AIRobotRechargeStation).GetMethod("GetGizmos");
                var postfix = patchType.GetMethod("MiscRobotPostfix");

                harmonyInstance.Patch(original, null, new HarmonyMethod(postfix));
            }
        }

        public static IEnumerable<Gizmo> MiscRobotPostfix(IEnumerable<Gizmo> values, Thing __instance)
        {
            foreach (var value in values)
            {
                if ((value is Command_Action)
                    && value.disabled
                    && ((Command_Action)value).defaultLabel == "AIRobot_Label_SpawnRobot".Translate()
                    && (!__instance.Map.IsPlayerHome || __instance.Map.IsTempIncidentMap)
                    && (__instance.Map.ParentHolder is UndergroundMapParent))
                {
                    value.disabled = false;
                }

                yield return value;
            }
        }

        private static void MapBiomePostfix(Map __instance, ref BiomeDef __result)
		{
			if (__instance.ParentHolder is UndergroundMapParent)
				__result = DefDatabase<BiomeDef>.GetNamed("Underground");
		}
	}
}
