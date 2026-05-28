using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyExpanded
{
    public static class EE_JobQueueUtility
    {
        public static void SortFirstAidJobQueue(Pawn medic)
        {
            if (medic == null || medic.jobs == null || medic.jobs.jobQueue == null || medic.jobs.jobQueue.Count <= 1)
            {
                return;
            }

            List<QueuedJob> allJobs = new List<QueuedJob>();
            while (medic.jobs.jobQueue.Count > 0)
            {
                allJobs.Add(medic.jobs.jobQueue.Dequeue());
            }

            List<QueuedJob> firstAidJobs = new List<QueuedJob>();
            List<QueuedJob> otherJobs = new List<QueuedJob>();

            foreach (var qj in allJobs)
            {
                if (qj.job.def == EE_DefOf.EE_ApplyFirstAid)
                {
                    firstAidJobs.Add(qj);
                }
                else
                {
                    otherJobs.Add(qj);
                }
            }

            if (firstAidJobs.Count > 1)
            {
                firstAidJobs.Sort((a, b) =>
                {
                    Pawn patientA = a.job.GetTarget(TargetIndex.A).Thing as Pawn;
                    Pawn patientB = b.job.GetTarget(TargetIndex.A).Thing as Pawn;

                    if (patientA == null && patientB == null) return 0;
                    if (patientA == null) return 1;
                    if (patientB == null) return -1;

                    Thing medA = a.job.GetTarget(TargetIndex.B).Thing;
                    Thing medB = b.job.GetTarget(TargetIndex.B).Thing;

                    EmergencyItemType typeA = medA != null ? EE_FirstAidUtility.GetEmergencyItemType(medA.def) : EmergencyItemType.None;
                    EmergencyItemType typeB = medB != null ? EE_FirstAidUtility.GetEmergencyItemType(medB.def) : EmergencyItemType.None;

                    float scoreA = CalculatePriorityScore(patientA, typeA);
                    float scoreB = CalculatePriorityScore(patientB, typeB);

                    return scoreB.CompareTo(scoreA); // Higher score first
                });
            }

            foreach (var qj in firstAidJobs)
            {
                medic.jobs.jobQueue.EnqueueLast(qj.job, qj.tag);
            }

            foreach (var qj in otherJobs)
            {
                medic.jobs.jobQueue.EnqueueLast(qj.job, qj.tag);
            }
        }

        private static float CalculatePriorityScore(Pawn patient, EmergencyItemType medType)
        {
            float score = 0f;

            bool hasMassiveBleeding = false;
            if (EE_DefOf.MassiveBleeding != null)
            {
                hasMassiveBleeding = patient.health.hediffSet.HasHediff(EE_DefOf.MassiveBleeding);
            }

            float bleedRate = patient.health.hediffSet.BleedRateTotal;
            
            // 考虑所有可以导致失血性休克或死亡的模组 Hediff
            float bloodLossSeverity = 0f;
            Hediff bloodLoss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null) bloodLossSeverity = bloodLoss.Severity;

            if (medType == EmergencyItemType.Tourniquet && bleedRate > 0.5f)
            {
                score += 1000f; // Highest priority for tourniquet on heavy bleeders
            }

            if (hasMassiveBleeding)
            {
                score += 500f; // Massive bleeding is extremely critical
            }

            // 加上失血程度（兼容所有模组导致失血的因素）
            score += bloodLossSeverity * 300f;

            // 提高流血速度的权重，因为 BleedRateTotal 兼容任何模组添加的新出血状态
            score += bleedRate * 50f; 

            // 检查是否有极其危险的头部伤口或躯干伤口（通用逻辑）
            BodyPartRecord brain = patient.health.hediffSet.GetBrain();
            if (brain != null)
            {
                float brainHealth = patient.health.hediffSet.GetPartHealth(brain);
                if (brainHealth <= 5f)
                {
                    score += 300f; // 脑部重创
                }
            }

            // 检查任意高致命性状态 (兼容其他毒素/感染/特殊疾病 Mod)
            foreach (var hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.def.lethalSeverity > 0f)
                {
                    float remaining = hediff.def.lethalSeverity - hediff.Severity;
                    if (remaining > 0f && remaining < 0.2f)
                    {
                        score += 400f; // 高度致命，濒死状态
                    }
                }
            }

            if (medType == EmergencyItemType.Splint)
            {
                score -= 100f; // Splints are lower priority than bleeding control
            }
            else if (medType == EmergencyItemType.IngestibleDirect)
            {
                score -= 200f; // Feeding is usually lowest priority
            }

            return score;
        }
    }
}
