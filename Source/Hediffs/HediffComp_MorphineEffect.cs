using Verse;
using RimWorld;

namespace EmergencyExpanded
{
    public class HediffCompProperties_MorphineEffect : HediffCompProperties
    {
        public HediffCompProperties_MorphineEffect()
        {
            this.compClass = typeof(HediffComp_MorphineEffect);
        }
    }

    public class HediffComp_MorphineEffect : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead) return;
            if (!Pawn.IsHashIntervalTick(250)) return;

            float severity = parent.Severity;

            // ================= 吗啡过量致死阶段：判定呼吸骤停 =================
            if (severity >= EE_Constants.MorphineFatalThreshold)
            {
                if (Rand.Chance(EE_Constants.MorphineRespiratoryArrestChancePerRareTick))
                {
                    HediffDef arrestDef = EE_DefOf.EE_MorphineRespiratoryArrest;
                    if (arrestDef != null && !Pawn.health.hediffSet.HasHediff(arrestDef))
                    {
                        Hediff arrest = HediffMaker.MakeHediff(arrestDef, Pawn);
                        // 严重度设为 1.0
                        arrest.Severity = 1.0f;
                        Pawn.health.AddHediff(arrest, null, null, null);

                        if (Pawn.Spawned && Pawn.Map != null)
                        {
                            MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "吗啡中毒 - 呼吸骤停!", UnityEngine.Color.red);
                        }
                    }
                }
            }
        }
    }
}
