using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Recipe_ExtractHemogenEE : Recipe_ExtractHemogen
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                // 二级失血性休克的阈值为 0.15。如果达到或超过 0.15，强行禁止抽血，防止配合 0.45 抽血量直接突破 0.55 致死线
                if (bloodLoss != null && bloodLoss.Severity >= 0.15f)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
