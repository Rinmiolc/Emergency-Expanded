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
        private class GroupComparer : IComparer<IGrouping<BodyPartRecord, Hediff>>
        {
            public static readonly GroupComparer Instance = new GroupComparer();

            public int Compare(IGrouping<BodyPartRecord, Hediff> x, IGrouping<BodyPartRecord, Hediff> y)
            {
                int priorityX = GetPartPriority(x.Key);
                int priorityY = GetPartPriority(y.Key);
                int compare = priorityX.CompareTo(priorityY);
                if (compare != 0) return compare;

                string labelX = x.Key != null ? x.Key.Label : "";
                string labelY = y.Key != null ? y.Key.Label : "";
                return string.Compare(labelX, labelY, System.StringComparison.Ordinal);
            }
        }

        public static void Postfix(ref IEnumerable<IGrouping<BodyPartRecord, Hediff>> __result)
        {
            if (__result == null) return;

            // 预估容量，拷贝结果并进行就地排序以完全消灭 LINQ 每帧产生的 GC 垃圾
            List<IGrouping<BodyPartRecord, Hediff>> sortedList = new List<IGrouping<BodyPartRecord, Hediff>>();
            foreach (var group in __result)
            {
                sortedList.Add(group);
            }
            sortedList.Sort(GroupComparer.Instance);
            __result = sortedList;
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
