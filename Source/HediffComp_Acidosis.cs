using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Acidosis : HediffCompProperties
    {
        public float severityIncreasePerDay = 6.0f;
        public float severityDecreasePerDay = 0.8f;
        public float bloodPumpingThreshold = 0.55f;  
        public float breathingThreshold = 0.55f;      
        
        public string hypoxiaDefName = "TissueHypoxia"; 
        public float hypoxiaDamage = 3.0f; 
        
        public HediffCompProperties_Acidosis()
        {
            this.compClass = typeof(HediffComp_Acidosis);
        }
    }

    public class HediffComp_Acidosis : HediffComp
    {
        public HediffCompProperties_Acidosis Props => (HediffCompProperties_Acidosis)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.IsHashIntervalTick(60)) return; 
            
            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing); 
            
            if (pumping <= Props.bloodPumpingThreshold || breathing <= Props.breathingThreshold)
            {
                float severityFactor = (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                    ? EE_Settings.VitalCriticalMultiplier : 1f;
                severityAdjustment += (Props.severityIncreasePerDay * severityFactor / 1000f); 
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }

            if (parent.Severity >= EE_Settings.AcidosisSilentHypoxiaStart)
            {
                float chance;
                if (parent.Severity >= EE_Settings.AcidosisHighThreshold) chance = EE_Settings.AcidosisChanceHigh;      
                else if (parent.Severity >= EE_Settings.AcidosisMidThreshold) chance = EE_Settings.AcidosisChanceMid;  
                else chance = EE_Settings.AcidosisChanceLow;                               

                if (Rand.Chance(chance))
                {
                    ApplySilentHypoxia();
                }
            }
        }

        private void ApplySilentHypoxia()
        {
            IEnumerable<BodyPartRecord> extremities = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def.defName == "Finger" || p.def.defName == "Toe" || 
                            p.def.defName == "Nose" || p.def.defName == "Ear");

            IEnumerable<BodyPartRecord> coreOrgans = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def.defName == "Brain" || p.def.defName == "Heart" || 
                            p.def.defName == "Liver" || p.def.defName == "Kidney");

            BodyPartRecord partToAffect = null;
            bool isCoreOrgan = false; 

            if (parent.Severity >= EE_Settings.AcidosisHighThreshold && coreOrgans.Any() && Rand.Chance(EE_Settings.AcidosisCoreAttackChance))
            {
                partToAffect = coreOrgans.RandomElement();
                isCoreOrgan = true;
            }
            else if (extremities.Any())
            {
                partToAffect = extremities.RandomElement();
            }

            if (partToAffect == null) return;

            HediffDef hypoxiaDef = HediffDef.Named(Props.hypoxiaDefName);
            if (hypoxiaDef == null) return;

            Hediff hypoxia = HediffMaker.MakeHediff(hypoxiaDef, Pawn, partToAffect);
            
            hypoxia.Severity = isCoreOrgan ? (Props.hypoxiaDamage * EE_Settings.AcidosisCoreDamageMultiplier) : Props.hypoxiaDamage;
            Pawn.health.AddHediff(hypoxia, partToAffect, null, null);
        }
    }
}