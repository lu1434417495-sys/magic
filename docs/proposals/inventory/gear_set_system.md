# 装备套装系统实现方案

> 本文档是对 `set_bonus_design.md` 中提出的套装效果的技术落地方案，包含对抗性审查中发现的问题、修正后的最终实现设计，以及支持全部 1500 件装备所需的补充路线图。
>
> **状态说明（2026-08-16）**：本文保留为早期需求与缺口记录，不再作为当前实现蓝图。其中 GDScript registry、目录自行扫描、按 tag occurrence 计数、弱 `Dictionary` DTO 与 opaque `special_effect_ids` 均已被当前 C# typed content/runtime 边界取代。当前实现真相见 [`docs/design/progression/equipment_sets.md`](../../design/progression/equipment_sets.md)；首个完整套装已落地为凤凰重生套装。 [龙鳞铠甲套装完整落地方案](dragon_scale_set_full_landing.md)仍是 proposal，不代表当前实现。
>
> **落地核对摘要（2026-08-16）**：套装核心引擎已按 typed C# 架构落地，包括内容快照、显式成员计数、阈值叠加、属性/trait 投影、战斗换装刷新、UI 摘要与凤凰套装回归；正式内容目前只有 `phoenix_rebirth_set` 一套，尚未达到本文的 100 套内容目标。第 1–3 节保留历史 GDScript 设计，不能再按文件路径或 API 字面执行；第 4–6 节已补充当前缺口状态。面向后续方案设计的自我完备现状包见 [装备套装系统现状与缺口设计输入](gear_set_current_state_and_gaps.md)。

---

## 一、对抗性审查摘要

在方案设计阶段，通过代码走查和调用链分析，发现以下关键问题及修正：

### 1.1 BLOCKER 级问题

| 问题 | 影响 | 修正 |
|------|------|------|
| `AttributeModifier` 引用共享 | 直接 `append(modifier)` 会污染原始 `GearSetDef` 资源实例 | 返回前执行 `modifier.duplicate()`，再覆盖 `source_type`/`source_id` |
| Set 11 与 Set 95 的 `star_weaver_set` tag 冲突 | 两套独立设计（饰品 2/4/6 + 护甲 2/4）共享同一 tag，导致混穿计数叠加、效果混淆 | **决策：重命名 Set 95 为 `cosmic_star_weaver_set`**，保持两套独立 |

### 1.2 HIGH 级问题

| 问题 | 影响 | 修正 |
|------|------|------|
| Source tracking 缺失 | 所有装备 modifier 混为 `source_type = "equipment"`，UI 无法区分单件属性 vs 套装奖励 | 套装修正的 `source_type = "gear_set"`，`source_id = set_tag` |
| Registry 注入点分散 | `PartyEquipmentService` 在 `GameRuntimeFacade` 和 `CharacterManagementModule` 两处独立实例化 | `PartyEquipmentService` 在 `setup()` 中自动从目录加载；支持外部注入覆盖 |
| 条件性效果无归属 | "日出后1小时""夜间"等条件效果无法在当前属性结算中表达 | 条件效果走 `special_effect_ids`，由对应系统（环境/战斗）后续查询；`GearSetThresholdDef` 预留 `conditions: Array[Dictionary]` 字段 |

### 1.3 MEDIUM 级问题

| 问题 | 说明 |
|------|------|
| Threshold 叠加语义 | 已确认：所有满足阈值的 effect 同时生效（2件+4件+6件叠加），符合 ARPG convention |
| 双手武器计数 | `get_entry_slot_ids()` 保证双手武器只计 1 件，正确 |
| 战斗系统集成缺口 | `special_effect_ids` 需要战斗系统后续增加查询接口；当前只完成属性修正部分 |

---

## 二、最终实现方案

### 2.1 新增文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `scripts/player/progression/gear_set_threshold_def.gd` | 新脚本 | 单个阈值效果定义 |
| `scripts/player/progression/gear_set_def.gd` | 新脚本 | 套装定义 |
| `scripts/systems/inventory/gear_set_registry.gd` | 新脚本 | 套装注册表（自动加载 + 索引） |
| `scripts/systems/inventory/party_equipment_service.gd` | **修改** | 集成套装计数与属性追加 |
| `data/configs/gear_sets/*.tres` | 新配置 | 100 套套装定义（每套一个 `.tres`） |

