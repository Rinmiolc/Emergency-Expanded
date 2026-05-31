using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;
using UnityEngine;

namespace EmergencyExpanded
{
    // 改为拦截 AddHediff 以彻底兼容 Combat Extended 及其它第三方武器伤害模型
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_DamageWorker_Fracture
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            if (EE_GlobalFlags.IsForcingDown) return;

            // 1. 基础过滤：仅针对新增的外伤 (Injury)
            if (hediff == null || !(hediff is Hediff_Injury)) return;
            if (!dinfo.HasValue) return;

            Pawn pawn = ___pawn;
            if (pawn == null || pawn.Dead) return;

            // 2. 兼容性与性能过滤：仅限人类 flesh 生物，排除机械族与蹒跚怪等
            if (!pawn.RaceProps.IsFlesh) return;
            if (pawn.RaceProps.BloodDef == null) return;
            if (pawn.IsShambler) return;

            if (part == null) return;

            // 3. 核心修复：如果这个部位已经被这次伤害彻底摧毁（生命值为0）或已经缺失，就不应该再添加骨折状态。
            // 否则会触发 RimWorld 引擎底层报错 "Tried to add health diff to missing part"
            if (__instance.hediffSet.GetPartHealth(part) <= 0 || __instance.hediffSet.PartIsMissing(part))
            {
                return;
            }

            if (EE_BodyPartCache.IsBonePart(part.def))
            {
                // 判定是否已经具有该部位的骨折，避免重复生成
                if (__instance.hediffSet.hediffs.Any(h => (h.def == EE_DefOf.EE_ClosedFracture || h.def == EE_DefOf.EE_OpenFracture) && h.Part == part))
                {
                    return;
                }

                float amt = dinfo.Value.Amount;
                float maxHP = part.def.GetMaxHealth(pawn);

                float fractureChance = 0f;
                float openChance = 0f;

                DamageDef def = dinfo.Value.Def;

                // 4. 根据不同伤害类型套用平衡性数学模型
                if (def == DamageDefOf.Blunt || def == DamageDefOf.Crush || (def.armorCategory != null && def.armorCategory.defName == "Blunt"))
                {
                    // 近战钝击：骨折的核心来源
                    if (amt >= EE_Constants.FractureBluntDamageThreshold)
                    {
                        fractureChance = Mathf.Clamp01(amt / (maxHP * EE_Constants.FractureBluntMaxHPRatio)) * EE_Constants.FractureBluntBaseFactor;
                        fractureChance = Mathf.Max(fractureChance, EE_Constants.FractureBluntMinChance);
                        
                        if (amt >= EE_Constants.FractureBluntHeavyThreshold) fractureChance = Mathf.Max(fractureChance, EE_Constants.FractureBluntHeavyMinChance);

                        openChance = EE_Constants.FractureBluntOpenChance;
                    }
                }
                else if (def == DamageDefOf.Bullet || def.isRanged || def.defName.Contains("Arrow"))
                {
                    // 远程射击/箭矢：大幅度平衡下调，主要引发闭合骨折
                    if (amt >= EE_Constants.FractureRangedDamageThreshold)
                    {
                        fractureChance = EE_Constants.FractureRangedChance;
                        openChance = EE_Constants.FractureRangedOpenChance;
                    }
                }
                else if (def == DamageDefOf.Bomb || def.isExplosive || def.defName.Contains("Explosion"))
                {
                    // 爆炸伤害：平衡性下调至 30% 几率
                    if (amt >= EE_Constants.FractureExplosionDamageThreshold)
                    {
                        fractureChance = EE_Constants.FractureExplosionChance;
                        openChance = EE_Constants.FractureExplosionOpenChance;
                    }
                }
                else if (def.armorCategory == DamageArmorCategoryDefOf.Sharp)
                {
                    // 近战锐器斩击：致残几率适中
                    if (amt >= EE_Constants.FractureSharpDamageThreshold)
                    {
                        fractureChance = EE_Constants.FractureSharpChance;
                        openChance = EE_Constants.FractureSharpOpenChance;
                    }
                }

                // 乘上全局设置倍率
                fractureChance *= EE_Settings.FractureChanceMultiplier;

                if (EE_Settings.DebugMode)
                {
                    fractureChance = 0.90f;
                    openChance = 0.90f;
                }

                // 5. 摇号判定骨折生成
                if (fractureChance > 0f && Rand.Chance(fractureChance))
                {
                    bool isOpen = Rand.Chance(openChance);
                    HediffDef fracDef = isOpen ? EE_DefOf.EE_OpenFracture : EE_DefOf.EE_ClosedFracture;

                    if (fracDef != null)
                    {
                        // 实例化并添加骨折 Hediff
                        Hediff_Fracture fracture = (Hediff_Fracture)HediffMaker.MakeHediff(fracDef, pawn, part);
                        fracture.Severity = Mathf.Clamp(amt * EE_Constants.FractureSeverityConversionFactor, EE_Constants.FractureSeverityMin, EE_Constants.FractureSeverityMax); 
                        
                        __instance.AddHediff(fracture, part, dinfo, result);

                        // 开放性骨折自带高额出血，在此联动大出血机制，极其真实
                        if (isOpen && EE_DefOf.MassiveBleeding != null)
                        {
                            // 修复：大出血不应生成在骨头上，而是生成在包裹骨头的血肉（即父节点部位）上
                            // 关键修复：确保安全追溯非缺失部位，避免 CE 等模组下瞬间多处摧毁带来的 Missing Part 报错
                            BodyPartRecord bleedPart = EE_MedicalUtility.GetNearestNonMissingPart(pawn, part.parent ?? part);

                            if (bleedPart != null && !__instance.hediffSet.PartIsMissing(bleedPart))
                            {
                                Hediff rupture = HediffMaker.MakeHediff(EE_DefOf.MassiveBleeding, pawn, bleedPart);
                                rupture.Severity = 1.0f;
                                __instance.AddHediff(rupture, bleedPart, dinfo, result);
                            }
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
