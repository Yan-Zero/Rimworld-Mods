using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Rimatomics;
using RimWorld;
using UnityEngine;
using Verse;

namespace PPC_Plugin
{ 
	[DefOf]
    public class PPCDef
	{
		public static ThingDef PPC_Plugin;

		/// <summary>
		/// Patch
		/// </summary>
		static PPCDef()
        {
			var _this = typeof(Util_Fix);
            Harmony harmony = new Harmony("Yan.PPCDef");

			harmony.Patch(AccessTools.Constructor(typeof(CompProperties_Battery)), new HarmonyMethod(_this, "CompProperties_Battery"));
			harmony.Patch(AccessTools.Method("Rimatomics.PPC_Util:DissipateCharge"), new HarmonyMethod(_this, "DissipateCharge"));
            harmony.Patch(AccessTools.Method("Rimatomics.PPC_Util:HasCharge"), new HarmonyMethod(_this, "HasCharge"));
			Log.Message("[PPCP] Patched Rimatomics");

            DefOfHelper.EnsureInitializedInCtor(typeof(PPCDef));
		}
	}

	[StaticConstructorOnStartup]
	public class PPC_MC : UniversalPipeMapComp
    {
		public static Dictionary<int, PPC_MC> CompCache = new Dictionary<int, PPC_MC>();
		public static PPC_MC loccachecomp = null;

		public List<CompPPC> CPBs = new List<CompPPC>();

		public PPC_MC(Map map) : base(map)
		{
			if (CompCache.ContainsKey(base.map.uniqueID))
				CompCache[base.map.uniqueID] = this;
			else
				CompCache.Add(base.map.uniqueID, this);
			loccachecomp = null;
		}

		public override void MapRemoved()
		{
			base.MapRemoved();
			CompCache.Remove(map.uniqueID);
			loccachecomp = null;
		}

	}

	public static class Util_Fix
    {
		public static PPC_MC PPCPlugin(this Map map)
		{
			if (PPC_MC.loccachecomp != null && PPC_MC.loccachecomp.map.uniqueID == map.uniqueID)
				return PPC_MC.loccachecomp;
			PPC_MC.loccachecomp = PPC_MC.CompCache[map.uniqueID];
			return PPC_MC.loccachecomp;
		}

        /// <summary>
        /// Patch PPC_Util 的 HasCharge。是判断电量是否大于 charge
        /// </summary>
        /// <param name="__result"></param>
        /// <param name="PowerNet"></param>
        /// <param name="charge"></param>
        /// <returns></returns>
        public static bool HasCharge(ref bool __result, PowerNet PowerNet, float charge)
		{
			// 查找PPC，或者安装了PPC插件的电池。
			List<CompPowerBattery> list = (from x in PowerNet.Map.Rimatomics().PPCs
										   where x.PowerComp.PowerNet == PowerNet
										   select x.batt into x
										   where x.StoredEnergy > 0f
										   select x).ToList();
			list.AddRange(from x in PowerNet.Map.PPCPlugin().CPBs
						  where x.PowerNet == PowerNet && x.StoredEnergy > 0f && x.parent.GetComp<CompUpgradable>().HasUpgrade(PPCDef.PPC_Plugin)
						  select x);

			if (list.NullOrEmpty())
            {
				__result = false;
				return false;
			}
			if (list.Sum((CompPowerBattery x) => x.StoredEnergy) > charge)
            {
				__result = true;
				return false;
			}
			__result = false;
			return false;
		}

		public static bool DissipateCharge(ref bool __result, PowerNet PowerNet, float charge)
		{
			List<CompPowerBattery> list = (from x in PowerNet.Map.Rimatomics().PPCs
										   where x.PowerComp.PowerNet == PowerNet
										   select x.batt into x
										   where x.StoredEnergy > 0f
										   select x).ToList();
			list.AddRange(from x in PowerNet.Map.PPCPlugin().CPBs
						  where x.PowerNet == PowerNet && x.StoredEnergy > 0f && x.parent.GetComp<CompUpgradable>().HasUpgrade(PPCDef.PPC_Plugin)
						  select x);

			if (list.NullOrEmpty())
			{
				__result = false;
				return false;
			}
			if (list.Sum((CompPowerBattery x) => x.StoredEnergy) < charge)
			{
				__result = false;
				return false;
			}
			
			int i = 0;
			while (charge > 0f)
			{
				i++;
				list.RemoveAll((CompPowerBattery x) => x.StoredEnergy <= 0f);
				if (list.NullOrEmpty() || list.Sum((CompPowerBattery x) => x.StoredEnergy) < charge)
                {
					__result = false;
					return false;
				}
				float power = Mathf.Min(charge / list.Count, list.Min((CompPowerBattery x) => x.StoredEnergy));
				foreach (CompPowerBattery item in list)
				{
					item.DrawPower(power);
					charge -= power;
				}
				if (i > 5000)
                {
					__result = false;
					return false;
				}
			}
			if (DebugSettings.godMode)
				Log.Warning(i.ToString() + "Pulse loops (god mode on)");
			__result = true;
			return false;
		}

		public static bool CompProperties_Battery(ref CompProperties_Battery __instance)
        {
			__instance.compClass = typeof(CompPPC);
			return false;
        }
	}

    /// <summary>
    /// 替换 CompPowerBattery 的
    /// </summary>
    public class CompPPC : CompPowerBattery
	{
		public bool is_PPC = false;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref is_PPC, "is_PPC", false);
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			is_PPC = parent.def.defName == "PPC";
			if (is_PPC)
				return;
			if (!parent.Map.PPCPlugin().CPBs.Contains(this))
				parent.Map.PPCPlugin().CPBs.Add(this);
			if (!parent.Map.Rimatomics().Upgradables.Contains(parent))
				parent.Map.Rimatomics().Upgradables.Add(parent);
		}

		public override void PostDeSpawn(Map map)
		{
			if (!is_PPC)
			{
				if (map.Rimatomics().Upgradables.Contains(parent))
					map.Rimatomics().Upgradables.Remove(parent);
				if (map.PPCPlugin().CPBs.Contains(this))
					map.PPCPlugin().CPBs.Remove(this);
			}
			base.PostDeSpawn(map);
		}
	}
    public class CompProperties_PPC : CompProperties_Battery
    {
        public CompProperties_PPC()
        {
            compClass = typeof(CompPPC);
        }
    }
}
