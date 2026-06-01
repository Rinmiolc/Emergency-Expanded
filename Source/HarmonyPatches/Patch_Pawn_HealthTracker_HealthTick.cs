using HarmonyLib;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick")]
    public static class Patch_Pawn_HealthTracker_HealthTick
    {
        public static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn)
        {
            Pawn pawn = ___pawn;
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh) return;

            if (pawn.IsHashIntervalTick(60))
            {
                // 如果还没有倒计时状态，我们检测是否满足条件
                if (!pawn.health.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer))
                {
                    // 临时生成一个组件来使用 CheckConditionA() 检查逻辑，或者独立提取出来
                    if (CheckConditionA(pawn))
                    {
                        Hediff timer = HediffMaker.MakeHediff(EE_DefOf.EE_BiologicalDeathTimer, pawn, null);
                        pawn.health.AddHediff(timer, null, null, null);
                    }
                }
            }
        }

        public static bool CheckConditionA(Pawn pawn)
        {
            // 1. 优先短路：极低成本的能力值检测
            float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
            if (pumping > EE_Constants.VitalFlatlineThreshold) return false;
            
            float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            if (breathing > EE_Constants.VitalFlatlineThreshold) return false;

            // 2. 脑部状态检测
            BodyPartRecord brain = pawn.health.hediffSet.GetBrain();
            if (brain != null && !pawn.health.hediffSet.HasHediff(EE_DefOf.VegetativeState, brain))
            {
                // 有脑子，且没有植物人状态 -> 不满足
                return false;
            }

            // 3. 心脏状态检测
            // 优先检查心肌梗死状态（O(1) 复杂度），如果满级直接返回 true，无需遍历器官
            Hediff vf = pawn.health.hediffSet.GetFirstHediffOfDef(EE_DefOf.EE_MyocardialInfarction);
            if (vf != null && vf.Severity >= 1.0f)
            {
                return true;
            }

            // 4. 如果没找到满级的心肌梗死，遍历检查心脏是否物理缺失
            var pumpingSources = EE_BodyPartCache.GetBloodPumpingSources(pawn);
            if (pumpingSources != null)
            {
                foreach (BodyPartRecord part in pumpingSources)
                {
                    if (pawn.health.hediffSet.PartIsMissing(part))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
