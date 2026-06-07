using System.Collections.Generic;
using RimWorld;
using Verse;
using System.Linq;

namespace EmergencyExpanded
{
    // ================= 隐藏的 MODS 机制标志位 =================
    // 这个特殊的 Hediff 类覆盖了 Visible 属性，使其永远不在玩家的“健康”面板中显示。
    // 它仅作为后台检测是否满足微循环崩溃并启动对核心脏器大剂量伤害的 Flag。
    public class Hediff_HiddenMultipleOrganFailure : HediffWithComps
    {
        public override bool Visible => false;
    }

    public class HediffCompProperties_MODS : HediffCompProperties
    {
        public float severityIncreasePerDay = 3.0f;
        public float severityDecreasePerDay = 2.0f;
        
        public HediffCompProperties_MODS()
        {
            this.compClass = typeof(HediffComp_MODS);
        }
    }

    public class HediffComp_MODS : HediffComp
    {
        public HediffCompProperties_MODS Props => (HediffCompProperties_MODS)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.RaceProps.IsFlesh || Pawn.IsShambler) return;
            if (!Pawn.IsHashIntervalTick(60)) return;

            float shockSev = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Shock)?.Severity ?? 0f;
            float acidosisSev = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.MetabolicAcidosis)?.Severity ?? 0f;

            // 当休克进入不可逆期 (>0.7) 或极重度酸中毒时，高频触发多器官衰竭
            if (shockSev >= EE_Constants.ShockIrreversibleThreshold || acidosisSev >= EE_Constants.AcidosisMidThreshold)
            {
                float factor = 1.0f + (shockSev - EE_Constants.ShockIrreversibleThreshold) * 3f + acidosisSev;
                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }

            // MODS 进展期及以后 (Severity > 0.4)，开始造成实质性的核心脏器缺血坏死积累
            if (parent.Severity >= EE_Constants.ModsProgressionThreshold)
            {
                ApplySpecificOrganDamage(parent.Severity);
                HandleSynergies();
            }
            else if (parent.Severity < 0.2f)
            {
                // 脱离危险期后，器官的可逆损伤开始自然愈合
                RecoverSpecificOrganDamage();
            }
            
            // 脑部仍然按老方式单独高频随机微量累加（如果需要），或者可以整合，这里保留脑缺氧机制触发植物人
            if (parent.Severity >= EE_Constants.ModsProgressionThreshold && Rand.Chance(EE_Constants.ModsBrainDamageChanceBase * parent.Severity / EE_Constants.ModsBrainDamageChanceDivisor))
            {
                 ApplyBrainDamage();
            }
        }

        private void ApplySpecificOrganDamage(float modsSeverity)
        {
            // 基础倍率：根据 MODS 本身严重度加速，越到后期器官坏死越快
            float multiplier = modsSeverity * EE_Constants.ModsDamageMultiplier; 

            // 处理各核心器官
            ProcessOrgan(BodyPartTagDefOf.BloodFiltrationSource, "Kidney", EE_DefOf.EE_AcuteKidneyInjury, EE_DefOf.EE_KidneyFailure, EE_Constants.ModsKidneyDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BloodFiltrationSource, "Liver", EE_DefOf.EE_AcuteLiverInjury, EE_DefOf.EE_LiverFailure, EE_Constants.ModsLiverDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BreathingSource, "Lung", EE_DefOf.EE_AcuteRespiratoryDistress, EE_DefOf.EE_RespiratoryFailure, EE_Constants.ModsLungDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BloodPumpingSource, "Heart", EE_DefOf.EE_MyocardialIschemia, EE_DefOf.EE_HeartFailure, EE_Constants.ModsHeartDamageRate * multiplier);
        }

        private void RecoverSpecificOrganDamage()
        {
            float recoverAmount = EE_Constants.ModsOrganRecoveryRate / 1000f; // 每天恢复量折算到 60 Ticks
            
            // 采用单次倒序 for 循环，规避 LINQ 闭包与 List 的实例化分配，达到 0 GC 消耗
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff h = hediffs[i];
                if (h.def == EE_DefOf.EE_AcuteKidneyInjury || 
                    h.def == EE_DefOf.EE_AcuteLiverInjury || 
                    h.def == EE_DefOf.EE_AcuteRespiratoryDistress || 
                    h.def == EE_DefOf.EE_MyocardialIschemia)
                {
                    h.Severity -= recoverAmount;
                }
            }
        }

        private void ProcessOrgan(BodyPartTagDef tag, string fallbackKeyword, HediffDef injuryDef, HediffDef failureDef, float damageRatePerDay)
        {
            if (injuryDef == null || failureDef == null) return;
            
            float damageAmount = damageRatePerDay / 1000f; // 1000 ticks of 60-hash intervals = 60,000 ticks (1 day)
            
            // 用 Pawn.RaceProps.body.AllParts 的 for 循环遍历代替 GetNotMissingParts() 这种会产生 IEnumerable 状态机的遍历
            List<BodyPartRecord> parts = Pawn.RaceProps.body.AllParts;
            for (int i = 0; i < parts.Count; i++)
            {
                BodyPartRecord part = parts[i];
                if (part.def == null || !Pawn.health.hediffSet.GetNotMissingParts().Contains(part)) continue;
                
                if (EE_BodyPartCache.IsOrganType(part.def, tag, fallbackKeyword))
                {
                    // 如果已经发生不可逆衰竭，则跳过
                    if (Pawn.health.hediffSet.HasHediff(failureDef, part)) continue;
                    
                    Hediff injury = null;
                    List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
                    for (int j = 0; j < hediffs.Count; j++)
                    {
                        Hediff h = hediffs[j];
                        if (h.def == injuryDef && h.Part == part)
                        {
                            injury = h;
                            break;
                        }
                    }

                    if (injury != null)
                    {
                        injury.Severity += damageAmount;
                        if (injury.Severity >= 1.0f)
                        {
                            TriggerOrganFailure(part, injury, failureDef);
                        }
                    }
                    else
                    {
                        Hediff newInjury = HediffMaker.MakeHediff(injuryDef, Pawn, part);
                        newInjury.Severity = damageAmount;
                        Pawn.health.AddHediff(newInjury, part, null, null);
                    }
                }
            }
        }

        private void TriggerOrganFailure(BodyPartRecord part, Hediff injury, HediffDef failureDef)
        {
            Pawn.health.RemoveHediff(injury);
            Hediff failure = HediffMaker.MakeHediff(failureDef, Pawn, part);
            Pawn.health.AddHediff(failure, part, null, null);
            
            Find.LetterStack.ReceiveLetter("EE_LetterOrganFailure_Label".Translate(), "EE_LetterOrganFailure_Desc".Translate(Pawn.NameShortColored, part.Label, failure.Label), LetterDefOf.NegativeEvent, Pawn);
        }
        
        private void HandleSynergies()
        {
            // 1. 心肌梗死联动
            Hediff ischemia = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialIschemia);
            if (ischemia != null && ischemia.Severity > EE_Constants.ModsHeartAttackThreshold)
            {
                // 每 60 ticks 判定一次，转化为每小时概率 (每小时有 41.6 个 60 ticks，因为 1 小时 = 2500 ticks)
                // P(per 60 ticks) = ChancePerHour / (2500 / 60) = ChancePerHour / 41.66
                float chance = EE_Constants.ModsHeartAttackChancePerHour / 41.66f;
                // 严重度越高，概率越大
                chance *= (ischemia.Severity / EE_Constants.ModsHeartAttackThreshold);
                
                if (Rand.Chance(chance))
                {
                    HediffDef heartAttackDef = EE_DefOf.HeartAttack;
                    if (heartAttackDef != null && !Pawn.health.hediffSet.HasHediff(heartAttackDef, ischemia.Part))
                    {
                        Hediff heartAttack = HediffMaker.MakeHediff(heartAttackDef, Pawn, ischemia.Part);
                        Pawn.health.AddHediff(heartAttack, ischemia.Part, null, null);
                        Find.LetterStack.ReceiveLetter("EE_LetterMyocardialInfarction_Label".Translate(), "EE_LetterMyocardialInfarction_Desc".Translate(Pawn.NameShortColored), LetterDefOf.NegativeEvent, Pawn);
                    }
                }
            }
            
            // 2. 气胸联动
            Hediff ards = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_AcuteRespiratoryDistress);
            if (ards != null && ards.Severity > EE_Constants.ModsPneumothoraxThreshold)
            {
                // 每天有 1000 个 60-ticks
                float chance = EE_Constants.ModsPneumothoraxChancePerDay / 1000f;
                if (Rand.Chance(chance))
                {
                    if (EE_DefOf.EE_Pneumothorax != null && !Pawn.health.hediffSet.HasHediff(EE_DefOf.EE_Pneumothorax, ards.Part))
                    {
                        Hediff pneumo = HediffMaker.MakeHediff(EE_DefOf.EE_Pneumothorax, Pawn, ards.Part);
                        pneumo.Severity = EE_Constants.PneumothoraxBaseSeverity;
                        Pawn.health.AddHediff(pneumo, ards.Part, null, null);
                        Find.LetterStack.ReceiveLetter("EE_LetterSpontaneousPneumothorax_Label".Translate(), "EE_LetterSpontaneousPneumothorax_Desc".Translate(Pawn.NameShortColored), LetterDefOf.NegativeEvent, Pawn);
                    }
                }
            }
        }

        private void ApplyBrainDamage()
        {
            BodyPartRecord brain = Pawn.health.hediffSet.GetBrain();
            if (brain == null) return;
            
            // 如果已经脑死亡，则停止累加脑损伤
            if (Pawn.health.hediffSet.HasHediff(EE_DefOf.VegetativeState, brain)) return;
            
            Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
            if (existingDamage != null)
            {
                existingDamage.Severity += EE_Constants.ModsBrainDamageAmount;
                if (existingDamage.Severity >= 1.0f)
                {
                    TriggerVegetativeState(brain);
                }
            }
            else
            {
                Hediff damage = HediffMaker.MakeHediff(EE_DefOf.HypoxicBrainDamage, Pawn, brain);
                damage.Severity = EE_Constants.ModsBrainDamageAmount;
                Pawn.health.AddHediff(damage, brain, null, null);
            }
        }
        
        private void TriggerVegetativeState(BodyPartRecord brain)
        {
            HediffDef vegDef = EE_DefOf.VegetativeState;
            if (vegDef != null && !Pawn.health.hediffSet.HasHediff(vegDef))
            {
                Hediff vegHediff = HediffMaker.MakeHediff(vegDef, Pawn, brain);
                Pawn.health.AddHediff(vegHediff, brain, null, null);
                Find.LetterStack.ReceiveLetter("EE_LetterBrainDeath_Label".Translate(), "EE_LetterBrainDeathMods_Desc".Translate(Pawn.NameShortColored), LetterDefOf.NegativeEvent, Pawn);
            }

            Hediff hypoxia = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia);
            if (hypoxia != null) Pawn.health.RemoveHediff(hypoxia);

            Hediff damage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
            if (damage != null) Pawn.health.RemoveHediff(damage);
        }
    }
}
