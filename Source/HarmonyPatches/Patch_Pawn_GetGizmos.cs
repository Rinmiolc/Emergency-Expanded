using System.Collections.Generic;
using HarmonyLib;
using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos_FastFirstAid
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || !__instance.Faction.IsPlayer || __instance.Dead || __instance.Downed)
            {
                return;
            }

            if (!__instance.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                !__instance.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return;
            }

            // Must have medical items
            List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(__instance);
            if (availableItems.Count > 0)
            {
                __result = AddFastFirstAidGizmo(__result, __instance);
            }
        }

        private static IEnumerable<Gizmo> AddFastFirstAidGizmo(IEnumerable<Gizmo> gizmos, Pawn pawn)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            yield return new Command_FastFirstAid(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos_DeclareDeath
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || __instance.Dead || !__instance.RaceProps.IsFlesh)
            {
                return;
            }

            // 如果小人身上有生物学死亡倒计时，说明满足宣布死亡的条件
            if (__instance.health.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer))
            {
                __result = AddDeclareDeathGizmo(__result, __instance);
            }
        }

        private static IEnumerable<Gizmo> AddDeclareDeathGizmo(IEnumerable<Gizmo> gizmos, Pawn pawn)
        {
            foreach (Gizmo gizmo in gizmos)
            {
                yield return gizmo;
            }
            yield return new Command_DeclareDeath(pawn);
        }
    }
}
