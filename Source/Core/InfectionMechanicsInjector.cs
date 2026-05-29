using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [StaticConstructorOnStartup]
    public static class InfectionMechanicsInjector
    {
        static InfectionMechanicsInjector()
        {
            // 在游戏启动时，为所有的物理外伤 (Hediff_Injury) 动态挂载污染度组件
            // 这种方式比修改 XML 兼容性更好，且不会导致读档报错
            foreach (var def in DefDatabase<HediffDef>.AllDefs)
            {
                if (typeof(Hediff_Injury).IsAssignableFrom(def.hediffClass))
                {
                    if (def.comps == null)
                    {
                        def.comps = new List<HediffCompProperties>();
                    }
                    if (!def.comps.Any(c => c is HediffCompProperties_Contamination))
                    {
                        def.comps.Add(new HediffCompProperties_Contamination());
                    }
                }
            }
        }
    }
}
