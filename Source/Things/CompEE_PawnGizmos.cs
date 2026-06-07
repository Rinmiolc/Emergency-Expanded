using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EmergencyExpanded
{
    public class CompProperties_EE_PawnGizmos : CompProperties
    {
        public CompProperties_EE_PawnGizmos()
        {
            this.compClass = typeof(CompEE_PawnGizmos);
        }
    }

    public class CompEE_PawnGizmos : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Pawn pawn = this.parent as Pawn;
            if (pawn == null || pawn.Dead) yield break;

            bool addFirstAid = false;
            bool addDeclareDeath = false;
            bool addVitalMonitor = false;

            // 1. 快速急救按钮判定
            if (pawn.Faction != null && pawn.Faction.IsPlayer && !pawn.Downed)
            {
                if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
                    pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                {
                    List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(pawn);
                    if (availableItems.Count > 0)
                    {
                        addFirstAid = true;
                    }
                }
            }

            // 2. 宣布死亡按钮判定
            if (pawn.RaceProps.IsFlesh)
            {
                if (pawn.health.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer))
                {
                    addDeclareDeath = true;
                }
            }

            // 3. 心电监测仪 UI 判定 (只在单选时显示)
            if (pawn.RaceProps.IsFlesh && 
                !pawn.IsShambler && 
                !pawn.RaceProps.IsMechanoid && 
                EE_Settings.EnableEcgGui)
            {
                bool isHealthTabOpen = (UnityEngine.Time.frameCount - Patch_HealthCardUtility_UI.lastHealthTabDrawFrame) <= 2;
                if (Find.Selector.SingleSelectedThing == pawn && !isHealthTabOpen)
                {
                    addVitalMonitor = true;
                }
            }

            if (addFirstAid)
            {
                yield return new Command_FastFirstAid(pawn);
            }
            if (addDeclareDeath)
            {
                yield return new Command_DeclareDeath(pawn);
            }
            if (addVitalMonitor)
            {
                yield return new Gizmo_VitalMonitor(pawn);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = this.parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.RaceProps.IsFlesh || pawn.IsShambler) return;

            // 每 60 ticks 执行一次监测
            if (pawn.IsHashIntervalTick(60))
            {
                // 1. 运行系统性危机监测 (脑缺氧、酸中毒、SIRS、凝血障碍、器官衰竭、末梢坏死与心率状态更新)
                Patch_Pawn_HealthTracker_SystemicCrisisMonitor_Helper.RunCrisisMonitor(pawn.health, pawn);

                // 2. 运行生物学死亡倒计时检测
                if (!pawn.health.hediffSet.HasHediff(EE_DefOf.EE_BiologicalDeathTimer))
                {
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
