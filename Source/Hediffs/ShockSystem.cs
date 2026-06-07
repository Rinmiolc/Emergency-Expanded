using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class Hediff_Shock : Hediff_PercentageDisplay
    {
        public override void Tick()
        {
            // Do nothing to prevent double-updating.
            // Updates are driven by HypoxiaMonitor.RunCrisisMonitor.
        }

        public void UpdateShockSeverity(float pumping)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh || pawn.IsShambler) return;

            float totalPressure = 0f;

            // 1. 低血容量压力 - 失血
            Hediff bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                totalPressure += bloodLoss.Severity * EE_Constants.ShockPressureFromBloodLoss;
            }

            // 2. 低血容量压力 - 烧伤体液流失
            float burnPressure = 0f;
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == EE_DefOf.Burn)
                {
                    HediffComp_Burn burnComp = hediff.TryGetComp<HediffComp_Burn>();
                    if (burnComp != null)
                    {
                        if (burnComp.BurnDegree == 3) burnPressure += hediff.Severity * EE_Constants.ShockPressureFromBurnDegree3;
                        else if (burnComp.BurnDegree == 2) burnPressure += hediff.Severity * EE_Constants.ShockPressureFromBurnDegree2;
                    }
                }
            }
            totalPressure += burnPressure;

            // 3. 分布性压力 - SIRS / 败血症
            Hediff sirs = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.SIRS);
            if (sirs != null)
            {
                totalPressure += sirs.Severity * EE_Constants.ShockPressureFromSIRS;
            }

            // 4. 梗阻性压力 - 气胸
            Hediff pneumothorax = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_Pneumothorax);
            if (pneumothorax != null)
            {
                totalPressure += pneumothorax.Severity * EE_Constants.ShockPressureFromPneumothorax;
            }

            // 5. 心源性休克 (一旦心脏骤停，循环立刻崩溃，休克压力激增)
            Hediff heartAttack = pawn.health.hediffSet.hediffs.Find(h => h.def == EE_DefOf.HeartAttack);
            Hediff vf = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
            if (heartAttack != null || vf != null)
            {
                totalPressure += 5.0f; // 绝对致死压力
            }

            // TODO: 未来可根据是否有“肾上腺素注射”或“生理盐水输注”等状态，削减 totalPressure

            // 核心演进逻辑：
            // 当身体承受的压力突破基础阈值 (0.3f) 时，休克开始加深
            if (totalPressure > 0.3f) 
            {
                // 每 60 刻度增加量
                float severityIncrease = (totalPressure - 0.3f) * EE_Constants.ShockSeverityIncreasePerDay / 1000f;

                // 吗啡镇静减缓休克恶化速度 (减缓 40%)
                if (EE_DefOf.EE_MorphineActive != null && pawn.health.hediffSet.HasHediff(EE_DefOf.EE_MorphineActive))
                {
                    severityIncrease *= EE_Constants.MorphineShockSirsSpeedMultiplier;
                }

                this.Severity += severityIncrease;
            }
            else
            {
                // 压力解除时，使用重构常量消退
                this.Severity -= EE_Constants.ShockRecoveryRateNormal / 1000f;
            }
        }
    }

    public class HediffCompProperties_ShockTrigger : HediffCompProperties
    {
        public HediffCompProperties_ShockTrigger()
        {
            this.compClass = typeof(HediffComp_ShockTrigger);
        }
    }

    public class HediffComp_ShockTrigger : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            // Do nothing to prevent duplicate shock triggering.
            // All physiological monitors are now run sequentially by RunCrisisMonitor.
        }
    }
}
