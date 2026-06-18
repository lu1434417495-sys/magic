# 类型字段归属与封装改造方案

日期：2026-06-17

## 背景

当前 C# 运行时代码中，多个核心状态类型以 public 字段暴露内部状态。典型例子包括 `PartyMemberState.current_hp`、`BattleUnitState.current_hp`、`BattleUnitState.coord`、`BattleState` 中的单位和格子索引、人物身份字段、血脉字段、飞升字段、战斗资源字段等。

这带来三个问题：

1. 字段归属不清。属于人物、战斗单位、装备、仓库、地图格子或战斗全局状态的字段，经常被外部服务直接写入。
2. 不变量容易被绕过。例如 `current_hp` 与 `is_alive/is_dead`、`body_size` 与 `body_size_category`、`coord` 与 `occupied_coords` 需要同步维护，但直接写字段无法强制同步。
3. 类型边界难以收紧。只要字段 public，下游就会自然绕过 owner 类型的接口，后续重构和验证成本会持续增加。

本方案的目标是把“字段归属”落实到代码结构中：属于某个类型的状态，必须通过该类型提供的接口读取或修改；外部模块不能随意暴露或改写内部字段。

## 总原则

1. Owner 类型拥有字段，Owner 类型维护不变量。
2. 外部模块只调用 owner 的读写接口，不直接写 owner 的内部字段。
3. 只读流程使用 read view、快照或只读集合，避免通过引用绕回内部对象。
4. 写接口必须表达业务意图，而不是简单包装字段赋值。
5. 迁移分阶段进行，先补接口并替换生产代码，再收紧字段可见性，最后加自动化检查。

示例：

```csharp
// 禁止
unit.current_hp = 0;
unit.is_alive = false;

// 允许
unit.ApplyHpDamage(damage);
unit.MarkDead();
```

```csharp
// 禁止
member.body_size_category = "large";
member.body_size = 3;

// 允许
member.SetBodySizeCategory("large");
```

## 归属矩阵

| Owner 类型 | 拥有字段范围 | 允许修改者 | 必须维护的不变量 |
| --- | --- | --- | --- |
| `PartyMemberState` | 人物长期身份、人物长期资源、死亡状态、种族、亚种、年龄、体型、血脉、飞升、装备引用、成长引用 | `PartyMemberState` 自身接口；人物创建、存档恢复、角色管理服务只能调用接口 | `current_hp >= 0`；`current_mp >= 0`；`current_aura >= 0`；`is_dead` 与 HP 死亡语义一致；`body_size` 与 `body_size_category` 一致 |
| `UnitBaseAttributes` | 基础属性、隐藏幸运、信仰幸运、自定义属性 | `UnitBaseAttributes` 自身接口；属性生成和成长服务只能调用接口 | 属性 id 规范化；基础属性和 custom stats 不冲突；幸运派生值统一由该类型计算 |
| `UnitProgress` | 技能进度、职业进度、知识、核心技能、成长进度、成就、锁定技能、融合技能来源、解锁战斗资源 | `UnitProgress` 自身接口；成长、学习、职业分配服务只能调用接口 | 集合去重；空 id 过滤；进度非负；引用对象深拷贝 |
| `EquipmentState` | 装备槽、装备条目、装备实例视图 | `EquipmentState` 自身接口；装备服务只能调用接口 | 槽位规范化；双手/占用槽一致；装备实例不共享可变引用 |
| `WarehouseState` | 背包堆叠、装备实例列表 | `WarehouseState` 自身接口；商店、掉落、战斗结算只能调用接口 | 数量非负；空堆叠过滤；实例 id 唯一 |
| `PartyState` | 队伍金钱、成员索引、出战/后备列表、任务集合、仓库、元标记 | `PartyState` 自身接口；运行时 facade 只能调用接口 | 成员 id 规范化；leader 必须指向有效成员或为空；金币非负；成员集合和 roster 不冲突 |
| `BattleState` | 战斗全局状态、单位索引、格子索引、阵营单位 id、时间线、日志、地形列、屏障字段 | `BattleState` 自身接口；战斗 runtime 服务只能调用接口 | 单位索引 key 与 `unit_id` 一致；格子索引 key 与 `coord` 一致；拓扑变更递增 geometry revision；日志预算受控 |
| `BattleUnitState` | 战斗单位运行时投影：位置、生命、资源、AP、移动点、状态效果、技能快照、武器投影、护盾、冷却、charge、AI runtime 状态 | `BattleUnitState` 自身接口；resolver 只能调用接口 | `coord/footprint/occupied_coords` 一致；`current_hp >= 0`；`is_alive` 与 HP 一致；资源非负且可按上限 clamp；状态效果 key 规范化 |
| `BattleCellState` | 单个格子的地形、高度、通过性、移动成本、占用单位、堆叠层 | `BattleCellState` 自身接口；网格服务只能调用接口 | `coord` 与索引一致；move cost 非负；occupant id 规范化 |
| Content `Resource` 类型 | `SkillDef`、`ItemDef`、`QuestDef`、`RaceDef` 等静态内容字段 | 内容 registry 和 loader；运行时只读 | 内容字段经 registry 校验；运行时使用 normalized getter，不直接修改 |

