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
            
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh || !pawn.IsHashIntervalTick(60)) 
                return;

            float pumping = __instance.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = __instance.capacities.GetLevel(PawnCapacityDefOf.Breathing);

            if (pumping < EE_Settings.HypoxiaMonitorThreshold || breathing < EE_Settings.HypoxiaMonitorThreshold)
            {
                // 1. 使用 EE_DefOf 预置定义，极大地提升高频执行性能，避免高频哈希碰撞
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