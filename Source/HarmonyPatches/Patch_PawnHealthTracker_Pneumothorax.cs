using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_PawnHealthTracker_Pneumothorax
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn_HealthTracker __instance, Pawn ___pawn, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo)
        {
            if (EE_GlobalFlags.IsForcingDown) return true;
            if (hediff == null || part == null) return true;
            if (___pawn == null || ___pawn.Dead || !___pawn.RaceProps.IsFlesh) return true;

            // Only Sharp damage (bullets, stabs, etc)
            bool isSharp = false;
            if (dinfo.HasValue && dinfo.Value.Def != null)
            {
                isSharp = dinfo.Value.Def.armorCategory == DamageArmorCategoryDefOf.Sharp;
            }
            else if (hediff is Hediff_Injury inj && inj.def != null)
            {
                string defName = inj.def.defName.ToLower();
                if (defName.Contains("gunshot") || defName.Contains("cut") || defName.Contains("stab") || defName.Contains("scratch") || defName.Contains("bite") || defName.Contains("pierce"))
                {
                    isSharp = true;
                }
            }

            if (!isSharp) return true;

            // Check if part is Lung or BreathingSource (Alien Race Compatibility)
            bool isLung = (part.def.tags != null && part.def.tags.Contains(BodyPartTagDefOf.BreathingSource)) || 
                          part.def.defName.IndexOf("lung", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isLung)
            {
                if (hediff is Hediff_Injury injury)
                {
                    float originalDamage = injury.Severity;
                    float currentHealth = __instance.hediffSet.GetPartHealth(part);

                    // If this injury would destroy the lung (originalDamage >= currentHealth)
                    if (originalDamage >= currentHealth && originalDamage <= EE_Constants.PneumothoraxDamageCap)
                    {
                        // Cap severity to leave 1 HP
                        float newSeverity = currentHealth - 1f;
                        if (newSeverity < 0f) newSeverity = 0.1f;
                        injury.Severity = newSeverity;
                    }

                    // Generate pneumothorax based on original damage
                    if (EE_DefOf.EE_Pneumothorax != null)
                    {
                        float severityIncrease = EE_Constants.PneumothoraxBaseSeverity + originalDamage * EE_Constants.PneumothoraxSeverityFactor;
                        
                        // 哮喘急性联动：如果拥有原版哮喘，气胸严重度和易感性显著增加
                        bool hasAsthma = ___pawn.health.hediffSet.HasHediff(HediffDef.Named("Asthma"));
                        if (hasAsthma)
                        {
                            severityIncrease = (severityIncrease * EE_Constants.AsthmaPneumothoraxChanceMultiplier) + EE_Constants.AsthmaPneumothoraxSeverityBonus;
                        }

                        // Check if already has pneumothorax on this part
                        Hediff existingPneumo = __instance.hediffSet.hediffs.FirstOrDefault(h => h.def == EE_DefOf.EE_Pneumothorax && h.Part == part);
                        
                        if (existingPneumo != null)
                        {
                            existingPneumo.Severity += severityIncrease;
                        }
                        else
                        {
                            Hediff pneumo = HediffMaker.MakeHediff(EE_DefOf.EE_Pneumothorax, ___pawn, part);
                            pneumo.Severity = Mathf.Clamp01(severityIncrease); 
                            __instance.AddHediff(pneumo, part, dinfo, null);
                            
                            if (___pawn.Spawned && ___pawn.Map != null)
                            {
                                string moteText = hasAsthma ? "气胸 (合并哮喘)!" : "气胸!";
                                MoteMaker.ThrowText(___pawn.DrawPos, ___pawn.Map, moteText, Color.red);
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