## 读写边界

### 读路径

读路径分三层：

1. Owner 内部读：Owner 类型内部可以直接读自己的字段。
2. 同模块运行时读：生产代码优先使用 typed getter，例如 `GetCurrentHp()`、`GetKnownSkillLevelTyped()`、`GetStatusEffectsTyped()`。
3. UI、AI 评分、预览和查询：优先使用 read view，例如 `BattleStateReadView`、`BattleUnitReadView`、`BattleCellReadView`。

read view 不允许返回可变集合的内部引用。凡是列表、字典、装备状态、状态效果集合，必须返回副本、只读包装或值对象。

### 写路径

写路径只能经过 owner 的方法。方法名要表达语义：

```csharp
unit.ApplyHpDamage(damage);
unit.ApplyHealing(amount, hpMax);
unit.SpendSkillCosts(costs);
unit.RefundSkillCosts(costs, resourceCaps);
unit.SetAnchorCoord(coord);
unit.SetBodySizeCategory(category);
unit.SetStatusEffect(status);
unit.EraseStatusEffect(statusId);
```

不建议增加这种接口：

```csharp
unit.SetField("current_hp", value);
unit.SetCurrentHpRaw(value);
```

除存档恢复和 schema round-trip 之外，不提供 raw setter。确实需要 raw setter 时，使用 `internal` 并命名为 `Restore...FromPayload`，只允许序列化路径调用。

## `PartyMemberState` 接口设计

`PartyMemberState` 是人物长期状态 owner。它负责人物身份、长期生命资源、死亡标记、种族亚种、年龄、体型、血脉、飞升状态。

建议新增接口：

```csharp
public int GetCurrentHp();
public int GetCurrentMp();
public int GetCurrentAura();
public bool IsDead();

public void SetVitals(int hp, int mp, int aura);
public void ClampVitals(int hpMax, int mpMax, int auraMax);
public void RestoreVitals(int hpMax, int mpMax, int auraMax);
public void MarkDead();
public void ReviveWithVitals(int hp, int mp, int aura);

public void SetIdentity(StringName raceId, StringName subraceId);
public void SetAgeProjection(
    int ageYears,
    int biologicalAgeYears,
    int astralMemoryYears,
    int birthAtWorldStep
);
public bool SetBodySizeCategory(StringName category);

public void SetBloodline(StringName bloodlineId, StringName stageId);
public void ClearBloodline();
public void SetAscension(
    StringName ascensionId,
    StringName stageId,
    int startedAtWorldStep,
    StringName originalRaceIdBeforeAscension
);
public void ClearAscension();
```

不变量：

1. `SetVitals` 统一 clamp 到非负值。
2. `MarkDead` 必须设置 `current_hp = 0`，并清空或保留 MP/Aura 的规则必须固定，不能由调用者分散决定。
3. `ReviveWithVitals` 必须设置 `is_dead = false`，且 HP 至少为 1。
4. `SetBodySizeCategory` 同时写 `body_size_category` 和 `body_size`。
5. `SetAscension` 和 `ClearAscension` 统一维护 `ascension_id`、`ascension_stage_id`、`ascension_started_at_world_step`、`original_race_id_before_ascension`。

