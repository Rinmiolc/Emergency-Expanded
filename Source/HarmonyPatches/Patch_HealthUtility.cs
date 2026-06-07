using HarmonyLib;
using Verse;

namespace EmergencyExpanded
{
    /// <summary>
    /// 全局状态标志，用于在各种高频判定中快速跳过逻辑，避免性能损耗和原版生成逻辑冲突。
    /// </summary>
    public static class EE_GlobalFlags
    {
        public static bool IsForcingDown = false;
    }

    /// <summary>
    /// 拦截原版生成远古小人、剧情事件时使用的强制击倒函数，
    /// 防止我们在它计算精准伤害的过程中意外添加“大出血”等高危状态导致小人意外死亡。
    /// </summary>
    [HarmonyPatch(typeof(HealthUtility), "DamageUntilDowned")]
    public static class Patch_HealthUtility_DamageUntilDowned
    {
        public static void Prefix()
        {
            EE_GlobalFlags.IsForcingDown = true;
        }

        public static void Finalizer()
        {
            EE_GlobalFlags.IsForcingDown = false;
        }
    }
    
    [HarmonyPatch(typeof(HealthUtility), "DamageLegsUntilIncapableOfMoving")]
    public static class Patch_HealthUtility_DamageLegsUntilIncapableOfMoving
    {
        public static void Prefix()
        {
            EE_GlobalFlags.IsForcingDown = true;
        }

        public static void Finalizer()
        {
            EE_GlobalFlags.IsForcingDown = false;
        }
    }

    [HarmonyPatch(typeof(HealthUtility), "DamageUntilDead")]
    public static class Patch_HealthUtility_DamageUntilDead
    {
        public static void Prefix()
        {
            EE_GlobalFlags.IsForcingDown = true;
        }

        public static void Finalizer()
        {
            EE_GlobalFlags.IsForcingDown = false;
        }
    }
}
