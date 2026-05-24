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
            float threshold = 0.4f;

            // 当机能跌破红线，触发全身综合急症
            if (pumping < threshold || breathing < threshold)
            {
                // 1. 触发脑缺氧 (作用于大脑)
                HediffDef brainHypoxiaDef = HediffDef.Named("CerebralHypoxia");
                if (brainHypoxiaDef != null && !__instance.hediffSet.HasHediff(brainHypoxiaDef))
                {
                    BodyPartRecord brain = __instance.hediffSet.GetBrain();
                    if (brain != null)
                    {
                        Hediff brainHypoxia = HediffMaker.MakeHediff(brainHypoxiaDef, pawn, brain);
                        brainHypoxia.Severity = 0.01f; 
                        __instance.AddHediff(brainHypoxia, brain, null, null);
                    }
                }

                // 2. 触发全身代谢性酸中毒 (作用于全身，不需要指定部位)
                HediffDef acidosisDef = HediffDef.Named("MetabolicAcidosis");
                if (acidosisDef != null && !__instance.hediffSet.HasHediff(acidosisDef))
                {
                    Hediff acidosis = HediffMaker.MakeHediff(acidosisDef, pawn, null); 
                    acidosis.Severity = 0.01f;
                    __instance.AddHediff(acidosis, null, null, null);
                }
            }
        }
    }
}