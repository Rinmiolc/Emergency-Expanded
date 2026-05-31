using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Coagulopathy : HediffCompProperties
    {
        public float severityIncreasePerDay = 2.5f;
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
            
            bool liverFailed = Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_LiverFailure);
            float liverInjurySev = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_AcuteLiverInjury)?.Severity ?? 0f;

            if (liverFailed || liverInjurySev > 0.4f || (acidosisSev > EE_Settings.CoagulopathyAcidosisThreshold && bloodLossSev > EE_Settings.CoagulopathyBloodLossThreshold))
            {
                float hypothermiaFactor = hypothermiaSev > 0.20f ? (hypothermiaSev * 4.0f) : (hypothermiaSev * 2.0f);
                float factor = acidosisSev * 2f + hypothermiaFactor + bloodLossSev;
                
                // MODS 联动：肝功能障碍直接破坏凝血因子合成，导致极难止血
                if (liverFailed) factor += 4.0f;
                else if (liverInjurySev > 0f) factor += liverInjurySev * 2.0f;
                
                if (factor < 0.5f) factor = 1.0f;

                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }
        }
    }
}
