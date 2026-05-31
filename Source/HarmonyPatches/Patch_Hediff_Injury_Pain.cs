using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Hediff_Injury), "PainOffset", MethodType.Getter)]
    public static class Patch_Hediff_Injury_Pain
    {
        public static void Postfix(Hediff_Injury __instance, ref float __result)
        {
            if (__instance.def.defName == "Burn")
            {
                HediffComp_Burn burnComp = __instance.TryGetComp<HediffComp_Burn>();
                if (burnComp != null && burnComp.BurnDegree == 3)
                {
                    // 三度烧伤全层皮肤与神经末梢坏死。
                    // 虽然伤口边缘（相当于II度烧伤区域）依然剧痛，但中心区域是无痛的。
                    // 因此，不管三度烧伤的总面积（Severity）多大，其造成的最大痛楚被限制。
                    // 相当于限制在 12 点 Severity 产生的痛楚，避免严重烧伤叠加出不科学的无限痛楚。
                    float maxPain = 12f * __instance.def.injuryProps.painPerSeverity;
                    
                    if (__result > maxPain)
                    {
                        __result = maxPain;
                    }
                }
            }
        }
    }
}
