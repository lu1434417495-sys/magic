# 通用人物/装备特性系统设计计划

更新日期：`2026-06-19`

## 状态

- 当前状态：`Proposed Design Plan`
- 范围：`TraitDef` / `TraitInstanceState` / 人物永久特性 / 装备随机特性 / 战斗 effective trait 投影。
- 原类型 / 方法体 / roll 算法 / 序列化与校验方案：见 [`trait_system_implementation_plan.md`](trait_system_implementation_plan.md)。
- 兼容策略：不兼容旧存档；不添加 legacy alias、fallback migration 或旧 payload/schema 支持。
- 验证策略：以 focused regression 为主；battle simulation 只用于后续数值或 AI 行为分析，不用于验证结构正确性。

## 背景与目标

当前项目已有一条身份特性链：

- `RaceTraitDef`
- `RaceTraitEffectKind`
- `TraitTriggerContentRules`
- `TraitTriggerHooks`
- `BattleUnitState.race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids`

这条链能承载种族、血脉、升华等身份特性，但不适合直接支持两类新需求：

- 人物可以通过奖励、剧情、成就、修炼等方式永久获得特性。
- 装备实例可以携带随机 roll 出来的特性，并在装备后把这些特性绑定到装备者身上。

目标是新增一个通用特性系统，把身份特性、人物永久特性、装备固定特性和装备随机特性纳入同一套定义、实例、聚合与战斗投影模型。

## 核心原则

- `TraitDef` 是内容定义真相源。
- `TraitInstanceState` 是实例事实源。
- `EffectiveTraitSet` / `EffectiveTraitInstance` 是运行时聚合结果。
- `effective_trait_ids` 只是派生查询投影，不能作为事实源。
- 人物永久特性和装备实例特性必须分开存储。
- 装备特性只绑定当前装备者，不写回人物永久特性。
- 属性型特性进入属性快照；触发型特性进入战斗触发器。
- `CharacterTraitService` 只负责聚合和解析，不负责随机 roll、不负责触发器执行、不直接修改 battle state。
- `trigger_type` 只表示行为触发时机，不表示 charge 生命周期；charge 的 reset/seed 由独立字段和 effect policy 决定。
- 战斗中的 `effective_trait_instances` 是反规范化 typed canonical state；进行中的战斗存档不再依赖 `TraitContentRegistry` 重新解释 trait 行为，只有 `BattleUnitState.ToDictionary()/FromDictionary()` 存档边界把它序列化成 payload dictionary。

### 字典使用边界

本系统正式代码除存档序列化和 UI 适配边界外，不允许使用 dictionary 作为数据模型、私有索引、runtime payload 或资源动态参数：

- 不允许新增 `Dictionary<StringName, T>` / `IReadOnlyDictionary<StringName, T>` 作为 trait 聚合、装备 roll、战斗投影、触发器或 AI guard 的正式输入、缓存或查询索引。需要查询时使用 typed list/array + owner 方法线性查找，除非另开设计并由用户确认。
- 不允许新增 `Godot.Collections.Dictionary` 作为 `.tres` trait 动态字段、battle runtime state、effective trait payload、roll value backing store 或服务间 DTO。
- 允许使用 `Godot.Collections.Dictionary` 的位置仅限 `ToDictionary()/FromDictionary()`、strict save payload parser、既有 UI 适配投影、测试构造 save payload 的边界。进入正式 runtime 前必须转换成 typed state。
- `TraitDef.@params` 禁止出现；effect-specific 配置必须落成显式 typed 字段，例如 `highest_roll_compare_key`、`vision_range`、`proficiency_choice_count`。
- `TraitInstanceState.roll_values` 在内存中是 `Array<TraitRollValueState>`，由 `TraitDef.roll_value_schema` 约束；只有 party/equipment/battle save payload 的 `roll_values` 字段序列化为 dictionary。
- `BattleUnitState.effective_trait_instances` 在内存中是 `Array<BattleEffectiveTraitInstanceState>`；只有 battle save schema 的 `effective_trait_instances` 字段序列化为 dictionary 数组。
- 必须保留 `tests/progression/schema/run_trait_dictionary_boundary_regression.cs`，防止 trait 正式路径回退到 dictionary-backed runtime data。

## 决策冻结（实施前置）

以下规则必须在写任何聚合/触发/属性代码之前定稿，因为它们是后续所有阶段的硬约束。

### stack policy 语义

- `unique_by_trait`：同一 `trait_id` 只保留一个有效实例。
- `highest_roll`：同一 `trait_id` 保留指定 roll key 数值最高的实例。
- `additive`：同一 `trait_id` 多实例叠加数值，但仍视为一个触发单元。
- `stack_by_instance`：每个实例独立生效、独立触发。

### `effective_instance_key` 派生规则

分两步，避免「每实例 key」与「stack 收敛后 key」混淆：

1. **raw key（stack 收敛前，每个原始实例）**，必须跨 clone/回合/存档稳定：
   - identity 来源（无 `trait_instance_id`）：`identity::{source_id}::{trait_id}`。
   - equipment_fixed 来源（无 `trait_instance_id`）：`equipment_fixed::{equipment_instance_id}::{trait_id}`；不得用 `item_id`，否则同一模板的两件装备会碰撞。
   - character / equipment_roll 来源：`trait_instance_id`（mint 时生成，永久持久）。
2. **final `effective_instance_key`（charge key）**，由 stack policy 收敛后产出：
   - `unique_by_trait` / `highest_roll` / `additive`：收敛成单触发单元，key = `trait_id`（与旧 charge 行为兼容）。
   - `stack_by_instance`：保留多触发单元，每个的 key = 其 raw key。

聚合层负责产出 key，触发层不得自行重新拼装。详见实现细则第 8 节 `_rawKey` / `_applyStackPolicies`。

### charge key 策略

战斗触发 charge 不再默认用 `trait_id` 作 key（现有 `TraitTriggerHooks` 以 `trait_id` 写 `per_battle_charges`/`per_turn_charges`，多实例会互相清零）。

- charge key = `effective_instance_key`。
- `unique_by_trait` / `highest_roll` / `additive` 的 final charge key 等于 `trait_id`，与旧单触发单元行为兼容。
- 中途换装新增触发型 trait 时，必须在重聚合后补种该 trait 的 charge（见战斗系统接入）。

