namespace EmergencyExpanded
{
    public static class EE_Settings
    {
        // ================= 基础与系统设定 =================
        public static float InitialHediffSeverity = 0.05f;  // 疾病生成的初始严重度
        public static float HypoxiaMonitorThreshold = 0.55f;// 触发系统危机的呼吸/泵血阈值
        
        // ================= 致命维生阈值 =================
        public static float VitalCriticalThreshold = 0.1f;  // 极低维生阈值 (触发恶化翻倍)
        public static float VitalCriticalMultiplier = 2.0f; // 极低维生时的恶化倍率

        // ================= 脑缺氧 (Cerebral Hypoxia) =================
        public static float ComaSeverityThreshold = 0.6f;   // 进入深昏迷的严重度门槛
        public static float ComaSeverityFactor = 0.35f;     // 深昏迷时的恶化放缓倍率
        public static float VegStateThreshold = 1.0f;       // 触发脑死亡(植物人)的严重度
        
        public static float BrainDamageStartThreshold = 0.6f;// 脑损伤开始判定的严重度
        public static float BrainDamageBaseChance = 0.02f;   // 脑损伤基础几率
        public static float BrainDamageCriticalThreshold = 0.85f; // 脑损伤极高危门槛
        public static float BrainDamageCriticalMultiplier = 2.5f; // 极高危状态下的几率倍率
        public static float BrainDamageSeverityIncrement = 0.15f; // 每次脑损伤叠加的严重度

        // ================= 代谢性酸中毒 (Metabolic Acidosis) =================
        public static float AcidosisSilentHypoxiaStart = 0.4f; // 触发静默缺氧的严重度门槛
        public static float AcidosisMidThreshold = 0.6f;       // 缺氧中概率门槛
        public static float AcidosisHighThreshold = 0.85f;     // 缺氧高概率/攻击核心门槛
        
        public static float AcidosisChanceLow = 0.02f;         // 静默缺氧低几率
        public static float AcidosisChanceMid = 0.08f;         // 静默缺氧中几率
        public static float AcidosisChanceHigh = 0.25f;        // 静默缺氧高几率
        
        public static float AcidosisCoreAttackChance = 0.3f;   // 防线崩溃时，攻击核心脏器的几率
        public static float AcidosisCoreDamageMultiplier = 2.0f;// 核心脏器受到的伤害倍率

        // ================= 物理流血 (Blood Loss) =================
        public static float MinBleedMultiplier = 0.1f;         // 心跳骤停时的保底流血率
    }
}