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

    // ================= 组织缺氧局部外伤 =================
    // 在系统性窒息或全身循环崩溃（血泵或呼吸低于安全门槛）时，强行冻结局部组织坏死伤口的自愈与治疗愈合！
    public class Hediff_TissueHypoxia : Hediff_Injury
    {
        public override void Heal(float amount)
        {
            if (pawn == null || pawn.Dead)
            {
                base.Heal(amount);
                return;
            }

            float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            
            if (pumping < EE_Settings.HypoxiaMonitorThreshold || breathing < EE_Settings.HypoxiaMonitorThreshold)
            {
                return; 
            }

            base.Heal(amount);
        }

        public override void Tick()
        {
            base.Tick();
            if (pawn == null || pawn.Dead) return;

            // 如果血液循环恢复，则快速逆转缺氧状态
            if (pawn.IsHashIntervalTick(60))
            {
                float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                if (pumping > EE_Settings.HypoxiaMonitorThreshold && breathing > EE_Settings.HypoxiaMonitorThreshold)
                {
                    base.Heal(0.08f); // 每天可恢复约 80 点缺氧程度，快速好转
                }
            }
        }
    }

    public class HediffCompProperties_CerebralHypoxia : HediffCompProperties
    {
        public float hypoxiaPerDay = EE_Constants.HypoxiaPerDay; 
        public float recoveryPerDay = EE_Constants.HypoxiaRecoveryPerDay; 
        public float safePumpingThreshold = EE_Constants.HypoxiaMonitorThreshold;
        public float safeBreathingThreshold = EE_Constants.HypoxiaMonitorThreshold;

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
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float overdoseSev = Pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.DrugOverdose)?.Severity ?? 0f;

            // 1. 结算缺氧进度（在泵血、呼吸能力低，或者重度药物过量时触发）
            if (pumping < Props.safePumpingThreshold || breathing < Props.safeBreathingThreshold || overdoseSev > 0.75f)
            {
                float severityFactor = 1f;
                if (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                    severityFactor = EE_Settings.VitalCriticalMultiplier; 

                if (parent.Severity >= EE_Settings.ComaSeverityThreshold)
                {
                    severityFactor *= EE_Settings.ComaSeverityFactor; 
                }

                // 麻醉脑保护机制：麻醉状态下脑耗氧量减半 (乘以 0.5f)
                if (Pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                {
                    severityFactor *= EE_Constants.AnestheticHypoxiaProtectionFactor;
                }

                // 药物过量中枢抑制加成：重度药物过量导致中枢性窒息，额外增加缺氧增长速度
                if (overdoseSev > 0.75f)
                {
                    severityFactor += (overdoseSev - 0.75f) * EE_Constants.DrugOverdoseHypoxiaSeverityIncrease;
                }

                severityAdjustment += (Props.hypoxiaPerDay * severityFactor) / 1000f;
            }
            else
            {
                severityAdjustment -= Props.recoveryPerDay / 1000f;
            }

            // 2. 只有当脑缺氧进度达到100%（Severity >= 1.0）时，缺氧性脑损伤的进度才会开始累加！
            if (parent.Severity >= 1.0f)
            {
                float severityFactor = parent.Severity >= EE_Settings.BrainDamageCriticalThreshold ? EE_Settings.BrainDamageCriticalMultiplier : 1f;
                float increment = EE_Settings.BrainDamageBaseChance * severityFactor * EE_Settings.BrainDamageSeverityIncrement;
                ApplySmoothBrainDamage(increment);
            }
        }

        private void TriggerVegetativeState()
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;

            HediffDef vegDef = EE_DefOf.VegetativeState;
            if (vegDef != null && !Pawn.health.hediffSet.HasHediff(vegDef))
            {
                Hediff vegHediff = HediffMaker.MakeHediff(vegDef, Pawn, brain);
                Pawn.health.AddHediff(vegHediff, brain, null, null);
                Find.LetterStack.ReceiveLetter("脑死亡", $"{Pawn.NameShortColored} 因长时间脑部缺氧与严重脑损伤，已经发生了不可逆的脑死亡，陷入了永久的植物人状态。", LetterDefOf.NegativeEvent, Pawn);
            }

            // 脑死亡触发后，安全地移除脑缺氧状态
            Pawn.health.RemoveHediff(parent);

            // 脑死亡触发后，安全地移除脑损伤状态，保持列表清爽
            Hediff damage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
            if (damage != null)
            {
                Pawn.health.RemoveHediff(damage);
            }
        }

        private void ApplySmoothBrainDamage(float increment)
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;

            HediffDef damageDef = EE_DefOf.HypoxicBrainDamage;
            if (damageDef == null) return;

            Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(damageDef);
            if (existingDamage != null)
            {
                existingDamage.Severity += increment; 
                // 只有当脑损伤走到100%时触发脑死亡
                if (existingDamage.Severity >= 1.0f)
                {
                    TriggerVegetativeState();
                }
            }
            else
            {
                Hediff damage = HediffMaker.MakeHediff(damageDef, Pawn, brain);
                damage.Severity = increment; 
                Pawn.health.AddHediff(damage, brain, null, null);
                
                if (damage.Severity >= 1.0f)
                {
                    TriggerVegetativeState();
                }
            }
        }
    }
}