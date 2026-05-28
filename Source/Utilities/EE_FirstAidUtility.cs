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
        Splint           // Primitive Splint [NEW]
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
                        if (hediff.TendableNow() && !(hediff is Hediff_Fracture)) return true;
                    }
                    return false;

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
                    if (part != null && (part.def.tags != null && (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || 
                                         part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) ||
                                         part.def.defName.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                                         part.def.defName.IndexOf("leg", System.StringComparison.OrdinalIgnoreCase) >= 0))
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
                case EmergencyItemType.FirstAidKit:
                    float kitQuality = item.def.defName == "EE_HerbalFirstAidKit" ? 0.35f : 0.65f;
                    consumeItem = ApplyFieldTend(doctor, patient, kitQuality, allowConsecutive: true);
                    break;
                case EmergencyItemType.Medicine:
                    float medMultiplier = item.def == ThingDefOf.MedicineHerbal ? 0.45f :
                                         item.def == ThingDefOf.MedicineIndustrial ? 0.85f : 1.7f;
                    float docSkill = doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                    float finalMedQuality = UnityEngine.Mathf.Clamp01((docSkill * 0.05f + 0.2f) * medMultiplier);
                    
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
                            float reduction = lethal * 0.12f;
                            bloodLoss.Severity -= reduction;
                            if (bloodLoss.Severity <= 0.001f)
                            {
                                patient.health.RemoveHediff(bloodLoss);
                            }
                            MoteMaker.ThrowText(patient.DrawPos, patient.Map, "输血：失血已减轻", 4.0f);
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
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{item.def.LabelCap}已使用", 3.0f);
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
                        if (part != null && (part.def.tags != null && (part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || 
                                             part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) ||
                                             part.def.defName.IndexOf("arm", System.StringComparison.OrdinalIgnoreCase) >= 0 || 
                                             part.def.defName.IndexOf("leg", System.StringComparison.OrdinalIgnoreCase) >= 0))
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
                MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{targetLimb.Label}已施加止血带", 3.5f);
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
                        fracture.alignmentQuality = 0.20f; // Primitive splint gives 20% alignment quality
                        fracture.Tended(0.40f, 1.0f); // Standard tend to trigger the bandage and mote!
                        patient.Drawer?.renderer?.SetAllGraphicsDirty();
                        MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"{fracture.Part.Label}已固定夹板", 3.5f);
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
                if (hediff.TendableNow() && !(hediff is Hediff_Fracture))
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
                float reduction = UnityEngine.Mathf.Clamp(0.1f + tendQuality * 0.15f, 0.1f, 0.25f);
                primaryWound.Severity -= reduction;
                
                primaryWound.Tended(tendQuality, 1.0f);

                if (primaryWound.Severity <= 0.001f)
                {
                    patient.health.RemoveHediff(primaryWound);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, "大出血伤口已闭合！", 4.0f);
                    return true;
                }
                else
                {
                    int remainTimes = (int)System.Math.Ceiling(primaryWound.Severity / reduction);
                    MoteMaker.ThrowText(patient.DrawPos, patient.Map, $"正在缝合 (还需 {remainTimes} 次)", 3.5f);
                    return !allowConsecutive;
                }
            }
            else
            {
                primaryWound.Tended(tendQuality, 1.0f);
                return true;
            }
        }
    }
}