迁移对象：

1. `CharacterCreationService` 不再写 `race_id/subrace_id/age_years/current_hp`。
2. `CharacterManagementModule` 不再写 `current_hp/current_mp/current_aura/is_dead/body_size`。
3. `GameSession` 存档恢复路径使用 `Restore...FromPayload`。
4. `BloodlineApplyService` 和 `AscensionApplyService` 只调用血脉/飞升接口。
5. `IdentityPayloadValidator` 不直接修正 `body_size` 字段，改为调用人物体型接口。

## `UnitBaseAttributes` 接口设计

`UnitBaseAttributes` 已经有 `GetAttributeValue` 和 `SetAttributeValue`，方向正确。需要继续收紧 public 基础属性字段。

建议新增或明确：

```csharp
public IReadOnlyDictionary<StringName, int> GetCustomStatsTyped();
public bool TryGetBaseAttribute(StringName attributeId, out int value);
public bool TrySetBaseAttribute(StringName attributeId, int value);
public void SetCustomStat(StringName statId, int value);
public void RemoveCustomStat(StringName statId);
public UnitBaseAttributesSnapshot CaptureSnapshot();
```

规则：

1. 基础六维只通过 `SetAttributeValue` 或 `TrySetBaseAttribute` 修改。
2. `custom_stats` 不直接暴露可变字典。
3. 幸运相关派生值只由 `UnitBaseAttributes` 计算，不由外部重复公式。

## `UnitProgress` 接口设计

`UnitProgress` 已经有不少 setter，例如 `SetSkillProgress`、`SetProfessionProgress`、`SetKnownKnowledgeIds`、`SetUnlockedCombatResourceIds`。后续重点是禁止外部直接访问内部集合和字典。

建议补充：

```csharp
public IReadOnlyList<StringName> GetKnownKnowledgeIdsTyped();
public IReadOnlyList<StringName> GetActiveCoreSkillIdsTyped();
public IReadOnlyDictionary<StringName, int> GetAttributeGrowthProgressTyped();
public bool UnlockCombatResource(StringName resourceId);
public bool LockLevelTriggerSkill(StringName skillId);
public bool ClearLevelTriggerSkillLock(StringName skillId);
```

规则：

1. 所有集合 setter 必须过滤空 id 并去重。
2. 返回集合时使用副本或只读包装。
3. `UnitProgress` 不直接修改 `PartyMemberState` 生命、身份和装备字段。

## `BattleState` 接口设计

`BattleState` 是战斗拓扑 owner。现有方向已经开始从公开 `cells/units` 字典迁移到私有索引和接口，例如 `SetUnit`、`SetCell`、`SetUnitsFromDictionary`、`SetCellsFromDictionary`、`ProjectCells`、`ProjectUnits`、`BattleStateReadView`。

建议正式确定：

```csharp
internal bool ContainsUnit(StringName unitId);
internal bool TryGetUnitTyped(StringName unitId, out BattleUnitState unitState);
internal BattleUnitReadView GetUnitView(StringName unitId);
internal IReadOnlyList<BattleState.BattleUnitEntry> GetUnitEntriesTyped();

internal void SetUnit(BattleUnitState unitState);
internal bool RemoveUnit(StringName unitId);
internal void ClearUnits();
internal void SetUnits(IEnumerable<BattleUnitState> units);

internal bool ContainsCell(Vector2I coord);
internal bool TryGetCellTyped(Vector2I coord, out BattleCellState cellState);
internal BattleCellReadView GetCellView(Vector2I coord);

internal void SetCell(Vector2I coord, BattleCellState cellState);
internal bool RemoveCell(Vector2I coord);
internal void ClearCells();
internal void SetCells(IEnumerable<BattleCellState> cells, bool rebuildColumns = true);
internal void RebuildCellColumns();
```

不变量：

1. `SetUnit` 规范化 `unit_id`，并保证索引 key 与 `unit_id` 一致。
2. `SetCell` 保证索引 key 与 `cell.coord` 一致。
3. 增删单位或格子必须调用 `MarkMovementGeometryChanged()`。
4. 外部不直接操作单位索引和格子索引。
5. `ProjectCells()` 和 `ProjectUnits()` 只用于序列化或兼容桥接，不作为可写入口。

