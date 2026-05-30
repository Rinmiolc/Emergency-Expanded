using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Recipe_NeedleDecompression : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_Pneumothorax && hediff.Part != null)
                {
                    if (hediff is Hediff_Pneumothorax pneumo && !pneumo.isDecompressed)
                    {
                        yield return hediff.Part;
                    }
                    else if (!(hediff is Hediff_Pneumothorax))
                    {
                        yield return hediff.Part;
                    }
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

            // Find the pneumothorax on this part
            Hediff targetHediff = null;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_Pneumothorax && hediff.Part == part)
                {
                    targetHediff = hediff;
                    break;
                }
            }

            if (targetHediff != null)
            {
                // Decompress it
                if (targetHediff is Hediff_Pneumothorax pneumo)
                {
                    pneumo.isDecompressed = true;
                    // Significantly reduce severity to simulate relieving the tension
                    pneumo.Severity = UnityEngine.Mathf.Max(0.1f, pneumo.Severity - 0.5f);
                    pneumo.Tended(1.0f, 1.0f); // Fully tend it
                }
                else
                {
                    targetHediff.Severity = UnityEngine.Mathf.Max(0.1f, targetHediff.Severity - 0.5f);
                    targetHediff.Tended(1.0f, 1.0f);
                }

                if (pawn.Spawned && pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "已减压 (胸腔穿刺)", UnityEngine.Color.green);
                }
            }
        }
    }
}
