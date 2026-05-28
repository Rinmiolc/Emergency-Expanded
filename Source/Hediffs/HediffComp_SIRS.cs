using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_SIRS : HediffCompProperties
    {
        public float severityIncreasePerDay = 4.0f;
        public float severityDecreasePerDay = 1.5f;
        
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
                if (hediff is Hediff_Injury || hediff.def == EE_DefOf.TissueHypoxia)
                {
                    traumaLoad += hediff.Severity;
                }
            }
            
            float bloodLossSeverity = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;

            if (traumaLoad > 10f || bloodLossSeverity > 0.30f)
            {
                float factor = (traumaLoad / 40f) + (bloodLossSeverity * 1.5f);
                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}
