using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_HeartAttackVFConverter : HediffCompProperties
    {
        public HediffCompProperties_HeartAttackVFConverter()
        {
            this.compClass = typeof(HediffComp_HeartAttackVFConverter);
        }
    }

    public class HediffComp_HeartAttackVFConverter : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || !Pawn.IsHashIntervalTick(60)) return;

            // 原版心脏病转化为室颤的判定
            bool shouldConvert = false;
            
            // 1. 如果严重度达到阈值 (如 0.85f)
            if (parent.Severity >= EE_Constants.HeartAttackVFConversionThreshold)
            {
                shouldConvert = true;
            }
            
            // 2. 如果发生了重度酸中毒
            if (!shouldConvert)
            {
                float acidosisSev = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
                if (acidosisSev >= EE_Constants.HeartAttackAcidosisConversionThreshold)
                {
                    shouldConvert = true;
                }
            }

            if (shouldConvert)
            {
                if (EE_DefOf.EE_MyocardialInfarction != null && !Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction))
                {
                    // 添加室颤
                    Hediff vf = HediffMaker.MakeHediff(EE_DefOf.EE_MyocardialInfarction, Pawn, parent.Part);
                    vf.Severity = 0.5f; // 给一个基础严重度，确保能触发后续机制
                    Pawn.health.AddHediff(vf);
                    
                    // 移除原版心脏病
                    Pawn.health.RemoveHediff(parent);
                    
                    if (Pawn.Spawned)
                    {
                        Messages.Message($"{Pawn.LabelShort}的心脏病极度恶化，已经转化为致命的室颤（心室蠕动）！", Pawn, MessageTypeDefOf.NegativeHealthEvent);
                    }
                }
            }
        }
    }
}
