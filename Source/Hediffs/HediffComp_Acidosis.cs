using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Acidosis : HediffCompProperties
    {
        public float severityIncreasePerDay = 3.0f;
        public float severityDecreasePerDay = 2.0f;
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
            // Do nothing here to prevent double-updating.
            // Updates are driven by HypoxiaMonitor.RunCrisisMonitor.
        }

        public void UpdateAcidosisSeverity(float pumping, float breathing)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;

            float bloodLossSeverity = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;

            // 当出现灌注/呼吸缺口，或者严重失血时累积酸中毒
            bool hasInsult = pumping <= Props.bloodPumpingThreshold || 
                             breathing <= Props.breathingThreshold || 
                             bloodLossSeverity > EE_Settings.AcidosisBloodLossThreshold1;

            if (hasInsult)
            {
                // 积分累积公式
                float severityFactor = 0.05f; // 基础增长较平缓

                if (bloodLossSeverity > EE_Settings.AcidosisBloodLossThreshold1)
                {
                    if (bloodLossSeverity <= EE_Settings.AcidosisBloodLossThreshold2)
                    {
                        severityFactor += (bloodLossSeverity - EE_Settings.AcidosisBloodLossThreshold1) * 1.0f; 
                    }
                    else
                    {
                        // Class IV
                        severityFactor += 0.1f + (bloodLossSeverity - EE_Settings.AcidosisBloodLossThreshold2) * 3.0f; 
                    }
                }

                // 累积速率与缺口成正比，引入重构常量
                if (pumping <= Props.bloodPumpingThreshold)
                {
                    severityFactor += (Props.bloodPumpingThreshold - pumping) * EE_Constants.AcidosisAccumulationFactor;
                }
                if (breathing <= Props.breathingThreshold)
                {
                    severityFactor += (Props.breathingThreshold - breathing) * EE_Constants.AcidosisAccumulationFactor;
                }

                // 致命三联征：失温显著加剧酸中毒
                Hediff hypothermia = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (hypothermia != null)
                {
                    severityFactor += hypothermia.Severity * 2.0f;
                }
                
                // MODS 联动：急性肾损伤及肾衰竭大幅加速酸中毒（肾脏无法排酸）
                if (Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_KidneyFailure))
                {
                    severityFactor += 3.0f; // 极大幅度加速
                }
                else
                {
                    Hediff aki = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_AcuteKidneyInjury);
                    if (aki != null) severityFactor += aki.Severity * 1.5f;
                }

                if (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                {
                    severityFactor *= EE_Settings.VitalCriticalMultiplier;
                }

                parent.Severity += (Props.severityIncreasePerDay * severityFactor / 1000f); 
            }
            else
            {
                // 灌注与失血恢复后，以 XML 配置的消退速率自然清偿消退
                parent.Severity -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}