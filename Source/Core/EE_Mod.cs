using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace EmergencyExpanded
{
    public class EE_ModSettings : ModSettings
    {
        public DifficultyPreset difficulty = DifficultyPreset.Hardcore;
        public bool debugMode = false;
        public bool enableHealthUiOverhaul = true;
        public bool enableEcgGui = true;
        public bool enableDynamicMassiveBleeding = true;
        public bool useMlhBleedRateUnit = true;
        public bool showBleedRateBanner = true;
        public bool enableCriticalBlink = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref difficulty, "difficulty", DifficultyPreset.Hardcore);
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            Scribe_Values.Look(ref enableHealthUiOverhaul, "enableHealthUiOverhaul", true);
            Scribe_Values.Look(ref enableEcgGui, "enableEcgGui", true);
            Scribe_Values.Look(ref enableDynamicMassiveBleeding, "enableDynamicMassiveBleeding", true);
            Scribe_Values.Look(ref useMlhBleedRateUnit, "useMlhBleedRateUnit", true);
            Scribe_Values.Look(ref showBleedRateBanner, "showBleedRateBanner", true);
            Scribe_Values.Look(ref enableCriticalBlink, "enableCriticalBlink", true);
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
            // Divide the area into two columns for structured look
            float gap = 24f;
            float columnWidth = (inRect.width - gap) / 2f;
            Rect leftRect = new Rect(inRect.x, inRect.y, columnWidth, inRect.height - 40f);
            Rect rightRect = new Rect(inRect.x + columnWidth + gap, inRect.y, columnWidth, inRect.height - 40f);

            // Left column: Gameplay & Difficulty Settings
            // Draw section background
            Widgets.DrawBoxSolid(leftRect, new Color(0.12f, 0.12f, 0.13f, 0.5f));
            Widgets.DrawBox(leftRect, 1);

            // Inner content margin
            Rect leftInner = leftRect.ContractedBy(16f);
            Listing_Standard listingLeft = new Listing_Standard();
            listingLeft.Begin(leftInner);

            Text.Font = GameFont.Medium;
            listingLeft.Label("EE_Settings_Category_Gameplay".Translate());
            listingLeft.Gap(10f);

            // Difficulty section header
            Text.Font = GameFont.Small;
            listingLeft.Label("EE_Settings_DifficultyPresetGlobal".Translate(), -1f, "EE_Settings_DifficultyPresetGlobal_Desc".Translate());
            listingLeft.Gap(6f);

            if (listingLeft.RadioButton("EE_Settings_DifficultyPresetEasy_Title".Translate(), Settings.difficulty == DifficultyPreset.Easy))
                Settings.difficulty = DifficultyPreset.Easy;
            DrawDifficultyDescription(listingLeft, "EE_Settings_DifficultyPresetEasy_Desc".Translate());
            listingLeft.Gap(8f);

            if (listingLeft.RadioButton("EE_Settings_DifficultyPresetHardcore_Title".Translate(), Settings.difficulty == DifficultyPreset.Hardcore))
                Settings.difficulty = DifficultyPreset.Hardcore;
            DrawDifficultyDescription(listingLeft, "EE_Settings_DifficultyPresetHardcore_Desc".Translate());
            listingLeft.Gap(8f);

            if (listingLeft.RadioButton("EE_Settings_DifficultyPresetRealistic_Title".Translate(), Settings.difficulty == DifficultyPreset.Realistic))
                Settings.difficulty = DifficultyPreset.Realistic;
            DrawDifficultyDescription(listingLeft, "EE_Settings_DifficultyPresetRealistic_Desc".Translate());
            listingLeft.Gap(12f);

            // Divider Line
            listingLeft.Gap(4f);
            Rect dividerLeft = listingLeft.GetRect(2f);
            Widgets.DrawBoxSolid(dividerLeft, new Color(0.3f, 0.3f, 0.3f, 0.3f));
            listingLeft.Gap(10f);

            // Gameplay Mechanics Section
            listingLeft.Label("EE_Settings_GameplayMechanics".Translate());
            listingLeft.Gap(6f);
            listingLeft.CheckboxLabeled("EE_Settings_EnableDynamicMassiveBleeding".Translate(), ref Settings.enableDynamicMassiveBleeding, "EE_Settings_EnableDynamicMassiveBleedingDesc".Translate());
            listingLeft.CheckboxLabeled("EE_Settings_EnableDebugMode".Translate(), ref Settings.debugMode, "EE_Settings_EnableDebugModeDesc".Translate());

            listingLeft.End();

            // Right column: Health Panel Settings
            // Draw section background
            Widgets.DrawBoxSolid(rightRect, new Color(0.12f, 0.12f, 0.13f, 0.5f));
            Widgets.DrawBox(rightRect, 1);

            // Inner content margin
            Rect rightInner = rightRect.ContractedBy(16f);
            Listing_Standard listingRight = new Listing_Standard();
            listingRight.Begin(rightInner);

            Text.Font = GameFont.Medium;
            listingRight.Label("EE_Settings_Category_HealthUI".Translate());
            Text.Font = GameFont.Small;
            listingRight.Gap(10f);

            listingRight.CheckboxLabeled("EE_Settings_EnableHealthUiOverhaul".Translate(), ref Settings.enableHealthUiOverhaul, "EE_Settings_EnableHealthUiOverhaulDesc".Translate());
            listingRight.CheckboxLabeled("EE_Settings_EnableEcgGui".Translate(), ref Settings.enableEcgGui, "EE_Settings_EnableEcgGuiDesc".Translate());
            listingRight.CheckboxLabeled("EE_Settings_EnableCriticalBlink".Translate(), ref Settings.enableCriticalBlink, "EE_Settings_EnableCriticalBlinkDesc".Translate());

            // Divider Line
            listingRight.Gap(8f);
            Rect dividerRight = listingRight.GetRect(2f);
            Widgets.DrawBoxSolid(dividerRight, new Color(0.3f, 0.3f, 0.3f, 0.3f));
            listingRight.Gap(12f);

            // Unit & Display Section
            listingRight.CheckboxLabeled("EE_Settings_UseMlhBleedRateUnit".Translate(), ref Settings.useMlhBleedRateUnit, "EE_Settings_UseMlhBleedRateUnitDesc".Translate());
            listingRight.CheckboxLabeled("EE_Settings_ShowBleedRateBanner".Translate(), ref Settings.showBleedRateBanner, "EE_Settings_ShowBleedRateBannerDesc".Translate());

            listingRight.End();
        }

        private void DrawDifficultyDescription(Listing_Standard listing, string description)
        {
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.65f, 0.65f, 0.65f);

            // Indent under the radio button circle
            listing.Label("   " + description);

            GUI.color = oldColor;
            Text.Font = oldFont;
        }

        public override string SettingsCategory() => "Emergency Expanded";
    }
}
