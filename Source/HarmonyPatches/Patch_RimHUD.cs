using HarmonyLib;
using Verse;
using RimWorld;
// 拦截RimHUD的血量预估函数，修正其对血量过低时的错误预估
namespace EmergencyExpanded
{
    // 这个标签告诉游戏在加载画面时运行这段代码
    [StaticConstructorOnStartup]
    public static class EmergencyExpandedMain
    {
        static EmergencyExpandedMain()
        {
            // 初始化 Harmony 实例并应用所有标记了 [HarmonyPatch] 的补丁
            var harmony = new Harmony("com.rinmiolc.emergencyexpanded");
            harmony.PatchAll(); 
            Log.Message("[EE] Harmony patch has been successfully loaded.");
        }
    }

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