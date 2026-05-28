using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Acidosis : HediffCompProperties
    {
        public float severityIncreasePerDay = 3.0f;
        public float severityDecreasePerDay = 1.0f;
        public float bloodPumpingThreshold = 0.50f;  
        public float breathingThreshold = 0.50f;      
        
        public HediffCompProperties_Acidosis()
        {
            this.compClass = typeof(HediffComp_Acidosis);
        }
    }

    public class HediffComp_Acidosis : HediffComp
    {
        public HediffCompProperties_Acidosis Props => (HediffCompProperties_Acidosis)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing); 
            float bloodLossSeverity = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;

            // 触发门槛提高至 30%失血 (Class III休克)
            if (pumping <= Props.bloodPumpingThreshold || breathing <= Props.breathingThreshold || bloodLossSeverity > 0.30f)
            {
                float severityFactor = 0.05f; // 基础增长极低，拉长游戏时间至 4-8 小时

                if (bloodLossSeverity > 0.30f)
                {
                    if (bloodLossSeverity <= 0.40f)
                    {
                        severityFactor += (bloodLossSeverity - 0.30f) * 1.0f; 
                    }
                    else
                    {
                        // Class IV
                        severityFactor += 0.1f + (bloodLossSeverity - 0.40f) * 3.0f; 
                    }
                }

                if (pumping <= Props.bloodPumpingThreshold)
                {
                    severityFactor += (Props.bloodPumpingThreshold - pumping) * 1.5f;
                }
                if (breathing <= Props.breathingThreshold)
                {
                    severityFactor += (Props.breathingThreshold - breathing) * 1.5f;
                }

                // 致命三联征：失温显著加剧酸中毒
                Hediff hypothermia = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (hypothermia != null)
                {
                    severityFactor += hypothermia.Severity * 2.0f;
                }

                if (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                {
                    severityFactor *= EE_Settings.VitalCriticalMultiplier;
                }

                severityAdjustment += (Props.severityIncreasePerDay * severityFactor / 1000f); 
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}