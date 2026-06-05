using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public enum EmergencyItemType
    {
        None,
        Medicine,        // Medicine (Herbal, Industrial, Ultratech)
        FirstAidKit,     // First Aid Kit (EE_FirstAidKit, EE_FirstAidBox)
        Tourniquet,      // Tourniquet
        IngestibleDirect, // Ingestible items/drugs
        Splint,          // Primitive Splint [NEW]
        Defibrillator,   // 除颤仪 [NEW]
        Irrigation       // 生理盐水冲洗 [NEW]
    }

    public static class EE_FirstAidUtility
    {
        // Dynamically get the emergency item type, provides high mod compatibility
        public static EmergencyItemType GetEmergencyItemType(ThingDef def)
        {
            if (def == null) return EmergencyItemType.None;

            // 1. Custom emergency items
            if (def.defName == "EE_Tourniquet") return EmergencyItemType.Tourniquet;
            if (def.defName == "EE_FractureRing") return EmergencyItemType.Splint;
            if (def.defName == "EE_FirstAidKit" || def.defName == "EE_FirstAidBox") return EmergencyItemType.FirstAidKit;
            if (def.defName == "EE_Defibrillator") return EmergencyItemType.Defibrillator;
            if (def.defName == "EE_Saline") return EmergencyItemType.Irrigation;

            // 2. Vanilla & modded medicines
            if (def.IsMedicine) return EmergencyItemType.Medicine;

            // 3. Ingestible items used from inventory
            if (def.IsIngestible) return EmergencyItemType.IngestibleDirect;

            return EmergencyItemType.None;
        }

        // Scan carrying pawn inventory for emergency items
        public static List<Thing> GetUsableItemsInInventory(Pawn pawn)
        {
            List<Thing> items = new List<Thing>();
            if (pawn?.inventory == null || pawn.inventory.innerContainer == null) return items;

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing != null && GetEmergencyItemType(thing.def) != EmergencyItemType.None)
                {
                    items.Add(thing);
                }
            }
            return items;
        }

        // Determine if target pawn can receive this first aid item
        public static bool CanApplyToTarget(Pawn patient, EmergencyItemType type, ThingDef itemDef)
        {
            if (patient == null || patient.Dead) return false;

            switch (type)
            {
                case EmergencyItemType.Tourniquet:
                    // Tourniquet: Must have a bleeding limb wound
                    return HasBleedingLimbWound(patient);

                case EmergencyItemType.Splint:
                    // Splint: Must have an un-immobilized fracture
                    return HasUnimmobilizedFracture(patient);

                case EmergencyItemType.FirstAidKit:
                case EmergencyItemType.Medicine:
                    // First Aid Kit / Medicine: Must have tendable wounds or conditions (EXCLUDING fractures)
                    foreach (Hediff hediff in patient.health.hediffSet.hediffs)
                    {
                        if (hediff.TendableNow() && !(hediff is Hediff_Fracture))
                        {
                            if (type == EmergencyItemType.FirstAidKit)
                            {
                                // First Aid Kits only treat physical trauma, massive bleeding and pneumothorax
                                if (!(hediff is Hediff_Injury) && !(hediff is Hediff_MissingPart) && hediff.def != EE_DefOf.MassiveBleeding && hediff.def != EE_DefOf.EE_Pneumothorax)
                                {
                                    continue;
                                }
                            }
                            return true;
                        }
                    }
                    return false;

                case EmergencyItemType.Defibrillator:
                    // 除颤仪：目标必须具有心室颤动 (VF) 或原版心脏病发作 (HeartAttack) 状态，且未死亡
                    return (EE_DefOf.EE_MyocardialInfarction != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction)) ||
                           (EE_DefOf.HeartAttack != null && patient.health.hediffSet.HasHediff(EE_DefOf.HeartAttack));
                    
                case EmergencyItemType.Irrigation:
                    // 生理盐水冲洗：目标必须有包含污染度的开放伤口
                    foreach (Hediff hediff in patient.health.hediffSet.hediffs)
                    {
                        if (hediff is Hediff_Injury injury)
                        {
                            var comp = injury.TryGetComp<HediffComp_Contamination>();
                            if (comp != null && comp.contamination > 0f) return true;
                        }
                    }
                    return false;

                case EmergencyItemType.IngestibleDirect:
                    // Hemogen pack: reduce blood loss
                    if (itemDef.defName == "HemogenPack")
                    {
                        return patient.health.hediffSet.HasHediff(HediffDefOf.BloodLoss);
                    }
                    // 检查针剂通用基类及其专属冷却状态
                    if (itemDef.ingestible != null && itemDef.ingestible.outcomeDoers != null)
                    {
                        foreach (var doer in itemDef.ingestible.outcomeDoers)
                        {
                            if (doer is IngestionOutcomeDoer_SyringeBase syringeDoer && syringeDoer.toxicityHediff != null)
                            {
                                float sev = patient.health.hediffSet.GetFirstHediffOfDef(syringeDoer.toxicityHediff)?.Severity ?? 0f;
                                if (sev >= syringeDoer.maxSeverity)
                                {
                                    return false; // 如果已达到最高蓄积上限，禁止继续注射
                                }
                                return true;
                            }
                        }
                    }
                    // Drugs/Food: Allowed if downed or hungry
                    if (itemDef.IsDrug && patient.Downed)
                    {
                        if (itemDef.ingestible != null && itemDef.ingestible.outcomeDoers != null)
                        {
                            foreach (var doer in itemDef.ingestible.outcomeDoers)
                            {
                                if (doer is RimWorld.IngestionOutcomeDoer_GiveHediff giveHediff && giveHediff.hediffDef != null)
                                {
                                    Verse.HediffDef hDef = giveHediff.hediffDef;
                                    // 忽略成瘾和耐受性状态
                                    if (hDef.isBad || hDef.defName.Contains("Tolerance") || hDef.defName.Contains("Addiction")) continue;
                                    
                                    Verse.Hediff existing = patient.health.hediffSet.GetFirstHediffOfDef(hDef);
                                    if (existing != null)
                                    {
                                        // 如果已经存在该药物带来的正面效果，为了防止无限喂药，拒绝再次施加
                                        return false; 
                                    }
                                }
                            }
                        }
                        return true;
                    }
                    if (itemDef.ingestible != null && itemDef.ingestible.CachedNutrition > 0f && patient.needs?.food != null && patient.needs.food.CurLevelPercentage < 0.9f) return true;
                    return false;
            }
            return false;
        }

        private static bool HasBleedingLimbWound(Pawn patient)
        {
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && injury.Bleeding)
                {
                    BodyPartRecord part = injury.Part;
                    if (part != null && EE_BodyPartCache.IsLimbPart(part.def))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasUnimmobilizedFracture(Pawn patient)
        {
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Fracture fracture)
                {
                    bool isImmobilized = fracture.isSplinted || fracture.isCasted || fracture.isInternallyFixed || fracture.isStrictBedrest;
                    if (!isImmobilized)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Apply item effects when job completes
        public static void ApplyFirstAidEffect(Pawn doctor, Pawn patient, Thing item)
        {
            if (doctor == null || patient == null || item == null) return;
            EmergencyItemType type = GetEmergencyItemType(item.def);
            if (type == EmergencyItemType.None) return;

            string labelCap = item.def.LabelCap;
            bool consumeItem = true;

            switch (type)
            {
                case EmergencyItemType.Tourniquet:
                    ApplyTourniquet(doctor, patient);
                    break;
                case EmergencyItemType.Splint:
                    ApplyPrimitiveSplint(doctor, patient);
                    break;
                case EmergencyItemType.Defibrillator:
                    consumeItem = ApplyDefibrillatorEffect(doctor, patient, item);
                    break;
                case EmergencyItemType.Irrigation:
                    ApplyFieldIrrigation(doctor, patient);
                    break;
                case EmergencyItemType.FirstAidKit:
                    float kitQuality = item.def.defName == "EE_FirstAidKit" ? EE_Constants.FirstAidKitHerbalQuality : EE_Constants.FirstAidKitStandardQuality;
                    consumeItem = ApplyFieldTend(doctor, patient, item, kitQuality, allowConsecutive: true, isFirstAidKit: true);
                    break;
                case EmergencyItemType.Medicine:
                    float medMultiplier = item.def == ThingDefOf.MedicineHerbal ? EE_Constants.MedicineHerbalMultiplier :
                                         item.def == ThingDefOf.MedicineIndustrial ? EE_Constants.MedicineIndustrialMultiplier : EE_Constants.MedicineUltratechMultiplier;
                    float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                    float finalMedQuality = UnityEngine.Mathf.Clamp01((docSkill * EE_Constants.MedicineSkillWeight + EE_Constants.MedicineBaseQuality) * medMultiplier);
                    
                    bool allowConsecutive = item.def == ThingDefOf.MedicineHerbal || 
                                            item.def == ThingDefOf.MedicineIndustrial || 
                                            item.def == ThingDefOf.MedicineUltratech;
                    consumeItem = ApplyFieldTend(doctor, patient, item, finalMedQuality, allowConsecutive, isFirstAidKit: false);
                    break;
                case EmergencyItemType.IngestibleDirect:
                    if (item.def.defName == "HemogenPack")
                    {
                        Hediff bloodLoss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                        if (bloodLoss != null)
                        {
                            float lethal = HediffDefOf.BloodLoss.lethalSeverity;
                            float reduction = lethal * EE_Constants.HemogenPackSeverityReductionFactor;
                            bloodLoss.Severity -= reduction;
                            if (bloodLoss.Severity <= 0.001f)
                            {
                                patient.health.RemoveHediff(bloodLoss);
                            }
                            MoteMaker.ThrowText(patient.DrawPos, patient.Map, "输血：失血已减轻", EE_Constants.FirstAidMoteDurationCritical);
                        }
                        consumeItem = true;
                    }
                    else
                    {
                        item.Ingested(patient, patient.needs?.food?.MaxLevel ?? 1.0f);
                        consumeItem = false;
                    }
                    break;
            }

            if (consumeItem)
            {
                if (!item.Destroyed)
                {
                    Thing consumed = item.SplitOff(1);
                    if (!consumed.Destroyed) 
                    {
                        consumed.Destroy();
                    }
                }
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{labelCap}已使用", EE_Constants.FirstAidMoteDurationStandard);
                if (doctor.skills != null)
                {
                    doctor.skills.Learn(SkillDefOf.Medicine, 180f);
                }
            }
        }

        private static void ApplyTourniquet(Pawn doctor, Pawn patient)
        {
            BodyPartRecord targetLimb = null;
            float maxBleeding = 0f;

            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury && injury.Bleeding)
                {
                    float bleed = injury.BleedRate;
                    if (bleed > maxBleeding)
                    {
                        BodyPartRecord part = injury.Part;
                        if (part != null && EE_BodyPartCache.IsLimbPart(part.def))
                        {
                            maxBleeding = bleed;
                            targetLimb = part;
                        }
                    }
                }
            }

            if (targetLimb != null)
            {
                foreach (Hediff hediff in patient.health.hediffSet.hediffs)
                {
                    if (hediff.Part == targetLimb && hediff is Hediff_Injury injury && injury.Bleeding)
                    {
                        injury.Tended(1.0f, 1.0f);
                    }
                }
                patient.Drawer?.renderer?.SetAllGraphicsDirty();
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{targetLimb.Label}已施加止血带", EE_Constants.FirstAidMoteDurationLong);
            }
        }

        private static void ApplyPrimitiveSplint(Pawn doctor, Pawn patient)
        {
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Fracture fracture)
                {
                    bool isImmobilized = fracture.isSplinted || fracture.isCasted || fracture.isInternallyFixed || fracture.isStrictBedrest;
                    if (!isImmobilized)
                    {
                        fracture.isSplinted = true;
                        fracture.alignmentQuality = EE_Constants.PrimitiveSplintAlignmentQuality; // Primitive splint gives 20% alignment quality
                        fracture.Tended(EE_Constants.PrimitiveSplintTendQuality, 1.0f); // Standard tend to trigger the bandage and mote!
                        patient.Drawer?.renderer?.SetAllGraphicsDirty();
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{fracture.Part.Label}已固定夹板", EE_Constants.FirstAidMoteDurationLong);
                        break;
                    }
                }
            }
        }

        private static void ApplyFieldIrrigation(Pawn doctor, Pawn patient)
        {
            bool didAnything = false;
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury)
                {
                    var comp = injury.TryGetComp<HediffComp_Contamination>();
                    if (comp != null && comp.contamination > 0f)
                    {
                        comp.contamination = UnityEngine.Mathf.Max(0f, comp.contamination - EE_Constants.SalineContaminationReduction);
                        didAnything = true;
                    }
                }
            }
            if (didAnything)
            {
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, "已野战冲洗伤口", EE_Constants.FirstAidMoteDurationStandard);
            }
        }

        private static int GetMaxMassiveBleedingTendAttempts(Thing item)
        {
            if (item == null || item.def == null) return EE_Constants.MassiveBleedingTendMaxAttempts;
            if (item.def.defName == "EE_FirstAidKit") return 5;
            if (item.def.defName == "EE_FirstAidBox") return 10;
            return EE_Constants.MassiveBleedingTendMaxAttempts;
        }

        private static bool ApplyFieldTend(Pawn doctor, Pawn patient, Thing item, float tendQuality, bool allowConsecutive, bool isFirstAidKit = false)
        {
            List<Hediff> hediffsToTend = new List<Hediff>();
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.TendableNow() && !(hediff is Hediff_Fracture))
                {
                    if (isFirstAidKit)
                    {
                        if (!(hediff is Hediff_Injury) && !(hediff is Hediff_MissingPart) && hediff.def != EE_DefOf.MassiveBleeding && hediff.def != EE_DefOf.EE_Pneumothorax)
                        {
                            continue;
                        }
                    }
                    hediffsToTend.Add(hediff);
                }
            }

            if (hediffsToTend.Count == 0) return false;

            hediffsToTend.Sort((a, b) =>
            {
                bool aRupture = a.def == EE_DefOf.MassiveBleeding;
                bool bRupture = b.def == EE_DefOf.MassiveBleeding;
                if (aRupture != bRupture) return bRupture.CompareTo(aRupture);

                bool aBleeds = a is Hediff_Injury aInj && aInj.Bleeding;
                bool bBleeds = b is Hediff_Injury bInj && bInj.Bleeding;
                if (aBleeds != bBleeds) return bBleeds.CompareTo(aBleeds);

                return b.Severity.CompareTo(a.Severity);
            });

            Hediff primaryWound = hediffsToTend[0];
            if (primaryWound.def == EE_DefOf.MassiveBleeding)
            {
                var comp = primaryWound.TryGetComp<HediffComp_MassiveBleeding>();
                if (comp != null)
                {
                    comp.tendAttempts++;
                }
                int currentAttempts = comp?.tendAttempts ?? 1;

                bool tendSuccess = false;
                float finalChance = 0f;

                if (currentAttempts <= EE_Constants.MassiveBleedingTendFailAttempts)
                {
                    // 前几次必定失败
                    tendSuccess = false;
                    finalChance = 0f;
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"止血失败 (0%)", EE_Constants.FirstAidMoteDurationLong);
                }
                else
                {
                    // 超过强制失败次数后，动态计算成功率。随着次数增加成功率递增。
                    // tendQuality 综合了医生技能、急救包类型和基础质量
                    float baseChance = tendQuality; 
                    int extraAttempts = currentAttempts - EE_Constants.MassiveBleedingTendFailAttempts;
                    finalChance = baseChance + (extraAttempts * 0.15f);
                    
                    // 限制在合理范围内
                    finalChance = UnityEngine.Mathf.Clamp(finalChance, 0.05f, 0.95f);

                    if (Rand.Value <= finalChance)
                    {
                        tendSuccess = true;
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"止血成功", EE_Constants.FirstAidMoteDurationLong);
                    }
                    else
                    {
                        tendSuccess = false;
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"止血失败 ({(finalChance * 100f):F0}%)", EE_Constants.FirstAidMoteDurationLong);
                    }
                }

                if (tendSuccess)
                {
                    // 平均3次成功才能完全包扎一个大出血 (初始严重度是 1.0)
                    // 每次成功的降低量大约在 0.25 - 0.45 之间（受包扎质量影响）
                    float reduction = 0.25f + (tendQuality * 0.20f); 
                    primaryWound.Severity -= reduction;
                    primaryWound.Tended(tendQuality, 1.0f);

                    if (primaryWound.Severity <= 0.001f)
                    {
                        patient.health.RemoveHediff(primaryWound);
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, "大出血伤口已完全闭合！", EE_Constants.FirstAidMoteDurationCritical);
                        return true; // 完全闭合，消耗掉这次使用的物资
                    }
                    else
                    {
                        return !allowConsecutive; // 尚未完全闭合，允许继续用这个包持续治疗
                    }
                }
                else
                {
                    // 失败了，处理道具损耗
                    int maxAttempts = GetMaxMassiveBleedingTendAttempts(item);

                    if (item.def.useHitPoints)
                    {
                        // 带有耐久度的急救包
                        int damageAmount = item.MaxHitPoints / maxAttempts;
                        item.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, damageAmount));
                        if (item.Destroyed || item.HitPoints <= 0)
                        {
                            MoteMaker.ThrowText(doctor.DrawPos, doctor.Map, "急救包已损坏", EE_Constants.FirstAidMoteDurationStandard);
                            return true; // 触发销毁逻辑
                        }
                    }
                    else
                    {
                        // 普通医药等无耐久物品，每失败N次强制消耗一个
                        if (currentAttempts % maxAttempts == 0)
                        {
                            MoteMaker.ThrowText(doctor.DrawPos, doctor.Map, $"{item.LabelCap}已消耗", EE_Constants.FirstAidMoteDurationStandard);
                            return true; // 强制消耗一个
                        }
                    }
                    
                    return !allowConsecutive; // 尚未完全闭合或消耗完，允许继续尝试
                }
            }
            else
            {
                primaryWound.Tended(tendQuality, 1.0f);
                return true;
            }
        }

        private static bool ApplyDefibrillatorEffect(Pawn doctor, Pawn patient, Thing item)
        {
            // 触发心电图电击波峰
            VitalTracker.TriggerDefibrillatorShock(patient);

            // 电击音效
            if (EE_DefOf.EE_Defibrillator_Shock != null)
            {
                Verse.Sound.SoundStarter.PlayOneShot(EE_DefOf.EE_Defibrillator_Shock, new TargetInfo(patient.Position, patient.Map));
            }

            // 1. 读取当前可电击的病情状态 (室颤 或 原版心脏病发作)
            Hediff vf = (EE_DefOf.EE_MyocardialInfarction != null) 
                ? patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction) 
                : null;
            Hediff heartAttack = (EE_DefOf.HeartAttack != null) 
                ? patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HeartAttack) 
                : null;

            if (vf == null && heartAttack == null) return false;

            bool isHeartAttack = heartAttack != null;
            float baseChance = 0f;

            if (isHeartAttack)
            {
                // 对原版心脏病实施电击复律 (Cardioversion)
                baseChance = EE_Constants.DefibCardioversionSuccessBase;
            }
            else
            {
                // 确定室颤阶段除颤的当前病情状态成功率
                baseChance = vf.Severity < 0.60f 
                    ? EE_Constants.DefibSuccessRateVF 
                    : EE_Constants.DefibSuccessRateCardiacArrestBase;

                // CPR 灌注加成
                bool hasCpr = EE_DefOf.EE_CPR_Receiving != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_CPR_Receiving);
                if (hasCpr && vf.Severity >= 0.60f)
                {
                    baseChance += EE_Constants.DefibSuccessRateCprBoost;
                }
            }

            // 医生医疗技能加成 (每级 +1.5%)
            float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
            baseChance += docSkill * EE_Constants.DefibSuccessRateSkillFactor;

            // ---- 新增：动态，高性能，符合现实情况的成功率判定 ----
            
            // 1. 年龄惩罚：年纪越大，心脏复苏越困难
            float age = patient.ageTracker.AgeBiologicalYearsFloat;
            if (age > EE_Constants.DefibAgePenaltyThreshold)
            {
                baseChance -= (age - EE_Constants.DefibAgePenaltyThreshold) * EE_Constants.DefibAgePenaltyPerYear;
            }

            // 2. 失血惩罚：体内没有足够的血液，除颤即使恢复心律也无法维持有效灌注
            Hediff bloodLoss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                baseChance -= bloodLoss.Severity * EE_Constants.DefibBloodLossMaxPenalty;
            }

            // 3. 低温惩罚：严重的低温症会导致心脏对电击无反应
            Hediff hypothermia = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
            if (hypothermia != null)
            {
                baseChance -= hypothermia.Severity * EE_Constants.DefibHypothermiaMaxPenalty;
            }

            // 4. 缺氧时间惩罚：脑缺氧和心肌缺血越严重，复苏希望越渺茫
            Hediff hypoxia = (EE_DefOf.CerebralHypoxia != null) 
                ? patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia) 
                : null;
            if (hypoxia != null)
            {
                baseChance -= hypoxia.Severity * EE_Constants.DefibHypoxiaMaxPenalty;
            }

            // 限制成功率在 5% 至 95% 之间
            baseChance = UnityEngine.Mathf.Clamp(baseChance, 0.05f, 0.95f);

            // 摇点判定
            if (Rand.Value <= baseChance)
            {
                // 电击复苏成功
                if (isHeartAttack)
                {
                    patient.health.RemoveHediff(heartAttack);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "电复律成功！已恢复窦性心律", EE_Constants.FirstAidMoteDurationCritical);
                }
                else
                {
                    patient.health.RemoveHediff(vf);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤成功！恢复窦性心律", EE_Constants.FirstAidMoteDurationCritical);
                    
                    // 如果伴有脑缺氧，飘字提醒
                    if (patient.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia))
                    {
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, "自主循环已恢复", EE_Constants.FirstAidMoteDurationLong);
                    }
                }
                
                return true; // 成功直接消耗
            }
            else
            {
                // 电击复苏失败
                if (isHeartAttack)
                {
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "电复律失败！", EE_Constants.FirstAidMoteDurationCritical);
                }
                else
                {
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤未成功！", EE_Constants.FirstAidMoteDurationCritical);
                }

                // 物理副作用：对 Torso（或防守后退部位）造成电击微量灼伤
                BodyPartRecord burnPart = null;
                foreach (BodyPartRecord part in patient.health.hediffSet.GetNotMissingParts())
                {
                    if (part.def == BodyPartDefOf.Torso)
                    {
                        burnPart = part;
                        break;
                    }
                }

                // 防御性回退：如果没有躯干，则使用大脑或任意部位
                if (burnPart == null)
                {
                    burnPart = patient.health.hediffSet.GetBrain() ?? patient.RaceProps.body.AllParts.FirstOrDefault();
                }

                if (burnPart != null)
                {
                    burnPart = EE_MedicalUtility.GetNearestNonMissingPart(patient, burnPart);
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, EE_Constants.DefibFailureBurnDamage, 0f, -1f, doctor, burnPart);
                    patient.TakeDamage(dinfo);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤副作用：轻度皮肤灼伤", EE_Constants.FirstAidMoteDurationLong);
                }
                
                // ---- 新增：五次失败消耗逻辑 ----
                if (item != null && !item.Destroyed)
                {
                    int damageAmount = item.MaxHitPoints / EE_Constants.DefibMaxFailures;
                    item.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, damageAmount));
                    
                    if (item.Destroyed || item.HitPoints <= 0)
                    {
                        MoteMaker.ThrowText(doctor.DrawPos, doctor.Map, "除颤仪损毁", EE_Constants.FirstAidMoteDurationStandard);
                        return true; // 视为已消耗（被摧毁）
                    }
                }

                return false; // 失败则不消耗（除非 HP <= 0）
            }
        }
    }
}
