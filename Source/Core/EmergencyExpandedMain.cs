using HarmonyLib;
using Verse;

namespace EmergencyExpanded
{
    /// <summary>
    /// Mod 的 Harmony 补丁主初始化入口，在游戏启动加载完 Defs 后运行。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EmergencyExpandedMain
    {
        static EmergencyExpandedMain()
        {
            var harmony = new Harmony("com.rinmiolc.emergencyexpanded");
            harmony.PatchAll(); 
            Log.Message("[EE] Harmony patch has been successfully loaded.");
        }
    }
}
