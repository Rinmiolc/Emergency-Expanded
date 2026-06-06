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

            // 2. 清除污染并造成切削伤害 (医术越差，伤害越高，甚至可能导致小器官截肢)
            float medSkill = billDoer?.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
            float damageAmount = UnityEngine.Mathf.Max(EE_Constants.DebridementDamageMin, EE_Constants.DebridementDamageBase - (medSkill * EE_Constants.DebridementDamageSkillReduction));
            bool partDestroyed = false;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs.Where(h => h.Part == part && h is Hediff_Injury).ToList();
            foreach (var h in hediffs)
            {
                var comp = h.TryGetComp<HediffComp_Contamination>();
                if (comp != null && comp.contamination > 0f)
                {
                    comp.contamination = 0f;
                    didAnything = true;
                }
            }

            if (didAnything)
            {
                float partHealthBefore = pawn.health.hediffSet.GetPartHealth(part);
                DamageInfo dinfo = new DamageInfo(DamageDefOf.Cut, damageAmount, 0f, -1f, billDoer, part, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true);
                pawn.TakeDamage(dinfo);
                
                if (pawn.health.hediffSet.GetPartHealth(part) <= 0f && partHealthBefore > 0f)
                {
                    partDestroyed = true;
                }
            }

            if (didAnything && pawn.Spawned)
            {
                if (partDestroyed)
                {
                    Messages.Message("EE_MessageDebridementAccidentalAmputation".Translate(billDoer?.LabelShort ?? "EE_Doctor".Translate(), pawn.LabelShort, part.Label), pawn, MessageTypeDefOf.NegativeHealthEvent);
                }
                else
                {
                    Messages.Message("EE_MessageDebridementSuccess".Translate(billDoer?.LabelShort ?? "EE_Doctor".Translate(), pawn.LabelShort, damageAmount.ToString("F1")), pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
        }
    }
}
