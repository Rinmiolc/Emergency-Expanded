using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_DamageWorker_Fracture
    {
        // ==========================================
        // 【重要兼容性提示】：
        // 这里的形参 thing 必须保持此名称，与 1.6 的底层反射签名一致。
        // ==========================================
        [HarmonyPostfix]
        public static void Postfix(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
        {
            // 1. 基础过滤：如果没有造成实质伤口，直接跳过
            if (__result == null || __result.hediffs == null || !__result.hediffs.Any()) return;

            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.Dead) return;

            // 2. 兼容性与性能过滤：仅限人类 flesh 生物，排除机械族与蹒跚怪等
            if (!pawn.RaceProps.IsFlesh) return;
            if (pawn.RaceProps.BloodDef == null) return;
            if (pawn.IsShambler) return;

            // 3. 遍历刚刚结算产生的伤口，寻找受损的骨骼部位
            bool fractureAdded = false;

            foreach (Hediff hediff in __result.hediffs.ToList())
            {
                if (fractureAdded) break; // 单次打击最多只触发一次骨折，防止刷屏

                if (hediff.Part != null && (hediff is Hediff_Injury || hediff is Hediff_MissingPart))
                {
                    if (IsBonePart(hediff.Part, pawn))
                    {
                        // 判定是否已经具有该部位的骨折，避免重复生成
                        if (pawn.health.hediffSet.hediffs.Any(h => (h.def == EE_DefOf.EE_ClosedFracture || h.def == EE_DefOf.EE_OpenFracture) && h.Part == hediff.Part))
                        {
                            continue;
                        }

                        float amt = dinfo.Amount;
                        float maxHP = hediff.Part.def.GetMaxHealth(pawn);

                        float fractureChance = 0f;
                        float openChance = 0f;

                        // 4. 根据不同伤害类型套用平衡性数学模型 (v3 规则)
                        if (dinfo.Def == DamageDefOf.Blunt || dinfo.Def == DamageDefOf.Crush)
                        {
                            // 近战钝击：骨折的核心来源
                            if (amt >= 10f)
                            {
                                fractureChance = Mathf.Clamp01(amt / (maxHP * 0.6f)) * 0.8f;
                                fractureChance = Mathf.Max(fractureChance, 0.50f); // 钝击保底 50% 骨折率
                                
                                if (amt >= 20f) fractureChance = Mathf.Max(fractureChance, 0.85f); // 重钝击 85% 骨折率

                                openChance = 0.05f; // 95% 闭合骨折，5% 开放骨折
                            }
                        }
                        else if (dinfo.Def == DamageDefOf.Bullet || dinfo.Def.defName == "Arrow" || dinfo.Def.defName.Contains("Arrow"))
                        {
                            // 远程射击/箭矢：大幅度平衡下调，主要引发闭合骨折
                            if (amt >= 8f)
                            {
                                fractureChance = 0.10f; // 仅 10% 几率
                                openChance = 0.20f;     // 80% 闭合骨折，20% 开放骨折
                            }
                        }
                        else if (dinfo.Def == DamageDefOf.Bomb || dinfo.Def.isExplosive || dinfo.Def.defName.Contains("Explosion"))
                        {
                            // 爆炸伤害：平衡性下调至 30% 几率
                            if (amt >= 10f)
                            {
                                fractureChance = 0.30f; // 30% 几率
                                openChance = 0.50f;     // 50% 闭合，50% 开放
                            }
                        }
                        else if (dinfo.Def.armorCategory == DamageArmorCategoryDefOf.Sharp)
                        {
                            // 近战锐器斩击：致残几率适中
                            if (amt >= 15f)
                            {
                                fractureChance = 0.30f; // 30% 几率
                                openChance = 0.60f;     // 40% 闭合，60% 开放
                            }
                        }

                        // 乘上全局设置倍率
                        fractureChance *= EE_Settings.FractureChanceMultiplier;

                        // 5. 摇号判定骨折生成
                        if (fractureChance > 0f && Rand.Chance(fractureChance))
                        {
                            bool isOpen = Rand.Chance(openChance);
                            HediffDef fracDef = isOpen ? EE_DefOf.EE_OpenFracture : EE_DefOf.EE_ClosedFracture;

                            if (fracDef != null)
                            {
                                BodyPartRecord targetPart = EE_MedicalUtility.GetNearestNonMissingPart(pawn, hediff.Part);

                                // 实例化并添加骨折 Hediff
                                Hediff_Fracture fracture = (Hediff_Fracture)HediffMaker.MakeHediff(fracDef, pawn, targetPart);
                                fracture.Severity = Mathf.Clamp(amt * 0.4f, 5f, 30f); // 骨折严重度与伤害量挂钩
                                
                                pawn.health.AddHediff(fracture, targetPart, dinfo, __result);
                                fractureAdded = true;

                                // 开放性骨折自带高额出血，在此联动大动脉破裂机制，极其真实
                                if (isOpen && EE_DefOf.ArterialRupture != null)
                                {
                                    Hediff rupture = HediffMaker.MakeHediff(EE_DefOf.ArterialRupture, pawn, targetPart);
                                    rupture.Severity = 1.0f;
                                    pawn.health.AddHediff(rupture, targetPart, dinfo, __result);
                                }

                                // 飘字警示
                                if (pawn.Spawned && pawn.Map != null)
                                {
                                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, isOpen ? "开放性骨折!" : "闭合性骨折!", Color.red);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 识别骨骼身体部位的辅助判定
        private static bool IsBonePart(BodyPartRecord part, Pawn pawn)
        {
            if (part == null || pawn == null) return false;

            // 优先检查新版本 1.6 的 Bone 标签 (动态扫描 defName，完美避免 BodyPartTagDefOf 缺少预定义字段的问题)
            if (part.def.tags != null && part.def.tags.Any(t => t.defName.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.defName.IndexOf("skeletal", System.StringComparison.OrdinalIgnoreCase) >= 0)) return true;

            // 字符串模糊匹配，提供 100% 的后备稳定性
            string name = part.def.defName;
            if (name.IndexOf("femur", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("tibia", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("humerus", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                name.IndexOf("radius", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("clavicle", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("spine", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                name.IndexOf("pelvis", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("rib", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("skull", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                name.IndexOf("jaw", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }
    }
}