### trigger 与 charge 生命周期拆分

现有资源中存在 `trigger_type=passive` 但仍需要在战斗开始或回合边界建立状态的 trait。为避免把旧 `trigger_type` 误用成 charge 过滤条件，第一版冻结为：

- `trigger_type` / `TraitTriggerKind` 只用于行为分发，例如自然 1、暴击、致死伤害、battle start handler、turn start handler。
- charge 是否存在、何时 reset/seed，由 `charge_scope` 与 `charge_reset_timing` 表达；没有 charge 的 trait 使用 `none`。
- `OnBattleStart` charge 初始化必须遍历所有 charge-bearing trait，不得只遍历 `trigger_type == on_battle_start` 的 trait。
- `OnTurnStart` charge 初始化 / 重置必须遍历 `charge_reset_timing == turn_start` 的 trait，不得误过滤掉 `trigger_type=passive` 的 trait。
- 中途换装重聚合时，新增 effective key 只根据其 charge policy 补种；移除 effective key 清理对应 charge。

建议字段语义：

```text
trigger_type: behavior dispatch timing
charge_scope: none | per_turn | per_battle
charge_reset_timing: none | battle_start | turn_start
```

`TraitTriggerHooks.GetEffectiveInstances(unit, triggerKind)` 仍按 `trigger_type` 过滤；`SeedChargeForKey` / battle-start seeding 走 `charge_scope` 和 `charge_reset_timing`。

### 属性数值单一真相

身份 trait 迁移成 `TraitDef` 后，其属性数值只能由一侧表达，禁止双算：

- 身份属性数值继续留在 `RaceDef.attribute_modifiers` 等身份内容定义，由 `AttributeService` 内部解析。
- 迁移出来的身份 `TraitDef` 不携带 `attribute_modifiers`，只携带行为 / 触发语义。
- item / content validator 必须校验身份来源 `TraitDef` 不含 `attribute_modifiers`。

### source scope 语义

每个 `TraitDef` 必须声明可被哪些来源引用，避免身份 trait 被装备 roll 到，或装备 trait 被 race/subrace 误引用。

- `allowed_source_kinds` 为空视为非法；不使用“默认允许所有来源”。
- race/subrace/bloodline/ascension 等身份内容只能引用允许 `identity` 的 trait。
- `PartyMemberState.trait_instances` 只能保存允许 `character` 的 trait。
- `ItemDef.trait_ids` 只能引用允许 `equipment_fixed` 的 trait。
- `ItemDef.trait_roll_groups` 只能引用允许 `equipment_roll` 的 trait。
- 允许 `identity` 的 trait 不得携带 `attribute_modifiers`，除非另开设计把身份属性的单一真相从身份 def 迁到 trait def。

`TraitContentRules` 需要提供 `IsSourceKindAllowed(TraitDef def, TraitSourceKind kind)`，所有 validator 和 `CharacterTraitService` 聚合入口都走同一规则。

## 内容定义

新增 `TraitDef` 作为通用特性定义资源，沿用 `RaceTraitDef` / `ItemDef` 的 `[GlobalClass] Resource` + `[Export]` 约定，`StringName` 字段在边界存字符串、逻辑走 typed enum：

```csharp
[GlobalClass]
public partial class TraitDef : Resource
{
    [Export] public StringName trait_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";

    // 分类标签，仅用于查询 / UI / validator 分组，不参与战斗判定。
    [Export] public Godot.Collections.Array<StringName> categories { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> allowed_source_kinds { get; set; } = new();

    // 边界存 StringName，逻辑经 typed enum/rules。trigger_type 只表示行为触发时机。
    [Export] public StringName effect_type { get; set; } = "";          // -> TraitEffectKind
    [Export] public StringName trigger_type { get; set; } = "passive";  // -> TraitTriggerKind
    [Export] public StringName stack_policy { get; set; } = "unique_by_trait"; // -> TraitStackPolicyKind
    [Export] public StringName charge_scope { get; set; } = "none";     // -> TraitChargeScopeKind
    [Export] public StringName charge_reset_timing { get; set; } = "none"; // -> TraitChargeResetTimingKind

    // Effect-specific 配置必须显式 typed，不使用 params dictionary。
    [Export] public StringName highest_roll_compare_key { get; set; } = "";
    [Export] public int vision_range { get; set; } = 0;
    [Export] public int proficiency_choice_count { get; set; } = 0;

    // 属性型效果。身份来源 TraitDef 必须留空（数值单一真相，见决策冻结）。
    [Export] public Godot.Collections.Array<AttributeModifier> attribute_modifiers { get; set; } = new();

    // 声明本 trait 允许的 roll key 及其取值约束；roll 实例必须匹配此 schema。
    [Export] public Godot.Collections.Array<TraitRollValueSchemaEntry> roll_value_schema { get; set; } = new();

    internal TraitEffectKind EffectKind
    {
        get => TraitContentRules.ToEffectKind(effect_type);
        set => effect_type = TraitContentRules.ToStringName(value);
    }
    internal TraitTriggerKind TriggerKind => TraitTriggerContentRules.ToTriggerKind(trigger_type);
    internal TraitStackPolicyKind StackPolicyKind => TraitContentRules.ToStackPolicyKind(stack_policy);
    internal TraitChargeScopeKind ChargeScopeKind => TraitContentRules.ToChargeScopeKind(charge_scope);
    internal TraitChargeResetTimingKind ChargeResetTimingKind => TraitContentRules.ToChargeResetTimingKind(charge_reset_timing);
}
```

`roll_value_schema` 用 typed 子资源声明每个 roll key 的类型与边界：

```csharp
internal enum TraitRollValueType { Unknown = 0, Int, StringName, Bool }

[GlobalClass]
public partial class TraitRollValueSchemaEntry : Resource
{
    [Export] public StringName key { get; set; } = "";
    [Export] public StringName value_type { get; set; } = "int"; // -> TraitRollValueType
    [Export] public int min_value { get; set; }                  // Int 类型用
    [Export] public int max_value { get; set; }                  // Int 类型用
    [Export] public Godot.Collections.Array<StringName> allowed_values { get; set; } = new(); // StringName 枚举用
    internal TraitRollValueType ValueTypeKind => TraitContentRules.ToRollValueType(value_type);
}
```

