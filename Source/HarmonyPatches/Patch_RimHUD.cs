using HarmonyLib;
using Verse;
using RimWorld;
// 拦截RimHUD的血量预估函数，修正其对血量过低时的错误预估
namespace EmergencyExpanded
{
    // 拦截代码 (重构为 Postfix 改善兼容性)
    [HarmonyPatch(typeof(HealthUtility), "TicksUntilDeathDueToBloodLoss")]
    public static class Patch_TicksUntilDeathDueToBloodLoss
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref int __result)
        {
            if (pawn == null || pawn.Dead) return;

            float bleedRateTotal = pawn.health.hediffSet.BleedRateTotal;
            if (bleedRateTotal < 0.0001f)
            {
                __result = int.MaxValue;
                return;
            }
            
            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss, false);
            float currentSeverity = (bloodLoss != null) ? bloodLoss.Severity : 0f;
            float targetSeverity = HediffDefOf.BloodLoss.lethalSeverity;
            
            if (currentSeverity >= targetSeverity)
            {
                __result = 0;
                return;
            }

            __result = (int)(((targetSeverity - currentSeverity) / bleedRateTotal) * 60000f);
        }
    }
}