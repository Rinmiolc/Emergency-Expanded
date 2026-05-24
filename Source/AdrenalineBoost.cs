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
                    crash.Severity = 1.0f; // 初始为 1.0 巅峰脱水疲劳度
                    Pawn.health.AddHediff(crash, null, null, null);
                }
            }
        }
    }

    // ================= 物理受击触发肾上腺素补丁 =================
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
    public static class Patch_DamageWorker_AdrenalineBoost
    {
        // ==========================================
        // 【AI/开发者注意】：
        // 此处的第二个参数必须是 'Thing thing'，不得重命名为 'victim'。
        // 在新版 Harmony / RimWorld 1.6 的特定编译环境下，该方法的对应形参已被确认为 'thing'。
        // 任何自动或手动的重命名都会导致运行时 Harmony 补丁加载失败！
        // ==========================================
        public static void Postfix(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
        {
            // 1. 基础过滤：若无伤口生成或受击对象无效，直接返回
            if (__result == null || __result.hediffs == null || !__result.hediffs.Any()) return;
            Pawn pawn = thing as Pawn;
            if (pawn == null || pawn.Dead) return;

            // 2. 生理过滤与硬核兼容保护
            if (!pawn.RaceProps.IsFlesh) return;        // 排除机械族、石头人等非血肉生命
            if (pawn.IsShambler) return;                // 排除 Anomaly DLC 的蹒跚怪(死尸驱动)
            if (pawn.RaceProps.IsMechanoid) return;     // 双重保障过滤机械族

            // 3. 伤害属性过滤
            if (dinfo.Def == DamageDefOf.Stun || 
                dinfo.Def == DamageDefOf.EMP || 
                dinfo.Def == DamageDefOf.Psychic) return; // 排除眩晕、电磁、精神等纯控制或非实体外伤伤害

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
                // 已处于肾上腺素爆发中：后续挨打不重置巅峰时间，完全遵循单次激素爆发的自然降解规律
                return;
            }

            // 5. 首次受创，点燃应激极限，施加肾上腺素
            Hediff boost = HediffMaker.MakeHediff(boostDef, pawn);
            boost.Severity = 1.0f; // 初始爆发为巅峰 (100% 严重度)
            pawn.health.AddHediff(boost, null, null, null);
        }
    }
}
