using UnityEngine;
using Verse;

namespace EmergencyExpanded
{
    public class EE_ModSettings : ModSettings
    {
        // ================= 基础与系统设定 =================
        public float initialHediffSeverity = 0.05f;
        public float hypoxiaMonitorThreshold = 0.55f;

        // ================= 致命维生阈值 =================
        public float vitalCriticalThreshold = 0.2f;
        public float vitalCriticalMultiplier = 2.0f;

        // ================= 脑缺氧 (Cerebral Hypoxia) =================
        public float comaSeverityThreshold = 0.5f;
        public float comaSeverityFactor = 0.35f;
        public float vegStateThreshold = 1.0f;
        
        public float brainDamageStartThreshold = 0.6f;
        public float brainDamageBaseChance = 0.02f;
        public float brainDamageCriticalThreshold = 0.85f;
        public float brainDamageCriticalMultiplier = 2.5f;
        public float brainDamageSeverityIncrement = 0.15f;

        // ================= 代谢性酸中毒 (Metabolic Acidosis) =================
        public float acidosisSilentHypoxiaStart = 0.4f;
        public float acidosisMidThreshold = 0.6f;
        public float acidosisHighThreshold = 0.85f;
        
        public float acidosisChanceLow = 0.02f;
        public float acidosisChanceMid = 0.08f;
        public float acidosisChanceHigh = 0.25f;
        
        public float acidosisCoreAttackChance = 0.3f;
        public float acidosisCoreDamageMultiplier = 2.0f;

        // ================= 物理流血与动脉破裂 =================
        public float minBleedMultiplier = 0.1f;
        public float arterialRuptureChanceTorso = 0.90f;
        public float arterialRuptureChanceLimb = 0.90f;