### Typed enum 与 rules

不要把正式分发退回松散字符串比较。新增 typed enum，并由 `TraitContentRules` 提供 `StringName <-> enum` 双向映射（沿用 `RaceTraitDef.ToEffectKind/ToStringName` 与 `TraitTriggerContentRules` 的模式）：

```csharp
internal enum TraitEffectKind
{
    Unknown = 0,
    // 第一版需要 typed 触发 handler 的三个：
    HalflingLuck, SavageAttacks, RelentlessEndurance,
    // 其余从 RaceTraitEffectKind 迁移过来的身份效果（Darkvision/DamageResistance/... ），
    // 第一版不接 trigger handler，仅作分类与属性/静态投影归属。
}

internal enum TraitStackPolicyKind { Unknown = 0, UniqueByTrait, HighestRoll, Additive, StackByInstance }

internal enum TraitSourceKind { Unknown = 0, Identity, Character, EquipmentFixed, EquipmentRoll }

internal enum TraitChargeScopeKind { Unknown = 0, None, PerTurn, PerBattle }

internal enum TraitChargeResetTimingKind { Unknown = 0, None, BattleStart, TurnStart }
```

`TraitTriggerKind` 复用现有 `TraitTriggerContentRules` 中的枚举（`Passive/OnNaturalOne/OnCrit/OnFatalDamage/OnBattleStart/OnTurnStart`），不重复定义。

触发器分发保持 typed `TraitTriggerDispatchRule[]` 规则列表，第一版只注册 `HalflingLuck/SavageAttacks/RelentlessEndurance` 三条；不得用 dictionary 作为分发规则表或 runtime cache。

Phase 1 既然迁移现有 race trait 内容，`TraitEffectKind` 与映射表必须一次性覆盖当前 `RaceTraitEffectKind` 的全部值。未接 trigger handler 的效果也要能被 registry/validator 识别，不能在资源迁移后因为 effect_type unknown 导致内容加载失败。落码前必须列出 `RaceTraitEffectKind -> TraitEffectKind/StringName` parity 表，并加内容数量 parity 断言。

### RaceTraitDef 迁移策略

`RaceTraitDef` 不应被当成简单可改名类型。现有实现绑定：

- `RaceTraitEffectKind`
- `RaceTraitContentRegistry`
- `race_trait_defs` content bucket
- progression phase2 trait reference validation
- 既有战斗 trigger regression

迁移应分阶段完成：

1. 新增 `TraitDef` / `TraitContentRegistry` / `trait_defs` bucket。
2. 让 race/subrace/bloodline/ascension 等身份内容的 `trait_ids` 引用 `TraitDef`。
3. 保持旧 battle schema 暂时可测，先验证通用 trait 内容加载与引用。
4. 战斗投影稳定后，再删除旧 `RaceTraitDef` / `race_trait_defs` 正式入口。

`.tres` 迁移不能只移动文件：每个资源的 `ext_resource` / `script_class` / script path 也必须从 `RaceTraitDef.cs` 改为 `TraitDef.cs`，目标目录为 `res://data/configs/traits`。Phase 5 删除旧目录前，必须用资源加载回归证明旧 `race_trait_defs` 不再被 `project.godot`、content bucket、validator 或测试 fixture 引用。

## 实例状态

新增 `TraitInstanceState`，保存人物永久特性和装备随机特性的实例信息。沿用 `EquipmentInstanceState` 的 `RefCounted` + 严格 `ToDictionary`/`FromDictionary`/`GetPayloadValidationError` 模式：

```csharp
public partial class TraitInstanceState : RefCounted
{
    private static readonly Godot.Collections.Array<string> STRICT_FIELDS = new()
    {
        "trait_instance_id", "trait_id", "source_type", "source_id",
        "rank", "stacks", "roll_values",
    };

    public StringName trait_instance_id = ""; // character/equipment 实例稳定 id；identity 来源不建实例
    public StringName trait_id = "";
    public StringName source_type = "";        // -> TraitSourceKind
    public StringName source_id = "";          // race_id / item instance_id / 奖励来源 id 等
    public int rank = 1;
    public int stacks = 1;
    public Godot.Collections.Array<TraitRollValueState> roll_values = new(); // runtime typed entries；save 边界才投成 dictionary

    internal TraitSourceKind SourceKind => TraitContentRules.ToSourceKind(source_type);

    // 正式读取一律走 typed helper，不直接回读 Godot dictionary。
    public int GetIntRoll(StringName key, int fallback = 0);
    public StringName GetStringNameRoll(StringName key, StringName fallback = default);
    public bool GetBoolRoll(StringName key, bool fallback = false);

    public Godot.Collections.Dictionary ToDictionary();
    public static TraitInstanceState FromDictionary(Godot.Collections.Dictionary data);
    public static string GetPayloadValidationError(Godot.Collections.Dictionary data);
    public TraitInstanceState DuplicateState(); // 深拷贝 roll_values，clone/AI 投影用
}
```

`FromDictionary` 严格校验：字段集恰好等于 `STRICT_FIELDS`（多余 / 缺失即失败）、`rank`/`stacks` 为 int 且 `>= 1`、`roll_values` 为 Dictionary、`trait_id` 非空。`source_type` 必须能解析成非 `Unknown` 的 `TraitSourceKind`。

`roll_values` 在 save 边界投影为 dictionary，runtime typed helper 内部按 `TraitDef.roll_value_schema` 的 `value_type` 取值，wrong typed key 返回 fallback 而非静默转型。

### 人物特性

`PartyMemberState.trait_instances` 只保存人物永久获得的特性（奖励、剧情、成就、修炼结果）。身份来源 trait 不写入此字段，继续从 race/subrace/bloodline/ascension/stage 内容定义派生。

字段与序列化改动：

```csharp
// PartyMemberState 新增字段
public Godot.Collections.Array<TraitInstanceState> trait_instances = new();

// TO_DICT_FIELDS 末尾追加 "trait_instances"
// ToDictionary(): ["trait_instances"] = trait_instances 各元素 .ToDictionary() 组成的 Array
// FromDictionary(): 逐元素 TraitInstanceState.FromDictionary()，任一失败即整体失败（不静默丢弃）
//   解析后断言每个实例 SourceKind == Character（identity/equipment 来源不得出现在此字段）
// DuplicateState(): 对每个 TraitInstanceState 调 DuplicateState() 深拷贝
```

