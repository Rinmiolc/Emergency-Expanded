using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Recipe_Irrigation : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            // 找出所有带有污染度的伤口，但还没有达到感染爆发阈值的部位
            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                if (pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h is Hediff_Injury && h.TryGetComp<HediffComp_Contamination>()?.contamination > 0f))
                {
                    yield return part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            bool didAnything = false;

            // 清洗污染度
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Part == part && h is Hediff_Injury).ToList();
            foreach (var h in hediffs)
            {
                var comp = h.TryGetComp<HediffComp_Contamination>();
                if (comp != null && comp.contamination > 0f)
                {
                    comp.contamination = 0f; // 直接清空污染度
                    didAnything = true;
                }
            }

            if (didAnything && pawn.Spawned)
            {
                Messages.Message("EE_MessageIrrigationSuccess".Translate(billDoer?.LabelShort ?? "EE_Doctor".Translate(), pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
