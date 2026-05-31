using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_MassiveBleeding : HediffCompProperties
    {
        public HediffCompProperties_MassiveBleeding()
        {
            this.compClass = typeof(HediffComp_MassiveBleeding);
        }
    }

    public class HediffComp_MassiveBleeding : HediffComp
    {
        public int tendAttempts = 0;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref tendAttempts, "tendAttempts", 0);
        }
    }
}
