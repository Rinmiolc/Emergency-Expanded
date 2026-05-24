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
        public static void Postfix(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
        {
            // 如果没有造成任何伤口，直接跳过
            if (__result == null || __result.hediffs == null || !__result.hediffs.Any()) return;
            
            Pawn pawn = thing as Pawn; 
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh) return;

            // 过滤伤害类型：只有枪击、穿刺、切割、撕裂等动能伤害会引发动脉破裂 (排除烧伤、钝器等)
            if (dinfo.Def != DamageDefOf.Bullet && 
                dinfo.Def != DamageDefOf.Cut && 
                dinfo.Def != DamageDefOf.Stab && 
                dinfo.Def.defName != "Shredded") return;

            bool ruptureAdded = false;

            // 遍历刚刚造成的伤口
            foreach (Hediff hediff in __result.hediffs)
            {
                if (ruptureAdded) break; // 单次受击最多生成一个动脉破裂

                if (hediff is Hediff_Injury injury && injury.Part != null)
                {
                    string partName = injury.Part.def.defName;
                    float ruptureChance = 0f;

                    // 躯干大概率
                    if (partName == "Torso")
                    {
                        ruptureChance = EE_Settings.ArterialRuptureChanceTorso; // 15% 概率
                    }
                    // 手臂和腿小概率 (可以根据需要加上 Shoulder, Femur 等更细分的骨骼部位)
                    else if (partName == "Arm" || partName == "Leg")
                    {
                        ruptureChance = EE_Settings.ArterialRuptureChanceLimb; // 5% 概率
                    }

                    // 掷骰子判定
                    if (ruptureChance > 0f && Rand.Chance(ruptureChance))
                    {
                        HediffDef ruptureDef = HediffDef.Named("ArterialRupture");
                        if (ruptureDef != null)
                        {
                            // 往同一部位添加动脉破裂
                            Hediff rupture = HediffMaker.MakeHediff(ruptureDef, pawn, injury.Part);
                            rupture.Severity = 1.0f; // 初始严重度
                            pawn.health.AddHediff(rupture, injury.Part, dinfo, __result);
                            
                            ruptureAdded = true;
                            
                            // 游戏内抛出红色警示飘字，增强战斗反馈
                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "动脉破裂!", Color.red);
                        }
                    }
                }
            }
        }
    }
}