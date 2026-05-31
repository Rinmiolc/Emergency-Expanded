using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class HediffCompProperties_BiologicalDeathTimer : HediffCompProperties
    {
        public HediffCompProperties_BiologicalDeathTimer()
        {
            this.compClass = typeof(HediffComp_BiologicalDeathTimer);
        }
    }

    public class HediffComp_BiologicalDeathTimer : HediffComp
    {
        public HediffCompProperties_BiologicalDeathTimer Props => (HediffCompProperties_BiologicalDeathTimer)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh) return;

            // 每 60 Ticks 检查一次条件，以节省性能
            if (!Pawn.IsHashIntervalTick(60)) return;

            if (CheckConditionA())
            {
                // 满足条件，判断是否达到最终死亡临界值 1.0
                if (parent.Severity >= 1.0f)
                {
                    ForceBiologicalDeath();
                }
            }
            else
            {
                // 如果不再满足条件，立刻移除倒计时
                Pawn.health.RemoveHediff(parent);
            }
        }

        public bool CheckConditionA()
        {
            return Patch_Pawn_HealthTracker_HealthTick.CheckConditionA(Pawn);
        }

        private void ForceBiologicalDeath()
        {
            // 给小人施加死亡 Hediff
            Hediff deathCause = HediffMaker.MakeHediff(EE_DefOf.EE_BiologicalDeath, Pawn, null);
            Pawn.health.AddHediff(deathCause, null, null, null);

            // 杀除小人
            Pawn.Kill(null, deathCause);
        }
    }
}
