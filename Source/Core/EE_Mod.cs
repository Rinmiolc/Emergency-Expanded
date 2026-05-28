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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref difficulty, "difficulty", DifficultyPreset.Hardcore);
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            Scribe_Values.Look(ref enableEcgGui, "enableEcgGui", true);
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
            listing.Label("全局难度预设 (Difficulty Preset)");
            Text.Font = GameFont.Small;
            
            if (listing.RadioButton("简单 (Easy) - 更少的大出血与骨折，更充裕的抢救时间", Settings.difficulty == DifficultyPreset.Easy))
                Settings.difficulty = DifficultyPreset.Easy;
            if (listing.RadioButton("硬核 (Hardcore) [默认] - 平衡且具有挑战性的体验", Settings.difficulty == DifficultyPreset.Hardcore))
                Settings.difficulty = DifficultyPreset.Hardcore;
            if (listing.RadioButton("拟真 (Realistic) - 极高的大出血与骨折率，濒死极快，适合受虐狂", Settings.difficulty == DifficultyPreset.Realistic))
                Settings.difficulty = DifficultyPreset.Realistic;
            
            listing.Gap(20f);
            
            Text.Font = GameFont.Medium;
            listing.Label("附加选项 (Additional Options)");
            Text.Font = GameFont.Small;

            listing.CheckboxLabeled("显示心电图监测仪 (ECG GUI)", ref Settings.enableEcgGui, "选中目标时在底部显示实时动态心电图与血氧饱和度。");
            listing.CheckboxLabeled("开启调试模式 (Debug Mode)", ref Settings.debugMode, "测试专用：将大出血、骨折等主要危机事件发生率强制提升至 90%。");

            listing.End();
        }

        public override string SettingsCategory() => "Emergency Expanded";
    }
}
