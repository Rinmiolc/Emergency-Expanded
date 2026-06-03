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
            // 心肌梗死 100% 进度不再强制致死，交由后续多脏器缺氧衰竭机制处理
            if (this.pawn != null && !this.pawn.Dead)
            {
                float baseRate = 8.0f; // 每天增加 8.0 严重度
                
                // 检测吗啡状态降低心肌梗死恶化速度 (减缓 30%)
                if (EE_DefOf.EE_MorphineActive != null && this.pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    baseRate *= EE_Constants.MorphineMyocardialInfarctionSpeedMultiplier;
                }

                // 转换成每 Tick 的严重度增量 (60000 ticks = 1天)
                float increment = baseRate / 60000f;
                this.Severity += increment;
            }
        }
    }
}
