using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_SIRS : HediffCompProperties
    {
        public float severityIncreasePerDay = 4.0f;
        public float severityDecreasePerDay = 3.0f;
        
        public HediffCompProperties_SIRS()
        {
            this.compClass = typeof(HediffComp_SIRS);
        }
    }

    public class HediffComp_SIRS : HediffComp
    {
        public HediffCompProperties_SIRS Props => (HediffCompProperties_SIRS)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float traumaLoad = EE_MedicalUtility.CalculateTraumaLoad(Pawn);
            
            Hediff bloodLoss = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            float bloodLossSeverity = bloodLoss?.Severity ?? 0f;
            float lethalSeverity = bloodLoss?.def?.lethalSeverity ?? 1f;
            if (lethalSeverity <= 0f) lethalSeverity = 1f;
            float bloodLossRatio = bloodLossSeverity / lethalSeverity;

            if (traumaLoad > 25f || bloodLossRatio > 0.45f)
            {
                float factor = (traumaLoad / 40f) + (bloodLossRatio * 1.5f);
                float increment = (Props.severityIncreasePerDay * factor / 1000f);

                // 吗啡镇静减缓 SIRS 恶化速度 (减缓 40%)
                if (EE_DefOf.EE_MorphineActive != null && Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    increment *= EE_Constants.MorphineShockSirsSpeedMultiplier;
                }

                severityAdjustment += increment;
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
            
            // 将分布性压力传导给休克机制。如果休克不存在，则主动挂载
            if (this.parent.Severity > 0.2f && !Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Shock))
            {
                Hediff shock = HediffMaker.MakeHediff(EE_DefOf.EE_Shock, Pawn, null);
                Pawn.health.AddHediff(shock, null, null, null);
            }
        }
    }
}
