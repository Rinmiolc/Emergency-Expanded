using HarmonyLib;
using RimWorld;
using Verse;
using EmergencyExpanded;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(HediffWithComps), "Tick")]
    public static class Patch_HeartAttack_Tick
    {
        public static void Postfix(HediffWithComps __instance)
        {
            if (!(__instance is Hediff_HeartAttack)) return;
            if (__instance.pawn == null || __instance.pawn.Dead || !__instance.pawn.IsHashIntervalTick(60)) return;

            // 原版心脏病转化为室颤的判定
            bool shouldConvert = false;
            
            // 1. 如果严重度达到阈值 (如 0.85f)
            if (__instance.Severity >= EE_Constants.HeartAttackVFConversionThreshold)
            {
                shouldConvert = true;
            }
            
            // 2. 如果发生了重度酸中毒
            if (!shouldConvert)
            {
                float acidosisSev = __instance.pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
                if (acidosisSev >= EE_Constants.HeartAttackAcidosisConversionThreshold)
                {
                    shouldConvert = true;
                }
            }

            if (shouldConvert)
            {
                if (EE_DefOf.VentricularFibrillation != null && !__instance.pawn.health.hediffSet.HasHediff(EE_DefOf.VentricularFibrillation))
                {
                    // 添加室颤
                    Hediff vf = HediffMaker.MakeHediff(EE_DefOf.VentricularFibrillation, __instance.pawn, __instance.Part);
                    vf.Severity = 0.5f; // 给一个基础严重度，确保能触发后续机制
                    __instance.pawn.health.AddHediff(vf);
                    
                    // 移除原版心脏病
                    __instance.pawn.health.RemoveHediff(__instance);
                    
                    if (__instance.pawn.Spawned)
                    {
                        Messages.Message($"{__instance.pawn.LabelShort}的心脏病极度恶化，已经转化为致命的室颤（心室蠕动）！", __instance.pawn, MessageTypeDefOf.NegativeHealthEvent);
                    }
                }
            }
        }
    }
}
