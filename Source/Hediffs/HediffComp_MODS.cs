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

        private static readonly List<BodyPartRecord> tmpCoreOrgans = new List<BodyPartRecord>();

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

            // MODS 二期及以后 (Severity > 0.4)，开始随机造成实质性器官坏死 (改为更高频、更平滑的微量叠加)
            if (parent.Severity > 0.4f && Rand.Chance(EE_Constants.ModsCoreDamageChanceBase * parent.Severity))
            {
                ApplyOrganDamage();
            }
        }

        private void ApplyOrganDamage()
        {
            tmpCoreOrgans.Clear();
            foreach (BodyPartRecord part in Pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == null) continue;
                bool isCore = false;
                if (part.def.tags != null)
                {
                    if (part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) || 
                        part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource) ||  
                        part.def.tags.Contains(BodyPartTagDefOf.BloodFiltrationSource) || 
                        part.def.tags.Contains(BodyPartTagDefOf.BreathingSource))        
                    {
                        isCore = true;
                    }
                }
                
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
                }
            }

            if (tmpCoreOrgans.Count > 0)
            {
                BodyPartRecord partToAffect = tmpCoreOrgans.RandomElement();
                if (partToAffect.def == EE_DefOf.Brain)
                {
                    Hediff existingDamage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
                    if (existingDamage != null)
                    {
                        existingDamage.Severity += EE_Constants.ModsBrainDamageAmount;
                        if (existingDamage.Severity >= 1.0f)
                        {
                            TriggerVegetativeState();
                        }
                    }
                    else
                    {
                        Hediff damage = HediffMaker.MakeHediff(EE_DefOf.HypoxicBrainDamage, Pawn, partToAffect);
                        damage.Severity = EE_Constants.ModsBrainDamageAmount;
                        Pawn.health.AddHediff(damage, partToAffect, null, null);
                    }
                }
                else
                {
                    Hediff hypoxia = HediffMaker.MakeHediff(EE_DefOf.TissueHypoxia, Pawn, partToAffect);
                    hypoxia.Severity = EE_Constants.ModsCoreDamageAmount;
                    Pawn.health.AddHediff(hypoxia, partToAffect, null, null);
                }
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
                Find.LetterStack.ReceiveLetter("脑死亡", $"{Pawn.NameShortColored} 因多脏器功能衰竭导致严重脑部坏死，已经发生了不可逆的脑死亡，陷入了永久的植物人状态。", LetterDefOf.NegativeEvent, Pawn);
            }

            // 安全地移除相关的脑缺氧状态
            Hediff hypoxia = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.CerebralHypoxia);
            if (hypoxia != null)
            {
                Pawn.health.RemoveHediff(hypoxia);
            }

            // 脑死亡触发后，移除脑损伤状态，保持列表清爽
            Hediff damage = Pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.HypoxicBrainDamage);
            if (damage != null)
            {
                Pawn.health.RemoveHediff(damage);
            }
        }
    }
}
