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

            bool isCprActive = EE_DefOf.EE_CPR_Receiving != null && Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_CPR_Receiving);

            if (CheckConditionA())
            {
                // 满足条件 (flatline + brain dead/vegetative)
                if (isCprActive)
                {
                    // CPR 人工循环维持中：抵消本周期内自动增长的 Severity (6.0 / 1000 = 0.006)
                    severityAdjustment -= 6.0f / 1000f;
                }

                // 判断是否达到最终死亡临界值 1.0
                if (parent.Severity >= 1.0f)
                {
                    ForceBiologicalDeath();
                }
            }
            else
            {
                // 如果不再满足条件，例如做 CPR 维持了呼吸循环，或者心脏恢复跳动了
                if (isCprActive)
                {
                    // CPR 强制维持中，同样只进行暂停，不恢复
                    severityAdjustment -= 6.0f / 1000f;
                }
                else
                {
                    // 心脏自主恢复跳动（自然恢复）：消退严重度 (抵消自动增加的 6.0，并额外消退 2.0，共 -8.0/天)
                    severityAdjustment -= 8.0f / 1000f;
                }

                if (parent.Severity <= 0.011f)
                {
                    Pawn.health.RemoveHediff(parent);
                }
            }
        }

        public bool CheckConditionA()
        {
            return CompEE_PawnGizmos.CheckConditionA(Pawn);
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
