using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    // ================= 肾上腺素增强 Comp 定义 =================
    public class HediffCompProperties_AdrenalineBoost : HediffCompProperties
    {
        public HediffCompProperties_AdrenalineBoost()
        {
            this.compClass = typeof(HediffComp_AdrenalineBoost);
        }
    }

    public class HediffComp_AdrenalineBoost : HediffComp
    {
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            // 当肾上腺素自然衰减消失时，若小人依然活着且为血肉生物，则触发后遗症阶段
            if (Pawn != null && !Pawn.Dead && Pawn.RaceProps.IsFlesh && !Pawn.IsShambler)
            {
                HediffDef crashDef = EE_DefOf.AdrenalineCrash;
                if (crashDef != null && !Pawn.health.hediffSet.HasHediff(crashDef))
                {
                    Hediff crash = HediffMaker.MakeHediff(crashDef, Pawn);
                    crash.Severity = 1.0f; // 初始为 1.0 巅峰疲劳度
                    Pawn.health.AddHediff(crash, null, null, null);
                }
            }
        }
    }

    // ================= 物理受击触发肾上腺素补丁 =================
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_DamageWorker_AdrenalineBoost
    {
        // 在新版 Harmony / RimWorld 1.6 的特定编译环境下，该方法的对应形参已被确认为 'thing'。
        public static void Postfix(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
        {
            // 1. 基础过滤：若无伤口生成或受击对象无效，直接返回
            if (__result == null || __result.hediffs == null || !__result.hediffs.Any()) return;
            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.Dead) return;

            // 2. 生理过滤与硬核兼容保护
            if (!pawn.RaceProps.IsFlesh) return;        // 排除机械族等非血肉生命
            if (pawn.IsShambler) return;                // 排除 1.5 蹒跚怪
            if (pawn.RaceProps.IsMechanoid) return;     // 双重保障过滤机械族

            // 3. 伤害属性过滤
            if (dinfo.Def == DamageDefOf.Stun || 
                dinfo.Def == DamageDefOf.EMP || 
                dinfo.Def == DamageDefOf.Psychic) return; // 排除眩晕、电磁、精神等纯控制或非实体外伤伤害

            // 核心修复：如果在强制击倒流程中（如生成远古小人），禁止触发肾上腺素。
            // 否则肾上腺素会提高倒地阈值，导致系统为了击倒小人而施加致命的过量伤害（如脖子中枪）从而意外死亡。
            if (EE_GlobalFlags.IsForcingDown) return;

            HediffDef boostDef = EE_DefOf.AdrenalineBoost;
            HediffDef crashDef = EE_DefOf.AdrenalineCrash;
            if (boostDef == null) return;

            // 4. 应激保护与枯竭判定
            if (pawn.health.hediffSet.HasHediff(crashDef))
            {
                // 若处于后遗症疲劳阶段，代表体能极度透支且激素已耗竭，无法再次触发肾上腺素！
                return;
            }

            if (pawn.health.hediffSet.HasHediff(boostDef))
            {
                // 已处于肾上腺素爆发中：后续挨打不重置巅峰时间，完全遵循自然降解规律
                return;
            }

            // 5. 首次受创，点燃应激极限，施加肾上腺素
            Hediff boost = HediffMaker.MakeHediff(boostDef, pawn);
            boost.Severity = EE_Constants.AdrenalineNaturalMaxSeverity; // 初始爆发为应激阶段 (默认 0.49 严重度)
            pawn.health.AddHediff(boost, null, null, null);
        }
    }

    // ================= 通用针剂隐藏冷却状态 =================
    public class Hediff_HiddenCooldown : Verse.HediffWithComps
    {
        public override bool Visible => false; // 隐藏状态，避免面板杂乱
    }

    // ================= 通用针剂使用基类 =================
    public abstract class IngestionOutcomeDoer_SyringeBase : RimWorld.IngestionOutcomeDoer
    {
        public Verse.HediffDef cooldownHediff; // XML 中配置的冷却状态
        public float cooldownSeverity = 1.0f;  // 冷却状态初始严重度

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Verse.Thing ingested, int ingestedCount)
        {
            if (pawn.Dead) return;

            // 检查是否处于对应的针剂冷却期
            if (cooldownHediff != null && pawn.health.hediffSet.HasHediff(cooldownHediff))
            {
                Verse.Messages.Message($"{pawn.LabelShort} 正处于针剂不应期，无法吸收药效！", pawn, RimWorld.MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 执行特定的针剂效果
            ApplySyringeEffect(pawn, ingested, ingestedCount);

            // 施加独立的冷却状态
            if (cooldownHediff != null)
            {
                Verse.Hediff cooldown = Verse.HediffMaker.MakeHediff(cooldownHediff, pawn);
                cooldown.Severity = cooldownSeverity;
                pawn.health.AddHediff(cooldown, null, null, null);
            }
        }

        protected abstract void ApplySyringeEffect(Pawn pawn, Verse.Thing ingested, int ingestedCount);
    }

    // ================= 肾上腺素注射器特定逻辑 =================
    public class IngestionOutcomeDoer_AdrenalineSyringe : IngestionOutcomeDoer_SyringeBase
    {
        protected override void ApplySyringeEffect(Pawn pawn, Verse.Thing ingested, int ingestedCount)
        {
            // 如果当前处于后遗症状态，则直接移除它（选项C：仅重置状态，无额外惩罚）
            Verse.HediffDef crashDef = EE_DefOf.AdrenalineCrash;
            if (crashDef != null)
            {
                Verse.Hediff crash = pawn.health.hediffSet.GetFirstHediffOfDef(crashDef);
                if (crash != null)
                {
                    pawn.health.RemoveHediff(crash);
                }
            }

            // 施加/增强肾上腺素
            Verse.HediffDef boostDef = EE_DefOf.AdrenalineBoost;
            if (boostDef != null)
            {
                Verse.Hediff boost = pawn.health.hediffSet.GetFirstHediffOfDef(boostDef);
                if (boost == null)
                {
                    boost = Verse.HediffMaker.MakeHediff(boostDef, pawn);
                    pawn.health.AddHediff(boost, null, null, null);
                }
                boost.Severity = EE_Constants.AdrenalineSyringeSeverity;
            }
        }
    }
}
