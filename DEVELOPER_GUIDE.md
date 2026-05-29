# Emergency Expanded - 开发者指南 & AI 查阅手册

本指南旨在帮助开发者和 AI 快速了解 `Emergency Expanded` (急救与医疗扩展) Mod 的总体架构、核心机制及代码分布。该 Mod 致力于为 RimWorld 1.6 引入符合现实且硬核的急救机制，主要聚焦于战斗产生的急症（枪伤、锐器伤、钝器伤）。

## 1. 目录结构概览

Mod 的 C# 代码主要位于 `Source` 目录下：

*   **`Source/Core/`**: 核心定义与设置。
    *   `EE_DefOf.cs`: 集中声明了所有自定义的 HediffDef, ThingDef, RecipeDef, JobDef 等，方便代码调用。
    *   `EE_Settings.cs`: Mod 设置页面逻辑与配置项存储（如几率倍率）。
*   **`Source/HarmonyPatches/`**: 核心逻辑的切入点（Harmony 补丁），负责在原版伤害结算后触发 Mod 机制。
    *   `Patch_DamageWorker_MassiveBleeding.cs`: 大出血机制判定。
    *   `Patch_DamageWorker_Fracture.cs`: 骨折机制判定。
*   **`Source/Hediffs/`**: 自定义健康状态（Hediff）及组件（HediffComp）的具体逻辑。
    *   `MassiveBleeding.cs`: 大出血的运作逻辑。
    *   `Hediff_Fracture.cs`: 骨折状态的运作逻辑（固定状态、愈合不良等）。
    *   并发症逻辑：`CerebralHypoxia.cs` (脑缺氧), `HediffComp_SIRS.cs` (全身炎症反应综合征), `HediffComp_MODS.cs` (多器官衰竭) 等。
*   **`Source/Utilities/`**: 通用工具类。
    *   `EE_FirstAidUtility.cs`: 战场急救物品的使用逻辑（止血带、夹板、急救包等）。
    *   `EE_MedicalUtility.cs`: 身体部位判定（如是否大血管、最近可用部位等）。
*   **`Source/Jobs/`**: 涉及新工作类型的 AI 逻辑（如打急救包）。
    *   `JobDriver_ApplyFirstAid.cs`: 执行急救工作的逻辑。
*   **`Source/Recipes/`**: 自定义手术配方代码（如清创、骨折复位）。
    *   `Recipe_Debridement.cs` (清创), `Recipe_Irrigation.cs` (冲洗), `Recipes_Fracture.cs` (骨科手术)。

XML 数据文件位于 `1.6/Defs/` 目录下，包含物品 (`EE_FirstAidItems.xml`), 手术 (`EE_SurgeryDefs.xml`), Hediff (`HediffDefs.xml`, `EE_InfectionHediffs.xml`) 等的定义。

---

## 2. 核心机制解析

### 2.1 创伤大出血机制 (Massive Bleeding)
*   **触发点**: `Patch_DamageWorker_MassiveBleeding.cs`
*   **规则**:
    *   **伤害类型限制**: 仅限于 `Sharp` (锐器/穿透) 伤害类别（兼容原版与 CE/VE 的子弹、刺击等）。
    *   **目标限制**: 仅限有血肉 (`IsFlesh`) 且有血液定义的人类/动物，排除机械族、蹒跚怪。
    *   **部位限制**: 只有命中大血管分布区（躯干或四肢核心部位）的伤口可能触发（通过 `EE_MedicalUtility.IsMajorVesselPart` 判定）。
    *   **几率算法**: 几率与单次伤害量线性挂钩。设定一基础伤害值(如突击步枪)为 100% 几率基数进行浮动。
*   **效果**: 添加 `EE_DefOf.MassiveBleeding` Hediff，这是一种极度致命的持续失血状态，需要特殊的急救手段（如止血带或急救包多次包扎）才能闭合。

### 2.2 骨折机制 (Fracture)
*   **触发点**: `Patch_DamageWorker_Fracture.cs`
*   **规则**:
    *   **判定条件**: 在伤害结算后，如果受损的是骨骼部位（通过检查 `bone`/`skeletal` 标签或名称模糊匹配判定），且该部位未被完全摧毁。
    *   **伤害类型与几率**: 采用极其平衡的硬核数学模型：
        *   **钝击 (`Blunt`, `Crush`)**: 极高概率骨折，保底 50%，重击可达 85%。主要为闭合性骨折，5%概率开放性。
        *   **锐器 (`Sharp`)**: 适中概率（约 30%），伴随较高开放性骨折几率（60%）。
        *   **爆炸 (`Bomb`)**: 约 30% 几率，50%为开放性。
        *   **射击 (`Bullet`, `Arrow`)**: 较低概率（约 10%），20%为开放性。
