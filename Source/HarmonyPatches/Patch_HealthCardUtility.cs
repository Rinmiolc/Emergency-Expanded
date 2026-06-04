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
            if (part == null || part.def == null) return 0;

            // 优先通过 BodyPartTagDef 识别以确保外星人种族兼容与性能
            // 1: 心脏 (Heart)
            if (HasTag(part, BodyPartTagDefOf.BloodPumpingSource))
                return 1;

            // 2: 大脑 (Brain)
            if (HasTag(part, BodyPartTagDefOf.ConsciousnessSource))
                return 2;

            // 3: 核心脏器 (Core Organs: Lung, Liver, Kidney, Stomach)
            if (HasTag(part, BodyPartTagDefOf.BreathingSource) || 
                HasTag(part, BodyPartTagDefOf.BloodFiltrationSource) ||
                HasTag(part, BodyPartTagDefOf.MetabolismSource))
                return 3;

            // 降级使用 defName 判定，使用 OrdinalIgnoreCase 避开 ToLower() 带来的 GC 内存分配
            string defName = part.def.defName;

            if (defName.IndexOf("heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            if (defName.IndexOf("brain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;

            if (defName.IndexOf("lung", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("liver", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("kidney", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("stomach", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;

            // 4: 躯干与四肢主体 (Torso, Limbs)
            if (defName.IndexOf("torso", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("leg", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                defName.IndexOf("shoulder", System.StringComparison.OrdinalIgnoreCase) >= 0)
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
