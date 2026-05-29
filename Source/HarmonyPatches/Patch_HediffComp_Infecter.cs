using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(HediffComp_Infecter), "CheckMakeInfection")]
    public static class Patch_HediffComp_Infecter_CheckMakeInfection
    {
        // 彻底屏蔽原版的随机感染逻辑，交由我们的污染度系统接管
        public static bool Prefix()
        {
            return false; // 返回 false 阻止原函数执行
        }
    }
}
