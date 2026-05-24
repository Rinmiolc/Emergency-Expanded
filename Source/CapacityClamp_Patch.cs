using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // 泵血锁血补丁
    [HarmonyPatch(typeof(PawnCapacityWorker_BloodPumping), "CalculateCapacityLevel")]
    public static class Patch_BloodPumping_AcidosisSurvival
    {
        public static void Postfix(HediffSet diffSet, ref float __result)
        {
            // 如果结算结果 <= 0，且小人处于你的急症状态，强制拉回 0.01 (1%)，防止被原版底层抹杀
            if (__result <= 0f && diffSet.HasHediff(HediffDef.Named("MetabolicAcidosis")))
            {
                __result = 0.01f;
            }
        }
    }

    // 呼吸锁血补丁
    [HarmonyPatch(typeof(PawnCapacityWorker_Breathing), "CalculateCapacityLevel")]
    public static class Patch_Breathing_AcidosisSurvival
    {
        public static void Postfix(HediffSet diffSet, ref float __result)
        {
            if (__result <= 0f && diffSet.HasHediff(HediffDef.Named("MetabolicAcidosis")))
            {
                __result = 0.01f;
            }
        }
    }
}