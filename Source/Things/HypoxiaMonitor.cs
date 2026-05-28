using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick")]
    public static class Patch_Pawn_HealthTracker_SystemicCrisisMonitor
    {
        public static void Postfix(Pawn_HealthTracker __instance)
        {
            Pawn pawn = __instance.hediffSet?.pawn;
            
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh || pawn.IsShambler || !pawn.IsHashIntervalTick(60)) 
                return;

            float pumping = __instance.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = __instance.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float bleedRate = __instance.hediffSet.BleedRateTotal;
            float bloodLossSeverity = __instance.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;

            // 1. 脑缺氧判定门槛
            if (pumping < EE_Settings.HypoxiaMonitorThreshold || breathing < EE_Settings.HypoxiaMonitorThreshold)
            {
                HediffDef vegStateDef = EE_DefOf.VegetativeState;
                bool isVegetative = vegStateDef != null && __instance.hediffSet.HasHediff(vegStateDef);

                if (!isVegetative)
                {
                    HediffDef brainHypoxiaDef = EE_DefOf.CerebralHypoxia;
                    if (brainHypoxiaDef != null && !__instance.hediffSet.HasHediff(brainHypoxiaDef))
                    {
                        BodyPartRecord brain = __instance.hediffSet.GetBrain();
                        if (brain != null)
                        {
                            Hediff brainHypoxia = HediffMaker.MakeHediff(brainHypoxiaDef, pawn, brain);
                            brainHypoxia.Severity = EE_Settings.InitialHediffSeverity; 
                            __instance.AddHediff(brainHypoxia, brain, null, null);
                        }
                    }
                }
            }

            // 2. 代谢性酸中毒 (Class III 30%失血门槛)
            if (pumping <= 0.50f || breathing <= 0.50f || bloodLossSeverity > 0.30f)
            {
                if (EE_DefOf.MetabolicAcidosis != null && !__instance.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis))
                {
                    Hediff acidosis = HediffMaker.MakeHediff(EE_DefOf.MetabolicAcidosis, pawn, null); 
                    acidosis.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(acidosis, null, null, null);
                }
            }

            // 3. 全身炎症反应综合征 (SIRS)
            float traumaLoad = 0f;
            foreach (var hediff in __instance.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury || hediff.def == EE_DefOf.TissueHypoxia)
                {
                    traumaLoad += hediff.Severity;
                }
            }
            if (traumaLoad > 10f || bloodLossSeverity > 0.30f)
            {
                if (EE_DefOf.SIRS != null && !__instance.hediffSet.HasHediff(EE_DefOf.SIRS))
                {
                    Hediff sirs = HediffMaker.MakeHediff(EE_DefOf.SIRS, pawn, null); 
                    sirs.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(sirs, null, null, null);
                }
            }

            // 4. 凝血功能障碍 (致死三联征)
            float acidosisSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
            float hypothermiaSev = __instance.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f;
            if (acidosisSev > 0.2f || (acidosisSev > 0f && hypothermiaSev > 0f && bloodLossSeverity > 0.2f))
            {
                if (EE_DefOf.Coagulopathy != null && !__instance.hediffSet.HasHediff(EE_DefOf.Coagulopathy))
                {
                    Hediff coagulopathy = HediffMaker.MakeHediff(EE_DefOf.Coagulopathy, pawn, null); 
                    coagulopathy.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(coagulopathy, null, null, null);
                }
            }

            // 5. 多器官功能衰竭 (MODS)
            float sirsSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.SIRS)?.Severity ?? 0f;
            if ((pumping <= 0.40f && sirsSev > 0.4f) || acidosisSev > 0.5f)
            {
                if (EE_DefOf.MultipleOrganFailure != null && !__instance.hediffSet.HasHediff(EE_DefOf.MultipleOrganFailure))
                {
                    Hediff mods = HediffMaker.MakeHediff(EE_DefOf.MultipleOrganFailure, pawn, null); 
                    mods.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(mods, null, null, null);
                }
            }
            // 6. 外周血管收缩与末梢缺血 (极早发生，缓慢累积，保护核心)
            if (pumping <= 0.70f || bloodLossSeverity > 0.20f)
            {
                if (Rand.Chance(EE_Constants.PeripheralHypoxiaChance))
                {
                    List<BodyPartRecord> tmpExtremities = new List<BodyPartRecord>();
                    foreach (BodyPartRecord part in __instance.hediffSet.GetNotMissingParts())
                    {
                        if (part.def == null || part.depth == BodyPartDepth.Inside) continue;
                        
                        bool isExtremity = false;
                        if (part.def.tags != null && 
                           (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment) || 
                            part.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment)))
                        {
                            if (part.parts == null || part.parts.Count == 0)
                            {
                                isExtremity = true;
                            }
                        }
                        if (!isExtremity)
                        {
                            string defNameLower = part.def.defName.ToLower();
                            if (defNameLower.Contains("finger") || defNameLower.Contains("toe") || 
                                defNameLower.Contains("nose") || defNameLower.Contains("ear"))
                            {
                                isExtremity = true;
                            }
                        }
                        if (isExtremity)
                        {
                            tmpExtremities.Add(part);
                        }
                    }

                    if (tmpExtremities.Count > 0)
                    {
                        BodyPartRecord partToAffect = tmpExtremities.RandomElement();
                        Hediff hypoxia = HediffMaker.MakeHediff(EE_DefOf.TissueHypoxia, pawn, partToAffect);
                        hypoxia.Severity = EE_Constants.PeripheralHypoxiaAmount;
                        __instance.AddHediff(hypoxia, partToAffect, null, null);
                    }
                }
            }
        }
    }
}