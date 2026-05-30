using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace EmergencyExpanded
{
    // 使用无参数的 [HarmonyPatch] 标记，由 TargetMethod() 在运行时动态通过反射返回目标方法，
    // 这在不同版本的 RimWorld 或是存在其他 Mod 修改该签名时，都具有 100% 的加载成功率与出色的兼容性！
    [HarmonyPatch]
    public static class Patch_ChoicesAtFor
    {
        // 动态定位目标方法
        public static MethodBase TargetMethod()
        {
            var method = AccessTools.Method(typeof(FloatMenuMakerMap), "GetOptions");
            if (method == null)
            {
                Log.Error("[EE] GetOptions not found on FloatMenuMakerMap!");
            }
            return method;
        }

        // Postfix 补丁：当玩家右键点击时触发
        [HarmonyPostfix]
        public static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
        {
            if (selectedPawns == null || selectedPawns.Count != 1) return;
            Pawn pawn = selectedPawns[0];

            // 1. 基础判定：治疗者必须是人类、未昏迷、具备操控与移动能力、被玩家控制
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Faction.IsPlayer) return;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || 
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return;

            // 2. 确定右键点击的目标 Pawn
            IntVec3 clickCell = IntVec3.FromVector3(clickPos);
            Pawn targetPawn = clickCell.GetFirstPawn(pawn.Map);
            if (targetPawn == null || targetPawn == pawn) return; // 无法对自己通过右键实施野外急救
            if (!targetPawn.RaceProps.IsFlesh || targetPawn.RaceProps.BloodDef == null) return; // 必须是血肉生物且具有血液

            // 2.5. CPR 心肺复苏判定 (不消耗任何道具，手空着也能做)
            if (EE_DefOf.VentricularFibrillation != null && targetPawn.Downed && targetPawn.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation))
            {
                string cprLabel = $"为 {targetPawn.LabelShort} 进行心肺复苏 (CPR)";
                Action cprAction = () =>
                {
                    if (EE_DefOf.EE_PerformCPR != null)
                    {
                        Job job = JobMaker.MakeJob(EE_DefOf.EE_PerformCPR, targetPawn);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                };
                FloatMenuOption cprOption = new FloatMenuOption(cprLabel, cprAction, MenuOptionPriority.High, null, targetPawn);
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(cprOption, pawn, targetPawn));
            }

            // 3. 扫描治疗者背包里的急救道具和食物/药物
            List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(pawn);
            if (availableItems.Count == 0) return;

            // 4. 按物品定义分组，避免背包有多层同类堆叠时出现重复选项
            var groupedItems = availableItems.GroupBy(t => t.def);
            foreach (var group in groupedItems)
            {
                ThingDef itemDef = group.Key;
                Thing firstThing = group.First();
                int totalCount = group.Sum(t => t.stackCount);
                EmergencyItemType type = EE_FirstAidUtility.GetEmergencyItemType(itemDef);

                // 5. 判定目标是否需要/能接受该类型的物品
                if (!EE_FirstAidUtility.CanApplyToTarget(targetPawn, type, itemDef))
                {
                    continue; 
                }

                // 6. 构建右键菜单选项
                string optionLabel = "";
                bool isBleeding = targetPawn.health.hediffSet.BleedRateTotal > 0.01f;

                if (type == EmergencyItemType.Tourniquet || 
                    ((type == EmergencyItemType.FirstAidKit || type == EmergencyItemType.Medicine) && isBleeding))
                {
                    optionLabel = $"为 {targetPawn.LabelShort} 紧急止血 (剩余: {totalCount})";
                }
                else if (type == EmergencyItemType.Defibrillator)
                {
                    optionLabel = $"使用除颤仪为 {targetPawn.LabelShort} 除颤 (剩余: {totalCount})";
                }
                else
                {
                    optionLabel = $"对 {targetPawn.LabelShort} 使用 {itemDef.LabelCap} (剩余: {totalCount})";
                }
                
                Action action = () =>
                {
                    // 下发自定义行为 Job
                    if (EE_DefOf.EE_ApplyFirstAid != null)
                    {
                        Job job = JobMaker.MakeJob(EE_DefOf.EE_ApplyFirstAid, targetPawn, firstThing);
                        job.count = 1;
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
                };

                // 创建带图标的高级 FloatMenuOption
                FloatMenuOption option = new FloatMenuOption(optionLabel, action, MenuOptionPriority.High, null, targetPawn);
                option.iconThing = firstThing; // 方括号换成所用物品的图标，实现完美原版质感

                // 添加高优先级修饰并注入到已有的右键结果列表中
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(option, pawn, targetPawn));
            }
        }
    }


    // Harmony 补丁：阻止医生自动去使用普通药品包扎骨折，也杜绝右键菜单产生普通包扎选项
    [HarmonyPatch(typeof(WorkGiver_Tend), "HasJobOnThing")]
    public static class Patch_WorkGiver_Tend_HasJobOnThing
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            Pawn patient = t as Pawn;
            if (patient == null) return true;

            if (OnlyHasFracturesNeedTending(patient))
            {
                __result = false;
                return false; // 拦截原版 logic
            }
            return true;
        }

        private static bool OnlyHasFracturesNeedTending(Pawn patient)
        {
            bool hasFracture = false;
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.TendableNow())
                {
                    if (hediff is Hediff_Fracture)
                    {
                        hasFracture = true;
                    }
                    else
                    {
                        // 还有其他需要包扎的常规伤口，允许继续常规包扎
                        return false;
                    }
                }
            }
            return hasFracture;
        }
    }
}
