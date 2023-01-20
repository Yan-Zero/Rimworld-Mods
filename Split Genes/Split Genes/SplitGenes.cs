using GeneRipper;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace Split_Genes
{
    [StaticConstructorOnStartup]
    [HarmonyPatch]
    public class Patches
    {
        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Building_Enterable), "SelectPawn")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void SelectPawn(Building_GeneRipper instance, Pawn pawn) { throw new NotImplementedException("It's a stub"); }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Dialog_SelectGene), "DrawGenesInfo")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DrawGenesInfo(Dialog_SelectGene instance, Rect rect, Thing target, float initialHeight, ref Vector2 size, ref Vector2 scrollPosition, GeneSet pregnancyGenes)
        { throw new NotImplementedException("It's a stub"); }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(Building_GeneRipper), "Settings", MethodType.Getter)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static GeneRipperSettings Settings(Building_GeneRipper instance)
        { throw new NotImplementedException("It's a stub"); }

        [HarmonyReversePatch(HarmonyReversePatchType.Snapshot)]
        [HarmonyPatch(typeof(Building_GeneRipper), "KillOccupant")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void KillOccupant(Building_GeneRipper instance, Pawn occupant)
        { throw new NotImplementedException("It's a stub"); }

        /// <summary>
        /// 不可后退 Patch
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(Dialog_SelectGene), "DoWindowContents")]
        [HarmonyPrefix]
        private static bool Dialog_SelectGene_DoWindowContents(Dialog_SelectGene __instance, Rect inRect, Pawn ___target, ref Vector2 ___scrollPosition, object ___acceptAction)
        {
            if (!___target.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                return true;

            __instance.closeOnCancel = false;
            inRect.yMax -= Window.CloseButSize.y;
            Rect rect = inRect;
            rect.xMin += 34f;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect, "ViewGenes".Translate() + ": " + ___target.genes.XenotypeLabelCap);
            Text.Font = GameFont.Small;
            GUI.color = XenotypeDef.IconColor;
            GUI.DrawTexture(new Rect(inRect.x, inRect.y, 30f, 30f), ___target.genes.XenotypeIcon);
            GUI.color = Color.white;
            inRect.yMin += 34f;
            Vector2 size = Vector2.zero;
            DrawGenesInfo(__instance, inRect, ___target, __instance.InitialSize.y, ref size, ref ___scrollPosition, null);
            if (Widgets.ButtonText(new Rect(inRect.xMax - Window.CloseButSize.x, inRect.yMax, Window.CloseButSize.x, Window.CloseButSize.y), "GeneRipper_Select".Translate(), drawBackground: true, doMouseoverSound: true, __instance.SelectedGene != null))
            {
                (___acceptAction as Action<Pawn, GeneDef>)?.Invoke(___target, __instance.SelectedGene);
                __instance.Close();
            }
            return false;
        }

        /// <summary>
        /// Gene Ripper 图标 Patch
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(Building_GeneRipper), "GetGizmos")]
        [HarmonyPostfix]
        private static IEnumerable<Gizmo> GeneRipper_GetGizmos(IEnumerable<Gizmo> values, Building_GeneRipper __instance, Pawn ___selectedPawn)
        {
            // 不允许取消
            foreach (var i in values)
            {
                var t = (i as Command_Action)?.Label;
                if (t == "CommandCancelExtraction".Translate() || t == "CommandCancelLoad".Translate())
                    if (___selectedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                    {
                        i.disabled = true;
                        i.disabledReason = "CanNotStopSplitting".Translate().CapitalizeFirst();
                    }
                yield return i;
            }

            if (___selectedPawn != null)
                yield break;

            Command_Action commandAction = new Command_Action
            {
                defaultLabel = "SplitGenes".Translate() + "...",
                defaultDesc = "SplitGenesDesc".Translate(),
                icon = __instance.InsertPawnTex,
                action = delegate
                {
                    // 先看看装了的基因包
                    List<FloatMenuOption> list = new List<FloatMenuOption>();

                    var compGenepack = __instance.TryGetComp<CompGenepackContainer>();

                    foreach (var item in compGenepack?.ContainedGenepacks)
                    {
                        var genepack = item;
                        // 选择小人
                        list.Add(new FloatMenuOption(item.LabelShortCap, delegate
                        {
                            List<FloatMenuOption> pawns = new List<FloatMenuOption>();
                            foreach (Pawn i in __instance.Map.mapPawns.AllPawnsSpawned)
                            {
                                Pawn pawn = i;
                                AcceptanceReport acceptanceReport = __instance.CanAcceptPawn(i);
                                if (i.genes != null)
                                {
                                    if (!acceptanceReport.Accepted)
                                    {
                                        if (!acceptanceReport.Reason.NullOrEmpty())
                                            pawns.Add(new FloatMenuOption(i.LabelShortCap + ": " + acceptanceReport.Reason, null, pawn, Color.white));
                                    }
                                    else
                                            pawns.Add(new FloatMenuOption(i.LabelShortCap + ", " + pawn.genes.XenotypeLabelCap, delegate
                                            {
                                                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("SplitGenes_Confirmation".Translate(pawn.NameShortColored), delegate
                                                {
                                                    pawn.genes.ClearXenogenes();
                                                    foreach (var j in genepack.GeneSet.GenesListForReading)
                                                        pawn.genes.AddGene(j, true);

                                                    compGenepack.innerContainer.TryDropAll(__instance.def.hasInteractionCell ? __instance.InteractionCell : __instance.Position, __instance.Map, ThingPlaceMode.Near, (x, y) => { x.DeSpawn(); });

                                                    Find.WindowStack.Add(new Dialog_SelectGene(pawn, delegate (Pawn p, GeneDef g)
                                                    {
                                                        AccessTools.FieldRefAccess<Building_GeneRipper, object>(__instance, "selectedGene") = g;
                                                        SelectPawn(__instance, p);
                                                    }));

                                                    pawn.health.AddHediff(HediffDefOf.XenogermLossShock);
                                                    pawn.health.AddHediff(HediffDefOf.XenogermReplicating);
                                                    //Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.XenogermReplicating, pawn);
                                                    //hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = Mathf.RoundToInt(60000f * GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange * 5);
                                                    //pawn.health.AddHediff(hediff);
                                                }, destructive: true));
                                            }, pawn, Color.white));
                                }
                            }
                            if (!pawns.Any())
                                pawns.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
                            Find.WindowStack.Add(new FloatMenu(pawns));
                        }, genepack, Color.white));
                    }
                    if (!list.Any())
                        list.Add(new FloatMenuOption("NoExtractableGene".Translate(), null));
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
            if (!__instance.PowerOn)
                commandAction.Disable("NoPower".Translate().CapitalizeFirst());
            yield return commandAction;
        }

        /// <summary>
        /// 可接受人 Patch
        /// </summary>
        [HarmonyPatch(typeof(Building_GeneRipper), "CanAcceptPawn")]
        [HarmonyPostfix]
        private static void GeneRipper_CanAcceptPawn(Building_GeneRipper __instance, ref AcceptanceReport __result, Pawn ___selectedPawn, Pawn pawn)
        {
            // 排除 Genepack 影响
            foreach (var c in __instance.innerContainer)
                if (c is Pawn)
                    return;
            if (__result.Reason.NullOrEmpty())
                return;

            // 允许抽取基因（对于某些 XenogermLossShock 的人）
            if (___selectedPawn == pawn)
                if(!pawn.health.hediffSet.HasHediff(HediffDefOf.XenogermLossShock))
                    return;
            if(__result.Reason != "Occupied".Translate() && __result.Reason != "GeneRipper_CurrentlyRegenerating".Translate())
                return;

            __result = true;
        }

        /// <summary>
        /// 有概率不死人 Patch
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(Building_GeneRipper), "KillOccupant")]
        [HarmonyPrefix]
        private static bool GeneRipper_KillOccupant(Building_GeneRipper __instance, Pawn occupant, ThingOwner ___innerContainer)
        {
            if (occupant == null || Settings(__instance) == null)
                return true;
            if(!occupant.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating) || new FloatRange(0f, 1f).RandomInRange <= Settings(__instance).BlendingChance)
                return true;

            // 加点 Debuff
            occupant.health.AddHediff(HediffDefOf.XenogerminationComa);
            occupant.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating)
                .TryGetComp<HediffComp_Disappears>().ticksToDisappear = Mathf.RoundToInt(60000f * GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange * 5);

            // 抛出小人
            ___innerContainer.TryDropAll(__instance.def.hasInteractionCell ? __instance.InteractionCell : __instance.Position, __instance.Map, ThingPlaceMode.Near);
            return false;
        }

        /// <summary>
        /// 防止钻漏子 Patch
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___selectedPawn"></param>
        [HarmonyPatch(typeof(Building_GeneRipper), "DeSpawn")]
        [HarmonyPrefix]
        private static void GeneRipper_DeSpawn(Building_GeneRipper __instance, Pawn ___selectedPawn)
        {
            if (___selectedPawn == null || !___selectedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                return;
            KillOccupant(__instance, ___selectedPawn);
        }

        /// <summary>
        /// 多余时间 Patch
        /// </summary>
        [HarmonyPatch(typeof(Building_GeneRipper), "TryAcceptPawn")]
        [HarmonyPostfix]
        private static void GeneRipper_TryAcceptPawn(Building_GeneRipper __instance, Pawn pawn, Pawn ___selectedPawn, ref int ___ticksRemaining)
        {
            if (pawn != ___selectedPawn || !pawn.health.hediffSet.HasHediff(HediffDefOf.XenogermLossShock))
                return;
            ___ticksRemaining += m_settings.SplitTicks;
        }

        private static SplitGenesSettings m_settings = LoadedModManager.GetMod<SplitGenesMod>().GetSettings<SplitGenesSettings>();

        /// <summary>
        /// 设置窗口 Patch
        /// </summary>
        /// <returns></returns>
        [HarmonyPatch(typeof(GeneRipperMod), "DoSettingsWindowContents")]
        [HarmonyPrefix]
        private static bool GeneRipperMod_DoSettingsWindowContents(GeneRipperMod __instance, GeneRipperSettings ____settings, Rect inRect)
        {
            Rect rect = inRect;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRipper_ExtractionHours".Translate());
            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            ____settings.ExtractionHours = (int)Widgets.HorizontalSlider_NewTemp(rect, ____settings.ExtractionHours, 0f, 72f, middleAlignment: true, $"{____settings.ExtractionHours} h", "0 h", "72 h", 1f);
            rect = inRect;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRipper_ExtractionHoursTooltip".Translate());

            rect = inRect;
            rect.y = inRect.y + 30f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRipper_BlendingChance".Translate());

            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.y = inRect.y + 30f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            ____settings.BlendingChance = Widgets.HorizontalSlider_NewTemp(rect, ____settings.BlendingChance, 0f, 1f, middleAlignment: true, $"{____settings.BlendingChance * 100f}%", "0%", "100%", 0.1f);
            rect = inRect;
            rect.y = inRect.y + 30f;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRipper_BlendingChanceTooltip".Translate());

            rect = inRect;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            rect.y = inRect.y + 60;
            Widgets.Label(rect, "SplitGenes_SplitHours".Translate());

            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            rect.y = inRect.y + 60;
            m_settings.SplitHours = (int)Widgets.HorizontalSlider_NewTemp(rect, m_settings.SplitHours, 0f, 72f, middleAlignment: true, $"{m_settings.SplitHours} h", "0 h", "24 h", 1f);
            rect = inRect;
            rect.height = 24f;
            rect.y = inRect.y + 60;
            TooltipHandler.TipRegion(rect, "SplitGenes_SplitHoursTooltip".Translate());

            //__instance.DoSettingsWindowContents(inRect);

            return false;
        }

        /// <summary>
        /// 初始化 Patch
        /// </summary>
        static Patches()
        {
            new Harmony("Yan.SplitGenes").PatchAll();
            Log.Message("[SplitGenes] GeneRipper Patched");
        }
    }

    /// <summary>
    /// 设置数据类
    /// </summary>
    public class SplitGenesSettings : ModSettings
    {
        public int SplitHours = 6;
        public int SplitTicks => SplitHours * 2500;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref SplitHours, "SplitHours", 6);
            base.ExposeData();
        }
    }
    
    /// <summary>
    /// Mod 设置
    /// </summary>
    public class SplitGenesMod : Mod
    {
        private SplitGenesSettings m_settings;

        public SplitGenesMod(ModContentPack content) : base(content)
        {
            m_settings = GetSettings<SplitGenesSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = inRect;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRipper_ExtractionHours".Translate());
            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            m_settings.SplitHours = (int)Widgets.HorizontalSlider_NewTemp(rect, m_settings.SplitHours, 0f, 72f, middleAlignment: true, $"{m_settings.SplitHours} h", "0 h", "24 h", 1f);
            rect = inRect;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRipper_ExtractionHoursTooltip".Translate());
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "SplitGenes".Translate();
        }
    }

    /// <summary>
    /// innerContainer 绑定到父类身上
    /// </summary>
    public class CompGenepackContainer : RimWorld.CompGenepackContainer
    {
        public override void PostPostMake()
        {
            if (!ModLister.CheckBiotech("Genepack container") || (parent as Building_Enterable) == null)
            {
                parent.Destroy();
                return;
            }
            innerContainer = (parent as Building_Enterable).innerContainer;
        }
    }

    /// <summary>
    /// 专门托管 Split Genes' CompGenepackContainer
    /// </summary>
    public class CompProperties_GenepackContainer : RimWorld.CompProperties_GenepackContainer
    {
        public CompProperties_GenepackContainer()
        {
            compClass = typeof(CompGenepackContainer);
        }
    }
}
