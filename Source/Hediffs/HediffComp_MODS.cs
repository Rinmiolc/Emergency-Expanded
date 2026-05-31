using System.Collections.Generic;
using RimWorld;
using Verse;

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
            if (shockSev >= EE_Constants.ShockIrreversibleThreshold || acidosisSev > 0.6f)
            {
                float factor = 1.0f + (shockSev - EE_Constants.ShockIrreversibleThreshold) * 3f + acidosisSev;
                severityAdjustment += (Props.severityIncreasePerDay * factor / 1000f);
            }
            else
            {
                severityAdjustment -= (Props.severityDecreasePerDay / 1000f);
            }

            // MODS 进展期及以后 (Severity > 0.4)，开始造成实质性的核心脏器缺血坏死积累
            if (parent.Severity > 0.4f)
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
            if (parent.Severity > 0.4f && Rand.Chance(3.0f * parent.Severity / 5f))
            {
                 ApplyBrainDamage();
            }
        }

        private void ApplySpecificOrganDamage(float modsSeverity)
        {
            // 基础倍率：根据 MODS 本身严重度加速，越到后期器官坏死越快
            float multiplier = modsSeverity * 2.0f; 

            // 处理各核心器官
            ProcessOrgan(BodyPartTagDefOf.BloodFiltrationSource, "Kidney", EE_DefOf.EE_AcuteKidneyInjury, EE_DefOf.EE_KidneyFailure, EE_Constants.ModsKidneyDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BloodFiltrationSource, "Liver", EE_DefOf.EE_AcuteLiverInjury, EE_DefOf.EE_LiverFailure, EE_Constants.ModsLiverDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BreathingSource, "Lung", EE_DefOf.EE_AcuteRespiratoryDistress, EE_DefOf.EE_RespiratoryFailure, EE_Constants.ModsLungDamageRate * multiplier);
            ProcessOrgan(BodyPartTagDefOf.BloodPumpingSource, "Heart", EE_DefOf.EE_MyocardialIschemia, EE_DefOf.EE_HeartFailure, EE_Constants.ModsHeartDamageRate * multiplier);
        }

        private void RecoverSpecificOrganDamage()
        {
            float recoverAmount = EE_Constants.ModsOrganRecoveryRate / 1000f; // 每天恢复量折算到 60 Ticks
            
            HediffDef[] injuryDefs = { 
                EE_DefOf.EE_AcuteKidneyInjury, 
                EE_DefOf.EE_AcuteLiverInjury, 
                EE_DefOf.EE_AcuteRespiratoryDistress, 
                EE_DefOf.EE_MyocardialIschemia 
            };
            
            foreach (HediffDef def in injuryDefs)
            {
                List<Hediff> injuries = new List<Hediff>();
                Pawn.health.hediffSet.GetHediffs(ref injuries, h => h.def == def);
                foreach (Hediff h in injuries)
                {
                    h.Severity -= recoverAmount;
                }
            }
        }

        private void ProcessOrgan(BodyPartTagDef tag, string fallbackKeyword, HediffDef injuryDef, HediffDef failureDef, float damageRatePerDay)
        {
            if (injuryDef == null || failureDef == null) return;
            
            float damageAmount = damageRatePerDay / 1000f; // 1000 ticks of 60-hash intervals = 60,000 ticks (1 day)
            
            foreach (BodyPartRecord part in Pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == null) continue;
                bool match = false;
                
                if (part.def.tags != null && part.def.tags.Contains(tag)) match = true;
                if (!match && part.def.defName.ToLower().Contains(fallbackKeyword.ToLower())) match = true;
                
                if (match)
                {
                    // 如果已经发生不可逆衰竭，则跳过
                    if (Pawn.health.hediffSet.HasHediff(failureDef, part)) continue;
                    
                    Hediff injury = Pawn.health.hediffSet.GetFirstHediffOfDef(injuryDef);
                    if (injury != null && injury.Part == part)
                    {
                        injury.Severity += damageAmount;
                        if (injury.Severity >= 1.0f)
                        {
                            TriggerOrganFailure(part, injury, failureDef);
                        }
                    }
                    else
                    {
                        // 可能存在同类 Hediff 但不在同一个 part（例如双肾），安全起见使用循环寻找当前 part 的 injury
                        bool found = false;
                        foreach (Hediff h in Pawn.health.hediffSet.hediffs)
                        {
                            if (h.def == injuryDef && h.Part == part)
                            {
                                h.Severity += damageAmount;
                                found = true;
                                if (h.Severity >= 1.0f) TriggerOrganFailure(part, h, failureDef);
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            Hediff newInjury = HediffMaker.MakeHediff(injuryDef, Pawn, part);
                            newInjury.Severity = damageAmount;
                            Pawn.health.AddHediff(newInjury, part, null, null);
                        }
                    }
                }
            }
        }

        private void TriggerOrganFailure(BodyPartRecord part, Hediff injury, HediffDef failureDef)
        {
            Pawn.health.RemoveHediff(injury);
            Hediff failure = HediffMaker.MakeHediff(failureDef, Pawn, part);
            Pawn.health.AddHediff(failure, part, null, null);
            
            Find.LetterStack.ReceiveLetter("器官衰竭", $"{Pawn.NameShortColored} 的 {part.Label} 由于长时间处于缺血休克状态，发生了不可逆的坏死，演变为 {failure.Label}！现在只能通过器官移植来挽救这部分机能。", LetterDefOf.NegativeEvent, Pawn);
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
                    HediffDef heartAttackDef = HediffDef.Named("HeartAttack");
                    if (heartAttackDef != null && !Pawn.health.hediffSet.HasHediff(heartAttackDef, ischemia.Part))
                    {
                        Hediff heartAttack = HediffMaker.MakeHediff(heartAttackDef, Pawn, ischemia.Part);
                        Pawn.health.AddHediff(heartAttack, ischemia.Part, null, null);
                        Find.LetterStack.ReceiveLetter("心肌梗死", $"{Pawn.NameShortColored} 因严重心肌缺血突发心肌梗死！需立即进行抢救，否则极易恶化为心室颤动并导致死亡。", LetterDefOf.NegativeEvent, Pawn);
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
                        Find.LetterStack.ReceiveLetter("自发性气胸", $"{Pawn.NameShortColored} 因急性呼吸窘迫（ARDS）导致肺部顺应性极度下降，自发诱发了张力性气胸！", LetterDefOf.NegativeEvent, Pawn);
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
                Find.LetterStack.ReceiveLetter("脑死亡", $"{Pawn.NameShortColored} 因多脏器功能衰竭导致严重脑部坏死，已经发生了不可逆的脑死亡，陷入了永久的植物人状态。", LetterDefOf.NegativeEvent, Pawn);
            }

            Hediff hypoxia = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia);
            if (hypoxia != null) Pawn.health.RemoveHediff(hypoxia);

            Hediff damage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
            if (damage != null) Pawn.health.RemoveHediff(damage);
        }
    }
}