### 装备特性

`EquipmentInstanceState.trait_instances` 只保存装备实例随机 roll 出来的特性：

```csharp
// EquipmentInstanceState 新增字段
public Godot.Collections.Array<TraitInstanceState> trait_instances = new();

// requiredFields 由 4 项扩为 5 项，追加 "trait_instances"
//   并把当前 "恰好 4 字段" 的精确 count 校验改为 5。
// ToDictionary()/FromDictionary()/DuplicateState() 同步处理 trait_instances；
//   解析后断言每个实例 SourceKind == EquipmentRoll。
```

`ItemDef.trait_ids` 表示固定装备特性，由定义派生，不复制进每个 `EquipmentInstanceState`，避免内容更新后实例 stale。`ItemDef.trait_roll_groups` 用 typed 子资源声明随机词缀池：

```csharp
// ItemDef 新增字段
[Export] public Godot.Collections.Array<StringName> trait_ids { get; set; } = new();        // 固定特性，引用 TraitDef
[Export] public Godot.Collections.Array<TraitRollGroupDef> trait_roll_groups { get; set; } = new();

[GlobalClass]
public partial class TraitRollGroupDef : Resource
{
    [Export] public StringName group_id { get; set; } = "";
    [Export] public int roll_count { get; set; } = 1;        // 从本组抽取的特性数量
    [Export] public Godot.Collections.Array<TraitRollGroupEntryDef> entries { get; set; } = new();
}

[GlobalClass]
public partial class TraitRollGroupEntryDef : Resource
{
    [Export] public StringName trait_id { get; set; } = "";       // 引用 TraitDef
    [Export] public int weight { get; set; } = 1;                 // 加权抽取，必须 > 0
    [Export] public StringName exclusive_group { get; set; } = ""; // 同 exclusive_group 内至多命中一个
}
```

roll 出的具体 `roll_values` 由被命中 `TraitDef.roll_value_schema` 的区间生成，不在 group entry 里重复声明，避免内容漂移。

item/content validator 校验：

- `trait_ids` 与每个 entry 的 `trait_id` 在 `TraitContentRegistry` 中存在。
- `weight > 0`；`roll_count` 介于 `1` 与该组可命中数量之间。
- `exclusive_group` 命中互斥规则可满足（不会出现 roll_count 超过互斥约束后的最大可命中数）。
- 被命中 trait 的 `roll_value_schema` 自洽（Int 有 `min<=max`，StringName 有非空 `allowed_values`）。
- `trait_ids` 只能引用允许 `equipment_fixed` 的 trait；roll group 只能引用允许 `equipment_roll` 的 trait；身份来源 `TraitDef`（只允许 `identity`）不得出现在装备字段中。
- `roll_count` 不得在 mint 时静默 clamp。内容非法应由 validator 拒绝；生产 mint 只处理已验证内容。

`ItemDef.MergeWithTemplate()` 的 trait 继承语义必须固定，避免模板和实例定义产生隐式覆盖：

- `trait_ids`：模板与实例数组 merge + 去重，保持模板顺序优先，实例新增项追加。
- `trait_roll_groups`：以 `group_id` 为 key；实例定义中同 `group_id` 的 group 替换模板 group；新 `group_id` 追加。
- 第一版不支持隐式清空模板 trait。若后续需要“清空继承 trait”，必须先增加显式字段，例如 `clear_inherited_traits` 或 `trait_inheritance_mode`，不能把空数组解释为清空。
- item validator 不直接查询全局单例；由 content validation 调用方显式传入 trait catalog，或新增独立 `ItemTraitContentValidator.Validate(itemDefs, traitDefs)`。

装备实例的 trait 处理必须区分两种语义，不能一刀切走同一条路径：

```csharp
public sealed class EquipmentTraitRollService
{
    // 仅真正新建实例调用：按 ItemDef.trait_roll_groups 加权抽取，
    // 命中后按 TraitDef.roll_value_schema 区间生成 roll_values，写回实例的 trait_instances。
    // rng 由调用方注入（掉落用世界 rng，固定种子 fixture 用确定性 rng）。
    public void MintWithRolls(EquipmentInstanceState instance, ItemDef itemDef, RandomNumberGenerator rng);

    // 加载 / 克隆 / AI 投影调用：零 RNG，仅深拷贝既有 trait_instances（DuplicateState），
    // 用于校验实例的 trait_instances 与 itemDef 引用一致性，但不重新抽取。
    public void RehydrateOrClone(EquipmentInstanceState instance, ItemDef itemDef);
}
```

- `MintWithRolls()`：仅在真正新建实例时调用，执行随机 roll，写入 `roll_values`。
- `RehydrateOrClone()`：加载存档、克隆、AI 投影时调用，零 RNG，原样保留已有 `trait_instances` / `roll_values`。

如果把加载/克隆也走 roll，会改写存档 roll 结果并在 AI 投影里引入 RNG，破坏 battle simulation 多进程可复现性。

现有创建点必须逐一归属（已核实）：

- 掉落 / loot transient 创建阶段如果还没有稳定 `instance_id`，不得 mint；必须等稳定装备实例 id 分配后再 mint，或由调用方先分配稳定 id 再调用 mint。
- `EquipmentDropService.RollItemInstances` 只能在实例已有稳定 id 时 mint；否则先产出无 trait 的 transient，commit 到仓库 / 队伍时分配 id 后再 mint。
- `PartyWarehouseService.CreateTransientInstance` / 直接创建路径：id allocator 产出稳定 `instance_id` 后 mint。
- `GameSession.CreateInstance`：仅当调用语境是“新装备实例”且已有稳定 id 时 mint；加载、克隆、预览、AI 投影一律 rehydrate/clone。
- `EquipmentInstanceState.FromDictionary` / `FromTransientLootDictionary` / `DuplicateState` → rehydrate/clone，保留 roll。
- `BattleAiMutationGuard` 的实例构造 → clone，保形、零 RNG。
- `BattleSimFormalCombatFixture` / `HeadlessGameTestSession` 的 fixture → 注入预 roll 实例，不在 sim/test 内掷 RNG（除非用固定种子显式 mint）。

