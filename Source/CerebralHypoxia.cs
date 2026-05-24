using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class HediffCompProperties_CerebralHypoxia : HediffCompProperties
    {
        // 放缓了缺氧速度。原来15.0意味着1.6小时拉满，现在8.0大约需要3个游戏小时，符合急救的"黄金窗口"
        public float hypoxiaPerDay = 8.0f; 
        public float recoveryPerDay = 3.0f; 
        public float safePumpingThreshold = 0.55f;
        public float safeBreathingThreshold = 0.55f;
        
        // 脑损伤相关配置
        public string brainDamageDefName = "HypoxicBrainDamage"; // 缺氧性脑损伤
        public string vegetativeStateDefName = "VegetativeState"; // 植物人状态
        public float damageChancePerSecond = 0.025f; // 重度缺氧时，每秒有 2.5% 几率造成永久性脑损伤

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
        
        // 心跳或呼吸极低时，基础恶化翻倍
        if (pumping <= 0.1f || breathing <= 0.1f) severityFactor = 2f; 

        // 【新增】：当脑缺氧达到重度（0.6）后，机体进入深度昏迷的低耗氧状态，恶化速度大幅放缓
        // 这里的 0.35f 可以根据你的测试感觉微调。数值越小，变成植物人的窗口期越长
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

    // 2. 达到100%时，触发脑死亡（植物人）并移除当前缺氧状态
    if (parent.Severity >= 1.0f)
    {
        TriggerVegetativeState();
        return;
    }

    // 3. 达到重度阶段 (0.6以上)，大脑开始发生不可逆损伤
    if (parent.Severity >= 0.6f)
    {
        // 0.02f 意味着每秒有 2% 的概率发生脑软化。重度阶段必定会掉脑部血量。
        float baseChance = 0.02f; 
        float currentChance = baseChance * (parent.Severity >= 0.85f ? 2.5f : 1f); // 极重度概率翻倍
        
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
                
                // 可选：发送一条红色信件通知玩家悲剧发生
                Find.LetterStack.ReceiveLetter("脑死亡", $"{Pawn.NameShortColored} 因长时间脑部缺氧，已经发生了不可逆的脑死亡，陷入了永久的植物人状态。", LetterDefOf.NegativeEvent, Pawn);
            }

            // 将脑缺氧移除，因为已经转化为了终态
            Pawn.health.RemoveHediff(parent);
        }

        private void ApplyPermanentBrainDamage()
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;

            HediffDef damageDef = HediffDef.Named(Props.brainDamageDefName);
            if (damageDef == null) return;

            Hediff damage = HediffMaker.MakeHediff(damageDef, Pawn, brain);
            damage.Severity = 1.0f; // 每次扣除 1 点脑部血量
            
            // 静默添加永久性损伤
            Pawn.health.AddHediff(damage, brain, null, null);
        }
    }
}