using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Hediff_MyocardialInfarction : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";

        public override void Tick()
        {
            base.Tick();
            
            if (this.pawn == null || this.pawn.Dead) return;

            // 每 60 Ticks 更新一次，稀疏化 Severity 变动通知以大幅节省 CPU 负载 (60000 ticks = 1天)
            if (this.pawn.IsHashIntervalTick(60))
            {
                float baseRate = 8.0f; // 每天增加 8.0 严重度
                
                // 检测吗啡状态降低心肌梗死恶化速度 (减缓 30%)
                if (EE_DefOf.EE_MorphineActive != null && this.pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    baseRate *= EE_Constants.MorphineMyocardialInfarctionSpeedMultiplier;
                }

                // 每 60 Ticks 对应的增量为 baseRate / 1000f
                this.Severity += baseRate / 1000f;
            }
        }
    }

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
                        Messages.Message("EE_MessageHeartAttackVFWorsened".Translate(Pawn.LabelShort), Pawn, MessageTypeDefOf.NegativeHealthEvent);
                    }
                }
            }
        }
    }
}
