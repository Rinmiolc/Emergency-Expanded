using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class Hediff_MyocardialInfarction : HediffWithComps
    {
        public override string SeverityLabel => (this.Severity * 100f).ToString("F0") + "%";

        public override void Tick()
        {
            base.Tick();
            
            if (this.pawn == null || this.pawn.Dead) return;

            // 每 60 Ticks 更新一次，稀疏化 Severity 变动通知以大幅节省 CPU 负载 (60000 ticks = 1天)
            if (this.pawn.IsHashIntervalTick(60))
            {
                float baseRate = 8.0f; // 每天增加 8.0 严重度
                
                // 检测吗啡状态降低心肌梗死恶化速度 (减缓 30%)
                if (EE_DefOf.EE_MorphineActive != null && this.pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    baseRate *= EE_Constants.MorphineMyocardialInfarctionSpeedMultiplier;
                }

                // 每 60 Ticks 对应的增量为 baseRate / 1000f
                this.Severity += baseRate / 1000f;
            }
        }
    }
}