### 2.2 `GearSetThresholdDef`

```gdscript
class_name GearSetThresholdDef
extends Resource

@export var required_count: int = 2
@export_multiline var description: String = ""
@export var attribute_modifiers: Array[AttributeModifier] = []
@export var special_effect_ids: Array[StringName] = []
## 预留：条件效果列表（当前未实现，供未来扩展环境/战斗条件）
@export var conditions: Array[Dictionary] = []
```

### 2.3 `GearSetDef`

```gdscript
class_name GearSetDef
extends Resource

@export var set_tag: StringName = &""
@export var set_name: String = ""
@export var threshold_effects: Array[GearSetThresholdDef] = []
```

### 2.4 `GearSetRegistry`

```gdscript
class_name GearSetRegistry
extends RefCounted

const GEAR_SET_DEF_SCRIPT = preload("res://scripts/player/progression/gear_set_def.gd")

var _defs_by_tag: Dictionary = {}  # StringName -> GearSetDef

## 从目录自动加载所有 .tres 文件
func load_from_directory(dir_path: String) -> void:
    var dir := DirAccess.open(dir_path)
    if dir == null:
        push_warning("GearSetRegistry: cannot open directory %s" % dir_path)
        return
    dir.list_dir_begin()
    var file_name := dir.get_next()
    while file_name != "":
        if file_name.ends_with(".tres"):
            var full_path := dir_path.path_join(file_name)
            var res := load(full_path)
            if res is GearSetDef and res.set_tag != &"":
                _defs_by_tag[res.set_tag] = res
            else:
                push_warning("GearSetRegistry: skipped invalid file %s" % full_path)
        file_name = dir.get_next()

func register(def: GearSetDef) -> void:
    if def == null or def.set_tag == &"":
        push_warning("GearSetRegistry: skipped invalid def")
        return
    _defs_by_tag[def.set_tag] = def

func get_def(set_tag: StringName) -> GearSetDef:
    return _defs_by_tag.get(set_tag)

func get_all_tags() -> Array[StringName]:
    var result: Array[StringName] = []
    for tag in _defs_by_tag.keys():
        result.append(tag)
    return result
```

### 2.5 `PartyEquipmentService` 修改

**新增字段：**
```gdscript
const GEAR_SET_REGISTRY_SCRIPT = preload("res://scripts/systems/inventory/gear_set_registry.gd")

var _gear_set_registry: GearSetRegistry = null
```

**`setup()` 扩展：**
```gdscript
func setup(
    party_state,
    item_defs: Dictionary = {},
    warehouse_service = null,
    equipment_instance_id_allocator: Callable = Callable(),
    gear_set_registry = null
) -> void:
    # ... existing setup ...
    _party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
    _item_defs = item_defs if item_defs != null else {}
    _warehouse_service = warehouse_service if warehouse_service != null else PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
    if _warehouse_service != null and _warehouse_service.has_method("setup"):
        _warehouse_service.setup(_party_state, _item_defs, equipment_instance_id_allocator)

    # [新增] 套装注册表
    _gear_set_registry = gear_set_registry
    if _gear_set_registry == null:
        _gear_set_registry = GEAR_SET_REGISTRY_SCRIPT.new()
        _gear_set_registry.load_from_directory("res://data/configs/gear_sets/")
```

**修改 `build_attribute_modifiers()`：**
```gdscript
func build_attribute_modifiers(equipment_state_variant: Variant) -> Array[AttributeModifier]:
    var modifiers: Array[AttributeModifier] = []
    var equipment_state = _normalize_equipment_state(equipment_state_variant)
    if equipment_state == null:
        return modifiers

    var equipped_tags: Array[StringName] = []
    for entry_slot_id in equipment_state.get_entry_slot_ids():
        var item_id: StringName = equipment_state.get_equipped_item_id(entry_slot_id)
        var item_def = get_item_def(item_id)
        if item_def == null or not item_def.is_equipment():
            continue
        for modifier in item_def.get_attribute_modifiers():
            if modifier is AttributeModifier:
                modifiers.append(modifier)
        _append_armor_max_dex_modifier(modifiers, item_def)
        equipped_tags.append_array(item_def.get_tags())

    # [新增] 套装属性修正
    modifiers.append_array(_resolve_gear_set_modifiers(equipped_tags))
    return modifiers
```

