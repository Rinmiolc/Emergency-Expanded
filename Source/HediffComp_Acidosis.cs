using System.Collections.Generic;
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

        // 使用静态 List 缓存机制，完全规避 Tick 中的 GC 垃圾回收（Zero-alloc LINQ optimization）
        private static readonly List<BodyPartRecord> tmpExtremities = new List<BodyPartRecord>();
        private static readonly List<BodyPartRecord> tmpCoreOrgans = new List<BodyPartRecord>();

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float pumping = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            float breathing = Pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing); 
            float bleedRate = Pawn.health.hediffSet.BleedRateTotal; // 获取当前全身流血速率
            
            // 触发条件：泵血/呼吸低于早期阈值 (0.80)，或者处于中度大出血状态 (bleedRate > 0.15)
            if (pumping <= Props.bloodPumpingThreshold || breathing <= Props.breathingThreshold || bleedRate > 0.15f)
            {
                float severityFactor = 1f;

                // 1. 基础维生极低时的翻倍
                if (pumping <= EE_Settings.VitalCriticalThreshold || breathing <= EE_Settings.VitalCriticalThreshold) 
                {
                    severityFactor *= EE_Settings.VitalCriticalMultiplier;
                }

                // 2. 根据流血速率进行温和的代谢累加（降为 1.5f 倍）
                if (bleedRate > 0f)
                {
                    severityFactor += (bleedRate * 1.5f); 
                }

                // 3. 泵血缺口惩罚（降为 2.0f 倍）
                float pumpingDeficit = UnityEngine.Mathf.Clamp01(Props.bloodPumpingThreshold - pumping);
                severityFactor += (pumpingDeficit * 2f);

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
            tmpExtremities.Clear();
            tmpCoreOrgans.Clear();

            // 替代 LINQ .Where 筛选，不分配多余的 Enumerator 和 List 空间
            // 采用 BodyPartTag 检测，极大增强了与其他异形、种族和义肢 Mod 的兼容性
            foreach (BodyPartRecord part in Pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == null) continue;

                // 1. 核心维生器官判定 (脑、心、肺、肾、肝等)
                bool isCore = false;
                if (part.def.tags != null)
                {
                    if (part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) || // 大脑/意识源
                        part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource) ||  // 心脏
                        part.def.tags.Contains(BodyPartTagDefOf.BloodFiltrationSource) || // 肾脏/肝脏
                        part.def.tags.Contains(BodyPartTagDefOf.BreathingSource))        // 肺部
                    {
                        isCore = true;
                    }
                }
                
                // 字符串回退判断，以防万一有 Mod 没给器官加 tag
                if (!isCore)
                {
                    string defNameLower = part.def.defName.ToLower();
                    if (defNameLower.Contains("brain") || defNameLower.Contains("heart") || 
                        defNameLower.Contains("liver") || defNameLower.Contains("kidney") || 
                        defNameLower.Contains("lung"))
                    {
                        isCore = true;
                    }
                }

                if (isCore)
                {
                    tmpCoreOrgans.Add(part);
                    continue;
                }

                // 2. 末梢小部位判定 (指、趾、耳、鼻等)
                bool isExtremity = false;
                if (part.def.tags != null)
                {
                    // 检查是否属于操作或移动肢体的末梢段
                    if (part.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbSegment) || 
                        part.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment))
                    {
                        // 且该部位没有子部位 (即最末梢，如手指/脚趾)
                        if (part.parts == null || part.parts.Count == 0)
                        {
                            isExtremity = true;
                        }
                    }
                }

                if (!isExtremity)
                {
                    string defNameLower = part.def.defName.ToLower();
                    if (defNameLower.Contains("finger") || defNameLower.Contains("toe") || 
                        defNameLower.Contains("nose") || defNameLower.Contains("ear"))
                    {
                        isExtremity = true;
                    }
                }

                if (isExtremity)
                {
                    tmpExtremities.Add(part);
                }
            }

            BodyPartRecord partToAffect = null;
            bool isCoreOrgan = false; 

            if (parent.Severity >= EE_Settings.AcidosisHighThreshold && tmpCoreOrgans.Count > 0 && Rand.Chance(EE_Settings.AcidosisCoreAttackChance))
            {
                partToAffect = tmpCoreOrgans.RandomElement();
                isCoreOrgan = true;
            }
            else if (tmpExtremities.Count > 0)
            {
                partToAffect = tmpExtremities.RandomElement();
            }

            if (partToAffect == null) return;

            // 如果受影响部位是大脑，统一转化为增加“缺氧性脑损伤”（HypoxicBrainDamage），避免大脑上并存两种冲突的缺氧Hediff
            if (partToAffect.def == EE_DefOf.Brain)
            {
                HediffDef brainDamageDef = EE_DefOf.HypoxicBrainDamage;
                if (brainDamageDef != null)
                {
                    // 缩放伤口物理值到脑损伤严重度比例（除以 25。5.0 的伤害值转化为 0.20 严重度增量）
                    float rawDamage = isCoreOrgan ? (Props.hypoxiaDamage * EE_Settings.AcidosisCoreDamageMultiplier) : Props.hypoxiaDamage;
                    float brainDamageIncrement = rawDamage / 25f;

                    Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(brainDamageDef);
                    if (existingDamage != null)
                    {
                        existingDamage.Severity += brainDamageIncrement;
                    }
                    else
                    {
                        Hediff damage = HediffMaker.MakeHediff(brainDamageDef, Pawn, partToAffect);
                        damage.Severity = brainDamageIncrement;
                        Pawn.health.AddHediff(damage, partToAffect, null, null);
                    }
                }
            }
            else
            {
                // 其他常规器官或末梢部位继续使用原生的 TissueHypoxia（组织缺氧）外伤
                HediffDef hypoxiaDef = EE_DefOf.TissueHypoxia;
                if (hypoxiaDef == null) return;

                Hediff hypoxia = HediffMaker.MakeHediff(hypoxiaDef, Pawn, partToAffect);
                hypoxia.Severity = isCoreOrgan ? (Props.hypoxiaDamage * EE_Settings.AcidosisCoreDamageMultiplier) : Props.hypoxiaDamage;
                Pawn.health.AddHediff(hypoxia, partToAffect, null, null);
            }
        }
    }
}