using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;

namespace EmergencyExpanded
{
    public class HediffCompProperties_FractureTrigger : HediffCompProperties
    {
        public HediffCompProperties_FractureTrigger()
        {
            this.compClass = typeof(HediffComp_FractureTrigger);
        }
    }

    public class HediffComp_FractureTrigger : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);

            if (EE_GlobalFlags.IsForcingDown) return;

            Pawn pawn = this.Pawn;
            if (pawn == null || pawn.Dead) return;

            // 1. 兼容性与性能过滤：仅限人类 flesh 生物，排除机械族与蹒跚怪等
            if (!pawn.RaceProps.IsFlesh) return;
            if (pawn.RaceProps.BloodDef == null) return;
            if (pawn.IsShambler) return;
            if (ModsConfig.AnomalyActive && pawn.IsMutant) return;

            BodyPartRecord part = this.parent.Part;
            if (part == null) return;

            // 2. 核心修复：如果这个部位已经被这次伤害彻底摧毁（生命值为0）或已经缺失，就不应该再添加骨折状态。
            if (pawn.health.hediffSet.GetPartHealth(part) <= 0 || pawn.health.hediffSet.PartIsMissing(part))
            {
                return;
            }

            if (EE_BodyPartCache.IsBonePart(part.def))
            {
                // 判定是否已经具有该部位的骨折，避免重复生成
                if (pawn.health.hediffSet.hediffs.Any(h => (h.def == EE_DefOf.EE_ClosedFracture || h.def == EE_DefOf.EE_OpenFracture) && h.Part == part))
                {
                    return;
                }

                // 如果 dinfo 为空 (如使用开发者工具 Add Hediff)，根据伤口严重度来推算
                float amt = dinfo.HasValue ? dinfo.Value.Amount : this.parent.Severity;
                DamageDef def = dinfo.HasValue ? dinfo.Value.Def : null;

                if (def == null)
                {
                    string defName = this.parent.def.defName;
                    if (defName.Contains("Crush") || defName.Contains("Blunt") || defName.Contains("Bruise")) def = DamageDefOf.Blunt;
                    else if (defName.Contains("Cut") || defName.Contains("Scratch") || defName.Contains("Stab") || defName.Contains("Bite")) def = DamageDefOf.Cut;
                    else if (defName.Contains("Gunshot") || defName.Contains("Arrow")) def = DamageDefOf.Bullet;
                    else if (defName.Contains("Bomb") || defName.Contains("Explosion")) def = DamageDefOf.Bomb;
                    else def = DamageDefOf.Blunt; // Fallback
                }

                float maxHP = part.def.GetMaxHealth(pawn);
                float fractureChance = 0f;
                float openChance = 0f;

                // 3. 根据不同伤害类型套用平衡性数学模型
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
                else if (def.isExplosive || def == DamageDefOf.Bomb)
                {
                    // 爆炸伤害：平衡性下调
                    if (amt >= EE_Constants.FractureExplosionDamageThreshold)
                    {
                        fractureChance = EE_Constants.FractureExplosionChance;
                        openChance = EE_Constants.FractureExplosionOpenChance;
                    }
                }
                else if (def.isRanged || def == DamageDefOf.Bullet)
                {
                    // 远程射击/箭矢：大幅度平衡下调，主要引发闭合骨折
                    if (amt >= EE_Constants.FractureRangedDamageThreshold)
                    {
                        fractureChance = EE_Constants.FractureRangedChance;
                        openChance = EE_Constants.FractureRangedOpenChance;
                    }
                }
                else if (def.armorCategory == DamageArmorCategoryDefOf.Sharp || def == DamageDefOf.Cut || def == DamageDefOf.Stab)
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

                // 4. 摇号判定骨折生成
                if (fractureChance > 0f && Rand.Chance(fractureChance))
                {
                    bool isOpen = Rand.Chance(openChance);
                    HediffDef fracDef = isOpen ? EE_DefOf.EE_OpenFracture : EE_DefOf.EE_ClosedFracture;

                    if (fracDef != null)
                    {
                        // 实例化并添加骨折 Hediff
                        Hediff_Fracture fracture = (Hediff_Fracture)HediffMaker.MakeHediff(fracDef, pawn, part);
                        fracture.Severity = Mathf.Clamp(amt * EE_Constants.FractureSeverityConversionFactor, EE_Constants.FractureSeverityMin, EE_Constants.FractureSeverityMax); 
                        
                        pawn.health.AddHediff(fracture, part, dinfo, null);

                        // 开放性骨折自带高额出血，在此联动大出血机制
                        if (isOpen && EE_DefOf.MassiveBleeding != null)
                        {
                            // 修复：大出血不应生成在骨头上，而是生成在包裹骨头的血肉（即父节点部位）上
                            BodyPartRecord bleedPart = EE_MedicalUtility.GetNearestNonMissingPart(pawn, part.parent ?? part);

                            if (bleedPart != null && !pawn.health.hediffSet.PartIsMissing(bleedPart))
                            {
                                Hediff rupture = HediffMaker.MakeHediff(EE_DefOf.MassiveBleeding, pawn, bleedPart);
                                rupture.Severity = 1.0f;
                                pawn.health.AddHediff(rupture, bleedPart, dinfo, null);
                            }
                        }

                        // 飘字警示与播放骨折音效
                        if (pawn.Spawned && pawn.Map != null)
                        {
                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, isOpen ? "开放性骨折!" : "闭合性骨折!", Color.red);

                            if (EE_DefOf.EE_BoneCrunch != null)
                            {
                                Verse.Sound.SoundStarter.PlayOneShot(EE_DefOf.EE_BoneCrunch, new TargetInfo(pawn.Position, pawn.Map));
                            }
                        }
                    }
                }
            }
        }
    }
}