需要避免：

```csharp
state.units[id] = unit;
state.cells[coord] = cell;
state.UnitIndex[id] = unit;
```

允许：

```csharp
state.SetUnit(unit);
state.SetCell(coord, cell);
```

## `BattleUnitState` 接口设计

`BattleUnitState` 是战斗单位运行时状态 owner。它是当前最需要收紧的类型。

### 位置与体型

已有 `SetAnchorCoord`、`RefreshFootprint`、`SetBodySizeCategory`。建议明确禁止外部直接写：

```csharp
coord
body_size
body_size_category
footprint_size
occupied_coords
```

接口：

```csharp
public void SetAnchorCoord(Vector2I anchorCoord);
public bool SetBodySizeCategory(StringName category);
public IReadOnlyList<Vector2I> GetOccupiedCoordsTyped();
```

规则：

1. 改 `coord` 必须刷新 footprint 和 occupied coords。
2. 改 `body_size_category` 必须同步 `body_size` 并刷新 footprint。

### 生命与存活

建议新增：

```csharp
public int GetCurrentHp();
public bool IsAlive();
public void SetCurrentHp(int value);
public void SetCurrentHpClamped(int value, int hpMax);
public int ApplyHpDamage(int damage);
public int ApplyHealing(int amount, int hpMax);
public void MarkDead();
public void ReviveWithHp(int hp, int hpMax);
```

规则：

1. `SetCurrentHp` clamp 到 `>= 0`。
2. 每次 HP 变化同步 `is_alive = current_hp > 0`。
3. 外部不直接写 `is_alive`，除序列化恢复路径。
4. 伤害、治疗、死亡、复活都通过接口表达意图。

### 战斗资源

建议新增：

```csharp
public void SetCombatResources(
    int hp,
    int mp,
    int stamina,
    int aura,
    int ap,
    int movePoints
);
public void ClampCombatResources(BattleResourceCaps caps);
public bool SpendSkillCosts(SkillCostTransaction costs);
public void RefundSkillCosts(SkillCostTransaction costs, BattleResourceCaps caps);
public void SetCurrentMovePoints(int value);
public void SetCurrentAp(int value);
public void SetCurrentStamina(int value);
public void SetCurrentMp(int value);
public void SetCurrentAura(int value);
```

`BattleResourceCaps` 可作为小型值对象：

```csharp
internal readonly record struct BattleResourceCaps(
    int HpMax,
    int MpMax,
    int StaminaMax,
    int AuraMax,
    int ApMax,
    int MovePointMax
);
```

规则：

1. MP、体力、Aura、AP、移动点不允许负数。
2. 消耗成本和退还成本走统一逻辑，避免每个 resolver 自己写 `Math.Max/Math.Min`。
3. AP 和移动点的 turn reset 由 `BattleTimelineDriver` 调用接口完成。

### 状态效果

已有 `SetStatusEffect`、`EraseStatusEffect`、`GetStatusEffectsTyped`。建议补充：

```csharp
public void ClearStatusEffects();
public IReadOnlyList<StringName> GetSortedStatusEffectIdsTyped();
public IReadOnlyDictionary<StringName, BattleStatusEffectState> CaptureStatusEffectsTyped();
internal void RestoreStatusEffectsFromPayload(GDictionary payload);
```

规则：

1. `status_effects` 不直接暴露可变字典。
2. 任何新增、删除、恢复都经过 key 规范化。
3. 从 payload 恢复时过滤非法 entry。

### 技能、冷却与 charge

已有 typed helpers，例如 `GetKnownSkillLevelTyped`、`SetCooldownTyped`、`SetPerBattleChargeTyped`、`SetPerTurnChargeTyped`。建议继续补齐：

```csharp
public void SetKnownActiveSkillIds(IEnumerable<StringName> skillIds);
public IReadOnlyList<StringName> GetKnownActiveSkillIdsTyped();
public bool KnowsActiveSkill(StringName skillId);
public void SetKnownSkillLevel(StringName skillId, int level);
public void RemoveKnownSkillLevel(StringName skillId);
```

规则：

