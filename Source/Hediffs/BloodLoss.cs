using System;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(HediffSet), "CalculateBleedRate")]
    public static class Patch_CalculateBleedRate_CardiacArrest
    {
        [ThreadStatic]
        private static bool isCalculating;

        public static void Postfix(HediffSet __instance, ref float __result)
        {
            if (isCalculating) return;
            if (__result <= 0f || __instance?.pawn == null || __instance.pawn.Dead) return;
            if (__instance.pawn.health?.capacities == null) return;

            try
            {
                isCalculating = true;
                float pumping = __instance.pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                float multiplier = Mathf.Clamp(pumping, EE_Settings.MinBleedMultiplier, 1.0f);
                __result *= (multiplier * EE_Settings.GlobalBleedingFactor);
            }
            finally
            {
                isCalculating = false;
            }
        }
    }
}