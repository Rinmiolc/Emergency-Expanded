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

            // Check if part is Lung
            if (part.def.defName.IndexOf("lung", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Log.Message($"[EE] Lung hit by sharp injury: {hediff.def.defName}, Severity: {hediff.Severity}");
                if (hediff is Hediff_Injury injury)
                {
                    float originalDamage = injury.Severity;
                    float currentHealth = __instance.hediffSet.GetPartHealth(part);
                    Log.Message($"[EE] Original Damage: {originalDamage}, Current Health: {currentHealth}");

                    // If this injury would destroy the lung (originalDamage >= currentHealth)
                    // We save it from destruction if original damage is below 25
                    if (originalDamage >= currentHealth && originalDamage <= 25f)
                    {
                        // Cap severity to leave 1 HP
                        float newSeverity = currentHealth - 1f;
                        if (newSeverity < 0f) newSeverity = 0.1f;
                        injury.Severity = newSeverity;
                        Log.Message($"[EE] Capped severity to {newSeverity} to save the lung.");
                    }

                    // Generate pneumothorax based on original damage
                    if (EE_DefOf.EE_Pneumothorax != null)
                    {
                        Log.Message($"[EE] Generating Pneumothorax...");
                        // Check if already has pneumothorax on this part
                        Hediff existingPneumo = __instance.hediffSet.hediffs.FirstOrDefault(h => h.def == EE_DefOf.EE_Pneumothorax && h.Part == part);
                        
                        if (existingPneumo != null)
                        {
                            existingPneumo.Severity += originalDamage * 0.04f;
                        }
                        else
                        {
                            Hediff pneumo = HediffMaker.MakeHediff(EE_DefOf.EE_Pneumothorax, ___pawn, part);
                            pneumo.Severity = Mathf.Clamp01(originalDamage * 0.04f); 
                            __instance.AddHediff(pneumo, part, dinfo, null);
                            
                            if (___pawn.Spawned && ___pawn.Map != null)
                            {
                                MoteMaker.ThrowText(___pawn.DrawPos, ___pawn.Map, "气胸!", Color.red);
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
