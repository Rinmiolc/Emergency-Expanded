using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // 重写脑缺氧类，使其在 UI 上自带百分比后缀
    public class Hediff_CerebralHypoxia : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";
    }

    // 重写脑损伤类，使其在 UI 上自带百分比后缀
    public class Hediff_HypoxicBrainDamage : Hediff
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
        public float damageChancePerSecond = 0.015f; 

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
                if (pumping <= 0.1f || breathing <= 0.1f) severityFactor = 2f; 

                if (parent.Severity >= 0.6f)
                {
                    severityFactor *= 0.35f; 
                }

                severityAdjustment += (Props.hypoxiaPerDay * severityFactor) / 1000f;
            }
            else
            {
                severityAdjustment -= Props.recoveryPerDay / 1000f;
            }

            // 2. 达到 100% 时触发植物人
            if (parent.Severity >= 1.0f)
            {
                TriggerVegetativeState();
                return;
            }

            // 3. 达到重度阶段 (0.6以上)，大脑开始发生不可逆损伤
            if (parent.Severity >= 0.6f)
            {
                float baseChance = 0.02f; // 还原判定概率，确保窗口期内能摇出脑损伤
                float currentChance = baseChance * (parent.Severity >= 0.85f ? 2.5f : 1f);
                
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

            // 查找小人脑部是否已经有这个损伤了
            Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(damageDef);
            if (existingDamage != null)
            {
                // 如果有了，每次累加 0.15 的严重度（慢慢跨越你 XML 里的 4 个阶段）
                existingDamage.Severity += 0.15f; 
            }
            else
            {
                // 如果没有，新建一个并给予初始严重度
                Hediff damage = HediffMaker.MakeHediff(damageDef, Pawn, brain);
                damage.Severity = 0.15f; 
                Pawn.health.AddHediff(damage, brain, null, null);
            }
        }
    }
}