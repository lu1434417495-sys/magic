# 装备套装系统实现方案

> 本文档是对 `set_bonus_design.md` 中提出的套装效果的技术落地方案，包含对抗性审查中发现的问题、修正后的最终实现设计，以及支持全部 1500 件装备所需的补充路线图。

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

套装系统只解决了 **200 个 threshold 效果中的属性修正部分**（约 30%）。要完全支持 1500 件装备的设计，还需要以下补充：

### 4.1 P0 — 阻塞级（必须先解决）

| # | 补充项 | 影响范围 | 说明 |
|---|--------|----------|------|
| 1 | **43 个自定义属性 ID 的处理器** | 1000+ 件装备 | `resistance_cold`、`stealth_bonus`、`saving_throw_wisdom`、`movement_speed`、`max_mana` 等可存入 `UnitBaseAttributes.custom_stats`，但 `AttributeService` 和战斗系统不读取。需要：a) 在 `AttributeService` 中添加 custom stat 的 derived rule；b) 或在战斗/判定系统中直接读取 custom stats |
| 2 | **15 个未知属性 ID 的映射** | 200+ 件装备 | `max_hp` → 映射到 `hp_max`；`spell_dc_bonus` → 新增 custom stat + 战斗系统读取；`critical_threat_range` → 需要战斗系统支持；`attack_bonus_ranged`/`attack_bonus_undead` → 需要条件攻击加值系统 |
| 3 | **6 个缺失 damage tag** | 300+ 处效果 | `acid`、`cold`、`force`、`necrotic`、`poison`、`silver` 不在 `BattleDamageResolver` 常量中。`cold` 可映射到现有 `freeze`，其余需要新增 |

### 4.2 P1 — 高影响

| # | 补充项 | 设计文档中出现次数 | 说明 |
|---|--------|-------------------|------|
| 4 | **装备 on-hit / on-crit / on-kill 效果系统** | ~379 | `ItemDef`/`WeaponProfileDef` 需要增加 `effect_defs` 或类似的触发效果字段；`BattleDamageResolver` 需要查询装备特效并应用 |
| 5 | **每日/充能能力系统** | ~1120 | `ItemDef` 的 `granted_skill_id` 只能授予一个技能，没有使用次数。需要新增 `granted_skill_charges: int`、`granted_skill_cooldown_tu: int` 等字段，或在角色状态中追踪 |
| 6 | **元素伤害附加（武器）** | ~162 | `WeaponProfileDef` 需要 `bonus_damage_tags: Array[Dictionary]` 来支持"近战附加 1D6 cold" |

### 4.3 P2 — 中等影响

| # | 补充项 | 设计文档中出现次数 | 说明 |
|---|--------|-------------------|------|
| 7 | **反应性触发系统**（受击/低血量） | ~343 | 战斗运行时增加 equipment-trigger hooks：`BattleDamageResolver` 在造成伤害/受到伤害时查询 `PartyEquipmentService.get_active_gear_set_bonuses()` 中的 reactive `special_effect_ids` |
| 8 | **吸血/生命偷取** | ~330 | 新增 vampirism 效果类型，或扩展 `CombatEffectDef` 支持 `heal_percent_of_damage_dealt` |
| 9 | **光环/范围效果** | ~373 | 装备 aura 系统：战斗开始时将 aura `special_effect_ids` 注册到 `BattleUnitState` 或战场管理器 |

### 4.4 P3 — 低影响/复杂

| # | 补充项 | 设计文档中出现次数 | 说明 |
|---|--------|-------------------|------|
| 10 | **传送/瞬移效果** | ~90 | `CombatEffectDef` 新增 `forced_move_mode = "teleport"` 或独立效果类型 |
| 11 | **召唤系统** | ~286 | 全新子系统：召唤物定义、AI、生命周期管理 |
| 12 | **伤害吸收/转化** | ~512 | 扩展伤害结算管线：在 `BattleDamageResolver._apply_damage_to_target()` 中增加吸收/转化钩子 |
| 13 | **状态效果映射** | ~500+ | 设计文档使用大量 D&D 标准状态（`blinded`、`frightened`、`poisoned`、`charmed`、`restrained`、`prone`、`grappled`、`invisible` 等），需要在 `BattleStatusSemanticTable` 中新增映射或别名 |

---

## 五、遗留决策

| 决策项 | 状态 | 建议 |
|--------|------|------|
| Set 95 重命名 | **待确认** | 建议将 Set 95 的 tag 从 `star_weaver_set` 改为 `cosmic_star_weaver_set`，避免与 Set 11 混穿 |
| 条件效果实现时机 | **推迟** | "日出后1小时""夜间"等条件效果当前用 `special_effect_ids` 占位，由环境/战斗系统后续扩展 |
| 自定义属性处理器优先级 | **P0** | 建议先实现 `resistance_*` 系列（出现频率最高），再逐步添加技能 bonus |

---

## 六、测试计划

### 6.1 单元测试（headless GDScript）

`tests/equipment/gear_set/run_gear_set_smoke.gd`

```gdscript
func run_tests():
    # Test 1: 2-piece bonus applied
    # Test 2: 4-piece + 2-piece stacked
    # Test 3: 6-piece accessory (2+4+6 stacked)
    # Test 4: Two-handed weapon counts as 1
    # Test 5: Two rings from same set count as 2
    # Test 6: Non-set items ignored
    # Test 7: Modifier source_type = "gear_set"
    # Test 8: get_active_gear_set_bonuses() returns correct special_effect_ids
```

### 6.2 集成测试

- 通过 `CharacterManagementModule` 构建 attribute context，验证套装 modifier 流入 `AttributeService`
- 验证 UI 面板能正确显示套装激活状态（通过 `get_active_gear_set_bonuses()`）

---

*文档版本: 1.0*
*基于对抗性审查后的修正方案*
