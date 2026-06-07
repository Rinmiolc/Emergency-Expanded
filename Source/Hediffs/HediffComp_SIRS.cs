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
            // Do nothing to prevent double-updating.
            // Updates are driven by HypoxiaMonitor.RunCrisisMonitor.
        }

        public void UpdateSirsSeverity()
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;

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

                parent.Severity += increment;
            }
            else
            {
                parent.Severity -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}
