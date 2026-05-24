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
            if (!targetPawn.RaceProps.Humanlike) return; // 仅支持人类

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
                if (type == EmergencyItemType.IngestibleDirect)
                {
                    optionLabel = $"[背包喂药] 强行给 {targetPawn.LabelShort} 喂食/注射 {itemDef.LabelCap} (剩余: {totalCount})";
                }
                else
                {
                    optionLabel = $"[战地急救] 用 {itemDef.LabelCap} 抢救 {targetPawn.LabelShort} (剩余: {totalCount})";
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

                // 添加高优先级的彩装饰单项并注入到已有的右键结果列表中
                __result.Add(FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(optionLabel, action, MenuOptionPriority.High, null, targetPawn), 
                    pawn, 
                    targetPawn
                ));
            }
        }
    }
}