**新增 `_resolve_gear_set_modifiers()`（核心）：**
```gdscript
func _resolve_gear_set_modifiers(equipped_tags: Array[StringName]) -> Array[AttributeModifier]:
    if _gear_set_registry == null:
        return []

    var tag_counts: Dictionary = {}
    for tag in equipped_tags:
        tag_counts[tag] = tag_counts.get(tag, 0) + 1

    var result: Array[AttributeModifier] = []
    for set_tag in tag_counts.keys():
        var gear_set_def = _gear_set_registry.get_def(set_tag)
        if gear_set_def == null:
            continue
        var count: int = tag_counts[set_tag]
        for threshold in gear_set_def.threshold_effects:
            if threshold is not GearSetThresholdDef:
                continue
            if count >= threshold.required_count:
                for modifier in threshold.attribute_modifiers:
                    if modifier is AttributeModifier:
                        var dup := modifier.duplicate()
                        dup.source_type = &"gear_set"
                        dup.source_id = set_tag
                        result.append(dup)
    return result
```

**新增查询接口（供战斗系统 / UI 使用）：**
```gdscript
func get_active_gear_set_bonuses(equipment_state_variant: Variant) -> Dictionary:
    var equipment_state = _normalize_equipment_state(equipment_state_variant)
    if equipment_state == null or _gear_set_registry == null:
        return {}

    var equipped_tags: Array[StringName] = []
    for entry_slot_id in equipment_state.get_entry_slot_ids():
        var item_id: StringName = equipment_state.get_equipped_item_id(entry_slot_id)
        var item_def = get_item_def(item_id)
        if item_def != null and item_def.is_equipment():
            equipped_tags.append_array(item_def.get_tags())

    var tag_counts: Dictionary = {}
    for tag in equipped_tags:
        tag_counts[tag] = tag_counts.get(tag, 0) + 1

    var result: Dictionary = {}
    for set_tag in tag_counts.keys():
        var gear_set_def = _gear_set_registry.get_def(set_tag)
        if gear_set_def == null:
            continue
        var count: int = tag_counts[set_tag]
        var active_thresholds: Array[Dictionary] = []
        for threshold in gear_set_def.threshold_effects:
            if threshold is not GearSetThresholdDef:
                continue
            if count >= threshold.required_count:
                active_thresholds.append({
                    "required_count": threshold.required_count,
                    "description": threshold.description,
                    "attribute_modifiers": threshold.attribute_modifiers,
                    "special_effect_ids": threshold.special_effect_ids,
                })
        if not active_thresholds.is_empty():
            result[set_tag] = {
                "set_name": gear_set_def.set_name,
                "equipped_count": count,
                "active_thresholds": active_thresholds,
            }
    return result
```

### 2.6 配置示例

**文件**：`data/configs/gear_sets/dawn_paladin_set.tres`

```gdscript
[gd_resource type="Resource" script_class="GearSetDef" load_steps=6 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/attribute_modifier.gd" id="1_mod"]
[ext_resource type="Script" path="res://scripts/player/progression/gear_set_threshold_def.gd" id="2_thr"]
[ext_resource type="Script" path="res://scripts/player/progression/gear_set_def.gd" id="3_set"]

[sub_resource type="Resource" id="mod1"]
script = ExtResource("1_mod")
attribute_id = &"resistance_radiant"
mode = &"flat"
value = 10

[sub_resource type="Resource" id="mod2"]
script = ExtResource("1_mod")
attribute_id = &"armor_ac_bonus"
mode = &"flat"
value = 1

[sub_resource type="Resource" id="thr1"]
script = ExtResource("2_thr")
required_count = 2
description = "黎明的低语：radiant 抗性 +10"
attribute_modifiers = [SubResource("mod1")]

[sub_resource type="Resource" id="thr2"]
script = ExtResource("2_thr")
required_count = 4
description = "殉道者之光：AC+1，日出对邪恶+2命中，低血量团队护盾"
attribute_modifiers = [SubResource("mod2")]
special_effect_ids = [&"dawn_paladin_martyr_light"]

[resource]
script = ExtResource("3_set")
set_tag = &"dawn_paladin_set"
set_name = "晨光圣骑士"
threshold_effects = [SubResource("thr1"), SubResource("thr2")]
```

