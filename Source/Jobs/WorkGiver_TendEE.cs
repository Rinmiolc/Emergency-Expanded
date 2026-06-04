using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyExpanded
{
    public class WorkGiver_TendEE : WorkGiver_Tend
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // First call base class implementation
            if (!base.HasJobOnThing(pawn, t, forced))
            {
                return false;
            }

            Pawn patient = t as Pawn;
            if (patient == null) return true;

            // If patient ONLY has fractures that need tending, block standard tending
            if (OnlyHasFracturesNeedTending(patient))
            {
                return false;
            }

            return true;
        }

        private bool OnlyHasFracturesNeedTending(Pawn patient)
        {
            bool hasFracture = false;
            foreach (Hediff hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.TendableNow())
                {
                    if (hediff is Hediff_Fracture)
                    {
                        hasFracture = true;
                    }
                    else
                    {
                        // If there are other standard wounds that need tending, allow normal tending
                        return false;
                    }
                }
            }
            return hasFracture;
        }
    }
}
