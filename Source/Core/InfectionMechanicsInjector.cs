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
            // 在游戏启动时，为所有的物理外伤 (Hediff_Injury) 动态挂载组件
            // 这种方式比修改 XML 兼容性更好，且不会导致读档报错
            foreach (var def in DefDatabase<HediffDef>.AllDefs)
            {
                // 彻底移除原版的随机感染组件，由我们挂载的污染度系统 (HediffComp_Contamination) 接管
                if (def.comps != null)
                {
                    def.comps.RemoveAll(c => c is HediffCompProperties_Infecter);
                }

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

                    // 挂载自定义骨折生成组件
                    if (!def.comps.Any(c => c is HediffCompProperties_FractureTrigger))
                    {
                        def.comps.Add(new HediffCompProperties_FractureTrigger());
                    }
                    
                    // 为烧伤专门挂载分级组件
                    if (def.defName == "Burn" && !def.comps.Any(c => c is HediffCompProperties_Burn))
                    {
                        def.comps.Add(new HediffCompProperties_Burn());
                    }
                }
                
                // 为原版失血挂载休克触发器
                if (def == HediffDefOf.BloodLoss || def.defName == "BloodLoss")
                {
                    if (def.comps == null) def.comps = new List<HediffCompProperties>();
                    if (!def.comps.Any(c => c is HediffCompProperties_ShockTrigger))
                    {
                        def.comps.Add(new HediffCompProperties_ShockTrigger());
                    }
                }
                
                // 为原版心脏病挂载室颤转化器
                if (def.defName == "HeartAttack")
                {
                    if (def.comps == null) def.comps = new List<HediffCompProperties>();
                    if (!def.comps.Any(c => c is HediffCompProperties_HeartAttackVFConverter))
                    {
                        def.comps.Add(new HediffCompProperties_HeartAttackVFConverter());
                    }
                }
            }

            // 动态拦截并无缝替换原版健康标签页 ITab_Pawn_Health 为大修版的 ITab_Pawn_Health_EE
            // 原位替换以保持健康标签页在最右侧的顺序不变
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                if (def.race != null && def.race.IsFlesh)
                {
                    // 1. 处理 inspectorTabs 类型列表
                    if (def.inspectorTabs == null)
                    {
                        def.inspectorTabs = new List<System.Type>();
                    }

                    // 移除任何可能的独立监护标签页定义
                    def.inspectorTabs.RemoveAll(t => t != null && t.Name == "ITab_Pawn_MedicalMonitor");

                    // 寻找原版健康标签页的索引
                    int healthIndex = def.inspectorTabs.IndexOf(typeof(ITab_Pawn_Health));
                    if (healthIndex >= 0)
                    {
                        // 原地替换，保留其在列表中的位置
                        def.inspectorTabs[healthIndex] = typeof(ITab_Pawn_Health_EE);
                    }
                    else if (!def.inspectorTabs.Contains(typeof(ITab_Pawn_Health_EE)))
                    {
                        def.inspectorTabs.Add(typeof(ITab_Pawn_Health_EE));
                    }

                    // 2. 处理已实例化的 inspectorTabsResolved 列表
                    if (def.inspectorTabsResolved != null)
                    {
                        // 移除已注册的独立监护标签页实例
                        def.inspectorTabsResolved.RemoveAll(t => t != null && t.GetType().Name == "ITab_Pawn_MedicalMonitor");

                        try
                        {
                            InspectTabBase tabInstance = InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_Health_EE));
                            if (tabInstance != null)
                            {
                                int resolvedIndex = def.inspectorTabsResolved.FindIndex(t => t != null && t.GetType() == typeof(ITab_Pawn_Health));
                                if (resolvedIndex >= 0)
                                {
                                    // 原地替换实例，保留原本的绘制顺序
                                    def.inspectorTabsResolved[resolvedIndex] = tabInstance;
                                }
                                else if (!def.inspectorTabsResolved.Contains(tabInstance))
                                {
                                    def.inspectorTabsResolved.Add(tabInstance);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error("[EE] Failed to resolve ITab_Pawn_Health_EE for " + def.defName + ": " + ex.Message);
                        }

                        // 再次确保移除原版健康标签的残留实例（已被替换则此处不会再被匹配到）
                        def.inspectorTabsResolved.RemoveAll(t => t != null && t.GetType() == typeof(ITab_Pawn_Health));
                    }
                }
            }
        }
    }
}
