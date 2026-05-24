using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // 脑缺氧百分比显示
    public class Hediff_CerebralHypoxia : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";
    }

    // 脑损伤百分比显示
    public class Hediff_HypoxicBrainDamage : Hediff
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";
    }

    // 代谢性酸中毒百分比显示
    public class Hediff_MetabolicAcidosis : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";
    }

    public class HediffCompProperties_CerebralHypoxia : HediffCompProperties
    {
        public float hypoxiaPerDay = 8.0f; 
        public float recoveryPerDay = 3.0f; 
        public float safePumpingThreshold = 0.55f;
        public float safeBreathingThreshold = 0.55f;
        
        public string brainDamageDefName = "HypoxicBrainDamage"; 
        public string vegetativeStateDefName = "VegetativeState"; 

        public HediffCompProperties_CerebralHypoxia()
        {
            this.compClass = typeof(HediffComp_CerebralHypoxia);
        }
    }

    public class HediffComp_CerebralHypoxia : HediffComp
    {
        public HediffCompProperties_CerebralHypoxia Props => (HediffCompProperties_CerebralHypoxia)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!Pawn.IsHashIntervalTick(60)) return;

            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);

            // 1. 结算缺氧进度
            if (pumping < Props.safePumpingThreshold || breathing < Props.safeBreathingThreshold)
            {
                float severityFactor = 1f;
                if (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                    severityFactor = EE_Settings.VitalCriticalMultiplier; 

                if (parent.Severity >= EE_Settings.ComaSeverityThreshold)
                {
                    severityFactor *= EE_Settings.ComaSeverityFactor; 
                }

                severityAdjustment += (Props.hypoxiaPerDay * severityFactor) / 1000f;
            }
            else
            {
                severityAdjustment -= Props.recoveryPerDay / 1000f;
            }

            // 2. 达到阈值触发植物人
            if (parent.Severity >= EE_Settings.VegStateThreshold)
            {
                TriggerVegetativeState();
                return;
            }

            // 3. 大脑发生不可逆损伤
            if (parent.Severity >= EE_Settings.BrainDamageStartThreshold)
            {
                float currentChance = EE_Settings.BrainDamageBaseChance * (parent.Severity >= EE_Settings.BrainDamageCriticalThreshold ? EE_Settings.BrainDamageCriticalMultiplier : 1f);
                
                if (Rand.Chance(currentChance))
                {
                    ApplyPermanentBrainDamage();
                }
            }
        }

        private void TriggerVegetativeState()
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;

            HediffDef vegDef = HediffDef.Named(Props.vegetativeStateDefName);
            if (vegDef != null && !Pawn.health.hediffSet.HasHediff(vegDef))
            {
                Hediff vegHediff = HediffMaker.MakeHediff(vegDef, Pawn, brain);
                Pawn.health.AddHediff(vegHediff, brain, null, null);
                Find.LetterStack.ReceiveLetter("脑死亡", $"{Pawn.NameShortColored} 因长时间脑部缺氧，已经发生了不可逆的脑死亡，陷入了永久的植物人状态。", LetterDefOf.NegativeEvent, Pawn);
            }

            Pawn.health.RemoveHediff(parent);
        }

        private void ApplyPermanentBrainDamage()
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;

            HediffDef damageDef = HediffDef.Named(Props.brainDamageDefName);
            if (damageDef == null) return;

            Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(damageDef);
            if (existingDamage != null)
            {
                existingDamage.Severity += EE_Settings.BrainDamageSeverityIncrement; 
            }
            else
            {
                Hediff damage = HediffMaker.MakeHediff(damageDef, Pawn, brain);
                damage.Severity = EE_Settings.BrainDamageSeverityIncrement; 
                Pawn.health.AddHediff(damage, brain, null, null);
            }
        }
    }
}