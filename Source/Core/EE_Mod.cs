using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace EmergencyExpanded
{
    public class EE_ModSettings : ModSettings
    {
        public DifficultyPreset difficulty = DifficultyPreset.Hardcore;
        public bool debugMode = false;
        public bool enableEcgGui = true;
        public bool enableDynamicMassiveBleeding = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref difficulty, "difficulty", DifficultyPreset.Hardcore);
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            Scribe_Values.Look(ref enableEcgGui, "enableEcgGui", true);
            Scribe_Values.Look(ref enableDynamicMassiveBleeding, "enableDynamicMassiveBleeding", true);
        }
    }

    public class EE_Mod : Mod
    {
        public static EE_ModSettings Settings;

        public EE_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<EE_ModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("EE_Settings_DifficultyPresetGlobal".Translate());
            Text.Font = GameFont.Small;
            
            if (listing.RadioButton("EE_Settings_DifficultyPresetEasy".Translate(), Settings.difficulty == DifficultyPreset.Easy))
                Settings.difficulty = DifficultyPreset.Easy;
            if (listing.RadioButton("EE_Settings_DifficultyPresetHardcore".Translate(), Settings.difficulty == DifficultyPreset.Hardcore))
                Settings.difficulty = DifficultyPreset.Hardcore;
            if (listing.RadioButton("EE_Settings_DifficultyPresetRealistic".Translate(), Settings.difficulty == DifficultyPreset.Realistic))
                Settings.difficulty = DifficultyPreset.Realistic;
            
            listing.Gap(20f);
            
            Text.Font = GameFont.Medium;
            listing.Label("EE_Settings_AdditionalOptions".Translate());
            Text.Font = GameFont.Small;

            listing.CheckboxLabeled("EE_Settings_EnableEcgGui".Translate(), ref Settings.enableEcgGui, "EE_Settings_EnableEcgGuiDesc".Translate());
            listing.CheckboxLabeled("EE_Settings_EnableDynamicMassiveBleeding".Translate(), ref Settings.enableDynamicMassiveBleeding, "EE_Settings_EnableDynamicMassiveBleedingDesc".Translate());
            listing.CheckboxLabeled("EE_Settings_EnableDebugMode".Translate(), ref Settings.debugMode, "EE_Settings_EnableDebugModeDesc".Translate());

            listing.End();
        }

        public override string SettingsCategory() => "Emergency Expanded";
    }
}
