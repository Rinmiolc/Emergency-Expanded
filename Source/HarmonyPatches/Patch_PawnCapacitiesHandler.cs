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

            // 失血性休克锁定机制：当失血超过40%时，限制最高供血和呼吸能力
            if (capacity == PawnCapacityDefOf.BloodPumping || capacity == PawnCapacityDefOf.Breathing)
            {
                float bloodLoss = ___pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
                if (bloodLoss >= 0.40f)
                {
                    // 失血40%以上时，最大能力锁定为20%
                    if (__result > 0.20f)
                    {
                        __result = 0.20f;
                    }
                }

                // 当小人正在接受心肺复苏 (CPR) 时，人工维持呼吸和血液循环能力到最低 60%
                if (EE_DefOf.EE_CPR_Receiving != null && ___pawn.health.hediffSet.HasHediff(EE_DefOf.EE_CPR_Receiving))
                {
                    if (__result < EE_Constants.CprMinCapacityLevel)
                    {
                        __result = EE_Constants.CprMinCapacityLevel;
                    }
                }
            }

            // ================= 防暴毙底层保底 (Death Prevention Clamp) =================
            // 拦截原版“关键属性跌破0直接斩杀”的底层逻辑
            // 如果计算结果 <= 0，且该属性归零会导致直接死亡 (如意识、泵血、呼吸)
            bool isFatalCapacity = capacity == PawnCapacityDefOf.Consciousness || 
                                   capacity == PawnCapacityDefOf.BloodPumping || 
                                   capacity == PawnCapacityDefOf.Breathing || 
                                   capacity == PawnCapacityDefOf.BloodFiltration;

            if (__result <= 0.001f && isFatalCapacity)
            {
                // 我们强行将结果保底锁定在 1% (0.01f)，阻止系统立刻执行死亡代码。
                // 这允许我们大量使用安全的 offset 扣除属性，而死神将被交还给 MODS (多器官衰竭) 或缺氧机制。
                __result = 0.01f;
            }
        }
    }
}