        public override void ExposeData()
        {
            base.ExposeData();
            // 基础
            Scribe_Values.Look(ref initialHediffSeverity, "initialHediffSeverity", 0.05f);
            Scribe_Values.Look(ref hypoxiaMonitorThreshold, "hypoxiaMonitorThreshold", 0.55f);
            
            // 维生
            Scribe_Values.Look(ref vitalCriticalThreshold, "vitalCriticalThreshold", 0.2f);
            Scribe_Values.Look(ref vitalCriticalMultiplier, "vitalCriticalMultiplier", 2.0f);
            
            // 脑缺氧
            Scribe_Values.Look(ref comaSeverityThreshold, "comaSeverityThreshold", 0.5f);
            Scribe_Values.Look(ref comaSeverityFactor, "comaSeverityFactor", 0.35f);
            Scribe_Values.Look(ref vegStateThreshold, "vegStateThreshold", 1.0f);
            Scribe_Values.Look(ref brainDamageStartThreshold, "brainDamageStartThreshold", 0.6f);
            Scribe_Values.Look(ref brainDamageBaseChance, "brainDamageBaseChance", 0.02f);
            Scribe_Values.Look(ref brainDamageCriticalThreshold, "brainDamageCriticalThreshold", 0.85f);
            Scribe_Values.Look(ref brainDamageCriticalMultiplier, "brainDamageCriticalMultiplier", 2.5f);
            Scribe_Values.Look(ref brainDamageSeverityIncrement, "brainDamageSeverityIncrement", 0.15f);

            // 酸中毒
            Scribe_Values.Look(ref acidosisSilentHypoxiaStart, "acidosisSilentHypoxiaStart", 0.4f);
            Scribe_Values.Look(ref acidosisMidThreshold, "acidosisMidThreshold", 0.6f);
            Scribe_Values.Look(ref acidosisHighThreshold, "acidosisHighThreshold", 0.85f);
            Scribe_Values.Look(ref acidosisChanceLow, "acidosisChanceLow", 0.02f);
            Scribe_Values.Look(ref acidosisChanceMid, "acidosisChanceMid", 0.08f);
            Scribe_Values.Look(ref acidosisChanceHigh, "acidosisChanceHigh", 0.25f);
            Scribe_Values.Look(ref acidosisCoreAttackChance, "acidosisCoreAttackChance", 0.3f);
            Scribe_Values.Look(ref acidosisCoreDamageMultiplier, "acidosisCoreDamageMultiplier", 2.0f);

            // 流血与动脉
            Scribe_Values.Look(ref minBleedMultiplier, "minBleedMultiplier", 0.1f);
            Scribe_Values.Look(ref arterialRuptureChanceTorso, "arterialRuptureChanceTorso", 0.90f);
            Scribe_Values.Look(ref arterialRuptureChanceLimb, "arterialRuptureChanceLimb", 0.90f);
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

            // 用两列或多页面简化显示，这里用标准列表配合滚动视图或分类说明
            Text.Font = GameFont.Medium;
            listing.Label("急救与医疗扩展 (Emergency Expanded) 设定");
            Text.Font = GameFont.Small;
            listing.Gap(10f);

            listing.Label($"【大出血发生率 - 躯干】: {Settings.arterialRuptureChanceTorso.ToStringPercent()} (乘以单次伤害系数)");
            Settings.arterialRuptureChanceTorso = listing.Slider(Settings.arterialRuptureChanceTorso, 0f, 1f);

            listing.Label($"【大出血发生率 - 四肢】: {Settings.arterialRuptureChanceLimb.ToStringPercent()} (乘以单次伤害系数)");
            Settings.arterialRuptureChanceLimb = listing.Slider(Settings.arterialRuptureChanceLimb, 0f, 1f);

            listing.Label($"【血液灌注/呼吸监控阈值】: {Settings.hypoxiaMonitorThreshold.ToStringPercent()} (低于此值将触发缺氧与酸中毒危机)");
            Settings.hypoxiaMonitorThreshold = listing.Slider(Settings.hypoxiaMonitorThreshold, 0.1f, 0.9f);

            listing.Label($"【致命维生极低阈值】: {Settings.vitalCriticalThreshold.ToStringPercent()} (低于此值将导致危机加速恶化)");
            Settings.vitalCriticalThreshold = listing.Slider(Settings.vitalCriticalThreshold, 0.05f, 0.4f);

            listing.Label($"【危机极低恶化倍率】: {Settings.vitalCriticalMultiplier:F1}x");
            Settings.vitalCriticalMultiplier = listing.Slider(Settings.vitalCriticalMultiplier, 1.0f, 5.0f);

            listing.Label($"【脑损伤判定起始门槛】: {Settings.brainDamageStartThreshold.ToStringPercent()} 缺氧严重度");
            Settings.brainDamageStartThreshold = listing.Slider(Settings.brainDamageStartThreshold, 0.1f, 0.9f);

            listing.Label($"【脑损伤基础几率】: {Settings.brainDamageBaseChance.ToStringPercent()}/tick-interval");
            Settings.brainDamageBaseChance = listing.Slider(Settings.brainDamageBaseChance, 0f, 0.1f);

            listing.Label($"【心脏骤停流血率下限】: {Settings.minBleedMultiplier.ToStringPercent()} (停搏时全身流血速度的保底比例)");
            Settings.minBleedMultiplier = listing.Slider(Settings.minBleedMultiplier, 0.01f, 0.5f);

            listing.Gap(15f);
            if (listing.ButtonText("恢复默认设置"))
            {
                Settings.arterialRuptureChanceTorso = 0.90f;
                Settings.arterialRuptureChanceLimb = 0.90f;
                Settings.hypoxiaMonitorThreshold = 0.55f;
                Settings.vitalCriticalThreshold = 0.2f;
                Settings.vitalCriticalMultiplier = 2.0f;
                Settings.comaSeverityThreshold = 0.5f;
                Settings.comaSeverityFactor = 0.35f;
                Settings.vegStateThreshold = 1.0f;
                Settings.brainDamageStartThreshold = 0.6f;
                Settings.brainDamageBaseChance = 0.02f;
                Settings.brainDamageCriticalThreshold = 0.85f;
                Settings.brainDamageCriticalMultiplier = 2.5f;
                Settings.brainDamageSeverityIncrement = 0.15f;
                Settings.acidosisSilentHypoxiaStart = 0.4f;
                Settings.acidosisMidThreshold = 0.6f;
                Settings.acidosisHighThreshold = 0.85f;
                Settings.acidosisChanceLow = 0.02f;
                Settings.acidosisChanceMid = 0.08f;
                Settings.acidosisChanceHigh = 0.25f;
                Settings.acidosisCoreAttackChance = 0.3f;
                Settings.acidosisCoreDamageMultiplier = 2.0f;
                Settings.minBleedMultiplier = 0.1f;
            }

            listing.End();
        }

        public override string SettingsCategory() => "Emergency Expanded";
    }
}