1. 技能 id 过滤空值并去重。
2. 冷却和 charge 字典不直接暴露可变引用。
3. clone 和 payload round-trip 通过 owner 方法恢复。

### 装备与武器投影

已有 `SetEquipmentView`、`ApplyWeaponProjectionTyped`、`ClearWeaponProjection`。建议保持：

1. `equipment_view` 只通过 `SetEquipmentView` 替换，并深拷贝。
2. `weapon_*` 字段只通过武器投影接口更新。
3. 不允许外部逐个写 `weapon_attack_range`、`weapon_uses_two_hands`、`weapon_one_handed_dice`。

## `BattleCellState` 接口设计

`BattleCellState` 应拥有格子自身字段。网格服务可以请求修改，但不直接写字段。

建议接口：

```csharp
public void SetCoord(Vector2I coord);
public void SetOccupant(StringName unitId);
public void ClearOccupant();
public void SetTerrain(StringName terrainId);
public void SetPassable(bool passable);
public void SetMoveCost(int moveCost);
public void SetHeightOffset(int height);
```

规则：

1. `SetMoveCost` clamp 到非负值。
2. occupant id 规范化，空值表示清空。
3. 格子坐标如果由 `BattleState.SetCell(coord, cell)` 赋值，必须走 `BattleCellState.SetCoord` 或等价 owner 接口。

## 内容定义类型处理

`SkillDef`、`ItemDef`、`QuestDef`、`RaceDef`、`SubraceDef`、`BloodlineDef` 等 Godot `Resource` 类型有序列化需求，不能简单全部改成 private 字段。

处理原则：

1. 静态内容字段可以为 Godot inspector/export 保持必要可见性。
2. 运行时不能把内容定义当作 mutable state 修改。
3. 下游读取使用 normalized getter，例如 `GetTagsTyped()`、`GetAttributeRequirementsTyped()`、`GetEquipmentSlotIdsTyped()`。
4. 内容 registry 负责校验并生成只读索引，例如 `GameContentCatalog` 已采用 `IReadOnlyDictionary` 快照的方向。

## 迁移阶段

### 阶段 0：冻结规则和文档

目标：

1. 合并本设计文档。
2. 明确后续 PR 不再新增跨 owner 直接写字段。
3. 新代码必须优先使用 owner 接口。

验收：

1. 文档存在。
2. review 时按归属矩阵检查新增代码。

### 阶段 1：补齐 owner 接口，不改可见性

目标：

1. `PartyMemberState` 增加人物长期状态接口。
2. `BattleUnitState` 增加生命、资源、技能集合、状态集合接口。
3. `BattleState` 固化单位/格子索引接口。
4. `BattleCellState` 增加格子修改接口。

此阶段不马上把字段 private 化，先降低调用方迁移成本。

验收：

1. 新接口覆盖当前高频直接写字段场景。
2. 新接口有 focused regression。
3. 旧字段仍保留，避免一次性破坏 Godot 序列化和大量测试。

### 阶段 2：替换生产代码直接写字段

优先替换 `scripts/`，暂缓大规模测试 fixture 改造。

优先级：

1. 战斗核心：`BattleDamageResolver`、`BattleRuntimeSkillTurnResolver`、`BattleMovementService`、`BattleTimelineDriver`、`BattleChangeEquipmentResolver`、`BattleSkillExecutionOrchestrator`。
2. 战斗构建：`BattleUnitFactory`、`EncounterRosterBuilder`、`BattleSimUnitSpec`。
3. 人物长期状态：`CharacterCreationService`、`CharacterManagementModule`、`PracticeGrowthService`、`GameRuntimeSettlementCommandHandler`。
4. 身份和血脉：`IdentityPayloadValidator`、`BloodlineApplyService`、`AscensionApplyService`。
5. 存档恢复：`GameSession` 使用专用 restore 接口。

替换例：

```csharp
// 旧
targetUnit.current_hp = Math.Max(projectedHp, 0);
targetUnit.is_alive = targetUnit.current_hp > 0;

// 新
targetUnit.SetCurrentHp(projectedHp);
```

```csharp
// 旧
active_unit.current_mp = Math.Max(active_unit.current_mp - costs.MpCost, 0);
active_unit.current_aura = Math.Max(active_unit.current_aura - costs.AuraCost, 0);

// 新
active_unit.SpendSkillCosts(costs);
```

