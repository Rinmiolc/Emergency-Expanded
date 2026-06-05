using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EmergencyExpanded
{
    public class Command_FastFirstAid : Command
    {
        public Pawn medic;
        public static Dictionary<Pawn, ThingDef> PreferredMedicine = new Dictionary<Pawn, ThingDef>();

        public Command_FastFirstAid(Pawn medic)
        {
            this.medic = medic;
            this.defaultLabel = "快速急救";
            this.defaultDesc = "框选多名伤员，该医生将依次前往进行急救。\n\n右键点击可切换使用的急救物品。";
            
            ThingDef selectedMed = GetSelectedMedicine();
            if (selectedMed != null)
            {
                this.icon = selectedMed.uiIcon;
                this.iconAngle = selectedMed.uiIconAngle;
                this.iconOffset = selectedMed.uiIconOffset;
            }
            else
            {
                this.icon = TexCommand.Draft; // Fallback
            }
        }

        private ThingDef GetSelectedMedicine()
        {
            List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(medic);
            if (availableItems.Count == 0) return null;

            if (PreferredMedicine.TryGetValue(medic, out ThingDef pref) && pref != null)
            {
                if (availableItems.Any(t => t.def == pref))
                {
                    return pref;
                }
            }

            // Default to the first one available
            ThingDef defaultDef = availableItems[0].def;
            PreferredMedicine[medic] = defaultDef;
            return defaultDef;
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            ThingDef selectedMed = GetSelectedMedicine();
            if (selectedMed != null)
            {
                Find.DesignatorManager.Select(new Designator_FastFirstAid(medic, selectedMed));
            }
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                List<Thing> availableItems = EE_FirstAidUtility.GetUsableItemsInInventory(medic);
                var groupedItems = availableItems.GroupBy(t => t.def);

                foreach (var group in groupedItems)
                {
                    ThingDef itemDef = group.Key;
                    int totalCount = group.Sum(t => t.stackCount);
                    Thing firstThing = group.First();

                    string label = $"{itemDef.LabelCap} (剩余: {totalCount})";
                    Action action = () =>
                    {
                        PreferredMedicine[medic] = itemDef;
                        // Refresh gizmos to update icon
                    };

                    FloatMenuOption option = new FloatMenuOption(label, action, MenuOptionPriority.Default, null, null);
                    option.iconThing = firstThing;
                    yield return option;
                }
            }
        }
    }

    public class Designator_FastFirstAid : Designator
    {
        private Pawn medic;
        private ThingDef selectedMedicineDef;
        private EmergencyItemType medType;

        public override bool DragDrawMeasurements => false;

        public override DrawStyleCategoryDef DrawStyleCategory => RimWorld.DrawStyleCategoryDefOf.FilledRectangle;

        public Designator_FastFirstAid(Pawn medic, ThingDef selectedMedicineDef)
        {
            this.medic = medic;
            this.selectedMedicineDef = selectedMedicineDef;
            this.medType = EE_FirstAidUtility.GetEmergencyItemType(selectedMedicineDef);

            this.defaultLabel = "快速急救";
            this.defaultDesc = $"框选需要急救的伤员。\n当前使用物品: {selectedMedicineDef.LabelCap}";
            this.icon = selectedMedicineDef.uiIcon;
            this.iconAngle = selectedMedicineDef.uiIconAngle;
            this.iconOffset = selectedMedicineDef.uiIconOffset;
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            this.useMouseIcon = true;
            this.soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Map)) return false;
            foreach (Thing t in loc.GetThingList(Map))
            {
                if (CanDesignateThing(t).Accepted) return true;
            }
            return false;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            foreach (Thing t in c.GetThingList(Map))
            {
                if (CanDesignateThing(t).Accepted)
                {
                    DesignateThing(t);
                }
            }
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            foreach (IntVec3 c in cells)
            {
                DesignateSingleCell(c);
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            Pawn patient = t as Pawn;
            if (patient == null || patient.Dead) return false;
            if (!patient.RaceProps.IsFlesh || patient.RaceProps.BloodDef == null) return false;

            // Optional: faction check. Usually we can treat anyone, but typically we treat colonists/prisoners/guests/neutrals
            // Actually, manual tend allows tending anyone. Let's just check reachability.
            if (!medic.CanReach(patient, PathEndMode.Touch, Danger.Deadly)) return false;

            if (!EE_FirstAidUtility.CanApplyToTarget(patient, medType, selectedMedicineDef))
            {
                return false;
            }

            // Optional: don't designate if they already have this designation?
            // Actually it's fine to queue multiple if they have multiple wounds!
            
            return true;
        }

        public override void DesignateThing(Thing t)
        {
            Pawn patient = t as Pawn;
            if (patient == null) return;

            // 1. Add Designation (if not already there)
            if (EE_DefOf.EE_FastFirstAid != null && Map.designationManager.DesignationOn(t, EE_DefOf.EE_FastFirstAid) == null)
            {
                Map.designationManager.AddDesignation(new Designation(t, EE_DefOf.EE_FastFirstAid));
            }

            // 2. Find medicine thing in inventory
            Thing firstThing = null;
            foreach (Thing item in medic.inventory.innerContainer)
            {
                if (item.def == selectedMedicineDef)
                {
                    firstThing = item;
                    break;
                }
            }
            
            if (firstThing == null)
            {
                // Medic might be carrying it in hands
                Thing carried = medic.carryTracker?.CarriedThing;
                if (carried != null && carried.def == selectedMedicineDef)
                {
                    firstThing = carried;
                }
            }

            if (firstThing == null)
            {
                Messages.Message($"{medic.LabelShort} 找不到 {selectedMedicineDef.LabelCap}!", medic, MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 3. Queue the job
            if (EE_DefOf.EE_ApplyFirstAid != null)
            {
                // Undraft the medic automatically FIRST to prevent JobQueue clearing
                if (medic.Drafted)
                {
                    medic.drafter.Drafted = false;
                }
                
                Job job = JobMaker.MakeJob(EE_DefOf.EE_ApplyFirstAid, patient, firstThing);
                job.count = 1;
                
                if (medic.CurJob != null && medic.CurJob.def == EE_DefOf.EE_ApplyFirstAid)
                {
                    medic.jobs.jobQueue.EnqueueLast(job);
                }
                else
                {
                    medic.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }

            // 4. Sort the queue to ensure highest priority patients are treated first
            EE_JobQueueUtility.SortFirstAidJobQueue(medic);
        }
    }
}
