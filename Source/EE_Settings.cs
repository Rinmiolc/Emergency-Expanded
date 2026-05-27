namespace EmergencyExpanded
{
    public enum DifficultyPreset
    {
        Easy,       // 简单
        Hardcore,   // 硬核 (默认)
        Realistic   // 拟真
    }

    public static class EE_Settings
    {
        // ================= 基础与系统设定 =================
        public static bool DebugMode => EE_Mod.Settings?.debugMode ?? false;
        public static bool EnableEcgGui => EE_Mod.Settings?.enableEcgGui ?? true;
        
        public static DifficultyPreset Difficulty => EE_Mod.Settings?.difficulty ?? DifficultyPreset.Hardcore;

        // ================= 难度乘数 (Difficulty Multipliers) =================
        private static float BleedingMultiplier => Difficulty == DifficultyPreset.Easy ? 0.8f : (Difficulty == DifficultyPreset.Realistic ? 2.0f : 1.0f);
        private static float FractureMultiplier => Difficulty == DifficultyPreset.Easy ? 0.8f : (Difficulty == DifficultyPreset.Realistic ? 1.5f : 1.0f);
        private static float GoldenTimeMultiplier => Difficulty == DifficultyPreset.Easy ? 0.5f : (Difficulty == DifficultyPreset.Realistic ? 1.2f : 1.0f);

        // ================= 提取自常量的设定 =================
        public static float InitialHediffSeverity => EE_Constants.InitialHediffSeverity;
        public static float HypoxiaMonitorThreshold => EE_Constants.HypoxiaMonitorThreshold;
        
        // ================= 致命维生阈值 =================
        public static float VitalCriticalThreshold => EE_Constants.VitalCriticalThreshold;
        public static float VitalCriticalMultiplier => EE_Constants.VitalCriticalMultiplierBase * GoldenTimeMultiplier;

        // ================= 脑缺氧 (Cerebral Hypoxia) =================
        public static float ComaSeverityThreshold => EE_Constants.ComaSeverityThreshold;
        public static float ComaSeverityFactor => EE_Constants.ComaSeverityFactor;
        public static float VegStateThreshold => EE_Constants.VegStateThreshold;
        
        public static float BrainDamageStartThreshold => EE_Constants.BrainDamageStartThreshold;
        public static float BrainDamageBaseChance => EE_Constants.BrainDamageBaseChance;
        public static float BrainDamageCriticalThreshold => EE_Constants.BrainDamageCriticalThreshold;
        public static float BrainDamageCriticalMultiplier => EE_Constants.BrainDamageCriticalMultiplier;
        public static float BrainDamageSeverityIncrement => EE_Constants.BrainDamageSeverityIncrement;

        // ================= 代谢性酸中毒 (Metabolic Acidosis) =================
        public static float AcidosisSilentHypoxiaStart => EE_Constants.AcidosisSilentHypoxiaStart;
        public static float AcidosisMidThreshold => EE_Constants.AcidosisMidThreshold;
        public static float AcidosisHighThreshold => EE_Constants.AcidosisHighThreshold;
        
        public static float AcidosisChanceLow => DebugMode ? 0.9f : EE_Constants.AcidosisChanceLow;
        public static float AcidosisChanceMid => DebugMode ? 0.9f : EE_Constants.AcidosisChanceMid;
        public static float AcidosisChanceHigh => DebugMode ? 0.9f : EE_Constants.AcidosisChanceHigh;
        
        public static float AcidosisCoreAttackChance => DebugMode ? 0.9f : EE_Constants.AcidosisCoreAttackChance;
        public static float AcidosisCoreDamageMultiplier => EE_Constants.AcidosisCoreDamageMultiplier;

        // ================= 物理流血 (Blood Loss) =================
        public static float MinBleedMultiplier => EE_Constants.MinBleedMultiplier;
        
        // ================= 大出血 (Massive Bleeding) =================
        public static float MassiveBleedingChanceTorso => DebugMode ? 0.90f : (EE_Constants.MassiveBleedingChanceTorsoBase * BleedingMultiplier);
        public static float MassiveBleedingChanceLimb => DebugMode ? 0.90f : (EE_Constants.MassiveBleedingChanceLimbBase * BleedingMultiplier);

        // ================= 骨折机制 (Bone Fracture) =================
        public static float FractureChanceMultiplier => EE_Constants.FractureChanceMultiplierBase * FractureMultiplier;
        public static float SecondaryDamageChance => DebugMode ? 0.90f : (EE_Constants.SecondaryDamageChanceBase * FractureMultiplier);
    }
}