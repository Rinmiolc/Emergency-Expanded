using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Antibiotics : HediffCompProperties
    {
        public float severityReductionPerDay = 0.5f;

        public HediffCompProperties_Antibiotics()
        {
            this.compClass = typeof(HediffComp_Antibiotics);
        }
    }

    public class HediffComp_Antibiotics : HediffComp
    {
        public HediffCompProperties_Antibiotics Props => (HediffCompProperties_Antibiotics)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.IsHashIntervalTick(600)) return; // 每天 100 次检查 (每600 Tick)

            foreach (var hediff in Pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_LocalizedInfection || hediff.def == EE_DefOf.EE_Sepsis)
                {
                    // 抗生素主动强行降低感染严重度
                    hediff.Severity -= (Props.severityReductionPerDay / 100f); 
                }
            }
        }
    }
}
