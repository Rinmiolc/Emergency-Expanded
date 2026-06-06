using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_Pawn_HealthTracker_AddHediff
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_HealthTracker __instance, Pawn ___pawn, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            if (EE_GlobalFlags.IsForcingDown) return true;
            if (hediff == null) return true;

            Pawn pawn = ___pawn;
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh) return true;

            // ================= 1. 烧伤融合机制 (Burn Merging) =================
            if (hediff.def == EE_DefOf.Burn)
            {
                BodyPartRecord targetPart = part ?? hediff.Part;
                List<Hediff> hediffList = pawn.health.hediffSet.hediffs;
                Hediff existingBurn = null;
                
                // 用 for 循环代替 FirstOrDefault 规避 GC 垃圾产生
                for (int i = 0; i < hediffList.Count; i++)
                {
                    Hediff h = hediffList[i];
                    if (h.def == EE_DefOf.Burn && h.Part == targetPart && !h.IsTended())
                    {
                        existingBurn = h;
                        break;
                    }
                }
                
                if (existingBurn != null)
                {
                    // 融合机制：不添加新词条，而是将新伤害累加到现有烧伤中，模拟烧伤加深
                    existingBurn.Severity += hediff.Severity;
                    
                    if (result != null)
                    {
                        result.AddHediff(existingBurn);
                    }
                    
                    // 阻断原版 AddHediff 的执行
                    return false;
                }
            }

            // ================= 2. 气胸与肺部伤害判定 (Pneumothorax) =================
            BodyPartRecord targetPartForPneumo = part ?? hediff.Part;
            if (targetPartForPneumo != null && hediff is Hediff_Injury injury)
            {
                // 判定是否为利器伤害 (bullets, stabs, cuts, etc)
                bool isSharp = false;
                if (dinfo.HasValue && dinfo.Value.Def != null)
                {
                    isSharp = dinfo.Value.Def.armorCategory == DamageArmorCategoryDefOf.Sharp;
                }
                else if (injury.def != null)
                {
                    string defName = injury.def.defName;
                    if (defName.IndexOf("gunshot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("cut", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("stab", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("scratch", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("bite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        defName.IndexOf("pierce", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isSharp = true;
                    }
                }

                if (isSharp)
                {
                    // 判定是否是肺部或呼吸器官
                    bool isLung = (targetPartForPneumo.def.tags != null && targetPartForPneumo.def.tags.Contains(BodyPartTagDefOf.BreathingSource)) || 
                                  targetPartForPneumo.def.defName.IndexOf("lung", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isLung)
                    {
                        float originalDamage = injury.Severity;
                        float currentHealth = __instance.hediffSet.GetPartHealth(targetPartForPneumo);

                        // 如果本次伤害会导致肺部彻底摧毁，且低于伤害上限门槛，强制保留 1 HP
                        if (originalDamage >= currentHealth && originalDamage <= EE_Constants.PneumothoraxDamageCap)
                        {
                            float newSeverity = currentHealth - 1f;
                            if (newSeverity < 0f) newSeverity = 0.1f;
                            injury.Severity = newSeverity;
                        }

                        // 生成气胸
                        if (EE_DefOf.EE_Pneumothorax != null && __instance.hediffSet.GetNotMissingParts().Contains(targetPartForPneumo))
                        {
                            float severityIncrease = EE_Constants.PneumothoraxBaseSeverity + originalDamage * EE_Constants.PneumothoraxSeverityFactor;
                            
                            // 哮喘急性联动：如果拥有原版哮喘，气胸严重度和易感性显著增加
                            bool hasAsthma = (EE_DefOf.Asthma != null) && pawn.health.hediffSet.HasHediff(EE_DefOf.Asthma);
                            if (hasAsthma)
                            {
                                severityIncrease = (severityIncrease * EE_Constants.AsthmaPneumothoraxChanceMultiplier) + EE_Constants.AsthmaPneumothoraxSeverityBonus;
                            }

                            // 检查是否已经具有气胸
                            List<Hediff> hediffList = __instance.hediffSet.hediffs;
                            Hediff existingPneumo = null;
                            for (int i = 0; i < hediffList.Count; i++)
                            {
                                Hediff h = hediffList[i];
                                if (h.def == EE_DefOf.EE_Pneumothorax && h.Part == targetPartForPneumo)
                                {
                                    existingPneumo = h;
                                    break;
                                }
                            }
                            
                            if (existingPneumo != null)
                            {
                                existingPneumo.Severity += severityIncrease;
                            }
                            else
                            {
                                Hediff pneumo = HediffMaker.MakeHediff(EE_DefOf.EE_Pneumothorax, pawn, targetPartForPneumo);
                                pneumo.Severity = Mathf.Clamp01(severityIncrease); 
                                __instance.AddHediff(pneumo, targetPartForPneumo, dinfo, null);
                                
                                if (pawn.Spawned && pawn.Map != null)
                                {
                                    string moteText = hasAsthma ? "EE_MotePneumothoraxAsthma".Translate() : "EE_MotePneumothorax".Translate();
                                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, moteText, Color.red);
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