*   **效果**:
    *   生成 `EE_ClosedFracture` (闭合性) 或 `EE_OpenFracture` (开放性)。
    *   **联动**: 开放性骨折会**直接联动触发**大出血机制。
    *   骨折严重度与伤害量挂钩。需要打夹板 (`Primitive Splint`)、石膏或进行切开复位内固定手术 (`ORIF`) 才能使其良好愈合，否则可能产生畸形愈合 (`EE_Malunion`)。

### 2.3 战场急救机制 (First Aid)
*   **核心逻辑**: `EE_FirstAidUtility.cs`
*   **分类与效果**:
    1.  **止血带 (`Tourniquet`)**: 用于四肢。如果小人有正在流血的四肢伤口，使用后立刻将该部位的所有流血伤口 Tend（包扎质量100%），快速止血保命。
    2.  **简易夹板 (`Primitive Splint`)**: 用于未固定的骨折。使用后使骨折状态变为 `isSplinted`，提供 20% 的基础复位质量。
    3.  **急救包/医药 (`FirstAidKit` / `Medicine`)**: 允许在野外连续对伤口进行倾向性包扎。特别地，对于大出血 (`MassiveBleeding`)，每次包扎会降低其 Severity，直到降为 0 视为伤口闭合。
    4.  **血袋 (`HemogenPack`)**: 使用后可直接降低 `BloodLoss` (失血) 的严重度，模拟输血。

### 2.4 感染与并发症 (Infection & Complications)
*   **逻辑分布**: `Source/Hediffs/` 目录下的各个 `HediffComp` 类。
*   **机制**:
    *   创伤和大量失血可能引发**脑缺氧** (`CerebralHypoxia`)、**代谢性酸中毒** (`HediffComp_Acidosis.cs`) 和**凝血病** (`HediffComp_Coagulopathy.cs`)（创伤致死三联征）。
    *   伤口处理不当或开放性伤口容易导致**局部感染** (`EE_LocalizedInfection`)，可能恶化为**坏死** (`EE_Necrosis`) 甚至**脓毒症** (`EE_Sepsis` / `SIRS`)，并最终导致**多器官衰竭** (`MODS`)。
    *   对应提供了清创 (`Debridement`) 和冲洗 (`Irrigation`) 手术来降低伤口的污染度。

---

## 3. 开发规范与避坑指南

1.  **Harmony 补丁的兼容性参数名**:
    在拦截 `DamageWorker_AddInjury.Apply` 时，1.6 版本的底层反射签名要求第二个参数名为 **`Thing thing`**。**绝对不要**将其重命名为 `victim` 或其他名称，否则会导致 Harmony 补丁加载失败。
    *(参考: `Patch_DamageWorker_MassiveBleeding.cs` 第 18 行)*

2.  **避免 `Tried to add health diff to missing part` 报错**:
    在添加骨折或大出血前，必须通过 `pawn.health.hediffSet.GetPartHealth(part) <= 0` 或 `PartIsMissing` 来检查目标部位是否已经由于本次打击被彻底摧毁。尤其是在开放性骨折联动大出血时，骨折的物理伤害可能刚好斩断该部位。

3.  **大血管与骨骼的判定**:
    游戏存在各种外星人 Mod。为保证兼容性，不要死磕特定的部位 DefName。
    *   血管判定使用 `EE_MedicalUtility.IsMajorVesselPart`，结合了核心部位判定。
    *   骨骼判定优先检查部位标签中是否含有 `bone` 或 `skeletal`，后备方案才是字符串模糊匹配。

4.  **UI 与 飘字 (Mote)**:
    大出血、骨折、急救物品使用时，应调用 `MoteMaker.ThrowText` 抛出红色或提示文字，增强硬核战斗的视觉反馈和紧张感。

## 4. 后续开发建议方向

*   **UI 面板增强**: 可以考虑引入类似于 RimHUD 的状态面板插件，用于直观显示角色的“酸碱度”、“血氧”和“凝血能力”。
*   **性能优化**: `GetEmergencyItemType` 和 `GetUsableItemsInInventory` 可能会在 AI 频繁寻路时调用，建议使用缓存机制减少性能消耗。
*   **兼容性测试**: 确保与 CE (Combat Extended) 的弹片伤害和流血机制不发生严重冲突。目前已通过 `ArmorCategory` 和动态伤害值进行了一定兼容。
