using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class Hediff_VentricularFibrillation : HediffWithComps
    {
        public override void Tick()
        {
            if (pawn != null && pawn.health.hediffSet.HasHediff(EE_DefOf.EE_AdrenalineStabilized))
            {
                // 冻结心律失常：肾上腺素强化期间不增加严重度，且不执行后续 comps
                return;
            }
            base.Tick();
        }
    }
}