验收：

1. `scripts/` 下高风险字段写入数量显著下降。
2. 生产代码不再直接写 `current_hp/is_alive/body_size/body_size_category/coord/status_effects`。
3. 相关战斗、人物、身份 regression 通过。

### 阶段 3：改造测试 fixture

目标：

1. 新增 `BattleUnitTestBuilder`。
2. 新增 `PartyMemberTestBuilder`。
3. 测试不再通过字段写入制造状态，除 schema 反序列化测试。

示例：

```csharp
BattleUnitState unit = BattleUnitTestBuilder
    .Create("hero")
    .At(new Vector2I(2, 2))
    .WithVitals(hp: 30, hpMax: 40)
    .WithResources(mp: 4, stamina: 30, aura: 2, ap: 2)
    .WithSkill("warrior_heavy_strike", level: 2)
    .Build();
```

验收：

1. 测试中直接写战斗单位核心字段的场景迁移到 builder。
2. 只有 schema payload 测试允许构造非法或边界 payload。

### 阶段 4：收紧字段可见性

按风险从高到低收紧：

第一批：

```text
BattleUnitState.current_hp
BattleUnitState.current_mp
BattleUnitState.current_stamina
BattleUnitState.current_aura
BattleUnitState.current_ap
BattleUnitState.current_move_points
BattleUnitState.is_alive
BattleUnitState.coord
BattleUnitState.status_effects
```

第二批：

```text
PartyMemberState.current_hp
PartyMemberState.current_mp
PartyMemberState.current_aura
PartyMemberState.is_dead
PartyMemberState.race_id
PartyMemberState.subrace_id
PartyMemberState.age_years
PartyMemberState.body_size
PartyMemberState.body_size_category
PartyMemberState.bloodline_id
PartyMemberState.ascension_id
```

第三批：

```text
known_active_skill_ids
cooldowns
charge dictionaries
damage_resistances
member_states
active_member_ids
reserve_member_ids
```

字段可见性建议：

1. 普通运行时字段改为 `private` backing field，加 public/internal getter。
2. 需要 Godot 序列化的字段，保留最小可见性，并用专用 payload 方法隔离。
3. 跨文件同 assembly 需要访问时，优先提供 `internal` 方法，而不是 `internal` 字段。

验收：

1. 编译期阻止外部直接写第一批字段。
2. 生产代码通过接口完成同等行为。

### 阶段 5：增加自动化边界检查

增加脚本或测试，扫描禁止模式。

建议检查：

```text
\.current_hp\s*=
\.current_mp\s*=
\.current_stamina\s*=
\.current_aura\s*=
\.current_ap\s*=
\.current_move_points\s*=
\.is_alive\s*=
\.is_dead\s*=
\.coord\s*=
\.body_size\s*=
\.body_size_category\s*=
\.status_effects\s*=
\.known_active_skill_ids\s*=
\.member_states\s*=
```

规则：

1. `scripts/` 默认不允许命中。
2. owner 类型自身文件允许命中。
3. schema/payload restoration 测试允许命中，但要有白名单。
4. CI 中失败时输出命中文件和推荐接口。

验收：

1. 新增 direct field write guard。
2. guard 被纳入 regression suite 或独立 CI 步骤。

## 存档与兼容策略

存档 payload 字段名可以保持不变，避免破坏历史存档。变化发生在对象内部：

1. `ToDictionary()` 继续输出原字段名。
2. `FromDictionary()` 内部不再直接 object initializer 写字段，而是调用 restore 接口。
3. restore 接口可接受 payload 的历史格式，但必须在 owner 内部规范化。
4. 新增字段时要更新 exact field list 和 schema regression。

示例：

```csharp
internal void RestoreVitalsFromPayload(int hp, int mp, int aura, bool isDead)
{
    current_hp = Math.Max(hp, 0);
    current_mp = Math.Max(mp, 0);
    current_aura = Math.Max(aura, 0);
    is_dead = isDead || current_hp <= 0;
}
```

## AI 和只读上下文

AI 评分、AI 决策、预览系统默认只读。AI action 不应直接修改 `BattleUnitState` 或 `BattleState`。

