using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    [HarmonyPatch]
    public static class Patch_HealthDisplayWindow_StillValid
    {
        public static bool Prepare()
        {
            // Only patch if Health Display is active in the mod list
            return ModLister.GetActiveModWithIdentifier("GT.Sam.HealthDisplay") != null;
        }

        public static MethodBase TargetMethod()
        {
            Type type = GenTypes.GetTypeInAnyAssembly("GTHealthDisplay.HealthDisplayWindow");
            if (type != null)
            {
                return type.GetMethod("StillValid", BindingFlags.Static | BindingFlags.NonPublic);
            }
            return null;
        }

        public static void Postfix(ref bool __result)
        {
            if (__result) return;

            if (Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect)
            {
                MainTabWindow_Inspect inspectTab = Find.MainTabsRoot.OpenTab.TabWindow as MainTabWindow_Inspect;
                if (inspectTab != null && inspectTab.OpenTabType == typeof(ITab_Pawn_Health_EE))
                {
                    __result = true;
                }
            }
        }
    }
}
