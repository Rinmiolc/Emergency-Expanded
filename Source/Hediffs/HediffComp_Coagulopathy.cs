using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Coagulopathy : HediffCompProperties
    {
        public float severityIncreasePerDay = 5.0f;
        public float severityDecreasePerDay = 2.0f;
        
        public HediffCompProperties_Coagulopathy()
        {
            this.compClass = typeof(HediffComp_Coagulopathy);
        }
    }

    public class HediffComp_Coagulopathy : HediffComp
    {
        public HediffCompProperties_Coagulopathy Props => (HediffCompProperties_Coagulopathy)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float acidosisSev = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;
            float hypothermiaSev = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f;
            float bloodLossSev = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;

            if (acidosisSev > 0.2f || (acidosisSev > 0f && hypothermiaSev > 0f && bloodLossSev > 0.2f))
            {
                float factor = acidosisSev * 2f + hypothermiaSev * 2f + bloodLossSev;
                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}