battle simulation 与 auto-tuning fixture 必须使用预 roll 实例，绝不在 sim 时刻 roll。

`EquipmentTraitRollService.MintWithRolls()` 必须拒绝空 `instance_id`，否则会产生 `_t01` 这类不稳定 `trait_instance_id`。预览 UI 若需要展示可能 roll 出的 trait，只能使用非持久 preview id，并且不得把 preview roll 写回正式 `EquipmentInstanceState`。

## 有效特性聚合

新增 `CharacterTraitService`，放在 progression/character management owner 附近，由 `CharacterManagementModule` setup 和调用。

聚合顺序固定为：

```text
identity -> character -> equipment
```

来源含义：

- `identity`：race/subrace/bloodline/ascension 等内容定义派生特性。
- `character`：`PartyMemberState.trait_instances` 中的人物永久特性。
- `equipment`：当前装备 view 中 `ItemDef.trait_ids` 固定特性和 `EquipmentInstanceState.trait_instances` 随机特性。

服务与 DTO 签名：

```csharp
public sealed class CharacterTraitService
{
    // 由 CharacterManagementModule setup；可传 equipment_state_override 复用现有
    // build_attribute_source_context 的换装预览路径。
    public EffectiveTraitSet BuildEffectiveTraits(
        StringName memberId,
        EquipmentState equipmentStateOverride = null
    );

    // 把有效特性中属性型部分解析为 AttributeModifier（含 source_type 区分），
    // 供 AttributeSourceContext.trait_attribute_modifiers 使用。
    public List<AttributeModifier> ResolveTraitAttributeModifiers(EffectiveTraitSet set);
}

public sealed class EffectiveTraitInstance
{
    public StringName TraitId;
    public TraitDef TraitDef;                  // 运行时引用，不序列化
    public TraitInstanceState TraitInstance;   // identity 来源为 null
    public TraitSourceKind SourceKind;
    public StringName SourceId;
    public StringName EffectiveInstanceKey;    // 聚合层产出，charge key 即用它
    public TraitStackPolicyKind StackPolicy;
    public TraitChargeScopeKind ChargeScope;
    public TraitChargeResetTimingKind ChargeResetTiming;
    public int Rank;
    public int Stacks;
    public Godot.Collections.Array<TraitRollValueState> RollValues; // runtime typed roll entries；identity/fixed 来源为空。
}

public sealed class EffectiveTraitSet
{
    public IReadOnlyList<EffectiveTraitInstance> Instances { get; }
    public bool TryGetByKey(StringName effectiveInstanceKey, out EffectiveTraitInstance instance);
    public IReadOnlyList<EffectiveTraitInstance> GetByTraitId(StringName traitId);

    // 派生投影：仅 UI/查询/trace 用，去重排序，不参与判定。
    public IReadOnlyList<StringName> DeriveTraitIds();
    // 战斗 typed state：每元素含 trait_id/effective_instance_key/source/effect/trigger/charge/rank/stacks/roll_values。
    public Godot.Collections.Array<BattleEffectiveTraitInstanceState> ToBattleEffectiveInstances();
}
```

`EffectiveTraitInstance` 至少包含上列字段。`effective_instance_key` 按「决策冻结」中的派生规则生成，由聚合层产出，触发层不得自行拼装。

`trait_def` 在 `EffectiveTraitInstance` 中只是运行时 Resource 引用，不进任何序列化 payload。进入战斗后，payload 里的 `effect_type` / `trigger_type` / `charge_scope` / `charge_reset_timing` 是反规范化行为契约；battle save/load 使用 payload 自身，不从 `TraitContentRegistry` 重新解释。非战斗 party/equipment 存档加载仍从 registry 校验 `TraitInstanceState.trait_id`。

`effective_trait_ids` 从 `EffectiveTraitSet` 派生，只用于 UI、查询和 trace，不参与正式叠加或触发判定。

### 叠加策略

stack policy 语义与 charge key 策略已在「决策冻结」定稿，聚合实现直接遵循该定义，不在此重复或另立默认值。

实现要点：

- 聚合按 stack policy 收敛实例集合后，为每个有效实例产出 `effective_instance_key`。
- `highest_roll` 必须指定参与比较的 roll key，避免无序退化。
- `additive` 收敛成单触发单元；`stack_by_instance` 保留多触发单元。
- charge key 一律等于 `effective_instance_key`，确保多实例 charge 相互隔离。
- 因此 additive 的多个实例共享最终 charge key `trait_id`；只有 `stack_by_instance` 保证同 trait 多实例 charge 隔离。测试矩阵必须分别覆盖这两种语义，不能把它们合并断言为“都隔离”。
- 第一版 `roll_values` 不会隐式改写 `AttributeModifier.GetValueForRank()` 的结果。若某个 trait 需要随机属性数值，必须先增加显式 roll-backed attribute binding schema；在此之前，validator 应拒绝“声明 roll_value_schema 但期望 attribute_modifiers 自动吃 roll”的内容。

## 属性系统接入

现状澄清：`AttributeService` 当前是混合模型。`equipment_state` / `passive_state` / `temporary_effects` 是预解析的 `List<AttributeModifier>`，但 `race_def` / `subrace_def` / `bloodline_def` / `ascension_def` 仍由 `AttributeService` 内部解析。本计划不改变身份 def 的内部解析路径。

trait attribute modifiers 采用与 `equipment_state` 相同的「预解析后传入」模式：`AttributeService` 不直接查询 trait catalog、item def、equipment instance 或 character state。

`CharacterTraitService.ResolveTraitAttributeModifiers()` 把有效特性解析为 `AttributeModifier`，通过 `AttributeSourceContext` 新增字段传入：

```csharp
// AttributeSourceContext 新增字段（与 equipment_state/passive_state 同模式）
public List<AttributeModifier> trait_attribute_modifiers = new();

// AttributeService 在 pipeline 中追加：
//   AppendTraitModifierEntries(entries, _trait_attribute_modifiers);
```

每条解析出的 `AttributeModifier.source_type` 用以下常量区分（沿用 `AttributeModifier.source_type` 字段）：

