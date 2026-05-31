using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Recipe_ExtractHemogen), "AvailableOnNow")]
    public static class Patch_Recipe_ExtractHemogen_AvailableOnNow
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, ref bool __result)
        {
            if (!__result) return;

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                // 二级失血性休克的阈值为 0.15。如果达到或超过 0.15，强行禁止抽血，防止配合 0.45 抽血量直接突破 0.55 致死线
                if (bloodLoss != null && bloodLoss.Severity >= 0.15f)
                {
                    __result = false;
                }
            }
        }
    }
}
