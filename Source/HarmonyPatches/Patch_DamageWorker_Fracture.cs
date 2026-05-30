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
            if (EE_GlobalFlags.IsForcingDown) return;

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

            for (int i = __result.hediffs.Count - 1; i >= 0; i--)
            {
                if (fractureAdded) break; // 单次打击最多只触发一次骨折，防止刷屏

                Hediff hediff = __result.hediffs[i];

                if (hediff.Part != null && hediff is Hediff_Injury)
                {
                    // 核心修复：如果这个部位已经被这次伤害彻底摧毁（生命值为0）或已经缺失，就不应该再添加骨折状态。
                    // 否则会触发 RimWorld 引擎底层报错 "Tried to add health diff to missing part"
                    if (pawn.health.hediffSet.GetPartHealth(hediff.Part) <= 0 || pawn.health.hediffSet.PartIsMissing(hediff.Part))
                    {
                        continue;
                    }

                    if (EE_BodyPartCache.IsBonePart(hediff.Part.def))
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
                                // 直接加在受击部位上，因为上面已经拦截了缺失的部位
                                BodyPartRecord targetPart = hediff.Part;

                                // 实例化并添加骨折 Hediff
                                Hediff_Fracture fracture = (Hediff_Fracture)HediffMaker.MakeHediff(fracDef, pawn, targetPart);
                                fracture.Severity = Mathf.Clamp(amt * 0.4f, 5f, 30f); // 骨折严重度与伤害量挂钩
                                
                                pawn.health.AddHediff(fracture, targetPart, dinfo, __result);
                                fractureAdded = true;

                                // 开放性骨折自带高额出血，在此联动大出血机制，极其真实
                                if (isOpen && EE_DefOf.MassiveBleeding != null)
                                {
                                    // 修复：大出血不应生成在骨头上，而是生成在包裹骨头的血肉（即父节点部位）上
                                    BodyPartRecord bleedPart = targetPart.parent ?? targetPart;

                                    // 关键修复：添加骨折的物理伤害可能刚好压垮了这根骨头最后的血量，导致部位在上一行代码被彻底摧毁。
                                    // 因此，在叠加血管破裂前，必须再次确认部位是否存活，否则会引发 "Tried to add health diff to missing part" 报错。
                                    if (!pawn.health.hediffSet.PartIsMissing(bleedPart))
                                    {
                                        Hediff rupture = HediffMaker.MakeHediff(EE_DefOf.MassiveBleeding, pawn, bleedPart);
                                        rupture.Severity = 1.0f;
                                        pawn.health.AddHediff(rupture, bleedPart, dinfo, __result);
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

    }
}