---

## 三、集成调用链验证

```
CharacterManagementModule._build_attribute_source_context()
  └─ _party_equipment_service.build_attribute_modifiers(equipment_state_variant)
       ├─ 遍历 get_entry_slot_ids() → 收集单件 attribute_modifiers
       ├─ [新增] _resolve_gear_set_modifiers(equipped_tags)
       │    ├─ 计数 tag 出现次数
       │    ├─ 查询 GearSetRegistry
       │    └─ 满足阈值的 modifier duplicate() + source_type="gear_set"
       └─ 返回 Array[AttributeModifier]（单件 + 套装）
  └─ context.equipment_state = Array[AttributeModifier]
     └─ AttributeService.setup_context(context)
          └─ _append_external_modifier_entries(state, &"equipment")
               └─ state is Array → 直接遍历追加所有 modifier
```

**关键保证**：`AttributeService` 完全无感知，不需要任何修改。

---

## 四、全局装备系统 Gap 补充路线图

以下是本文形成时的缺口记录，并补充 2026-08-16 的当前实现状态。旧文本所说“套装系统只解决 200 个 threshold 效果中的属性修正部分”已经过时：通用 typed equipment-ability framework 已覆盖其中一部分机制，但 100 套套装与 1500 件装备内容仍未批量落地。

### 4.1 P0 — 阻塞级（必须先解决）

| # | 补充项 | 当前状态（2026-08-16） | 说明 |
|---|--------|----------------------|------|
| 1 | **43 个自定义属性 ID 的处理器** | **未闭环** | custom attribute 现在可以进入 `AttributeSnapshot`，装备能力也可用 `attribute_value` fact 读取；但 `resistance_*`、`saving_throw_*`、`movement_speed`、`stealth_bonus` 等仍没有核心语义消费者。抗性的正式路径已改为 trait mitigation tier，不再应补 `resistance_* +N` 属性处理器。 |
| 2 | **15 个未知属性 ID 的映射** | **部分被替代，仍未闭环** | `max_hp` 没有别名，正式内容直接写 `hp_max`；`attack_bonus_ranged`/`attack_bonus_undead` 可由 typed equipment attack modifier 条件表达；`spell_dc_bonus` 是未优化内容留下的全局法术 DC 占位，不应实现为通用属性，后续应归一为按伤害类型/豁免标签区分的豁免 DC 加值；数值型 `critical_threat_range` 仍无运行时消费者。 |
| 3 | **6 个缺失 damage tag** | **部分落地** | `acid`、`force`、`poison` 已进入 `DamageTagContentRules`；`cold` 的正式映射是 `freeze`；`necrotic` 尚无别名（语义近似物为 `negative_energy`），`silver` 仍不存在。 |

### 4.2 P1 — 高影响

| # | 补充项 | 当前状态（2026-08-16） | 说明 |
|---|--------|----------------------|------|
| 4 | **装备 on-hit / on-crit / on-kill 效果系统** | **框架已落地** | 已由 typed equipment-ability trigger/timing/condition/action ABI 覆盖；on-crit 通过 hit/damage-roll trigger 加 `critical_hit` fact 表达。 |
| 5 | **每日/充能能力系统** | **框架已落地** | 已有 `per_battle`、`per_world_day`、`per_world_month` 与每周期次数账本；凤凰套装和多个单件装备已实际使用。 |
| 6 | **元素伤害附加（武器）** | **框架已落地** | 已由 `add_damage_dice` 的 typed damage tag 与 replacement group 支持；火焰、寒冰（`freeze`）、闪电、光辉等武器内容已有回归。 |

### 4.3 P2 — 中等影响