```csharp
// trait attribute modifier source_type 常量
private static readonly StringName SourceTraitIdentity = "trait_identity";
private static readonly StringName SourceTraitCharacter = "trait_character";
private static readonly StringName SourceTraitEquipmentFixed = "trait_equipment_fixed";
private static readonly StringName SourceTraitEquipmentRoll = "trait_equipment_roll";
// source_id = effective_instance_key，便于 trace 回溯到具体实例。
```

避免双算（数值单一真相见「决策冻结」）：

- 身份属性数值留在 `RaceDef.attribute_modifiers` 等身份 def 由 `AttributeService` 解析；迁移出来的身份 `TraitDef` 不带 `attribute_modifiers`，因此 `trait_identity` 实际不产出属性 entry（保留常量供未来非身份 identity-source 用）。
- 装备 `ItemDef.attribute_modifiers` 与装备 trait attribute modifiers 都生效，但来源用上面常量区分。

## 战斗系统接入

### BattleUnitState

最终目标是用统一字段替换旧分组 trait 投影：

```csharp
// 新字段（Phase 4a 先 additive 加入，4b 删旧字段）
public Godot.Collections.Array<BattleEffectiveTraitInstanceState> effective_trait_instances = new();
public GStringNameArray effective_trait_ids = new();              // 派生投影，不独立维护

// 删除字段（Phase 4b）
//   race_trait_ids / subrace_trait_ids / bloodline_trait_ids / ascension_trait_ids
```

`effective_trait_instances` 的每个内存元素是 `BattleEffectiveTraitInstanceState`，字段形如：

```text
{
  "trait_id": StringName,
  "effective_instance_key": StringName,
  "source_type": StringName,   // -> TraitSourceKind
  "source_id": StringName,
  "effect_type": StringName,   // -> TraitEffectKind，反规范化以免战斗进程查 registry
  "trigger_type": StringName,  // -> TraitTriggerKind
  "charge_scope": StringName,  // -> TraitChargeScopeKind
  "charge_reset_timing": StringName, // -> TraitChargeResetTimingKind
  "rank": int,
  "stacks": int,
  "roll_values": Array<TraitRollValueState>, // runtime typed entries；save 边界才投成 dictionary
}
```

序列化要点：

- `ToDictionary()` 写 `effective_trait_instances`（save payload 数组）+ `effective_trait_ids`（派生字符串数组）；strict 字段集用新两字段替换旧四字段。
- `FromDictionary()` 严格解析 save payload 数组并立即转换成 `BattleEffectiveTraitInstanceState`；`trait_def` 不在 payload 中，战斗存档恢复时以反规范化 typed 字段为准，不从 `TraitContentRegistry` 解析；旧四字段在 Phase 4b 作为 extra field 拒绝。
- save payload parser 必须校验 `effect_type`、`trigger_type`、`charge_scope`、`charge_reset_timing` 都能解析为非 `Unknown` typed enum；不能只校验非空字符串。
- `Clone()` 深拷贝 typed state 数组（含 typed `roll_values`），不共享引用。
- AI mutation guard 把 `effective_trait_instances` 纳入 mutation 白名单 / 深拷贝，零 RNG。

替换会影响 strict schema、clone、`ToDictionary()`、`FromDictionary()`、AI mutation guard、battle save/load 和现有 tests，因此放在 Phase 4 分阶段执行。

`effective_trait_instances` 是 canonical battle typed state；`effective_trait_ids` 是派生投影，由 canonical 现算，不长期各自维护。

### PassiveStatusOrchestrator

现有 identity projection 不只是 trait id，还包括：

- vision tags
- proficiency tags
- save advantage tags
- damage resistances
- racial skill charges

迁移 trait schema 时不能误删这些静态战斗投影。需要保持一个清晰边界：

- 身份静态战斗状态投影继续归 passive/status projection 链。
- trait 实例聚合归 `CharacterTraitService`。

已核实：`RaceTraitResolver` 从 `RaceDef` / `SubraceDef` 的字段（`vision_tags` / `proficiency_tags` / `save_advantage_tags` / `damage_resistances` / `racial_granted_skills`）投影这些静态状态，与 `trait_ids` 无关。

因此迁移时只有 `trait_ids` 收集语义搬到 `CharacterTraitService` 聚合层；`RaceTraitResolver` 其余投影逻辑原样保留在 passive/status 链。`CharacterTraitService` 不接管身份静态战斗投影。

如果后续决定由 `CharacterTraitService` 同时负责全部身份战斗投影，需要单独确认并扩大测试范围。

### Charge 生命周期

`OnBattleStart` 负责为触发型 trait 种 charge。战斗内换装重聚合（`RefreshEquipmentProjection()`）若新增触发型 trait，此时 `OnBattleStart` 已过，必须在重聚合后对新增 trait 按 charge key 补种 charge；移除的 trait 对应 charge 也应清理。racial skill charges 仍由 passive 链负责，与 trait trigger charge 分属不同 key 命名空间，不得混淆。

### TraitTriggerHooks

`TraitTriggerHooks` 改为按 `EffectiveTraitInstance` 分发，而不是只按 trait id。现有 `GetUnitTraitIds(unitState)`（从旧四数组拼接）替换为从 `effective_trait_instances` 反序列化出的有效实例迭代：

```csharp
// 旧：foreach (StringName traitId in GetUnitTraitIds(unitState))
// 新：foreach (EffectiveTraitInstance eff in GetEffectiveInstances(unitState, triggerKind))
//
// GetEffectiveInstances 按 trigger 时机过滤（TraitDef.TriggerKind == triggerKind），
// 再经 (TraitEffectKind, TraitTriggerKind) -> handler 映射分发；
// charge 读写一律用 eff.EffectiveInstanceKey 作 key（不再用 trait_id）。
```

分发规则：

- `TraitDef.trigger_type` 决定触发时机（`TraitTriggerKind`）。
- `TraitDef.charge_scope` / `charge_reset_timing` 决定 charge 生命周期；charge 初始化不得用 `trigger_type` 过滤。
- `TraitDef.effect_type` 经 typed `TraitEffectKind` 映射到 handler，分发注册来自 `TraitTriggerContentRules.GetDispatchTriggerRules()` typed rule list。
- handler 通过 `eff.TraitInstance` 的 typed helper 读取 roll、通过 `eff.SourceKind/SourceId` 读 source metadata。
- charge key = `eff.EffectiveInstanceKey`（见决策冻结），多实例互相隔离。
- 现有 `AttackTraitTriggerResult` payload 字段不变，仅 charge_key 取值来源改变。