允许：

1. AI 读取 `BattleStateReadView`。
2. AI 返回 `BattleCommand`。
3. Runtime resolver 执行 `BattleCommand` 并调用 owner 写接口。

禁止：

```csharp
context.unit_state.current_hp = 1;
context.state.SetUnit(mutatedUnit) // AI action 内直接改 runtime state
```

如果需要测试 mutation guard，可以保留专用 test action，但它应明确位于测试目录或测试用途类型中，并被 guard 标记为非法修改样本。

## 测试策略

### Owner 接口测试

每个 owner 增加 focused tests：

1. `PartyMemberState`：生命、死亡、复活、体型、血脉、飞升接口。
2. `BattleUnitState`：伤害、治疗、死亡同步、资源消耗、资源退还、位置刷新、状态效果 key 规范化。
3. `BattleState`：`SetUnit`、`RemoveUnit`、`SetCell`、`ClearCells` 触发 topology revision。
4. `BattleCellState`：occupant、move cost、高度、坐标接口。

### 回归测试

重点跑：

1. battle runtime tests。
2. battle rules tests。
3. battle AI mutation guard tests。
4. progression identity tests。
5. persistence save/load tests。
6. text runtime command tests。

### Schema 测试

schema tests 要验证：

1. payload 字段名不变。
2. `ToDictionary/FromDictionary` round-trip 不变。
3. 非法组合被拒绝或被 owner restore 规范化。
4. `body_size/body_size_category` 不一致时拒绝。

## 验收标准

短期验收：

1. 新增 owner 接口。
2. 生产代码高风险写字段改为接口调用。
3. read view 不暴露可变集合。
4. 核心 regression 通过。

中期验收：

1. `scripts/` 下不再直接写第一批高风险字段。
2. 测试 fixture 使用 builder。
3. direct field write guard 能阻止新增违规写法。

长期验收：

1. 高风险字段变为 private 或仅 owner 内可写。
2. 所有跨 owner 修改都能在方法名中看出业务意图。
3. 新增类型必须在归属矩阵中定义 owner、读接口、写接口和不变量。

## 风险与处理

### Godot 序列化风险

风险：部分 public 字段可能被 Godot 序列化或 inspector 依赖。

处理：

1. 先补接口，不立即改字段可见性。
2. 收紧字段前确认 `ToDictionary/FromDictionary` 和 `.tres` 资源加载路径。
3. Content `Resource` 类型单独处理，不和 runtime state 一起收紧。

### 测试改造量大

风险：测试中大量直接写字段，统一改造成本高。

处理：

1. 先改生产代码。
2. 再引入 builder 逐批替换测试。
3. schema 测试保留 payload 级构造能力。

### 行为回归

风险：统一接口后 clamp 和死亡同步规则可能改变旧行为。

处理：

1. 先记录现有行为。
2. 每个接口配 focused regression。
3. 对有争议的规则，例如死亡是否清空 MP/Aura，在接口注释和测试中固定。

## 推荐落地顺序

1. `BattleUnitState` 生命、资源、位置、状态效果接口。
2. `BattleState` 单位和格子索引接口定稿。
3. `PartyMemberState` 长期生命、身份、体型、血脉、飞升接口。
4. 替换 `scripts/systems/battle` 生产代码直接写字段。
5. 替换 `scripts/systems/progression` 生产代码直接写字段。
6. 增加 test builders。
7. 收紧第一批字段可见性。
8. 增加 direct field write guard。
9. 收紧第二、第三批字段。

## 后续实施 checklist

- [ ] 给 `BattleUnitState` 增加生命和资源接口。
- [ ] 给 `BattleUnitState` 增加技能集合和状态集合只读接口。
- [ ] 给 `BattleState` 补齐 view getter 和批量 set 方法。
- [ ] 给 `PartyMemberState` 增加长期生命、身份、体型、血脉、飞升接口。
- [ ] 修改战斗生产代码，移除第一批字段直接写入。
- [ ] 修改人物生产代码，移除长期状态字段直接写入。
- [ ] 增加 owner 接口 regression。
- [ ] 增加测试 builder。
- [ ] 增加 direct field write guard。
- [ ] 收紧高风险字段可见性。
