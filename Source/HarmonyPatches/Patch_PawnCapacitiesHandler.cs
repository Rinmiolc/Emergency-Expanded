using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(PawnCapacitiesHandler), "GetLevel")]
    public static class Patch_PawnCapacitiesHandler_GetLevel
    {
        [HarmonyPostfix]
        public static void Postfix(PawnCapacitiesHandler __instance, PawnCapacityDef capacity, ref float __result, Pawn ___pawn)
        {
            if (___pawn == null || ___pawn.Dead || !___pawn.RaceProps.IsFlesh) return;

            // 当小人正在接受心肺复苏 (CPR) 时，人工维持呼吸和血液循环能力到最低 60%
            if (capacity == PawnCapacityDefOf.BloodPumping || capacity == PawnCapacityDefOf.Breathing)
            {
                if (EE_DefOf.EE_CPR_Receiving != null && ___pawn.health.hediffSet.HasHediff(EE_DefOf.EE_CPR_Receiving))
                {
                    if (__result < EE_Constants.CprMinCapacityLevel)
                    {
                        __result = EE_Constants.CprMinCapacityLevel;
                    }
                }
            }
        }
    }
}
