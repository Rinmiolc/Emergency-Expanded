using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_ShockTrigger : HediffCompProperties
    {
        public HediffCompProperties_ShockTrigger()
        {
            this.compClass = typeof(HediffComp_ShockTrigger);
        }
    }

    public class HediffComp_ShockTrigger : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.pawn == null || parent.pawn.Dead) return;
            if (!parent.pawn.IsHashIntervalTick(120)) return;

            // 当失血量超过20% (Severity > 0.2f) 且没有休克时，引发休克
            if (parent.Severity > 0.2f && !parent.pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Shock))
            {
                Hediff shock = HediffMaker.MakeHediff(EE_DefOf.EE_Shock, parent.pawn, null);
                parent.pawn.health.AddHediff(shock, null, null, null);
            }
        }
    }
}
