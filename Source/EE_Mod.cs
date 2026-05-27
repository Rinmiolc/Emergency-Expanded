using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace EmergencyExpanded
{
    public class EE_ModSettings : ModSettings
    {
        public float initialHediffSeverity = 0.05f;
        public float hypoxiaMonitorThreshold = 0.55f;
        public float vitalCriticalThreshold = 0.2f;
        public float vitalCriticalMultiplier = 2.0f;
        public float comaSeverityThreshold = 0.5f;
        public float comaSeverityFactor = 0.35f;
        public float vegStateThreshold = 1.0f;
        public float brainDamageStartThreshold = 0.6f;
        public float brainDamageBaseChance = 0.02f;
        public float brainDamageCriticalThreshold = 0.85f;
        public float brainDamageCriticalMultiplier = 2.5f;
        public float brainDamageSeverityIncrement = 0.15f;
        public float acidosisSilentHypoxiaStart = 0.4f;
        public float acidosisMidThreshold = 0.6f;
        public float acidosisHighThreshold = 0.85f;
        public float acidosisChanceLow = 0.02f;
        public float acidosisChanceMid = 0.08f;
        public float acidosisChanceHigh = 0.25f;
        public float acidosisCoreAttackChance = 0.3f;
        public float acidosisCoreDamageMultiplier = 2.0f;
        public float minBleedMultiplier = 0.1f;
        public float massiveBleedingChanceTorso = 0.90f;
        public float massiveBleedingChanceLimb = 0.90f;
        public float fractureChanceMultiplier = 1.0f;
        public float secondaryDamageChance = 0.08f;
        public bool advancedMode = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialHediffSeverity, "initialHediffSeverity", 0.05f);
            Scribe_Values.Look(ref hypoxiaMonitorThreshold, "hypoxiaMonitorThreshold", 0.55f);
            Scribe_Values.Look(ref vitalCriticalThreshold, "vitalCriticalThreshold", 0.2f);
            Scribe_Values.Look(ref vitalCriticalMultiplier, "vitalCriticalMultiplier", 2.0f);
            Scribe_Values.Look(ref comaSeverityThreshold, "comaSeverityThreshold", 0.5f);
            Scribe_Values.Look(ref comaSeverityFactor, "comaSeverityFactor", 0.35f);
            Scribe_Values.Look(ref vegStateThreshold, "vegStateThreshold", 1.0f);
            Scribe_Values.Look(ref brainDamageStartThreshold, "brainDamageStartThreshold", 0.6f);
            Scribe_Values.Look(ref brainDamageBaseChance, "brainDamageBaseChance", 0.02f);
            Scribe_Values.Look(ref brainDamageCriticalThreshold, "brainDamageCriticalThreshold", 0.85f);
            Scribe_Values.Look(ref brainDamageCriticalMultiplier, "brainDamageCriticalMultiplier", 2.5f);
            Scribe_Values.Look(ref brainDamageSeverityIncrement, "brainDamageSeverityIncrement", 0.15f);
            Scribe_Values.Look(ref acidosisSilentHypoxiaStart, "acidosisSilentHypoxiaStart", 0.4f);
            Scribe_Values.Look(ref acidosisMidThreshold, "acidosisMidThreshold", 0.6f);
            Scribe_Values.Look(ref acidosisHighThreshold, "acidosisHighThreshold", 0.85f);
            Scribe_Values.Look(ref acidosisChanceLow, "acidosisChanceLow", 0.02f);
            Scribe_Values.Look(ref acidosisChanceMid, "acidosisChanceMid", 0.08f);
            Scribe_Values.Look(ref acidosisChanceHigh, "acidosisChanceHigh", 0.25f);
            Scribe_Values.Look(ref acidosisCoreAttackChance, "acidosisCoreAttackChance", 0.3f);
            Scribe_Values.Look(ref acidosisCoreDamageMultiplier, "acidosisCoreDamageMultiplier", 2.0f);
            Scribe_Values.Look(ref minBleedMultiplier, "minBleedMultiplier", 0.1f);
            Scribe_Values.Look(ref massiveBleedingChanceTorso, "massiveBleedingChanceTorso", 0.90f);
            Scribe_Values.Look(ref massiveBleedingChanceLimb, "massiveBleedingChanceLimb", 0.90f);
            Scribe_Values.Look(ref fractureChanceMultiplier, "fractureChanceMultiplier", 1.0f);
            Scribe_Values.Look(ref secondaryDamageChance, "secondaryDamageChance", 0.08f);
            Scribe_Values.Look(ref advancedMode, "advancedMode", false);
        }
    }

    public class EE_Mod : Mod
    {
        public static EE_ModSettings Settings;
        private enum Tab { BleedingTrauma, HypoxiaAcidosis, VitalsCriticalCare }
        private Tab currentTab = Tab.BleedingTrauma;
        private Vector2 scrollPosition;

        public EE_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<EE_ModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect tabRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);
            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("🩸 流血与外伤", () => { currentTab = Tab.BleedingTrauma; }, currentTab == Tab.BleedingTrauma),
                new TabRecord("🧠 缺氧与并发症", () => { currentTab = Tab.HypoxiaAcidosis; }, currentTab == Tab.HypoxiaAcidosis),
                new TabRecord("🫀 生命体征与抢救", () => { currentTab = Tab.VitalsCriticalCare; }, currentTab == Tab.VitalsCriticalCare)
            };
            TabDrawer.DrawTabs(tabRect, tabs, 200f);

            Rect contentRect = new Rect(tabRect.x, tabRect.y + 10f, tabRect.width, tabRect.height - 10f);
            Listing_Standard listing = new Listing_Standard();
            
            Rect viewRect = new Rect(0, 0, contentRect.width - 20f, 900f);
            Widgets.BeginScrollView(contentRect, ref scrollPosition, viewRect);
            listing.Begin(viewRect);

            listing.CheckboxLabeled("开启高级/核心数值设置 (硬核警告)", ref Settings.advancedMode, "显示所有的底层精细调整参数。如果你不清楚它们的作用，建议保持关闭。");
            listing.GapLine();

            switch (currentTab)
            {
                case Tab.BleedingTrauma:
                    DrawBleedingTrauma(listing);
                    break;
                case Tab.HypoxiaAcidosis:
                    DrawHypoxiaAcidosis(listing);
                    break;
                case Tab.VitalsCriticalCare:
                    DrawVitalsCriticalCare(listing);
                    break;
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawBleedingTrauma(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("大出血 (Massive Bleeding)");
            Text.Font = GameFont.Small;
            listing.Label($"躯干大出血基础几率: {Settings.massiveBleedingChanceTorso.ToStringPercent()}", tooltip: "受击部位为主躯干时的大出血几率。");
            Settings.massiveBleedingChanceTorso = listing.Slider(Settings.massiveBleedingChanceTorso, 0f, 1f);
            listing.Label($"四肢大出血基础几率: {Settings.massiveBleedingChanceLimb.ToStringPercent()}", tooltip: "受击部位为四肢核心时的大出血几率。");
            Settings.massiveBleedingChanceLimb = listing.Slider(Settings.massiveBleedingChanceLimb, 0f, 1f);
            
            listing.Gap(15f);
            Text.Font = GameFont.Medium;
            listing.Label("骨折 (Bone Fracture)");
            Text.Font = GameFont.Small;
            listing.Label($"全局骨折几率乘数: {Settings.fractureChanceMultiplier:F2}x", tooltip: "所有导致骨折的伤害判定的最终倍率。");
            Settings.fractureChanceMultiplier = listing.Slider(Settings.fractureChanceMultiplier, 0f, 3.0f);
            listing.Label($"骨折移动二次伤害几率: {Settings.secondaryDamageChance.ToStringPercent()}/RareTick", tooltip: "骨折且未固定的角色在移动时加重伤势的几率。");
            Settings.secondaryDamageChance = listing.Slider(Settings.secondaryDamageChance, 0.01f, 0.50f);

            if (listing.ButtonText("恢复本页默认设置"))
            {
                Settings.massiveBleedingChanceTorso = 0.90f;
                Settings.massiveBleedingChanceLimb = 0.90f;
                Settings.fractureChanceMultiplier = 1.0f;
                Settings.secondaryDamageChance = 0.08f;
            }
        }

        private void DrawHypoxiaAcidosis(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("脑缺氧 (Cerebral Hypoxia)");
            Text.Font = GameFont.Small;
            listing.Label($"脑死亡判定阈值: {Settings.vegStateThreshold.ToStringPercent()}", tooltip: "脑损伤达到此百分比时直接触发永久植物人状态。");
            Settings.vegStateThreshold = listing.Slider(Settings.vegStateThreshold, 0.5f, 1.0f);
            
            if (Settings.advancedMode)
            {
                listing.Label($"休克加剧因子: {Settings.comaSeverityFactor:F2}x", tooltip: "当进入休克后，缺氧恶化的额外乘数。");
                Settings.comaSeverityFactor = listing.Slider(Settings.comaSeverityFactor, 0.1f, 1.0f);
                listing.Label($"脑损伤起始门槛: {Settings.brainDamageStartThreshold.ToStringPercent()}", tooltip: "脑缺氧达到此严重度后，开始有几率造成不可逆脑损伤。");
                Settings.brainDamageStartThreshold = listing.Slider(Settings.brainDamageStartThreshold, 0.1f, 0.9f);
                listing.Label($"脑损伤基础几率: {Settings.brainDamageBaseChance.ToStringPercent()}/Tick", tooltip: "每次判定时发生实质脑损伤的基础概率。");
                Settings.brainDamageBaseChance = listing.Slider(Settings.brainDamageBaseChance, 0f, 0.1f);
            }

            listing.Gap(15f);
            Text.Font = GameFont.Medium;
            listing.Label("代谢性酸中毒 (Metabolic Acidosis)");
            Text.Font = GameFont.Small;
            if (Settings.advancedMode)
            {
                listing.Label($"器官坏死开始阈值: {Settings.acidosisSilentHypoxiaStart.ToStringPercent()}");
                Settings.acidosisSilentHypoxiaStart = listing.Slider(Settings.acidosisSilentHypoxiaStart, 0.2f, 0.8f);
                listing.Label($"轻度发生率: {Settings.acidosisChanceLow.ToStringPercent()}");
                Settings.acidosisChanceLow = listing.Slider(Settings.acidosisChanceLow, 0.01f, 0.1f);
                listing.Label($"中度发生率: {Settings.acidosisChanceMid.ToStringPercent()}");
                Settings.acidosisChanceMid = listing.Slider(Settings.acidosisChanceMid, 0.05f, 0.2f);
                listing.Label($"重度发生率: {Settings.acidosisChanceHigh.ToStringPercent()}");
                Settings.acidosisChanceHigh = listing.Slider(Settings.acidosisChanceHigh, 0.1f, 0.5f);
                listing.Label($"核心器官受损率: {Settings.acidosisCoreAttackChance.ToStringPercent()}");
                Settings.acidosisCoreAttackChance = listing.Slider(Settings.acidosisCoreAttackChance, 0.1f, 0.8f);
            }

            if (listing.ButtonText("恢复本页默认设置"))
            {
                Settings.vegStateThreshold = 1.0f;
                Settings.comaSeverityFactor = 0.35f;
                Settings.brainDamageStartThreshold = 0.6f;
                Settings.brainDamageBaseChance = 0.02f;
                Settings.acidosisSilentHypoxiaStart = 0.4f;
                Settings.acidosisChanceLow = 0.02f;
                Settings.acidosisChanceMid = 0.08f;
                Settings.acidosisChanceHigh = 0.25f;
                Settings.acidosisCoreAttackChance = 0.3f;
            }
        }

        private void DrawVitalsCriticalCare(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("生命体征监控 (Vitals Monitor)");
            Text.Font = GameFont.Small;
            listing.Label($"血液灌注/呼吸安全阈值: {Settings.hypoxiaMonitorThreshold.ToStringPercent()}", tooltip: "心搏或呼吸低于此值时，将触发缺氧与酸中毒危机。");
            Settings.hypoxiaMonitorThreshold = listing.Slider(Settings.hypoxiaMonitorThreshold, 0.1f, 0.9f);
            listing.Label($"致命维生极低阈值: {Settings.vitalCriticalThreshold.ToStringPercent()}", tooltip: "低于此值将导致各类危机加速恶化（濒死状态）。");
            Settings.vitalCriticalThreshold = listing.Slider(Settings.vitalCriticalThreshold, 0.05f, 0.4f);
            listing.Label($"心脏骤停流血率下限: {Settings.minBleedMultiplier.ToStringPercent()}", tooltip: "心跳停止后，残余血压导致的全身流血速度保底比例。");
            Settings.minBleedMultiplier = listing.Slider(Settings.minBleedMultiplier, 0.01f, 0.5f);
            
            if (Settings.advancedMode)
            {
                listing.Label($"濒死恶化倍率: {Settings.vitalCriticalMultiplier:F1}x");
                Settings.vitalCriticalMultiplier = listing.Slider(Settings.vitalCriticalMultiplier, 1.0f, 5.0f);
            }

            if (listing.ButtonText("恢复本页默认设置"))
            {
                Settings.hypoxiaMonitorThreshold = 0.55f;
                Settings.vitalCriticalThreshold = 0.2f;
                Settings.minBleedMultiplier = 0.1f;
                Settings.vitalCriticalMultiplier = 2.0f;
            }
        }

        public override string SettingsCategory() => "Emergency Expanded";
    }
}