第一版需要保持现有三个核心触发行为空间不退化：

- `halfling_luck`：自然 1 重掷。
- `savage_attacks`：暴击额外武器骰。
- `relentless_endurance`：致死伤害保 1 HP。

## Age Stage Trait Policy

当前 age stage trait projection 在内容校验中仍被显式禁止。

默认策略：第一版继续禁止 age stage trait runtime projection。

如需把 age stage 纳入统一 trait 聚合，需要单独扩展：

- age content validation
- `CharacterTraitService` identity source 收集
- effective age source metadata
- battle projection regression

## 存档与兼容策略

本计划不兼容旧存档。

实施时必须（当前值已核实）：

- bump `PartyState.version`：3 → 4。
- bump `SaveSerializer._save_version`：7 → 8。
- bump `GameSession.SaveVersion`：7 → 8（与 `SaveSerializer._save_version` 保持一致）。
- `_save_index_version`（当前 3）不受影响，显式不动，避免误 bump。
- 更新 `PartyMemberState.TO_DICT_FIELDS`，加入 `trait_instances`，并同步 strict 解析。
- 更新 `EquipmentInstanceState` 的 `requiredFields` 数组与精确 count 校验（当前硬断言恰好 4 字段），加入 `trait_instances` 并改 count。
- `BattleUnitState` 无独立 version int，其 strict 字段集即契约；替换 trait 字段会使 in-progress 战斗存档失效，按不兼容策略可接受。
- `ItemDef` 为 `.tres`，无 version int，只需扩 validator 与 `MergeWithTemplate`。
- 更新 `PartyState` round-trip。
- 更新 save payload tests。
- 明确旧 payload 缺少 trait 字段时失败，而不是静默恢复。

不要添加：

- legacy aliases
- old field fallback
- empty-list migration
- old `race_trait_defs` runtime fallback

如果后续需要兼容旧档，必须另开设计并说明具体 breakage 与 migration policy。

## 实施分期

### Phase 0：决策冻结

- 定稿 stack policy 语义、`effective_instance_key` 派生规则、charge key 策略、身份 vs trait 属性数值归属。
- 无代码，纯设计，但是后续所有阶段的硬前置。

### Phase 1：内容层

- 新增 `TraitDef`、`TraitRollValueSchemaEntry`、`TraitEffectKind`/`TraitStackPolicyKind`/`TraitSourceKind`/`TraitRollValueType` 枚举、`TraitContentRules`（`StringName <-> enum` 映射）、`TraitContentRegistry`。
- 新增 `TraitChargeScopeKind` / `TraitChargeResetTimingKind`，并把 `trigger_type` 与 charge 生命周期分开校验。
- 接入 `ProgressionContentRegistry` / `GameContentCatalog` / progression content validation。owner checklist：
  - `ProgressionContentRegistry` 新增 `_traitDefs` / `_traitDefIndex` / `_traitContentRegistry`、typed getter、rebuild/dispose 生命周期、`ValidateTyped` 接入、validation source replacement / definition bucket 接入。
  - `ContentValidationRunner` 加入 `trait_defs` bucket、有效/无效 fixture、identity/item trait reference 校验。
  - `GameContentCatalog` 暴露 trait defs snapshot/getter/revision，供 runtime service 和 tests 注入。
  - `GameSession` / `CharacterManagementModule.setup` / headless fixture / sim fixture 显式传入 trait catalog，不使用隐式全局 registry。
- 新增 `trait_defs` bucket。
- 迁移现有 race trait 内容到通用 trait 资源，包含 `.tres` script/ext_resource 重写和 `RaceTraitEffectKind` 全量 mapping parity。
- 先不替换 `BattleUnitState` schema。

### Phase 2：状态与装备层

- 新增 `TraitInstanceState` strict schema。
- 给 `PartyMemberState` 增加人物永久 `trait_instances`。
- 给 `EquipmentInstanceState` 增加随机 `trait_instances`。
- 给 `ItemDef` 增加 fixed `trait_ids` 与 `trait_roll_groups`。
- 接入 `ItemContentRegistry.MergeWithTemplate` 和 item validator。
- 新增 `EquipmentTraitRollService`，拆成 `MintWithRolls()`（含 RNG）与 `RehydrateOrClone()`（零 RNG，保形）两条 API。
- 按「装备特性」创建点归属表逐一接入：mint 路径 roll，rehydrate/clone 路径保留 `roll_values`。

### Phase 3：聚合与属性层

- 新增 `CharacterTraitService`。
- 新增 `EffectiveTraitSet` / `EffectiveTraitInstance`。
- 接入 `CharacterManagementModule.build_attribute_source_context()`。
- 将 trait attribute modifiers 作为明确来源传给 `AttributeSourceContext`。
- 覆盖 identity、人物永久、装备固定、装备随机 trait 的 source metadata 与 stack policy。

### Phase 4a：战斗新路径并行（桥接，additive）

- `BattleUnitState` 新增 `effective_trait_instances` 字段，不删旧四数组。
- strict schema 在 4a 同时接受旧四数组与新字段；旧四数组是由新 payload 派生的 parity projection，不再作为正式真相源。
- `TraitTriggerHooks` 优先读非空 `effective_trait_instances`；仅桥接期为了 parity 可回退旧四数组。4b 后删除回退。
- 以 `effective_trait_instances` 为 canonical，派生旧四数组与 `effective_trait_ids`。
- `BattleUnitFactory` 开战投影 effective traits；`RefreshBattleUnit()` 重新聚合；`RefreshEquipmentProjection()` 战斗内换装后重新聚合并补种/清理 charge。
- `TraitTriggerHooks` 改为读 effective 集分发（仍保留旧路径）。
- 跑 dual-projection parity 回归，证明 `halfling_luck` / `savage_attacks` / `relentless_endurance` 在新旧路径行为一致。

### Phase 4b：收口（删除旧字段）

