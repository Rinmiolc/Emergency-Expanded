using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EmergencyExpanded
{
    public static class Patch_Pawn_HealthTracker_SystemicCrisisMonitor_Helper
    {
        public static void RunCrisisMonitor(Pawn_HealthTracker __instance, Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh || pawn.IsShambler) return;

            // ================= Step 1: 物理状态与心脏检查 =================
            // 优先心肌梗死与心脏状态强校验：如果该种族有心脏，且心脏受损或物理缺失，直接施加 100% 进度的心脏骤停
            BodyPartRecord heart = EE_BodyPartCache.GetHeartPart(pawn);
            if (heart != null && EE_MedicalUtility.IsPartOrAnyAncestorDestroyedOrMissing(pawn, heart))
            {
                if (EE_DefOf.EE_MyocardialInfarction != null)
                {
                    Hediff mi = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
                    if (mi == null)
                    {
                        mi = HediffMaker.MakeHediff(EE_DefOf.EE_MyocardialInfarction, pawn, heart);
                        mi.Severity = 1.0f;
                        __instance.AddHediff(mi, heart, null, null);
                    }
                    else if (mi.Severity < 1.0f)
                    {
                        mi.Severity = 1.0f;
                    }
                }
            }

            // 获取基本能力 (仅基于物理状态)
            // 注意：因为我们已经移除了酸中毒和休克在 XML 中对 BloodPumping / Breathing 的直接限制，
            // GetLevel 返回的值目前仅包含物理器官完整度、失血限制等 Layer 2 基础能力值，
            // 从而自然实现了 CalculateBasePumpingFromPhysicalState / CalculateBaseBreathingFromPhysicalState 的目标！
            float pumping = __instance.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = __instance.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float bleedRate = __instance.hediffSet.BleedRateTotal;
            Hediff bloodLoss = __instance.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            float bloodLossSeverity = bloodLoss?.Severity ?? 0f;
            float lethalSeverity = bloodLoss?.def?.lethalSeverity ?? 1f;
            if (lethalSeverity <= 0f) lethalSeverity = 1f;
            float bloodLossRatio = bloodLossSeverity / lethalSeverity; // 相对失血率 (0.0~1.0)
            float overdoseSev = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;

            // ================= Step 2: 挂载生命体征/生理负债 Hediff (如果满足初始门槛且不存在) =================
            
            // 1. 脑缺氧
            bool hasBrainHypoxia = __instance.hediffSet.HasHediff(EE_DefOf.CerebralHypoxia);
            if (pumping < EE_Settings.HypoxiaMonitorThreshold || breathing < EE_Settings.HypoxiaMonitorThreshold || overdoseSev > 0.75f)
            {
                HediffDef vegStateDef = EE_DefOf.VegetativeState;
                bool isVegetative = vegStateDef != null && __instance.hediffSet.HasHediff(vegStateDef);

                if (!isVegetative && !hasBrainHypoxia)
                {
                    HediffDef brainHypoxiaDef = EE_DefOf.CerebralHypoxia;
                    if (brainHypoxiaDef != null)
                    {
                        BodyPartRecord brain = __instance.hediffSet.GetBrain();
                        if (brain != null)
                        {
                            Hediff brainHypoxia = HediffMaker.MakeHediff(brainHypoxiaDef, pawn, brain);
                            brainHypoxia.Severity = EE_Settings.InitialHediffSeverity; 
                            __instance.AddHediff(brainHypoxia, brain, null, null);
                            hasBrainHypoxia = true;
                        }
                    }
                }
            }

            // 2. 代谢性酸中毒 (Class III 30%失血门槛)
            bool hasAcidosis = __instance.hediffSet.HasHediff(EE_DefOf.MetabolicAcidosis);
            if (pumping <= 0.50f || breathing <= 0.50f || bloodLossRatio > 0.30f)
            {
                if (EE_DefOf.MetabolicAcidosis != null && !hasAcidosis)
                {
                    Hediff acidosis = HediffMaker.MakeHediff(EE_DefOf.MetabolicAcidosis, pawn, null); 
                    acidosis.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(acidosis, null, null, null);
                    hasAcidosis = true;
                }
            }

            // 3. 全身炎症反应综合征 (SIRS)
            float traumaLoad = EE_MedicalUtility.CalculateTraumaLoad(pawn);
            bool hasSirs = __instance.hediffSet.HasHediff(EE_DefOf.SIRS);
            if (traumaLoad > 25f || bloodLossRatio > 0.45f)
            {
                if (EE_DefOf.SIRS != null && !hasSirs)
                {
                    Hediff sirs = HediffMaker.MakeHediff(EE_DefOf.SIRS, pawn, null); 
                    sirs.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(sirs, null, null, null);
                    hasSirs = true;
                }
            }

            // 4. 休克 (当失血量超过20% 且没有休克时触发，或者由 SIRS 传导)
            bool hasShock = __instance.hediffSet.HasHediff(EE_DefOf.EE_Shock);
            float shockSev = 0f;
            if (!hasShock)
            {
                bool sirsTrigger = hasSirs && __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.SIRS).Severity > 0.2f;
                if (bloodLossRatio > 0.2f || sirsTrigger)
                {
                    if (EE_DefOf.EE_Shock != null)
                    {
                        Hediff shock = HediffMaker.MakeHediff(EE_DefOf.EE_Shock, pawn, null);
                        shock.Severity = EE_Settings.InitialHediffSeverity;
                        __instance.AddHediff(shock, null, null, null);
                        hasShock = true;
                    }
                }
            }
            if (hasShock)
            {
                shockSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Shock).Severity;
            }

            // 5. 凝血功能障碍 (致死三联征)
            float acidosisSev = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
            if (acidosisSev > EE_Settings.CoagulopathyAcidosisThreshold && bloodLossRatio > EE_Settings.CoagulopathyBloodLossThreshold)
            {
                if (EE_DefOf.Coagulopathy != null && !__instance.hediffSet.HasHediff(EE_DefOf.Coagulopathy))
                {
                    Hediff coagulopathy = HediffMaker.MakeHediff(EE_DefOf.Coagulopathy, pawn, null); 
                    coagulopathy.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(coagulopathy, null, null, null);
                }
            }

            // 6. 多器官功能衰竭 (MODS)
            bool hasMods = __instance.hediffSet.HasHediff(EE_DefOf.MultipleOrganFailure);
            if (shockSev >= EE_Constants.ShockIrreversibleThreshold || acidosisSev > 0.6f)
            {
                if (EE_DefOf.MultipleOrganFailure != null && !hasMods)
                {
                    Hediff mods = HediffMaker.MakeHediff(EE_DefOf.MultipleOrganFailure, pawn, null); 
                    mods.Severity = EE_Settings.InitialHediffSeverity;
                    __instance.AddHediff(mods, null, null, null);
                    hasMods = true;
                }
            }

            // ================= Step 3: 单向顺序生理负债与器质性进展更新 =================
            
            // 1. 更新休克严重度
            if (hasShock)
            {
                Hediff_Shock shockHediff = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Shock) as Hediff_Shock;
                if (shockHediff != null)
                {
                    shockHediff.UpdateShockSeverity(pumping);
                    shockSev = shockHediff.Severity; // 更新本帧局部的 shockSev
                }
            }

            // 2. 更新酸中毒严重度 (酸中毒受到低泵血/低呼吸的积分累积，同时休克失代偿也加深酸中毒)
            if (hasAcidosis)
            {
                HediffComp_Acidosis acidosisComp = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.TryGetComp<HediffComp_Acidosis>();
                if (acidosisComp != null)
                {
                    // 在此处更新酸中毒
                    acidosisComp.UpdateAcidosisSeverity(pumping, breathing);
                    
                    // 如果休克处于失代偿期以上，额外加深酸中毒 (保留原先 ShockSystem.cs 中的传导设计)
                    if (hasShock && shockSev >= EE_Constants.ShockDecompensatedThreshold)
                    {
                        float acidosisIncrease = (shockSev - EE_Constants.ShockDecompensatedThreshold) * 2.0f / 1000f;
                        acidosisComp.parent.Severity += acidosisIncrease;
                    }
                    
                    acidosisSev = acidosisComp.parent.Severity; // 更新本帧局部的 acidosisSev
                }
            }

            // 3. 更新 SIRS 严重度
            if (hasSirs)
            {
                HediffComp_SIRS sirsComp = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.SIRS)?.TryGetComp<HediffComp_SIRS>();
                if (sirsComp != null)
                {
                    sirsComp.UpdateSirsSeverity();
                }
            }

            // 4. 更新 MODS 严重度
            if (hasMods)
            {
                HediffComp_MODS modsComp = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.MultipleOrganFailure)?.TryGetComp<HediffComp_MODS>();
                if (modsComp != null)
                {
                    modsComp.UpdateModsSeverity();
                }
            }

            // 5. 更新脑缺氧与脑损伤严重度
            if (hasBrainHypoxia)
            {
                HediffComp_CerebralHypoxia brainComp = __instance.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia)?.TryGetComp<HediffComp_CerebralHypoxia>();
                if (brainComp != null)
                {
                    brainComp.UpdateCerebralHypoxiaSeverity(pumping, breathing);
                }
            }

            // ================= Step 4: 其他局部物理性缺氧更新与心率更新 =================

            // 外周血管收缩与末梢缺血 (物理性局部缺氧，无反馈死锁)
            bool activeSevereBleeding = bleedRate > 0.3f && bloodLossRatio > 0.20f;
            if (pumping <= 0.40f || bloodLossRatio > 0.45f || activeSevereBleeding)
            {
                if (Rand.Chance(EE_Constants.PeripheralHypoxiaChance))
                {
                    List<BodyPartRecord> tmpExtremities = new List<BodyPartRecord>();
                    foreach (BodyPartRecord part in __instance.hediffSet.GetNotMissingParts())
                    {
                        if (part.def == null || part.depth == BodyPartDepth.Inside) continue;
                        if (EE_BodyPartCache.IsExtremityPart(part))
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

            // 动态心率状态 Hediff 更新
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
                    if (!__instance.hediffSet.GetNotMissingParts().Contains(part))
                    {
                        isHeartMissing = true;
                        break;
                    }
                }
            }

            bool isHeartDestroyedOrMissing = (heart != null && EE_MedicalUtility.IsPartOrAnyAncestorDestroyedOrMissing(pawn, heart)) || isHeartMissing;
            bool maskHeartRateHediffs = isMI || isDeathTimer || isHeartDestroyedOrMissing;

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
                    float bradyMax = pawn.Awake() ? EE_Constants.BradycardiaMaxBpm : 35f;
                    if (bpm <= bradyMax && bpm > EE_Constants.EcgFlatlineThreshold)
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