using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    // ================= 通用针剂使用基类 =================
    public abstract class IngestionOutcomeDoer_SyringeBase : RimWorld.IngestionOutcomeDoer
    {
        public Verse.HediffDef toxicityHediff;       // 蓄积毒性/药效 Hediff
        public float severityIncrement = 1.0f;       // 每次注射增加的严重度
        public float maxSeverity = 3.0f;             // 严重度上限

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Verse.Thing ingested, int ingestedCount)
        {
            if (pawn.Dead) return;

            // 执行特定的针剂效果（如清除后遗症等）
            ApplySyringeEffect(pawn, ingested, ingestedCount);

            // 施加/累加严重度
            if (toxicityHediff != null)
            {
                Verse.Hediff activeHediff = pawn.health.hediffSet.GetFirstHediffOfDef(toxicityHediff);
                if (activeHediff == null)
                {
                    activeHediff = Verse.HediffMaker.MakeHediff(toxicityHediff, pawn);
                    activeHediff.Severity = severityIncrement;
                    pawn.health.AddHediff(activeHediff, null, null, null);
                }
                else
                {
                    activeHediff.Severity = UnityEngine.Mathf.Min(activeHediff.Severity + severityIncrement, maxSeverity);
                }
            }
        }

        protected abstract void ApplySyringeEffect(Pawn pawn, Verse.Thing ingested, int ingestedCount);
    }
}
