using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Recipe_Debridement : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            // 找出所有有污染度伤口或者坏死的部位
            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Necrosis, part) || 
                    pawn.health.hediffSet.hediffs.Any(h => h.Part == part && h is Hediff_Injury && h.TryGetComp<HediffComp_Contamination>()?.contamination > 0f))
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

            // 1. 移除坏死
            Hediff necrosis = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.def == EE_DefOf.EE_Necrosis && h.Part == part);
            if (necrosis != null)
            {
                pawn.health.RemoveHediff(necrosis);
                didAnything = true;
            }

            // 2. 清除污染并造成微量切削伤害
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Part == part && h is Hediff_Injury).ToList();
            foreach (var h in hediffs)
            {
                var comp = h.TryGetComp<HediffComp_Contamination>();
                if (comp != null && comp.contamination > 0f)
                {
                    comp.contamination = 0f;
                    
                    // 造成少许真实伤害以模拟清创切掉的肉
                    DamageInfo dinfo = new DamageInfo(DamageDefOf.Cut, 2f, 0f, -1f, billDoer, part, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true);
                    pawn.TakeDamage(dinfo);
                    didAnything = true;
                }
            }

            if (didAnything && pawn.Spawned)
            {
                Messages.Message($"{billDoer?.LabelShort ?? "医生"}成功为{pawn.LabelShort}执行了清创术，去除了污染和坏死组织。", pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
