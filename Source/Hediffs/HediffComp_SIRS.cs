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

            float traumaLoad = 0f;
            foreach (var hediff in Pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Injury injury)
                {
                    traumaLoad += injury.Severity * (injury.IsTended() ? 0.2f : 1.0f);
                }
                else if (hediff.def == EE_DefOf.TissueHypoxia)
                {
                    traumaLoad += hediff.Severity * 0.1f;
                }
                else if (EE_DefOf.EE_Sepsis != null && hediff.def == EE_DefOf.EE_Sepsis)
                {
                    traumaLoad += hediff.Severity * 40f; 
                }
                else if (EE_DefOf.EE_Necrosis != null && hediff.def == EE_DefOf.EE_Necrosis)
                {
                    traumaLoad += hediff.Severity * 10f;
                }
            }
            
            Hediff bloodLoss = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            float bloodLossSeverity = bloodLoss?.Severity ?? 0f;
            float lethalSeverity = bloodLoss?.def?.lethalSeverity ?? 1f;
            if (lethalSeverity <= 0f) lethalSeverity = 1f;
            float bloodLossRatio = bloodLossSeverity / lethalSeverity;

            if (traumaLoad > 25f || bloodLossRatio > 0.45f)
            {
                float factor = (traumaLoad / 40f) + (bloodLossRatio * 1.5f);
                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}