- parity 绿后删除 `race_trait_ids` / `subrace_trait_ids` / `bloodline_trait_ids` / `ascension_trait_ids`。
- 更新 strict schema、clone、`ToDictionary()` / `FromDictionary()`、AI mutation guard。
- 4b 后旧四数组在 payload 中必须作为 extra field 拒绝；只有 `effective_trait_instances` 与派生 `effective_trait_ids` 保留。
- `PassiveStatusOrchestrator` 只搬 `trait_ids` 收集语义，保留 vision/proficiency/save advantage/damage resistance/racial charge 投影。
- 桥接窗口只活这一段，收口即删，不长期维护双真相。

### Phase 5：清理与文档

- 删除旧 `RaceTraitDef` / `race_trait_defs` 正式入口，连带处理 `RaceTraitEffectKind` / `RaceTraitContentRegistry` / 被取代的 `TraitTriggerContentRules`，不留半迁移孤儿。
- 迁移 `scripts/ui/CharacterCreationWindow.cs` 中对 race trait / effect 的查询，特别是 Human Versatility 相关逻辑，改为 `TraitDef` / `TraitContentRegistry` 或 identity summary。
- 更新资源 fixture 和验证脚本。
- 更新 `docs/design/project_context_units.md` 的相关 CU read set 与 ownership 描述。

## 测试计划

### 内容与 Schema

- 新增 `tests/progression/identity/run_trait_content_registry_regression.cs`。
- 新增 `tests/progression/schema/run_trait_instance_state_schema_regression.cs`。
- 扩展 progression content validation，确认 `trait_defs` 是正式 bucket。
- 覆盖 `RaceTraitEffectKind -> TraitEffectKind/StringName` 全量 mapping parity 与 `.tres` script/ext_resource 迁移。
- 覆盖 `allowed_source_kinds`：identity/item fixed/item roll/character 的合法与非法引用。
- 扩展 `PartyMemberState`、`EquipmentInstanceState`、`PartyState` round-trip tests。
- 保存版本断言：top-level save version = 8，party version = 4，save index version 仍为 3。
- 旧 `race_trait_defs` 不再作为正式 runtime/content 入口。

### 装备与仓库

- 新增 `tests/equipment/run_equipment_trait_roll_regression.cs`。
- 扩展 equipment drop / warehouse batch swap / warehouse state validator tests。
- 验证装备、卸装、batch swap、warehouse round-trip 保留 `trait_instances`。
- 验证 item template merge 不丢 `trait_ids` / `trait_roll_groups`。
- 验证 `trait_roll_groups` 同 `group_id` 替换、不同 `group_id` 追加；空数组不清空模板 trait。
- 验证 `roll_count` 超过互斥约束后的可命中数会被 validator 拒绝，mint 不静默 clamp。
- 固定 RNG 验证 roll 结果，不断言概率分布。
- 验证加载存档 / `DuplicateState` / clone 不重 roll：`roll_values` 与铸造时一致且未消耗 RNG。
- 验证空 `instance_id` 调用 mint 失败，不产生 `_t01` 这类不稳定 trait instance id。

### 聚合与属性

- 新增 `tests/progression/identity/run_character_trait_service_regression.cs`。
- 覆盖聚合顺序 `identity -> character -> equipment`。
- 覆盖 ascension suppress 对原 race/subrace 的影响。
- 覆盖 source metadata、防御性拷贝、stack policy。
- 扩展 attribute context regression，确认 trait modifiers 进入快照。
- 确认 wrong typed key 不 fallback。
- 确认 save/load 后 `roll_values` key 统一 normalize 为 `StringName`，并用 `TraitDef.roll_value_schema` 验证类型与范围。
- 属性不双算：迁移身份 trait 的属性效果在快照中恰好出现一次。
- additive stack：两个同 `trait_id` 实例收敛为一个触发单元，charge key = `trait_id`，stacks 汇总。
- stack_by_instance：两个同 `trait_id` 实例 charge key 不同，消耗其一不影响其二。

### 战斗

- 重写 `tests/battle_runtime/skills/run_trait_trigger_regression.cs`。
- 新增或替换 character trait projection regression。
- 扩展 `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`。
- 覆盖开战投影、战斗内换装刷新、trigger dispatch、battle start、turn start。
- 覆盖 `trigger_type=passive` 但 `charge_reset_timing=battle_start/turn_start` 的 trait 仍会被正确 seed/reset。
- 旧 `race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids` 在新 schema 中作为 extra field 拒绝。
- clone / AI mutation guard 保形：`effective_trait_instances` 过 `BattleUnitState.Clone()` 与 AI 投影深拷贝后无损、无 RNG。
- 中途换装新增触发型 trait 的 charge 在重聚合后被正确补种；移除的 trait charge 被清理。
- `effective_trait_ids` 纯派生：篡改它对 trigger dispatch 无行为影响。
- battle save/load 使用反规范化 payload 作为行为真相源；移除 registry 后仍能恢复进行中战斗的 trait trigger 行为。
- Phase 4a 桥接期 dual-projection parity：三个核心触发在新旧路径行为一致。

### 推荐验证命令

```bash
dotnet build magic.csproj
python tests/run_regression_suite.py --pattern tests/progression
python tests/run_regression_suite.py --pattern tests/equipment
python tests/run_regression_suite.py --pattern tests/warehouse
python tests/run_regression_suite.py --pattern tests/battle_runtime
python tests/run_regression_suite.py --pattern tests/runtime
```

不要把 numeric battle simulation 加入常规验证。只有做数值模拟、平衡分析或 AI 行为分析时才单独运行 battle simulation。

## 非目标

- 第一版不实现完整词缀 UI。
- 第一版不做旧存档兼容。
- 第一版不做 age stage trait runtime projection。
- 不用 battle simulation 验证 trait schema、聚合顺序、roll determinism 或 trigger 单点行为。
- 不把 `CharacterTraitService` 扩展成战斗规则总管。

## 项目上下文影响

真正实施代码后，需要更新 `docs/design/project_context_units.md`，至少涉及：

- CU-02：Save / Session / Registry
- CU-10：背包 / 装备 / 物品
- CU-11：队伍与成员状态模型
- CU-12：CharacterManagement 桥接
- CU-13：Progression 内容定义
- CU-14：Progression 规则与属性服务
- CU-15：战斗运行时总编排
- CU-16：战斗规则 / AI / 伤害

本文件只是设计计划；在代码未实施前，不更新上下文索引。
