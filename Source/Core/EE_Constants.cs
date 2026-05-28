namespace EmergencyExpanded
{
    public static class EE_Constants
    {
        // ================= 基础与系统设定 (Base Settings) =================
        // 初始化时相关致命健康状态（Hediff）赋予的初始严重程度。
        public const float InitialHediffSeverity = 0.05f;
        // 当角色的血液泵送能力（Pumping）或呼吸能力（Breathing）低于此百分比时，系统将触发缺氧与并发症的监测。
        public const float HypoxiaMonitorThreshold = 0.55f;
        
        // ================= 致命维生阈值 (Vitals Critical Care) =================
        // 当血液泵送或呼吸低于此极低阈值时，小人进入濒死/心脏骤停状态，情况会迅速恶化。
        public const float VitalCriticalThreshold = 0.2f;
        // 处于濒死/心脏骤停状态时，健康状况恶化速度的基础倍率（受难度预设中的黄金抢救时间乘数影响）。
        public const float VitalCriticalMultiplierBase = 2.0f;

        // ================= 脑缺氧 (Cerebral Hypoxia) =================
        // 当脑缺氧的严重程度达到此值时，小人将陷入休克/昏迷状态。
        public const float ComaSeverityThreshold = 0.5f;
        // 陷入休克后，脑缺氧进一步恶化时附加的严重度乘数，代表深昏迷状态下病情恶化的缓和或加剧。
        public const float ComaSeverityFactor = 0.35f;
        // 脑部不可逆损伤达到此阈值（100%）时，小人被判定为脑死亡/永久植物人，此项为硬逻辑不可更改。
        public const float VegStateThreshold = 1.0f; 
        
        // 当脑缺氧严重程度达到该阈值后，系统开始判定是否会造成永久性的不可逆脑损伤。
        public const float BrainDamageStartThreshold = 0.6f;
        // 在每次监测周期（Tick/RareTick）内，发生不可逆脑损伤的基础概率。
        public const float BrainDamageBaseChance = 0.02f;
        // 当脑缺氧严重程度达到极危值（例如85%以上）时触发重度脑损判定。
        public const float BrainDamageCriticalThreshold = 0.85f;
        // 达到极危缺氧状态时，脑损伤概率及严重度增加的乘数（通常为2.5倍惩罚）。
        public const float BrainDamageCriticalMultiplier = 2.5f;
        // 一旦判定发生脑损伤，单次判定的脑损伤严重程度增量。
        public const float BrainDamageSeverityIncrement = 0.15f;

        // ================= 代谢性酸中毒 (Metabolic Acidosis) =================
        // 开始出现“无症状缺氧”并可能导致器官坏死或轻度酸中毒的严重度起始门槛。
        public const float AcidosisSilentHypoxiaStart = 0.4f;
        // 触发中度代谢性酸中毒判定的严重度阈值。
        public const float AcidosisMidThreshold = 0.6f;
        // 触发重度代谢性酸中毒判定的严重度阈值。
        public const float AcidosisHighThreshold = 0.85f;
        
        // 轻度缺氧阶段，每次判定发生酸中毒的概率。
        public const float AcidosisChanceLow = 0.02f;
        // 中度缺氧阶段，每次判定发生酸中毒的概率。
        public const float AcidosisChanceMid = 0.08f;
        // 重度缺氧阶段，每次判定发生酸中毒的概率。
        public const float AcidosisChanceHigh = 0.25f;
        
        // 当发生重度酸中毒时，核心器官（如肝、肾）受到严重连带攻击的概率。
        public const float AcidosisCoreAttackChance = 0.3f;
        // 酸中毒对核心器官造成损伤的严重度倍率，体现多器官衰竭的致命性。
        public const float AcidosisCoreDamageMultiplier = 2.0f;

        // ================= 物理流血 (Blood Loss) =================
        // 当心脏停跳后，因残余血压和重力导致的被动流血速度下限（占正常流血速度的百分比）。
        public const float MinBleedMultiplier = 0.1f;
        
        // ================= 大出血 (Massive Bleeding) =================
        // 当角色躯干部位受到创伤判定时，触发致命“大出血”事件的基础概率。
        public const float MassiveBleedingChanceTorsoBase = 0.25f;
        // 当角色四肢核心部位受到创伤判定时，触发致命“大出血”事件的基础概率。
        public const float MassiveBleedingChanceLimbBase = 0.25f;

        // ================= 骨折机制 (Bone Fracture) =================
        // 游戏中所有钝器或锐器伤害导致骨折判定的全局基础乘数。
        public const float FractureChanceMultiplierBase = 1.0f;
        // 当骨折未被固定夹板处理时，角色强行移动造成撕裂和二次伤害的基础概率（每次移动判定）。
        public const float SecondaryDamageChanceBase = 0.08f;

        // ================= 缺血与微循环衰竭 (Hypoxia & MODS) =================
        // 外周末梢缺血（指尖发绀等）触发的基础概率（每 60 刻度判定一次）。
        public const float PeripheralHypoxiaChance = 0.12f;
        // 单次外周缺血造成的组织坏死量。
        public const float PeripheralHypoxiaAmount = 1.0f;
        
        // MODS 造成核心脏器坏死的基础概率（基础概率 * 严重度）。
        public const float ModsCoreDamageChanceBase = 3.0f;
        // MODS 单次对核心器造成的急性坏死量。
        public const float ModsCoreDamageAmount = 2.0f;
        // MODS 单次对大脑造成的脑损伤伤害。
        public const float ModsBrainDamageAmount = 0.05f;
    }
}
