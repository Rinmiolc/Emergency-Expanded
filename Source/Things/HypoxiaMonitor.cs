using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace EmergencyExpanded
{
    public static class Patch_Pawn_HealthTracker_SystemicCrisisMonitor_Helper
    {
        public static void RunCrisisMonitor(Pawn_HealthTracker __instance, Pawn pawn)
        {

            float pumping = __instance.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = __instance.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float bleedRate = __instance.hediffSet.BleedRateTotal;
            Hediff bloodLoss = __instance.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            float bloodLossSeverity = bloodLoss?.Severity ?? 0f;
            float lethalSeverity = bloodLoss?.def?.lethalSeverity ?? 1f;
            if (lethalSeverity <= 0f) lethalSeverity = 1f;
            float bloodLossRatio = bloodLossSeverity / lethalSeverity; // 相对失血率 (0.0~1.0)

            // 1. 脑缺氧判定门槛 (呼吸/循环低于安全门槛，或者药物过量过重导致中枢性窒息)
            float overdoseSev = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;
            if (pumping < EE_Settings.HypoxiaMonitorThreshold || breathing < EE_Settings.HypoxiaMonitorThreshold || overdoseSev > 0.75f)
            {
                HediffDef vegStateDef = EE_DefOf.VegetativeState;
                bool isVegetative = vegStateDef != null && __instance.hediffSet.HasHediff(vegStateDef);

                if (!isVegetative)
                {
                    HediffDef brainHypoxiaDef = EE_DefOf.CerebralHypoxia;
                    if (brainHypoxiaDef != null && !__instance.hediffSet.HasHediff(brainHypoxiaDef))
                    {
                        BodyPartRecord brain = __instance.hediffSet.GetBrain();
                        if (brain != null)
                        {
                            Hediff brainHypoxia = HediffMaker.MakeHediff(brainHypoxiaDef, pawn, brain);
                            brainHypoxia.Severity = EE_Settings.InitialHediffSeverity; 
                            __instance.AddHediff(brainHypoxia, brain, null, null);
                        }
                    }
                }
            }

            // 2. 代谢性酸中毒 (Class III 30%失血门槛)
            if (pumping <= 0.50f || breathing <= 0.50f || bloodLossRatio > 0.30f)
            {
                if (EE_DefOf.MetabolicAcidosis != null && !__instance.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis))
                {
                    Hediff acidosis = HediffMaker.MakeHediff(EE_DefOf.MetabolicAcidosis, pawn, null); 
                    acidosis.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(acidosis, null, null, null);
                }
            }

            // 3. 全身炎症反应综合征 (SIRS)
            float traumaLoad = 0f;
            foreach (var hediff in __instance.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.TissueHypoxia)
                {
                    traumaLoad += hediff.Severity * EE_Constants.SirsWeightTissueHypoxia;
                }
                else if (hediff is Hediff_Injury injury)
                {
                    // 现实中：包扎好的伤口引发的全身炎症反应要小得多
                    traumaLoad += injury.Severity * (injury.IsTended() ? EE_Constants.SirsWeightTendedInjury : EE_Constants.SirsWeightUntendedInjury);
                }
                // 感染引发的强烈炎症
                else if (EE_DefOf.EE_Sepsis != null && hediff.def == EE_DefOf.EE_Sepsis)
                {
                    traumaLoad += hediff.Severity * 40f; 
                }
                else if (EE_DefOf.EE_Necrosis != null && hediff.def == EE_DefOf.EE_Necrosis)
                {
                    traumaLoad += hediff.Severity * 10f;
                }
            }
            // 现实中，引发SIRS需要严重的创伤（如多处枪伤/断肢）或重度失血休克
            if (traumaLoad > 25f || bloodLossRatio > 0.45f)
            {
                if (EE_DefOf.SIRS != null && !__instance.hediffSet.HasHediff(EE_DefOf.SIRS))
                {
                    Hediff sirs = HediffMaker.MakeHediff(EE_DefOf.SIRS, pawn, null); 
                    sirs.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(sirs, null, null, null);
                }
            }

            // 4. 凝血功能障碍 (致死三联征)
            float acidosisSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
            float hypothermiaSev = __instance.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f;
            if (acidosisSev > EE_Settings.CoagulopathyAcidosisThreshold && bloodLossRatio > EE_Settings.CoagulopathyBloodLossThreshold)
            {
                if (EE_DefOf.Coagulopathy != null && !__instance.hediffSet.HasHediff(EE_DefOf.Coagulopathy))
                {
                    Hediff coagulopathy = HediffMaker.MakeHediff(EE_DefOf.Coagulopathy, pawn, null); 
                    coagulopathy.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(coagulopathy, null, null, null);
                }
            }

            // 5. 多器官功能衰竭 (MODS)
            float shockSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Shock)?.Severity ?? 0f;
            if (shockSev >= EE_Constants.ShockIrreversibleThreshold || acidosisSev > 0.6f)
            {
                if (EE_DefOf.MultipleOrganFailure != null && !__instance.hediffSet.HasHediff(EE_DefOf.MultipleOrganFailure))
                {
                    Hediff mods = HediffMaker.MakeHediff(EE_DefOf.MultipleOrganFailure, pawn, null); 
                    mods.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(mods, null, null, null);
                }
            }
            // 6. 外周血管收缩与末梢缺血 (极早发生，缓慢累积，保护核心)
            // 修正：只有在重度失血(休克边缘)、心力衰竭、或伴随严重活动性出血时才触发。
            bool activeSevereBleeding = bleedRate > 0.3f && bloodLossRatio > 0.20f;
            if (pumping <= 0.40f || bloodLossRatio > 0.45f || activeSevereBleeding)
            {
                if (Rand.Chance(EE_Constants.PeripheralHypoxiaChance))
                {
                    List<BodyPartRecord> tmpExtremities = new List<BodyPartRecord>();
                    foreach (BodyPartRecord part in __instance.hediffSet.GetNotMissingParts())
                    {
                        if (part.def == null || part.depth == BodyPartDepth.Inside) continue;
                        
                        bool isExtremity = false;
                        if (part.def.tags != null && 
                           (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment) || 
                            part.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment)))
                        {
                            if (part.parts == null || part.parts.Count == 0)
                            {
                                isExtremity = true;
                            }
                        }
                        if (!isExtremity)
                        {
                            string defNameLower = part.def.defName.ToLower();
                            if (defNameLower.Contains("finger") || defNameLower.Contains("toe") || 
                                defNameLower.Contains("nose") || defNameLower.Contains("ear"))
                            {
                                isExtremity = true;
                            }
                        }
                        if (isExtremity)
                        {
                            tmpExtremities.Add(part);
                        }
                    }

                    if (tmpExtremities.Count > 0)
                    {
                        BodyPartRecord partToAffect = tmpExtremities.RandomElement();
                        Hediff hypoxia = HediffMaker.MakeHediff(EE_DefOf.TissueHypoxia, pawn, partToAffect);
                        hypoxia.Severity = EE_Constants.PeripheralHypoxiaAmount;
                        __instance.AddHediff(hypoxia, partToAffect, null, null);
                    }
                }
            }

            // 7. 动态心率状态 Hediff 更新 (Tachycardia, Bradycardia, Arrhythmia)
            bool isMI = __instance.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction);
            bool isDeathTimer = __instance.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer) || 
                                __instance.hediffSet.HasHediff(EE_DefOf.EE_DeclaredDead) || 
                                __instance.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeath);
            
            bool isHeartMissing = false;
            var pumpingSources = EE_BodyPartCache.GetBloodPumpingSources(pawn);
            if (pumpingSources != null)
            {
                foreach (BodyPartRecord part in pumpingSources)
                {
                    if (__instance.hediffSet.PartIsMissing(part))
                    {
                        isHeartMissing = true;
                        break;
                    }
                }
            }

            bool maskHeartRateHediffs = isMI || isDeathTimer || isHeartMissing;

            if (maskHeartRateHediffs)
            {
                if (EE_DefOf.EE_Tachycardia != null)
                {
                    Hediff existingTachy = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Tachycardia);
                    if (existingTachy != null) __instance.RemoveHediff(existingTachy);
                }
                if (EE_DefOf.EE_Bradycardia != null)
                {
                    Hediff existingBrady = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Bradycardia);
                    if (existingBrady != null) __instance.RemoveHediff(existingBrady);
                }
                if (EE_DefOf.EE_Arrhythmia != null)
                {
                    Hediff existingArr = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Arrhythmia);
                    if (existingArr != null) __instance.RemoveHediff(existingArr);
                }
            }
            else
            {
                BodyPartRecord heart = null;
                List<BodyPartRecord> sources = EE_BodyPartCache.GetBloodPumpingSources(pawn);
                if (sources != null && sources.Count > 0)
                {
                    heart = sources[0];
                }
                if (heart == null)
                {
                    foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                    {
                        if (part.def == BodyPartDefOf.Heart)
                        {
                            heart = part;
                            break;
                        }
                    }
                }
                if (heart == null)
                {
                    foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
                    {
                        if (part.def != null && part.def.defName.IndexOf("heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            heart = part;
                            break;
                        }
                    }
                }

                float bpm = VitalTracker.CalculateDynamicHeartRate(pawn);

                // 心动过速
                if (EE_DefOf.EE_Tachycardia != null)
                {
                    if (bpm >= EE_Constants.TachycardiaMinBpm)
                    {
                        if (!__instance.hediffSet.HasHediff(EE_DefOf.EE_Tachycardia))
                        {
                            Hediff tachycardia = HediffMaker.MakeHediff(EE_DefOf.EE_Tachycardia, pawn, heart);
                            tachycardia.Severity = 0.5f;
                            __instance.AddHediff(tachycardia, heart, null, null);
                        }
                        if (EE_DefOf.EE_Bradycardia != null)
                        {
                            Hediff existingBrady = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Bradycardia);
                            if (existingBrady != null) __instance.RemoveHediff(existingBrady);
                        }
                    }
                    else
                    {
                        Hediff existingTachy = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Tachycardia);
                        if (existingTachy != null) __instance.RemoveHediff(existingTachy);
                    }
                }

                // 心动过缓
                if (EE_DefOf.EE_Bradycardia != null)
                {
                    if (bpm <= EE_Constants.BradycardiaMaxBpm && bpm > EE_Constants.EcgFlatlineThreshold)
                    {
                        if (!__instance.hediffSet.HasHediff(EE_DefOf.EE_Bradycardia))
                        {
                            Hediff bradycardia = HediffMaker.MakeHediff(EE_DefOf.EE_Bradycardia, pawn, heart);
                            bradycardia.Severity = 0.5f;
                            __instance.AddHediff(bradycardia, heart, null, null);
                        }
                        if (EE_DefOf.EE_Tachycardia != null)
                        {
                            Hediff existingTachy = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Tachycardia);
                            if (existingTachy != null) __instance.RemoveHediff(existingTachy);
                        }
                    }
                    else
                    {
                        Hediff existingBrady = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Bradycardia);
                        if (existingBrady != null) __instance.RemoveHediff(existingBrady);
                    }
                }

                // 心律不齐
                if (EE_DefOf.EE_Arrhythmia != null)
                {
                    bool hasArrhythmiaTrigger = false;
                    if (acidosisSev >= EE_Constants.ArrhythmiaAcidosisThreshold) hasArrhythmiaTrigger = true;
                    else if (overdoseSev >= EE_Constants.ArrhythmiaMorphineThreshold && __instance.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive)) hasArrhythmiaTrigger = true;
                    else if (__instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.AdrenalineBoost)?.Severity >= EE_Constants.ArrhythmiaAdrenalineThreshold) hasArrhythmiaTrigger = true;
                    else if (__instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia)?.Severity >= EE_Constants.ArrhythmiaHypoxiaThreshold) hasArrhythmiaTrigger = true;

                    if (hasArrhythmiaTrigger && bpm > EE_Constants.EcgFlatlineThreshold)
                    {
                        if (!__instance.hediffSet.HasHediff(EE_DefOf.EE_Arrhythmia))
                        {
                            Hediff arrhythmia = HediffMaker.MakeHediff(EE_DefOf.EE_Arrhythmia, pawn, heart);
                            arrhythmia.Severity = 0.5f;
                            __instance.AddHediff(arrhythmia, heart, null, null);
                        }
                    }
                    else
                    {
                        Hediff existingArr = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Arrhythmia);
                        if (existingArr != null) __instance.RemoveHediff(existingArr);
                    }
                }
            }
        }
    }
}