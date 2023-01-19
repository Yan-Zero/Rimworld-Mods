using GeneRipper;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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

        private static IEnumerable<Gizmo> GeneRipper_GetGizmos_Postfix(IEnumerable<Gizmo> values, Building_GeneRipper __instance, Pawn ___selectedPawn)
        {
            // 不允许取消
            foreach (var i in values)
            {
                var t = (i as Command_Action)?.Label;
                if (t == "CommandCancelExtraction".Translate())
                    if (___selectedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                        i.disabled = true;
                if (t == "CommandCancelLoad".Translate())
                    if (___selectedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                        i.disabled = true;
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
                    var compGenepack = __instance.TryGetComp<CompGenepackContainer>()?.ContainedGenepacks;
                    foreach (var item in compGenepack)
                    {
                        var genepack = item;
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

                                                    genepack.Destroy();

                                                    Find.WindowStack.Add(new Dialog_SelectGene(pawn, delegate (Pawn p, GeneDef g)
                                                    {
                                                        AccessTools.FieldRefAccess<Building_GeneRipper, object>(__instance, "selectedGene") = g;
                                                        SelectPawn(__instance, p);
                                                    }));

                                                    pawn.health.AddHediff(HediffDefOf.XenogerminationComa);
                                                    pawn.health.AddHediff(HediffDefOf.XenogermLossShock);
                                                    Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.XenogermReplicating, pawn);
                                                    hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = Mathf.RoundToInt(60000f * GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange * 3);
                                                    pawn.health.AddHediff(hediff);
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

        private static void GeneRipper_CanAcceptPawn_Postfix(Building_GeneRipper __instance, ref AcceptanceReport __result, Pawn ___selectedPawn, Pawn pawn)
        {
            // 判断是不是要被抽取基因组的人
            if (___selectedPawn == null)
                return;
            var i = ___selectedPawn.health.hediffSet;
            if (__result.Reason == "GeneRipper_CurrentlyRegenerating".Translate() && i.HasHediff(HediffDefOf.XenogermLossShock))
                __result = true;
        }

        static Patches()
        {
            var _this = typeof(Patches);
            var harmony = new Harmony("Yan.SplitGenes");
            harmony.PatchAll();

            harmony.Patch(AccessTools.Method("GeneRipper.Building_GeneRipper:CanAcceptPawn"), null, new HarmonyMethod(_this, "GeneRipper_CanAcceptPawn_Postfix"));
            harmony.Patch(AccessTools.Method("GeneRipper.Building_GeneRipper:GetGizmos"), null, new HarmonyMethod(_this, "GeneRipper_GetGizmos_Postfix"));
            harmony.Patch(AccessTools.Method("GeneRipper.Dialog_SelectGene:DoWindowContents"), new HarmonyMethod(_this, "Dialog_SelectGene_DoWindowContents"));
            Log.Message("[SplitGenes] GeneRipper Patched");
        }
    }
}
