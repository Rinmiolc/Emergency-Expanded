using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public enum EmergencyItemType
    {
        None,
        Medicine,        // 常规医药 (Herbal, Industrial, Ultratech)
        FirstAidKit,     // 战地急救包 (EE_HerbalFirstAidKit, EE_FirstAidKit)
        Tourniquet,      // 止血带
        AdrenalinePen,   // 肾上腺素注射笔
        IngestibleDirect // 所有可食用/可注射的食物与成瘾品
    }

    public static class EE_FirstAidUtility
    {
        // 动态判定物品的急救类别，提供极高的Mod兼容性
        public static EmergencyItemType GetEmergencyItemType(ThingDef def)
        {
            if (def == null) return EmergencyItemType.None;

            // 1. 特殊定制急救品
            if (def.defName == "EE_Tourniquet") return EmergencyItemType.Tourniquet;
            if (def.defName == "EE_AdrenalinePen") return EmergencyItemType.AdrenalinePen;
            if (def.defName == "EE_HerbalFirstAidKit" || def.defName == "EE_FirstAidKit") return EmergencyItemType.FirstAidKit;

            // 2. 原版与Mod常规医药
            if (def.IsMedicine) return EmergencyItemType.Medicine;

            // 3. 所有可在背包使用的食品与成瘾品/药物
            if (def.IsIngestible) return EmergencyItemType.IngestibleDirect;

            return EmergencyItemType.None;
        }

        // 扫描施救者背包里所有能对他人使用的消耗品
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

        // 判断目标是否能接受此类物品的紧急施用
        public static bool CanApplyToTarget(Pawn patient, EmergencyItemType type, ThingDef itemDef)
        {
            if (patient == null || patient.Dead) return false;

            switch (type)
            {
                case EmergencyItemType.Tourniquet:
                    // 止血带：必须有四肢流血伤口
                    return HasBleedingLimbWound(patient);

                case EmergencyItemType.AdrenalinePen:
                    // 肾上腺素：有心脏骤停(VFib)、严重酸中毒、或者处于昏迷倒地状态
                    return patient.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation) || 
                           patient.health.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis) ||
                           patient.Downed;

                case EmergencyItemType.FirstAidKit:
                case EmergencyItemType.Medicine:
                    // 急急包/医药：身上有任何需要包扎(tendable)的伤口或疾病
                    return patient.health.hediffSet.HasTendableHediff();

                case EmergencyItemType.IngestibleDirect:
                    // 食物/成瘾品：只要对方是活体，且没有饱腹；或者是成瘾品且对方倒地
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
                    if (part != null && (part.def.tags != null && (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || 
                                         part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) ||
                                         part.def.defName.ToLower().Contains("arm") || 
                                         part.def.defName.ToLower().Contains("leg")))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // 核心应用：读条完成时的医学结算
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
                case EmergencyItemType.AdrenalinePen:
                    ApplyAdrenaline(doctor, patient);
                    break;
                case EmergencyItemType.FirstAidKit:
                    float kitQuality = item.def.defName == "EE_HerbalFirstAidKit" ? 0.35f : 0.65f;
                    consumeItem = ApplyFieldTend(doctor, patient, kitQuality, isKit: true);
                    break;
                case EmergencyItemType.Medicine:
                    float medMultiplier = item.def == ThingDefOf.MedicineHerbal ? 0.45f :
                                         item.def == ThingDefOf.MedicineIndustrial ? 0.85f : 1.7f;
                    float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                    float finalMedQuality = UnityEngine.Mathf.Clamp01((docSkill * 0.05f + 0.2f) * medMultiplier);
                    consumeItem = ApplyFieldTend(doctor, patient, finalMedQuality, isKit: false);
                    break;
                case EmergencyItemType.IngestibleDirect:
                    // 强行给队友喂食/注射成瘾品
                    item.Ingested(patient, patient.needs?.food?.MaxLevel ?? 1.0f);
                    break;
            }

            if (consumeItem)
            {
                // 扣除背包库存
                if (item.stackCount > 1)
                {
                    item.stackCount--;
                }
                else
                {
                    doctor.carryTracker?.innerContainer?.Remove(item);
                    doctor.inventory?.innerContainer?.Remove(item);
                    item.Destroy();
                }
            }

            // 产生浮空文字和音效
            MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{item.def.LabelCap} 已施用", 3.0f);
            if (doctor.skills != null)
            {
                doctor.skills.Learn(SkillDefOf.Medicine, 180f);
            }
        }

        private static void ApplyTourniquet(Pawn doctor, Pawn patient)
        {
            // 止血带简单粗暴：直接将受损最严重的那条流血四肢上的所有伤口强制止血 (Tended 100%)
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
                        if (part != null && (part.def.tags != null && (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || 
                                             part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) ||
                                             part.def.defName.ToLower().Contains("arm") || 
                                             part.def.defName.ToLower().Contains("leg")))
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
                        injury.Tended(1.0f, 1.0f); // 瞬间包扎，流血归零
                    }
                }
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"肢体 {targetLimb.Label} 已结扎止血", 3.5f);
            }
        }

        private static void ApplyAdrenaline(Pawn doctor, Pawn patient)
        {
            // 1. 冰冻严重度和恶化：施加一个临时的“肾上腺素强制稳定”Hediff (EE_AdrenalineStabilized)
            HediffDef stabDef = EE_DefOf.EE_AdrenalineStabilized;
            if (stabDef != null)
            {
                Hediff stab = patient.health.hediffSet.GetFirstHediffOfDef(stabDef);
                if (stab == null)
                {
                    stab = HediffMaker.MakeHediff(stabDef, patient);
                    stab.Severity = 1.0f; // 持续 12 小时
                    patient.health.AddHediff(stab);
                }
                else
                {
                    stab.Severity = 1.0f; // 刷新时间
                }
            }

            // 2. 激发状态
            HediffDef boostDef = EE_DefOf.AdrenalineBoost;
            if (boostDef != null)
            {
                Hediff boost = patient.health.hediffSet.GetFirstHediffOfDef(boostDef);
                if (boost == null)
                {
                    boost = HediffMaker.MakeHediff(boostDef, patient);
                    boost.Severity = 1.0f;
                    patient.health.AddHediff(boost);
                }
                else
                {
                    boost.Severity = 1.0f;
                }
            }
        }

        private static bool ApplyFieldTend(Pawn doctor, Pawn patient, float tendQuality, bool isKit)
        {
            // 战地倾向逻辑
            List<Hediff> hediffsToTend = new List<Hediff>();
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.TendableNow())
                {
                    hediffsToTend.Add(hediff);
                }
            }

            if (hediffsToTend.Count == 0) return false;

            // 优先处理最危险的动脉破裂和严重流血伤口
            hediffsToTend.Sort((a, b) =>
            {
                bool aRupture = a.def == EE_DefOf.ArterialRupture;
                bool bRupture = b.def == EE_DefOf.ArterialRupture;
                if (aRupture != bRupture) return bRupture.CompareTo(aRupture);
                return b.Severity.CompareTo(a.Severity);
            });

            Hediff primaryWound = hediffsToTend[0];
            if (primaryWound.def == EE_DefOf.ArterialRupture)
            {
                // 【动脉破裂特殊机制】：一次只能包扎一部分，根据治疗品质降低严重度 0.1 ~ 0.25
                float reduction = UnityEngine.Mathf.Clamp(0.1f + tendQuality * 0.15f, 0.1f, 0.25f);
                primaryWound.Severity -= reduction;
                
                // 原版 Tend 状态更新
                primaryWound.Tended(tendQuality, 1.0f);

                if (primaryWound.Severity <= 0.001f)
                {
                    patient.health.RemoveHediff(primaryWound);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "动脉破裂已缝合消除!", 4.0f);
                    return true;
                }
                else
                {
                    int remainTimes = (int)System.Math.Ceiling(primaryWound.Severity / reduction);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"大动脉缝合中 (还需 {remainTimes} 次)", 3.5f);
                    return false;
                }
            }
            else
            {
                // 普通伤口：1 次包扎即可解决
                primaryWound.Tended(tendQuality, 1.0f);
                return true;
            }
        }
    }
}
