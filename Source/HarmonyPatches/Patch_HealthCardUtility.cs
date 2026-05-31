using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(HealthCardUtility), "VisibleHediffGroupsInOrder")]
    public static class Patch_HealthCardUtility_VisibleHediffGroupsInOrder
    {
        public static void Postfix(ref IEnumerable<IGrouping<BodyPartRecord, Hediff>> __result)
        {
            if (__result == null) return;

            __result = __result.OrderBy(group => GetPartPriority(group.Key))
                               .ThenBy(group => group.Key != null ? group.Key.Label : "")
                               .ToList();
        }

        private static int GetPartPriority(BodyPartRecord part)
        {
            // 0: 全身 (Whole Body)
            if (part == null) return 0;

            string defName = part.def.defName.ToLower();

            // 1: 心脏 (Heart)
            if (defName.Contains("heart") || HasTag(part, BodyPartTagDefOf.BloodPumpingSource))
                return 1;

            // 2: 大脑 (Brain)
            if (defName.Contains("brain") || HasTag(part, BodyPartTagDefOf.ConsciousnessSource))
                return 2;

            // 3: 核心脏器 (Core Organs: Lung, Liver, Kidney, Stomach)
            if (defName.Contains("lung") || defName.Contains("liver") || 
                defName.Contains("kidney") || defName.Contains("stomach") ||
                HasTag(part, BodyPartTagDefOf.BreathingSource) || 
                HasTag(part, BodyPartTagDefOf.BloodFiltrationSource) ||
                HasTag(part, BodyPartTagDefOf.MetabolismSource))
                return 3;

            // 4: 躯干与四肢主体 (Torso, Limbs)
            if (defName.Contains("torso") || defName.Contains("arm") || defName.Contains("leg") || defName.Contains("shoulder"))
                return 4;

            // 5: 其他 (Others)
            return 5;
        }

        private static bool HasTag(BodyPartRecord part, BodyPartTagDef tag)
        {
            return part.def.tags != null && part.def.tags.Contains(tag);
        }
    }
}
