using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class IngestionOutcomeDoer_TxaSyringe : IngestionOutcomeDoer_SyringeBase
    {
        protected override void ApplySyringeEffect(Pawn pawn, Thing ingested, int ingestedCount)
        {
            // TXA 注射器摄入时的特定前置逻辑 (目前无特殊前置，全部逻辑在 IngestionOutcomeDoer_SyringeBase 中处理)
        }
    }
}
