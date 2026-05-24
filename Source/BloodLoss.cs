using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(HediffSet), "CalculateBleedRate")]
    public static class Patch_CalculateBleedRate_CardiacArrest
    {
        public static void Postfix(HediffSet __instance, ref float __result)
        {
            if (__result <= 0f || __instance.pawn == null || __instance.pawn.Dead) return;
            float pumping = __instance.pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float multiplier = Mathf.Clamp(pumping, EE_Settings.MinBleedMultiplier, 1.0f);
            __result *= multiplier;
        }
    }
}