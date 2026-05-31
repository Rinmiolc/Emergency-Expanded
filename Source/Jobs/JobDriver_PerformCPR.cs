using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace EmergencyExpanded
{
    public class JobDriver_PerformCPR : JobDriver
    {
        private const TargetIndex PatientIndex = TargetIndex.A;

        private Pawn Patient => (Pawn)job.GetTarget(PatientIndex).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 心肺复苏需要医生占用患者来进行高强度施救
            return pawn.Reserve(Patient, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 确保患者没有被移除、且未死亡
            this.FailOnDespawnedOrNull(PatientIndex);
            this.FailOn(() => Patient.Dead);
            // 只有患者处于心梗/心室颤动状态时，才可以继续实施 CPR
            this.FailOn(() => EE_DefOf.EE_MyocardialInfarction == null || !Patient.health.hediffSet.HasHediff(EE_DefOf.EE_MyocardialInfarction));

            // 走近患者
            yield return Toils_Goto.GotoThing(PatientIndex, PathEndMode.Touch);

            // CPR 核心 Toil，是一个长期的、由玩家手动或自动取消的引导动作
            Toil cprToil = ToilMaker.MakeToil("PerformCPR");
            cprToil.defaultCompleteMode = ToilCompleteMode.Never;

            cprToil.initAction = () =>
            {
                // 初始化阶段
            };

            // 播放经典的医用麻醉/操作音效作为背景音
            cprToil.PlaySustainerOrSound(SoundDef.Named("Recipe_Anesthetize"));

            // 循环的进度条，展示心肺复苏 Rhythmic（有节奏的）按压动态
            cprToil.WithProgressBar(PatientIndex, () =>
            {
                return (GenTicks.TicksGame % 120) / 120f;
            });

            cprToil.tickAction = () =>
            {
                // 医生强制面向患者
                pawn.rotationTracker.FaceCell(Patient.Position);

                // 防御性处理：防止患者在非倒地或未躺下的情况下乱动，施加定身
                if (Patient.Spawned && !Patient.Downed && Patient.CurrentBed() == null)
                {
                    if (Patient.stances != null && Patient.stances.stunner != null && Patient.stances.stunner.StunTicksLeft < 10)
                    {
                        Patient.pather?.StopDead();
                        Patient.stances.stunner.StunFor(60, pawn, false, false);
                    }
                    Patient.rotationTracker?.FaceCell(pawn.Position);
                }

                // 每一 Tick 刷新患者身上的“接受 CPR 中”状态，设定 120 ticks 的自动消退时长
                RefreshCprHediff(Patient);

                // 医生获得医疗经验（每 60 刻度获得 8 点经验）
                if (pawn.skills != null && pawn.IsHashIntervalTick(60))
                {
                    pawn.skills.Learn(SkillDefOf.Medicine, 8f);
                }
            };

            yield return cprToil;
        }

        private void RefreshCprHediff(Pawn patient)
        {
            if (patient == null || patient.Dead || EE_DefOf.EE_CPR_Receiving == null) return;

            Hediff existing = patient.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_CPR_Receiving);
            if (existing != null)
            {
                // 如果已存在状态，重置其消退时间
                HediffComp_Disappears comp = existing.TryGetComp<HediffComp_Disappears>();
                if (comp != null)
                {
                    comp.ticksToDisappear = 120;
                }
            }
            else
            {
                // 不存在则创建
                Hediff cpr = HediffMaker.MakeHediff(EE_DefOf.EE_CPR_Receiving, patient);
                patient.health.AddHediff(cpr);
            }
        }
    }
}
