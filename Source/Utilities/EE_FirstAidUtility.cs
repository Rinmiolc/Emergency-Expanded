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
        FirstAidKit,     // First Aid Kit (EE_HerbalFirstAidKit, EE_FirstAidKit)
        Tourniquet,      // Tourniquet
        IngestibleDirect, // Ingestible items/drugs
        Splint,          // Primitive Splint [NEW]
        Defibrillator    // 除颤仪 [NEW]
    }

    public static class EE_FirstAidUtility
    {
        // Dynamically get the emergency item type, provides high mod compatibility
        public static EmergencyItemType GetEmergencyItemType(ThingDef def)
        {
            if (def == null) return EmergencyItemType.None;

            // 1. Custom emergency items
            if (def.defName == "EE_Tourniquet") return EmergencyItemType.Tourniquet;
            if (def.defName == "EE_PrimitiveSplint") return EmergencyItemType.Splint;
            if (def.defName == "EE_HerbalFirstAidKit" || def.defName == "EE_FirstAidKit") return EmergencyItemType.FirstAidKit;
            if (def.defName == "EE_Defibrillator") return EmergencyItemType.Defibrillator;

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
                    // First Aid Kit / Medicine: Must have tendable wounds or conditions (EXCLUDING fractures and pneumothorax)
                    foreach (Hediff hediff in patient.health.hediffSet.hediffs)
                    {
                        if (hediff.TendableNow() && !(hediff is Hediff_Fracture) && hediff.def != EE_DefOf.EE_Pneumothorax) return true;
                    }
                    return false;

                case EmergencyItemType.Defibrillator:
                    // 除颤仪：目标必须具有心室颤动/心肌梗死状态，且未死亡
                    return EE_DefOf.VentricularFibrillation != null && patient.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation);

                case EmergencyItemType.IngestibleDirect:
                    // Hemogen pack: reduce blood loss
                    if (itemDef.defName == "HemogenPack")
                    {
                        return patient.health.hediffSet.HasHediff(HediffDefOf.BloodLoss);
                    }
                    // Drugs/Food: Allowed if downed or hungry
                    if (itemDef.IsDrug && patient.Downed) return true;
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
                    ApplyDefibrillatorEffect(doctor, patient);
                    consumeItem = true;
                    break;
                case EmergencyItemType.FirstAidKit:
                    float kitQuality = item.def.defName == "EE_HerbalFirstAidKit" ? EE_Constants.FirstAidKitHerbalQuality : EE_Constants.FirstAidKitStandardQuality;
                    consumeItem = ApplyFieldTend(doctor, patient, kitQuality, allowConsecutive: true);
                    break;
                case EmergencyItemType.Medicine:
                    float medMultiplier = item.def == ThingDefOf.MedicineHerbal ? EE_Constants.MedicineHerbalMultiplier :
                                         item.def == ThingDefOf.MedicineIndustrial ? EE_Constants.MedicineIndustrialMultiplier : EE_Constants.MedicineUltratechMultiplier;
                    float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                    float finalMedQuality = UnityEngine.Mathf.Clamp01((docSkill * EE_Constants.MedicineSkillWeight + EE_Constants.MedicineBaseQuality) * medMultiplier);
                    
                    bool allowConsecutive = item.def == ThingDefOf.MedicineHerbal || 
                                            item.def == ThingDefOf.MedicineIndustrial || 
                                            item.def == ThingDefOf.MedicineUltratech;
                    consumeItem = ApplyFieldTend(doctor, patient, finalMedQuality, allowConsecutive);
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
                    if (item.stackCount > 1)
                    {
                        item.stackCount--;
                    }
                    else
                    {
                        doctor.carryTracker?.innerContainer?.Remove(item);
                        doctor.inventory?.innerContainer?.Remove(item);
                        if (!item.Destroyed) item.Destroy();
                    }
                }
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{item.def.LabelCap}已使用", EE_Constants.FirstAidMoteDurationStandard);
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

        private static bool ApplyFieldTend(Pawn doctor, Pawn patient, float tendQuality, bool allowConsecutive)
        {
            List<Hediff> hediffsToTend = new List<Hediff>();
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.TendableNow() && !(hediff is Hediff_Fracture) && hediff.def != EE_DefOf.EE_Pneumothorax)
                {
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
                float reduction = UnityEngine.Mathf.Clamp(EE_Constants.MassiveBleedingTendReductionBase + tendQuality * EE_Constants.MassiveBleedingTendReductionFactor, EE_Constants.MassiveBleedingTendReductionBase, EE_Constants.MassiveBleedingTendReductionMax);
                primaryWound.Severity -= reduction;
                
                primaryWound.Tended(tendQuality, 1.0f);

                if (primaryWound.Severity <= 0.001f)
                {
                    patient.health.RemoveHediff(primaryWound);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "大出血伤口已闭合！", EE_Constants.FirstAidMoteDurationCritical);
                    return true;
                }
                else
                {
                    int remainTimes = (int)System.Math.Ceiling(primaryWound.Severity / reduction);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"正在缝合 (还需 {remainTimes} 次)", EE_Constants.FirstAidMoteDurationLong);
                    return !allowConsecutive;
                }
            }
            else
            {
                primaryWound.Tended(tendQuality, 1.0f);
                return true;
            }
        }

        private static void ApplyDefibrillatorEffect(Pawn doctor, Pawn patient)
        {
            if (EE_DefOf.VentricularFibrillation == null) return;

            // 触发心电图电击波峰
            VitalTracker.TriggerDefibrillatorShock(patient);

            // 电击音效
            Verse.SoundDef shockSound = DefDatabase<Verse.SoundDef>.GetNamed("EE_Defibrillator_Shock", false);
            if (shockSound != null)
            {
                Verse.Sound.SoundStarter.PlayOneShot(shockSound, new TargetInfo(patient.Position, patient.Map));
            }

            Hediff vf = patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.VentricularFibrillation);
            if (vf == null) return;

            // 确定当前病情状态成功率
            float baseChance = vf.Severity < 0.60f 
                ? EE_Constants.DefibSuccessRateVF 
                : EE_Constants.DefibSuccessRateCardiacArrestBase;

            // CPR 灌注加成
            bool hasCpr = EE_DefOf.EE_CPR_Receiving != null && patient.health.hediffSet.HasHediff(EE_DefOf.EE_CPR_Receiving);
            if (hasCpr && vf.Severity >= 0.60f)
            {
                baseChance += EE_Constants.DefibSuccessRateCprBoost;
            }

            // 医生医疗技能加成 (每级 +1.5%)
            float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
            baseChance += docSkill * EE_Constants.DefibSuccessRateSkillFactor;

            // 限制成功率在 5% 至 95% 之间，保持拟真和游戏悬念
            baseChance = UnityEngine.Mathf.Clamp(baseChance, 0.05f, 0.95f);

            // 摇点判定
            if (Rand.Value <= baseChance)
            {
                // 除颤成功
                patient.health.RemoveHediff(vf);
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤成功！恢复窦性心律", EE_Constants.FirstAidMoteDurationCritical);
                
                // 如果伴有脑缺氧，飘字提醒
                if (patient.health.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia))
                {
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "自主循环已恢复", EE_Constants.FirstAidMoteDurationLong);
                }
            }
            else
            {
                // 除颤失败
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤未成功！", EE_Constants.FirstAidMoteDurationCritical);

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
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Burn, EE_Constants.DefibFailureBurnDamage, 0f, -1f, doctor, burnPart);
                    patient.TakeDamage(dinfo);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "除颤副作用：轻度皮肤灼伤", EE_Constants.FirstAidMoteDurationLong);
                }
            }
        }
    }
}
