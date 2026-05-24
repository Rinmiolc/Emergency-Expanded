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
            // 如果原本没有流血，或者 pawn 无效，则跳过
            if (__result <= 0f || __instance.pawn == null || __instance.pawn.Dead) return;

            // 获取当前的泵血能力
            float pumping = __instance.pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            
            // 设定一个保底流血率（例如 10%），模拟重力导致的静脉流血和组织液渗出
            // 当 pumping 为 1.0 时，流血率为 100%；当 pumping 趋近于 0 时，流血率降至 10%
            float multiplier = Mathf.Clamp(pumping, 0.1f, 1.0f);
            
            __result *= multiplier;
        }
    }
}