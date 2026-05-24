using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
// 这个文件实现了一个新的HediffComp，用于模拟酸中毒的动态变化和缺氧伤害
namespace EmergencyExpanded
{
    // 1. 定义与 XML 对接的属性
    public class HediffCompProperties_Acidosis : HediffCompProperties
    {
        public float severityIncreasePerDay = 6.0f;  // 心跳骤停时的增加速度 (每天)
        public float severityDecreasePerDay = 0.8f;  // 正常时的代偿自愈速度 (每天)
        public float bloodPumpingThreshold = 0.2f;   // 泵血能力低于此时，酸中毒开始恶化
        
        public string hypoxiaDefName = "TissueHypoxia"; 
        public float hypoxiaDamage = 3.0f; // 每次缺氧扣除的血量
        
        public HediffCompProperties_Acidosis()
        {
            this.compClass = typeof(HediffComp_Acidosis);
        }
    }

    // 2. 核心运行逻辑
    public class HediffComp_Acidosis : HediffComp
    {
        public HediffCompProperties_Acidosis Props => (HediffCompProperties_Acidosis)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            // 每 60 tick (游戏内1秒) 结算一次，完美兼顾实时性且绝对不卡顿
            if (!Pawn.IsHashIntervalTick(60)) return; 
            
            // --- 核心一：基于泵血的动态拉锯战 ---
            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            
            if (pumping <= Props.bloodPumpingThreshold)
            {
                // 心肺停工，每天增加的值平摊到每次结算 (RimWorld 一天是 60000 tick，所以除以 1000)
                severityAdjustment += (Props.severityIncreasePerDay / 1000f); 
            }
            else
            {
                // 机体正常，每天减少的值平摊到每次结算
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }

            // --- 核心二：高频静默施加组织缺氧 ---
            if (parent.Severity >= 0.4f)
            {
                // 随着酸中毒加深，缺氧概率指数级飙升
                float chance = 0f;
                if (parent.Severity >= 0.85f) chance = 0.25f;      // 极重度：每秒 25% 触发！
                else if (parent.Severity >= 0.7f) chance = 0.08f;  // 重度：每秒 8% 触发
                else chance = 0.02f;                               // 中度：每秒 2% 触发

                if (Rand.Chance(chance))
                {
                    ApplySilentHypoxia();
                }
            }
        }

        private void ApplySilentHypoxia()
        {
            // 筛选四肢末端
            IEnumerable<BodyPartRecord> extremities = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def.defName == "Finger" || p.def.defName == "Toe" || 
                            p.def.defName == "Nose" || p.def.defName == "Ear");

            // 筛选核心内脏
            IEnumerable<BodyPartRecord> coreOrgans = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def.defName == "Brain" || p.def.defName == "Heart" || 
                            p.def.defName == "Liver" || p.def.defName == "Kidney");

            BodyPartRecord partToAffect = null;

            // 极重度(0.85)时防线崩溃，有 30% 的几率缺氧会直接攻击核心内脏,否则疯狂攻击末梢
            if (parent.Severity >= 0.85f && coreOrgans.Any() && Rand.Chance(0.3f))
            {
                partToAffect = coreOrgans.RandomElement();
            }
            else if (extremities.Any())
            {
                partToAffect = extremities.RandomElement();
            }

            if (partToAffect == null) return;

            HediffDef hypoxiaDef = HediffDef.Named(Props.hypoxiaDefName);
            if (hypoxiaDef == null) return;

            // 生成缺氧伤口
            Hediff hypoxia = HediffMaker.MakeHediff(hypoxiaDef, Pawn, partToAffect);
            hypoxia.Severity = Props.hypoxiaDamage;
            
            // 【完全静默】：绕过所有事件通知直接施加伤害，防止黄色警报弹窗
            Pawn.health.AddHediff(hypoxia, partToAffect, null, null);
        }
    }
}