using HarmonyLib;
using RimWorld;
using Verse;
using System.Linq;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_BurnMerging
    {
        public static bool Prefix(Pawn_HealthTracker __instance, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            if (hediff == null) return true;
            
            // 仅对烧伤进行融合
            if (hediff.def.defName == "Burn")
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn == null) return true;

                BodyPartRecord targetPart = part ?? hediff.Part;
                
                // 寻找同一个部位上且未包扎的现有烧伤
                Hediff existingBurn = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def.defName == "Burn" && h.Part == targetPart && !h.IsTended());
                
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
            
            return true;
        }
    }
}