| # | 补充项 | 当前状态（2026-08-16） | 说明 |
|---|--------|----------------------|------|
| 7 | **反应性触发系统**（受击/低血量） | **框架已落地** | 已由 `on_damage_taken_finalized`、`on_hit_received` 等 trigger 与 `hp_percent_bp` fact 覆盖；凤凰 7 件阈值是套装侧真实用例。旧 `special_effect_ids` 查询链不会实现。 |
| 8 | **吸血/生命偷取** | **框架已落地** | 已由 `heal_from_fact` 按 `hp_damage` 计算治疗实现，并已有武器内容；未使用 `vampirism` 或 `heal_percent_of_damage_dealt` 字段。 |
| 9 | **光环/范围效果** | **部分落地** | mitigation aura 已落地并用于凤凰徽章；通用属性/增益光环仍无独立机制，只能用 reaction + status 近似。 |

### 4.4 P3 — 低影响/复杂

| # | 补充项 | 当前状态（2026-08-16） | 说明 |
|---|--------|----------------------|------|
| 10 | **传送/瞬移效果** | **部分落地** | 技能侧已有 blink forced-move 管线；equipment ability 尚无原生 teleport/blink action，只能经授予或触发技能间接实现。 |
| 11 | **召唤系统** | **框架已落地** | `summon_units` 已包含召唤物属性、AI、生命周期、数量上限与消费动作，并已有武器内容；尚无套装召唤内容。 |
| 12 | **伤害吸收/转化** | **部分落地** | 已有 mitigation tier、护盾、固定减伤、减伤光环和致死拦截；尚无“把伤害转化为治疗/其他资源”的通用钩子或装备吸收池。 |
| 13 | **状态效果映射** | **部分落地** | 已有 `blind`、`poisoned`、`frightened`、`stunned`、`paralyzed`、`prone`、`petrified` 等映射；`charmed`、`restrained`、`grappled`、`invisible`、`deafened` 等仍缺 canonical 状态或语义。 |

---

## 五、遗留决策

| 决策项 | 状态（2026-08-16） | 建议 |
|--------|-------------------|------|
| Set 95 重命名 | **已废弃** | 当前套装不按 tag occurrence 计数，`star_weaver_set` tag 冲突不再会影响套装成员数；无需再做 `cosmic_star_weaver_set` 迁移。 |
| 条件效果实现时机 | **旧占位方案已废弃** | `special_effect_ids` 和弱 `conditions: Array[Dictionary]` 不会实现；条件效果应走 typed trait、condition/fact 与 equipment-ability binding。具体环境条件仍需新增 typed owner 后再接内容。 |
| 自定义属性处理器优先级 | **仍是开放缺口，但优先级重排** | 不建议补 `resistance_* +N` 处理器，也不实现全局 `spell_dc_bonus`；优先定义按伤害类型/豁免标签区分的豁免 DC 加值 owner，以及数值型 crit threshold、movement/stealth/skill bonus 的正式 owner。抗性继续走 mitigation tier。 |

---

## 六、测试状态

本节原为历史测试计划；其中 `run_gear_set_smoke.gd` 没有落地。当前对应覆盖已由 C# headless regression 取代：

- `tests/equipment/run_gear_set_evaluation_regression.cs`：覆盖显式成员计数、重复成员去重、破损件、槽位与 footprint 校验、阈值叠加、source provenance、属性流入 `AttributeService`、trait 与战斗能力投影。
- `tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs`：覆盖凤凰套装 3/5/7/10 阈值的真实战斗行为。
- `tests/battle_runtime/runtime/run_phoenix_rebirth_single_item_behavior_regression.cs`、`run_phoenix_rebirth_body_cloak_behavior_regression.cs`：覆盖套装单件能力。
- `tests/equipment/run_phoenix_rebirth_unique_acquisition_regression.cs`：覆盖凤凰唯一实例池与获取链。
- `tests/world_map/ui/run_party_management_window_regression.cs`：覆盖队伍装备页套装摘要。

旧测试计划中的两项语义已改变：同一成员物品的重复副本不再按 tag occurrence 计 2 件；`get_active_gear_set_bonuses()` / `special_effect_ids` 已由 typed `GearSetEvaluationSnapshot`、阈值 trait 和 equipment-ability binding 取代。双手武器通过单 entry 与 canonical footprint 只计 1 件，但仍可补一个使用双手套装成员的专项目归。

---

*文档版本: 1.1*
*基于对抗性审查后的历史方案；2026-08-16 补充当前落地核对状态*
