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
            
            // 只要有一项低于阈值，或者两项极低，系统就开始酸中毒崩溃
            if (pumping <= Props.bloodPumpingThreshold || breathing <= Props.breathingThreshold)
            {
                // 如果是深度休克/窒息（低于0.1），酸中毒速度翻倍
                float severityFactor = (pumping <= 0.1f || breathing <= 0.1f) ? 2f : 1f;
                severityAdjustment += (Props.severityIncreasePerDay * severityFactor / 1000f); 
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }

            // ... 下方的静默组织缺氧 (ApplySilentHypoxia) 逻辑保持不变 ...
            if (parent.Severity >= 0.4f)
            {
                float chance = 0f;
                if (parent.Severity >= 0.85f) chance = 0.25f;      
                else if (parent.Severity >= 0.7f) chance = 0.08f;  
                else chance = 0.02f;                               

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