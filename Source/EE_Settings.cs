namespace EmergencyExpanded
{
    public static class EE_Settings
    {
        // ================= 基础与系统设定 =================
        public static bool DebugMode => EE_Mod.Settings?.debugMode ?? false;
        public static bool EnableEcgGui => EE_Mod.Settings?.enableEcgGui ?? true;
        
        public static float InitialHediffSeverity => EE_Mod.Settings?.initialHediffSeverity ?? 0.05f;
        public static float HypoxiaMonitorThreshold => EE_Mod.Settings?.hypoxiaMonitorThreshold ?? 0.55f;
        
        // ================= 致命维生阈值 =================
        public static float VitalCriticalThreshold => EE_Mod.Settings?.vitalCriticalThreshold ?? 0.2f;
        public static float VitalCriticalMultiplier => EE_Mod.Settings?.vitalCriticalMultiplier ?? 2.0f;

        // ================= 脑缺氧 (Cerebral Hypoxia) =================
        public static float ComaSeverityThreshold => EE_Mod.Settings?.comaSeverityThreshold ?? 0.5f;
        public static float ComaSeverityFactor => EE_Mod.Settings?.comaSeverityFactor ?? 0.35f;
        public static float VegStateThreshold => EE_Mod.Settings?.vegStateThreshold ?? 1.0f;
        
        public static float BrainDamageStartThreshold => EE_Mod.Settings?.brainDamageStartThreshold ?? 0.6f;
        public static float BrainDamageBaseChance => EE_Mod.Settings?.brainDamageBaseChance ?? 0.02f;
        public static float BrainDamageCriticalThreshold => EE_Mod.Settings?.brainDamageCriticalThreshold ?? 0.85f;
        public static float BrainDamageCriticalMultiplier => EE_Mod.Settings?.brainDamageCriticalMultiplier ?? 2.5f;
        public static float BrainDamageSeverityIncrement => EE_Mod.Settings?.brainDamageSeverityIncrement ?? 0.15f;

        // ================= 代谢性酸中毒 (Metabolic Acidosis) =================
        public static float AcidosisSilentHypoxiaStart => EE_Mod.Settings?.acidosisSilentHypoxiaStart ?? 0.4f;
        public static float AcidosisMidThreshold => EE_Mod.Settings?.acidosisMidThreshold ?? 0.6f;
        public static float AcidosisHighThreshold => EE_Mod.Settings?.acidosisHighThreshold ?? 0.85f;
        
        public static float AcidosisChanceLow => DebugMode ? 0.9f : (EE_Mod.Settings?.acidosisChanceLow ?? 0.02f);
        public static float AcidosisChanceMid => DebugMode ? 0.9f : (EE_Mod.Settings?.acidosisChanceMid ?? 0.08f);
        public static float AcidosisChanceHigh => DebugMode ? 0.9f : (EE_Mod.Settings?.acidosisChanceHigh ?? 0.25f);
        
        public static float AcidosisCoreAttackChance => DebugMode ? 0.9f : (EE_Mod.Settings?.acidosisCoreAttackChance ?? 0.3f);
        public static float AcidosisCoreDamageMultiplier => EE_Mod.Settings?.acidosisCoreDamageMultiplier ?? 2.0f;

        // ================= 物理流血 (Blood Loss) =================
        public static float MinBleedMultiplier => EE_Mod.Settings?.minBleedMultiplier ?? 0.1f;
        
        // ================= 大出血 (Massive Bleeding) =================
        public static float MassiveBleedingChanceTorso => DebugMode ? 0.90f : (EE_Mod.Settings?.massiveBleedingChanceTorso ?? 0.25f);
        public static float MassiveBleedingChanceLimb => DebugMode ? 0.90f : (EE_Mod.Settings?.massiveBleedingChanceLimb ?? 0.25f);

        // ================= 骨折机制 (Bone Fracture) =================
        public static float FractureChanceMultiplier => EE_Mod.Settings?.fractureChanceMultiplier ?? 1.0f;
        public static float SecondaryDamageChance => DebugMode ? 0.90f : (EE_Mod.Settings?.secondaryDamageChance ?? 0.08f);
    }
}