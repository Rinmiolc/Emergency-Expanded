using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // C# Recipes for bone fracture treatments and surgeries

    // 1. Traditional Bone Setting (传统正骨复位)
    public class Recipe_BoneSetting : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Fracture fracture)
                {
                    bool isImmobilized = fracture.isSplinted || fracture.isCasted || fracture.isInternallyFixed || fracture.isStrictBedrest;
                    if (!isImmobilized)
                    {
                        yield return hediff.Part;
                    }
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff_Fracture fracture = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Fracture>()
                .FirstOrDefault(h => h.Part == part);

            if (fracture != null)
            {
                fracture.isStrictBedrest = true;
                fracture.isSplinted = false;
                fracture.isCasted = false;
                fracture.isInternallyFixed = false;

                float docSkill = billDoer.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                float alignment = docSkill * 0.035f + Rand.Range(-0.08f, 0.08f);
                if (EE_DefOf.EE_MorphineActive != null && pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    alignment += EE_Constants.MorphineSurgerySuccessOffset;
                }
                fracture.alignmentQuality = UnityEngine.Mathf.Clamp(alignment, 0.20f, 0.65f);

                // Call Tended to get standard bandage overlay and tended mote
                fracture.Tended(0.50f, 1.0f);
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();

                Messages.Message("EE_MessageTraditionalSettingSuccess".Translate(billDoer.LabelShort, pawn.LabelShort, part.Label, fracture.alignmentQuality.ToStringPercent()), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    // 2. Plaster Casting (石膏夹克固定)
    public class Recipe_PlasterCasting : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_ClosedFracture && hediff is Hediff_Fracture fracture)
                {
                    if (!fracture.isCasted && !fracture.isInternallyFixed)
                    {
                        yield return hediff.Part;
                    }
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff_Fracture fracture = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Fracture>()
                .FirstOrDefault(h => h.Part == part && h.def == EE_DefOf.EE_ClosedFracture);

            if (fracture != null)
            {
                fracture.isCasted = true;
                fracture.isSplinted = false;
                fracture.isStrictBedrest = false;
                fracture.isInternallyFixed = false;

                float docSkill = billDoer.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                float alignment = 0.50f + docSkill * 0.022f + Rand.Range(-0.05f, 0.05f);
                if (EE_DefOf.EE_MorphineActive != null && pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    alignment += EE_Constants.MorphineSurgerySuccessOffset;
                }
                fracture.alignmentQuality = UnityEngine.Mathf.Clamp(alignment, 0.60f, 0.98f);

                // Call Tended to get standard bandage overlay and tended mote
                fracture.Tended(0.85f, 1.0f);
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();

                Messages.Message("EE_MessageCastingSuccess".Translate(billDoer.LabelShort, pawn.LabelShort, part.Label, fracture.alignmentQuality.ToStringPercent()), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    // 3. Open Reduction Internal Fixation (切开复位内固定术 - ORIF)
    public class Recipe_ORIF : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_Fracture fracture && !fracture.isInternallyFixed)
                {
                    yield return hediff.Part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff_Fracture fracture = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Fracture>()
                .FirstOrDefault(h => h.Part == part);

            if (fracture != null)
            {
                fracture.isInternallyFixed = true;
                fracture.isSplinted = false;
                fracture.isCasted = false;
                fracture.isStrictBedrest = false;

                float docSkill = billDoer.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 5f;
                float alignment = 0.75f + docSkill * 0.013f + Rand.Range(-0.03f, 0.03f);
                if (EE_DefOf.EE_MorphineActive != null && pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    alignment += EE_Constants.MorphineSurgerySuccessOffset;
                }
                fracture.alignmentQuality = UnityEngine.Mathf.Clamp(alignment, 0.85f, 1.0f);

                // Call Tended to get standard bandage overlay and tended mote
                fracture.Tended(1.0f, 1.0f);
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();

                Messages.Message("EE_MessageOrifSuccess".Translate(billDoer.LabelShort, pawn.LabelShort, part.Label, fracture.alignmentQuality.ToStringPercent()), pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    // 4. Osteotomy (畸形愈合重折术)
    public class Recipe_Osteotomy : Recipe_Surgery
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_Malunion)
                {
                    yield return hediff.Part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Hediff malunion = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def == EE_DefOf.EE_Malunion && h.Part == part);

            if (malunion != null)
            {
                pawn.health.RemoveHediff(malunion);

                HediffDef openDef = EE_DefOf.EE_OpenFracture;
                if (openDef != null)
                {
                    Hediff_Fracture fracture = HediffMaker.MakeHediff(openDef, pawn, part) as Hediff_Fracture;
                    if (fracture != null)
                    {
                        fracture.Severity = 15f; // Surgical re-fracture severity
                        fracture.alignmentQuality = 0f;
                        pawn.health.AddHediff(fracture, part);
                    }
                }

                Find.LetterStack.ReceiveLetter("EE_LetterOsteotomySuccess_Label".Translate(), "EE_LetterOsteotomySuccess_Desc".Translate(billDoer.LabelShort, pawn.LabelShort, part.Label), LetterDefOf.PositiveEvent, pawn);
            }
        }
    }
}
