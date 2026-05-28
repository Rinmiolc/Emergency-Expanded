using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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
}
