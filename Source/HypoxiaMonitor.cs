// HypoxiaMonitor.cs 完整替换
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
            float threshold = 0.55f; // 已提高阈值

            if (pumping < threshold || breathing < threshold)
            {
                // 1. 触发脑缺氧 (互斥判定：不能是植物人)
                HediffDef vegStateDef = HediffDef.Named("VegetativeState");
                bool isVegetative = vegStateDef != null && __instance.hediffSet.HasHediff(vegStateDef);

                if (!isVegetative)
                {
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
                }

                // 2. 触发全身代谢性酸中毒
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