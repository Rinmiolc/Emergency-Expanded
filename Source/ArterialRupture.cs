using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_DamageWorker_ArterialRupture
    {
        // 注意：参数必须是 Thing thing，不可更改为其他名字，否则会导致 Harmony 找不到参数报错
        public static void Postfix(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
        {
            // 1. 基础过滤：如果没有造成任何伤口，直接跳过
            if (__result == null || __result.hediffs == null || !__result.hediffs.Any()) return;
            
            // 将受击物体转换为 Pawn (生物)
            Pawn pawn = thing as Pawn; 
            
            // 2. 生理过滤：必须是活的、有血肉的、体内有设定血液的、且不是异常活死人(蹒跚怪)的生物
            if (pawn == null || pawn.Dead) return;
            if (!pawn.RaceProps.IsFlesh) return;        // 排除机械族、石头人等
            if (pawn.RaceProps.BloodDef == null) return;// 排除任何没有设定血液的异星生物
            if (pawn.IsShambler) return;                // 排除 1.5 异常 DLC 的蹒跚怪(死尸驱动)

            // 3. 伤害类型过滤：只有动能穿透伤害会引发动脉破裂 (排除烧伤、钝器、毒素等)
            if (dinfo.Def != DamageDefOf.Bullet && 
                dinfo.Def != DamageDefOf.Cut && 
                dinfo.Def != DamageDefOf.Stab && 
                dinfo.Def.defName != "Shredded") return;

            bool ruptureAdded = false;

            // 4. 遍历刚刚结算生成的伤口（包括普通外伤与断肢）
            foreach (Hediff hediff in __result.hediffs)
            {
                if (ruptureAdded) break; // 保证单次受击(例如一颗子弹)最多只引发一次动脉破裂

                if (hediff.Part != null && (hediff is Hediff_Injury || hediff is Hediff_MissingPart))
                {
                    // 检查受损部位是否是大血管分布区
                    if (EE_MedicalUtility.IsMajorVesselPart(hediff.Part, pawn))
                    {
                        float ruptureChance = 0f;

                        // 部位判定
                        if (hediff.Part == pawn.RaceProps.body.corePart)
                        {
                            ruptureChance = EE_Settings.ArterialRuptureChanceTorso; 
                        }
                        else
                        {
                            ruptureChance = EE_Settings.ArterialRuptureChanceLimb; 
                        }

                        // 【硬核几率平衡】几率与单次伤害量线性挂钩：伤害量以 15（突击步枪级别）为 100% 基础几率，最小 4 点起判
                        float damageScale = Mathf.Clamp01(dinfo.Amount / 15f);
                        float finalChance = ruptureChance * damageScale;

                        // 5. 掷骰子判定是否破裂
                        if (dinfo.Amount >= 4f && Rand.Chance(finalChance))
                        {
                            HediffDef ruptureDef = EE_DefOf.ArterialRupture;
                            if (ruptureDef != null)
                            {
                                // 安全地获取最近的未缺失身体部分（如果该部位已断开，则加到其母体断面上，防止原版引擎报错）
                                BodyPartRecord targetPart = EE_MedicalUtility.GetNearestNonMissingPart(pawn, hediff.Part);

                                // 往该部位添加“动脉破裂”状态
                                Hediff rupture = HediffMaker.MakeHediff(ruptureDef, pawn, targetPart);
                                rupture.Severity = 1.0f; // 初始严重度
                                pawn.health.AddHediff(rupture, targetPart, dinfo, __result);
                                
                                ruptureAdded = true;
                                
                                // 游戏内抛出红色警示飘字，增强战场视觉反馈
                                if (pawn.Spawned && pawn.Map != null)
                                {
                                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "动脉破裂!", UnityEngine.Color.red);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}