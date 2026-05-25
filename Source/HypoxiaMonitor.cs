using HarmonyLib;
using RimWorld;
using Verse;

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

            // 1. 脑缺氧判定门槛（依然保持在 0.55 极低晚期危急门槛）
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

            // 2. 代谢性酸中毒判定门槛放宽（0.80 早期低灌注或大出血即启动）
            if (pumping < 0.80f || breathing < 0.80f || bleedRate > 0.15f)
            {
                HediffDef acidosisDef = EE_DefOf.MetabolicAcidosis;
                if (acidosisDef != null && !__instance.hediffSet.HasHediff(acidosisDef))
                {
                    Hediff acidosis = HediffMaker.MakeHediff(acidosisDef, pawn, null); 
                    acidosis.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(acidosis, null, null, null);
                }
            }
        }
    }
}