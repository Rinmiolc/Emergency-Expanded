using RimWorld;
using Verse;
using System.Linq;

namespace EmergencyExpanded
{
    public class HediffCompProperties_Antibiotics : HediffCompProperties
    {
        public float toxicBuildupPerDay = 0.15f;

        public HediffCompProperties_Antibiotics()
        {
            this.compClass = typeof(HediffComp_Antibiotics);
        }
    }

    public class HediffComp_Antibiotics : HediffComp
    {
        public HediffCompProperties_Antibiotics Props => (HediffCompProperties_Antibiotics)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.IsHashIntervalTick(600)) return; // 每天 100 次检查 (每600 Tick)

            // 1. 肝肾毒副作用 (Toxic Buildup)
            if (Props.toxicBuildupPerDay > 0f)
            {
                HealthUtility.AdjustSeverity(Pawn, HediffDefOf.ToxicBuildup, Props.toxicBuildupPerDay / 100f);
            }

            // 2. 拟真药效：减缓感染扩散，并加速免疫生成
            foreach (var hediff in Pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.EE_LocalizedInfection || hediff.def == EE_DefOf.EE_Sepsis || hediff.def.defName.Contains("Infection") || hediff.def.defName.Contains("Sepsis"))
                {
                    var immunizableComp = hediff.TryGetComp<HediffComp_Immunizable>();
                    if (immunizableComp != null && immunizableComp.Props is HediffCompProperties_Immunizable immProps)
                    {
                        ImmunityRecord immunityRecord = Pawn.health.immunity.GetImmunityRecord(hediff.def);
                        bool isImmune = immunityRecord != null && immunityRecord.immunity >= 1f;

                        if (!isImmune)
                        {
                            // 抵消部分严重度增长 (例如压制到原本的 30% 增长率，意味着要扣除 70% 的增长量)
                            float normalSeverityGain = immProps.severityPerDayNotImmune / 100f;
                            if (normalSeverityGain > 0f)
                            {
                                float counteractAmount = normalSeverityGain * (1f - EE_Constants.AntibioticSeveritySlowdownMultiplier);
                                hediff.Severity -= counteractAmount;
                            }

                            // 额外增加免疫力生成
                            if (immunityRecord != null)
                            {
                                float normalImmunityGain = immProps.immunityPerDaySick / 100f;
                                if (normalImmunityGain > 0f)
                                {
                                    float extraImmunity = normalImmunityGain * (EE_Constants.AntibioticImmunityBoostMultiplier - 1f);
                                    // 因为系统会自动根据 BloodFiltration 和 ImmunityGainSpeed 调整，这里我们提供基础的额外固定增加值
                                    immunityRecord.immunity += extraImmunity;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
