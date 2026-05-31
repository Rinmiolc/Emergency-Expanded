namespace EmergencyExpanded
{
    public static class EE_Constants
    {
        // ================= 基础与系统设定 (Base Settings) =================
        // 初始化时相关致命健康状态（Hediff）赋予的初始严重程度。
        public const float InitialHediffSeverity = 0.05f;
        // 当角色的血液泵送能力（Pumping）或呼吸能力（Breathing）低于此百分比时，系统将触发缺氧与并发症的监测。
        public const float HypoxiaMonitorThreshold = 0.45f;

        
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
        // 触发酸中毒的失血严重度阈值 1。
        public const float AcidosisBloodLossThreshold1 = 0.30f;
        // 触发酸中毒的失血严重度阈值 2（Class IV 休克）。
        public const float AcidosisBloodLossThreshold2 = 0.40f;
        
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

        // ================= 凝血病 (Coagulopathy) =================
        public const float CoagulopathyAcidosisThreshold = 0.2f;
        public const float CoagulopathyBloodLossThreshold = 0.30f;

        // ================= 物理流血 (Blood Loss) =================
        // 全局流血速度乘数，用来调整普通伤和大出血的流血速度
        public const float GlobalBleedingFactor = 0.8f;
        // 当心脏停跳后，因残余血压和重力导致的被动流血速度下限（占正常流血速度的百分比）。
        public const float MinBleedMultiplier = 0.1f;
        
        // ================= 大出血 (Massive Bleeding) =================
        // 大出血判定基准伤害量（例如突击步枪伤害为15点）。
        public const float MassiveBleedingDamageScaleBase = 15f;
        // 触发大出血判定的最小伤害阈值。
        public const float MassiveBleedingMinDamage = 4f;
        // 当角色躯干部位受到创伤判定时，触发致命“大出血”事件的基础概率。
        public const float MassiveBleedingChanceTorsoBase = 0.35f;
        // 当角色四肢核心部位受到创伤判定时，触发致命“大出血”事件的基础概率。
        public const float MassiveBleedingChanceLimbBase = 0.35f;
        
        // 动态大出血概率的最小与最大限制
        public const float MassiveBleedingChanceMin = 0.10f;
        public const float MassiveBleedingChanceMax = 0.90f;

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
        public const float ModsCoreDamageAmount = 0.5f;
        // MODS 单次对大脑造成的脑损伤伤害。
        public const float ModsBrainDamageAmount = 0.0125f;

        // ================= 急救物品与医疗机制 (First Aid Items & Medical) =================
        // 草药急救包的包扎质量倍率。
        public const float FirstAidKitHerbalQuality = 0.35f;
        // 标准急救包的包扎质量倍率。
        public const float FirstAidKitStandardQuality = 0.65f;
        // 草药的医疗效果倍率。
        public const float MedicineHerbalMultiplier = 0.45f;
        // 工业医药的医疗效果倍率。
        public const float MedicineIndustrialMultiplier = 0.85f;
        // 闪耀世界医药的医疗效果倍率。
        public const float MedicineUltratechMultiplier = 1.7f;
        // 医生医疗技能对最终包扎质量的权重影响。
        public const float MedicineSkillWeight = 0.05f;
        // 基础包扎质量。
        public const float MedicineBaseQuality = 0.20f;
        
        // 简易夹板对骨折的固定对齐率（决定了后续愈合是否留下后遗症）。
        public const float PrimitiveSplintAlignmentQuality = 0.20f;
        // 使用简易夹板固定时的标准包扎质量。
        public const float PrimitiveSplintTendQuality = 0.40f;
        
        // 单次大出血缝合的基础严重度削减量。
        public const float MassiveBleedingTendReductionBase = 0.1f;
        // 医生包扎质量对大出血缝合削减量的加成系数。
        public const float MassiveBleedingTendReductionFactor = 0.15f;
        // 单次大出血缝合削减量的最大上限。
        public const float MassiveBleedingTendReductionMax = 0.25f;
        
        // 大出血单个急救包（或单次持续缝合）的最大尝试次数上限
        public const int MassiveBleedingTendMaxAttempts = 8;
        // 大出血缝合前几次必定失败的次数阈值
        public const int MassiveBleedingTendFailAttempts = 3;

        // 血包（HemogenPack）直接使用时削减失血严重程度的比例（占致死量的百分比）。
        public const float HemogenPackSeverityReductionFactor = 0.12f;
        
        // 标准急救动作提示飘字的持续时间（秒）。
        public const float FirstAidMoteDurationStandard = 3.0f;
        // 较长急救动作提示飘字的持续时间（秒）。
        public const float FirstAidMoteDurationLong = 3.5f;
        // 极危急救动作（如止大出血/输血）提示飘字的持续时间（秒）。
        public const float FirstAidMoteDurationCritical = 4.0f;

        // ================= 伤口污染机制 (Wound Contamination) =================
        // 污染度检测间隔（Tick），600 ticks = 10秒
        public const int ContaminationCheckInterval = 600;
        // 每次检测时，局部感染基于当前污染度额外增加的严重程度（污染越重恶化越快）
        public const float InfectionDynamicSeverityBase = 0.005f;

        // 所有开放性伤口的默认基础污染度。
        public const float ContaminationBase = 0.05f;
        // 枪伤、破片等投射物造成的额外初始污染度。
        public const float ContaminationRangedAdded = 0.15f;
        // 动物撕咬造成的极高额外初始污染度。
        public const float ContaminationBiteAdded = 0.25f;
        // 刀剑等锐器砍伤造成的额外初始污染度。
        public const float ContaminationSharpAdded = 0.10f;
        // 钝器击打造成的额外初始污染度。
        public const float ContaminationBluntAdded = 0.05f;
        
        // 小人倒在泥地、沼泽、浅水等肮脏地形上时，每 10 秒（600 ticks）增加的污染度。
        public const float ContaminationMudFactor = 0.005f;
        // 地板清洁度为负数时，每单位肮脏度造成的每 10 秒污染度增加。
        public const float ContaminationCleanlinessFactor = 0.002f;
        // 伤口接触到血迹、呕吐物等污垢时，每 10 秒增加的污染度。
        public const float ContaminationFilthFactor = 0.003f;
        // 伤口在未包扎暴露状态下，每 10 秒自然增加的微量污染度（细菌增殖）。
        public const float ContaminationUntendedFactor = 0.001f;
        
        // 清创包扎动作降低污染度的基础值。
        public const float ContaminationTendReductionBase = 0.05f;
        // 包扎质量对降低污染度的加成系数。
        public const float ContaminationTendReductionFactor = 0.15f;

        // 伤口污染度达到此阈值时，必定引发局部伤口感染或组织坏死。
        public const float ContaminationLocalInfectionThreshold = 0.35f;
        // 伤口污染度达到此极限阈值时，病菌入血，触发全身败血症。
        public const float ContaminationSepsisThreshold = 0.85f;

        // ================= 急救动作与时间 (First Aid Action Ticks) =================
        // 施加战术止血带所需的 Tick 时长（180 ticks = 3秒）。
        public const int FirstAidTicksTourniquet = 180;
        // 使用急救包快速包扎所需的 Tick 时长（90 ticks = 1.5秒）。
        public const int FirstAidTicksKit = 90;
        // 喂食药品/血包所需的 Tick 时长。
        public const int FirstAidTicksIngestible = 100;
        
        // 自动体外除颤仪使用时间 (3.4 秒 * 60 ticks)
        public const int FirstAidTicksDefibrillator = 204;

        // 常规医疗包扎动作的基础 Tick 时长（240 ticks = 4秒）。
        public const int FirstAidTicksMedicineBase = 240;
        // 伤员未躺在床上（即地面野战急救）时，常规包扎动作的时间惩罚倍率。
        public const float FirstAidGroundPenaltyMultiplier = 2.5f;

        // ================= 骨折详细判定 (Fracture Mechanics) =================
        // 触发钝器骨折判定的最小伤害阈值。
        public const float FractureBluntDamageThreshold = 10f;
        // 钝器伤害转化为骨折几率时，占部位最大生命值的折算比例基准。
        public const float FractureBluntMaxHPRatio = 0.6f;
        // 钝器骨折几率的基础乘数。
        public const float FractureBluntBaseFactor = 0.8f;
        // 只要达到钝器骨折伤害阈值，保底的骨折触发概率（50%）。
        public const float FractureBluntMinChance = 0.50f;
        // 触发钝器“重击必断”判定的极高伤害阈值。
        public const float FractureBluntHeavyThreshold = 20f;
        // 达到重击阈值时，保底的极高骨折触发概率（85%）。
        public const float FractureBluntHeavyMinChance = 0.85f;
        // 钝器伤害引发开放性（刺破皮肤）骨折的概率（通常很低，仅5%）。
        public const float FractureBluntOpenChance = 0.05f;

        // 触发枪伤/远程射击骨折判定的最小伤害阈值。
        public const float FractureRangedDamageThreshold = 8f;
        // 远程射击命中骨骼部位时的骨折触发概率（10%）。
        public const float FractureRangedChance = 0.10f;
        // 远程枪伤引发开放性骨折的概率。
        public const float FractureRangedOpenChance = 0.20f;

        // 触发爆炸/破片骨折判定的最小伤害阈值。
        public const float FractureExplosionDamageThreshold = 10f;
        // 爆炸波及骨骼部位时的骨折触发概率（30%）。
        public const float FractureExplosionChance = 0.30f;
        // 爆炸伤害引发开放性骨折的概率。
        public const float FractureExplosionOpenChance = 0.50f;

        // 触发锐器砍劈骨折判定的最小伤害阈值。
        public const float FractureSharpDamageThreshold = 15f;
        // 锐器重砍命中骨骼部位时的骨折触发概率（30%）。
        public const float FractureSharpChance = 0.30f;
        // 锐器砍劈极易引发开放性骨折（60%）。
        public const float FractureSharpOpenChance = 0.60f;

        // ================= 脑缺氧速度参数 (Cerebral Hypoxia Rates) =================
        // 脑部缺氧每天增加的基础严重度百分比
        public const float HypoxiaPerDay = 2.0f;
        // 脑部缺氧在供氧充足时每天自然消除的基础严重度百分比（保持原版3.0f不变）
        public const float HypoxiaRecoveryPerDay = 4.5f;

        // ================= 心肺复苏与除颤仪 (CPR & Defibrillator) =================
        // CPR 时维持患者呼吸和血液循环能力的最低数值。
        public const float CprMinCapacityLevel = 0.60f;
        // 除颤仪对心室颤动（VF，严重度 < 60%）阶段的除颤基础成功率。
        public const float DefibSuccessRateVF = 0.80f;
        // 除颤仪对完全心跳骤停（Cardiac Arrest，严重度 >= 60%）阶段的除颤基础成功率。
        public const float DefibSuccessRateCardiacArrestBase = 0.15f;
        // 接受 CPR 状态（EE_CPR_Receiving）对心跳骤停阶段除颤成功率的额外加成。
        public const float DefibSuccessRateCprBoost = 0.35f;
        // 医生的医疗技能（Medicine）对除颤成功率的加成系数（每级增加的概率）。
        public const float DefibSuccessRateSkillFactor = 0.015f;
        public const float DefibFailureBurnDamage = 4f;
        
        // ================= 动态除颤参数 (Dynamic Defibrillation) =================
        // 除颤仪最大失败次数上限（达到后损毁）。
        public const int DefibMaxFailures = 5;
        // 患者年龄对除颤成功率的惩罚阈值（超过此年龄开始计算惩罚）。
        public const float DefibAgePenaltyThreshold = 50f;
        // 超过年龄阈值后，每年龄增加的成功率惩罚。
        public const float DefibAgePenaltyPerYear = 0.005f;
        // 失血严重度对除颤成功率的最大惩罚。
        public const float DefibBloodLossMaxPenalty = 0.40f;
        // 低温症对除颤成功率的最大惩罚（严重低温几乎无法除颤）。
        public const float DefibHypothermiaMaxPenalty = 0.50f;
        // 脑缺氧/缺血时间过长对除颤成功率的最大惩罚。
        public const float DefibHypoxiaMaxPenalty = 0.30f;
        // 除颤后摇时间（Tick），1秒 = 60 Ticks。
        public const int DefibBackswingTicks = 60;

        // ================= ECG 与 体征仪 UI 参数 (ECG & Vital Monitor UI) =================
        // 心动过速报警阈值（心率大于此值时，ECG变为黄色报警）
        public const float EcgTachycardiaThreshold = 140f;
        // 心动过缓报警阈值
        public const float EcgBradycardiaThreshold = 45f;
        // 心跳极微弱/停搏判定阈值
        public const float EcgFlatlineThreshold = 0.1f;
        // 血氧饱和度低下报警阈值
        public const float EcgHypoxiaSpO2Threshold = 90f;
        // ================= 伤口污染与清创系统 (Contamination & Debridement) =================
        // 清创手术造成的切割伤害基础值（庸医造成的巨大伤害）
        public const float DebridementDamageBase = 15f;
        // 医生的每级医疗技能能够降低的清创伤害
        public const float DebridementDamageSkillReduction = 1.0f;
        // 顶级医生清创时的最小保底伤害
        public const float DebridementDamageMin = 1f;
        
        // 野战生理盐水冲洗瞬间降低的污染度
        public const float SalineContaminationReduction = 0.40f;
        
        // 抗生素期间，病菌严重度增长速度被压制的倍率 (例如 0.3f 表示只按原速度 30% 增长)
        public const float AntibioticSeveritySlowdownMultiplier = 0.30f;
        // 抗生素期间，免疫力生成速度的额外倍率加成 (例如 1.2f 表示 120% 速度)
        public const float AntibioticImmunityBoostMultiplier = 1.25f;
        // ================= 气胸判定参数 (Pneumothorax) =================
        // 防止单次伤害直接摧毁肺部所允许的最大保留生命值伤害上限
        public const float PneumothoraxDamageCap = 25f;
        // 原初伤害转化为气胸严重度的比例
        public const float PneumothoraxSeverityFactor = 0.04f;
        // 气胸的初始保底严重度
        public const float PneumothoraxBaseSeverity = 0.35f;

        // ================= 骨折详细参数追加 (Fracture Extended) =================
        // 骨折剧痛造成的瞬间硬直Ticks (80 ticks = 1.33秒)
        public const int FractureStunTicks = 80;
        // 未固定骨折对移动/操作能力的惩罚
        public const float FractureCapacityOffsetNone = -0.50f;
        // 夹板固定骨折对移动/操作能力的惩罚
        public const float FractureCapacityOffsetSplint = -0.20f;
        // 石膏固定骨折对移动/操作能力的惩罚
        public const float FractureCapacityOffsetCast = -0.10f;
        // 正骨静卧对移动/操作能力的惩罚
        public const float FractureCapacityOffsetBedrest = -0.30f;
        // 骨折二次伤害（撕裂软组织）的固定伤害量
        public const float FractureSecondaryDamageAmount = 2f;
        // 正骨静卧期间若移动，导致正骨失效的基础概率 (每250 ticks判定)
        public const float FractureStrictBedrestFailChance = 0.15f;
        // 原初伤害转化为骨折严重度的基础比例
        public const float FractureSeverityConversionFactor = 0.4f;
        // 骨折的最小严重度
        public const float FractureSeverityMin = 5f;
        // 骨折的最大严重度
        public const float FractureSeverityMax = 30f;

        // ================= 声音效果与提示 (Sound & Effects) =================
        // 骨折音效的 DefName 标识符符文
        public const string SoundBoneCrunch = "EE_BoneCrunch";
    }
}
