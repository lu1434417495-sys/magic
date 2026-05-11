# 怪影杀戮（Phantasmal Kill）详细设计方案

## 技能概述

| 参数 | 值 |
|------|-----|
| 环位 | 9 环 |
| 射程 | 12 格 |
| 范围 | 7×7 |
| 目标 | 区域内所有有心智生物 |
| 豁免 | 意志 / 精神（willpower） |
| 类型 | 幻术、恐惧、精神、即死 |

---

## 核心机制

### 豁免结果分级

怪影杀戮采用**四级豁免**，基于 `BattleSaveResolver` 返回的 `natural_roll` + `success` 判定：

| 结果 | 判定条件 | 效果 |
|------|---------|------|
| **大成功** | `natural_roll >= 20` | 完全无效 |
| **成功** | `natural_roll > 1 && natural_roll < 20 && success == true` | 获得 **余悸** 1 轮（不能使用反应动作） |
| **失败** | `natural_roll > 1 && natural_roll < 20 && success == false` | 条件即死判定 |
| **大失败** | `natural_roll <= 1` | 强化条件即死判定 |

### 失败分支（条件即死）

```
若 目标当前HP ≤ max(50, 目标最大HP × 25%)：
    目标立即死亡
否则：
    受到 6d6 精神伤害（psychic）
    获得 恐惧 2 轮
    失去反应动作 1 轮
```

### 大失败分支（强化条件即死）

```
若 目标当前HP ≤ 目标最大HP × 35%：
    目标立即死亡
否则：
    受到 10d6 精神伤害（psychic）
    获得 恐惧 3 轮
    获得 震慑 1 轮
```

### 心智生物过滤

目标必须是"有心智"的生物：
- 免疫 `illusion`、`charm`、`mental`、`psychic` 标签的单位视为**无心智**
- 无大脑/无意识/构装/亡灵（取决于设定）视为无心智
- 当前系统无通用 `mindless` 标记，建议通过 `save_tag` 免疫系统或 `enemy_template_id` 查表实现

---

## 与现有系统的对接分析

### 当前系统能力

| 系统 | 当前能力 | 是否满足 |
|------|---------|---------|
| 地面AOE | `_handle_ground_skill_command` + `area_pattern` | ✅ 满足 |
| 豁免系统 | `BattleSaveResolver.resolve_save()` 返回 `success` + `natural_roll` | ✅ 满足（四级结果由调用方组合判定） |
| 精神伤害 | `DAMAGE_TAG_PSYCHIC` 已存在（damage resolver 行 72） | ✅ 满足 |
| 恐惧状态 | `SAVE_TAG_FRIGHTENED` 已存在；`battle_state.gd` 有 `fear`/`feared` 标签；但语义表未注册 | ⚠️ 需注册语义 |
| 震慑状态 | `stunned` 标签在 `battle_state.gd` 中存在；语义表未注册 | ⚠️ 需注册语义 |
| 心智免疫 | 无通用 `mindless` 标记；但 save_tag 免疫系统可用 | ⚠️ 需过滤逻辑 |
| 反应动作 | **系统无此概念**。有 `counterattack`（反击）但无通用 reaction | ❌ 需设计替代 |

### 关键发现

1. **四级豁免不需改 resolver**：`resolve_save` 已返回 `natural_roll`（1-20）和 `success`（bool）。调用方可以自行组合出四级结果。这是最小侵入路径。

2. **恐惧/震慑状态存在但无语义**：`battle_state.gd` 的 `STRONG_ATTACK_DISADVANTAGE_STATUS_IDS` 中已有 `fear`/`feared`/`stunned`，说明这些状态ID可能被某些系统消费（如攻击劣势判定）。但 `battle_status_semantic_table.gd` 中没有注册它们的语义（堆叠规则、持续时间管理等）。需要补注册。

3. **反应动作缺失**：当前战斗系统没有 D&D 式的"反应动作"（Reaction）概念。最接近的是 `counterattack`（反击）和 `guarding`（格挡）。建议将"失去反应动作"映射为：
   - 方案A：不能反击（`lock_counterattack`）+ 不能格挡（已有 `STATUS_BLACK_STAR_BRAND_NORMAL` 会封锁格挡，但那是debuff）
   - 方案B：跳过下一回合的部分行动（如不能移动或不能施法）
   - 方案C：**跳过整个下一回合**（最简洁，但惩罚过重）
   - 方案D：引入通用"反应动作"系统（大工程，不推荐）

4. **幻术/精神豁免标签**：`SAVE_TAG_ILLUSION`、`SAVE_TAG_CHARM`、`SAVE_TAG_FRIGHTENED` 已存在。`SAVE_TAG_MENTAL` / `SAVE_TAG_PSYCHIC` 不存在，但可以通过 `params` 自定义或新增常量。

---

## 代码变更清单（在哪改）

### 改动 1：豁免结果分级工具

**文件**：`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

**位置**：新增静态/私有方法

```gdscript
func _resolve_save_grade(save_result: Dictionary) -> StringName:
    var natural_roll = int(save_result.get("natural_roll", 0))
    var success = bool(save_result.get("success", false))
    
    if natural_roll >= 20:
        return &"critical_success"  # 大成功
    elif natural_roll <= 1:
        return &"critical_failure"  # 大失败
    elif success:
        return &"success"            # 成功
    else:
        return &"failure"            # 失败
```

> **为什么不改 `BattleSaveResolver`**：因为四级分级只在少数技能（怪影杀戮、可能还有其他幻术即死）中使用。改 resolver 会影响所有技能，侵入太大。在 orchestrator 中做分级是最小侵入方案。

---

### 改动 2：Orchestrator 特殊分支

**文件**：`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

**位置**：`_handle_skill_command()` 方法（行 369），插入：

```gdscript
if skill_def.skill_id == &"phantasmal_kill":
    applied = _handle_phantasmal_kill_command(active_unit, command, skill_def, cast_variant, batch)
```

**新增方法 `_handle_phantasmal_kill_command`**：

```gdscript
func _handle_phantasmal_kill_command(
    active_unit: BattleUnitState,
    command: BattleCommand,
    skill_def: SkillDef,
    cast_variant: CombatCastVariantDef,
    batch: BattleEventBatch
) -> bool:
    # 1. 验证（标准地面技能验证）
    var validation = _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
    if not bool(validation.get("allowed", false)):
        return false
    
    # 2. 消耗资源
    if not _consume_skill_costs(active_unit, skill_def, cast_variant, batch):
        return false
    _record_action_issued(...)
    
    # 3. 生成 7×7 效果坐标
    var target_coords: Array[Vector2i] = validation.get("target_coords", [])
    var effect_coords = _build_ground_effect_coords(skill_def, target_coords, active_unit.coord, active_unit, cast_variant)
    # 或直接用自定义的 7×7 坐标生成（因为 area_pattern 可能不匹配）
    
    # 4. 收集范围内所有单位
    var affected_units: Array[BattleUnitState] = []
    for coord in effect_coords:
        # 用 grid_service 查找该坐标的占据者
        var unit_at_coord = _runtime._grid_service.get_unit_at_coord(_runtime._state, coord)
        if unit_at_coord != null and not affected_units.has(unit_at_coord):
            affected_units.append(unit_at_coord)
    
    # 5. 过滤无心智单位
    var valid_targets: Array[BattleUnitState] = []
    for unit in affected_units:
        if _is_mindless_unit(unit):
            batch.log_lines.append("%s 没有心智，怪影杀戮对其无效。" % unit.display_name)
            continue
        valid_targets.append(unit)
    
    if valid_targets.is_empty():
        batch.log_lines.append("怪影杀戮范围内没有可被影响的心智生物。")
        return true
    
    # 6. 对每个目标结算
    for target_unit in valid_targets:
        _resolve_phantasmal_kill_on_target(active_unit, target_unit, skill_def, batch)
    
    batch.log_lines.append("%s 施放怪影杀戮，影响了 %d 个心智生物。" % [active_unit.display_name, valid_targets.size()])
    return true
```

---

### 改动 3：心智生物判断

**文件**：`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

**位置**：新增私有方法

```gdscript
func _is_mindless_unit(unit_state: BattleUnitState) -> bool:
    # 方案A：通过 save_tag 免疫判断
    if BattleSaveResolver.is_immune(unit_state, &"illusion"):
        return true
    if BattleSaveResolver.is_immune(unit_state, &"charm"):
        return true
    # 可扩展更多标签
    
    # 方案B：通过 enemy_template_id 查表（如果有 mindless 模板表）
    # var template = _runtime._enemy_templates.get(unit_state.enemy_template_id)
    # if template != null and bool(template.get("is_mindless", false)):
    #     return true
    
    # 方案C：通过 movement_tags / status 判断
    if unit_state.has_movement_tag(&"mindless"):
        return true
    if unit_state.has_movement_tag(&"construct"):
        return true
    if unit_state.has_movement_tag(&"undead"):
        return true
    
    return false
```

**推荐**：先用方案A（save_tag 免疫）+ 方案C（movement_tags），最简单且不需要新增数据字段。

---

### 改动 4：单体结算逻辑

**文件**：`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

```gdscript
func _resolve_phantasmal_kill_on_target(
    source_unit: BattleUnitState,
    target_unit: BattleUnitState,
    skill_def: SkillDef,
    batch: BattleEventBatch
) -> void:
    # 1. 豁免
    var effect_def = _build_phantasmal_kill_effect_def(skill_def)
    var save_result = BattleSaveResolver.resolve_save(source_unit, target_unit, effect_def, {"skill_id": skill_def.skill_id})
    var save_grade = _resolve_save_grade(save_result)
    
    match save_grade:
        &"critical_success":
            batch.log_lines.append("%s 大成功抵抗了怪影杀戮，完全无效！" % target_unit.display_name)
            return
        
        &"success":
            batch.log_lines.append("%s 勉强抵抗了怪影杀戮，心有余悸。" % target_unit.display_name)
            _runtime._set_runtime_status_effect(
                target_unit, &"aftershock", 60, source_unit.unit_id, 1,
                {"lock_reactions": true}
            )
            return
        
        &"failure":
            var max_hp = _get_unit_max_hp(target_unit)
            var threshold = maxi(50, max_hp * 25 / 100)
            
            if target_unit.current_hp <= threshold:
                batch.log_lines.append("%s 在怪影中看到了自己的死亡，被恐惧吞噬！" % target_unit.display_name)
                _apply_instant_death(source_unit, target_unit, batch)
            else:
                batch.log_lines.append("%s 被怪影侵袭！" % target_unit.display_name)
                # 6d6 精神伤害
                var damage_result = _apply_psychic_damage(source_unit, target_unit, 6, 6, batch)
                # 恐惧 2 轮
                _runtime._set_runtime_status_effect(target_unit, &"frightened", 120, source_unit.unit_id, 1, {})
                # 失去反应动作 1 轮
                _runtime._set_runtime_status_effect(target_unit, &"reaction_lock", 60, source_unit.unit_id, 1, {})
            return
        
        &"critical_failure":
            var max_hp = _get_unit_max_hp(target_unit)
            var threshold = max_hp * 35 / 100
            
            if target_unit.current_hp <= threshold:
                batch.log_lines.append("%s 在怪影中彻底崩溃，灵魂被撕裂！" % target_unit.display_name)
                _apply_instant_death(source_unit, target_unit, batch)
            else:
                batch.log_lines.append("%s 被怪影彻底吞噬！" % target_unit.display_name)
                # 10d6 精神伤害
                var damage_result = _apply_psychic_damage(source_unit, target_unit, 10, 6, batch)
                # 恐惧 3 轮
                _runtime._set_runtime_status_effect(target_unit, &"frightened", 180, source_unit.unit_id, 1, {})
                # 震慑 1 轮
                _runtime._set_runtime_status_effect(target_unit, &"stunned", 60, source_unit.unit_id, 1, {})
            return
```

**辅助方法** `_apply_psychic_damage`：

```gdscript
func _apply_psychic_damage(source, target, dice_count, dice_sides, batch) -> Dictionary:
    var damage = _roll_dice(dice_count, dice_sides)
    # 精神伤害走标准 damage resolver，但 damage_tag = "psychic"
    # 或直接在 orchestrator 中扣血（类似律令死亡）
    target.current_hp = maxi(target.current_hp - damage, 0)
    target.is_alive = target.current_hp > 0
    batch.log_lines.append("%s 受到 %d 点精神伤害。" % [target.display_name, damage])
    if not target.is_alive:
        _runtime._clear_defeated_unit(target, batch)
    return {"damage": damage}
```

---

### 改动 5：新状态注册

**文件**：`scripts/systems/battle/rules/battle_status_semantic_table.gd`

#### 5a. `frightened`（恐惧）

```gdscript
# 新增常量
const STATUS_FRIGHTENED: StringName = &"frightened"

# get_semantic() 中新增
STATUS_FRIGHTENED:
    var semantic := _build_refresh_timeline_semantic()
    semantic["attack_roll_penalty"] = 2  # 或根据设计调整
    return semantic

# is_harmful_status() 中新增
STATUS_FRIGHTENED:
    return true
```

#### 5b. `stunned`（震慑）

```gdscript
# 新增常量
const STATUS_STUNNED: StringName = &"stunned"

# get_semantic() 中新增
STATUS_STUNNED:
    return _build_refresh_timeline_semantic(TICK_TURN_START_AP_PENALTY)
    # 或更严格：跳过整个回合（类似 petrified 的 skip_turn）

# is_harmful_status() 中新增
STATUS_STUNNED:
    return true
```

#### 5c. `aftershock`（余悸）

```gdscript
# 新增常量
const STATUS_AFTERSHOCK: StringName = &"aftershock"

# get_semantic() 中新增
STATUS_AFTERSHOCK:
    return _build_refresh_timeline_semantic()
    # 纯标记状态，具体效果（不能反应）由消费端读取 params

# is_harmful_status() 中新增
STATUS_AFTERSHOCK:
    return true
```

#### 5d. `reaction_lock`（反应封锁）

```gdscript
# 新增常量
const STATUS_REACTION_LOCK: StringName = &"reaction_lock"

# get_semantic() 中新增
STATUS_REACTION_LOCK:
    return _build_refresh_timeline_semantic()

# is_harmful_status() 中新增
STATUS_REACTION_LOCK:
    return true
```

---

### 改动 6：反应动作限制的具体实现

由于系统没有通用反应动作，需要选择一个**现有机制**作为替代：

| 方案 | 实现位置 | 效果 |
|------|---------|------|
| **A. 封锁反击**（推荐） | `BattleRuntimeModule.is_unit_counterattack_locked()` | 已有方法，只需检查 `reaction_lock` 或 `aftershock` 状态 |
| **B. 跳过回合** | `BattleTimelineDriver._activate_next_ready_unit()` | 太重，惩罚过高 |
| **C. AP归零** | `BattleTimelineDriver._activate_next_ready_unit()` | 回合开始 AP=0，只能移动不能行动 |

**推荐方案A**：将"不能反应"映射为"不能反击"。在 `is_unit_counterattack_locked()` 方法（行 761）中追加：

```gdscript
func is_unit_counterattack_locked(unit_state: BattleUnitState) -> bool:
    return _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL) \
        or _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND) \
        or _has_status_param_bool(unit_state, &"lock_counterattack") \
        or _has_status(unit_state, &"reaction_lock") \
        or _has_status(unit_state, &"aftershock")
```

如果未来引入了通用反应动作系统，再扩展即可。

---

### 改动 7：恐惧状态的具体战斗效果

恐惧状态需要产生实际战斗影响。参考 D&D，`frightened` 通常：
- 攻击检定劣势（-2 或 disadvantage）
- 不能向恐惧源移动

在当前系统中，可以实现为：

**文件 1**：`scripts/systems/battle/rules/battle_status_semantic_table.gd`

```gdscript
STATUS_FRIGHTENED:
    var semantic := _build_refresh_timeline_semantic()
    semantic["attack_roll_penalty"] = 2
    return semantic
```

**文件 2**：`scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`

在 `get_skill_cast_block_reason` 或某个移动检查中，如果目标有 `frightened` 且试图向恐惧源移动，阻止移动。

但"不能向恐惧源移动"需要记录恐惧源坐标，比较复杂。**简化方案**：恐惧只提供攻击检定惩罚，不限制移动。

---

### 改动 8：AI 适配

**文件**：`scripts/systems/battle/ai/battle_ai_service.gd`

怪影杀戮的 AI 评分：
- 范围内心智生物数量越多，评分越高
- 低血量目标（接近即死阈值）提升评分
- 友军也会受影响（7×7 AOE），需评估友军误伤

```gdscript
# 伪代码
if skill_id == &"phantasmal_kill":
    score = 0
    for enemy in enemies_in_7x7:
        if _is_mindless_unit(enemy):
            continue
        score += 30
        # 接近即死阈值加分
        var max_hp = _get_unit_max_hp(enemy)
        var threshold = maxi(50, max_hp * 25 / 100)
        if enemy.current_hp <= threshold:
            score += 50  # 可能直接秒
    for ally in allies_in_7x7:
        if _is_mindless_unit(ally):
            continue
        score -= 40  # 友军误伤惩罚
```

---

### 改动 9：技能配置

**文件**：`data/configs/skills/mage_phantasmal_kill.tres`

```gdscript
[resource]
script = ExtResource("...")
skill_id = &"phantasmal_kill"
target_mode = &"ground"
target_team_filter = &"any"  # AOE 敌我不分
target_selection_mode = &"single_point"
range_value = 12
ap_cost = 1
mp_cost = ...
area_pattern = &"square"  # 或自定义
area_value = 3  # 7×7 = 半径3的方形
effect_defs = []  # 空，所有效果由 orchestrator 硬编码
save_ability = &"willpower"
save_tag = &"illusion"
save_dc_mode = &"caster_spell"
```

---

## 工作量评估

| 模块 | 工作量 | 风险 | 说明 |
|------|--------|------|------|
| Orchestrator 特殊分支（AOE+心智过滤+四级结算） | **中** | 中 | 核心逻辑，但无复杂几何 |
| 新状态注册（恐惧/震慑/余悸/反应封锁） | **小** | 低 | 标准状态注册 |
| 恐惧状态战斗效果（攻击惩罚） | **小** | 低 | 复用 blind 的攻击惩罚模式 |
| 反应封锁（映射为封锁反击） | **小** | 低 | 在已有方法中追加状态检查 |
| 精神伤害结算 | **小** | 低 | 复用标准伤害流程，damage_tag = psychic |
| 即死逻辑 | **小** | 低 | 直接设置 HP=0 |
| AI 评分 | **小** | 低 | 标准 AOE 评分 |
| .tres 配置 | **小** | 低 | 标准配置 |

**总计**：约 **0.8-1 个中等工作量**。比律令死亡略复杂（因为多了 AOE + 四级豁免 + 多重状态），但远小于陨星坠击。

---

## 关键设计决策建议

### 决策 1：四级豁免在哪分级？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **A. Orchestrator 调用方分级**（推荐） | 不改 resolver，最小侵入 | 每个需要四级的技能都要写分级逻辑 |
| **B. 改 `BattleSaveResolver.resolve_save` 返回 `grade`** | 统一，所有技能可用 | 改 resolver 影响面广，需测试所有现有技能 |

**推荐 A**：怪影杀戮可能是目前唯一需要四级的技能。在 orchestrator 中写一个 `_resolve_save_grade` 工具方法即可。如果未来更多技能需要四级，再考虑提取到 resolver。

### 决策 2：反应动作怎么替代？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **A. 封锁反击**（推荐） | 已有系统支持，一行代码 | 和 D&D "反应动作"概念不完全等价 |
| **B. 跳过回合** | 惩罚明确 | 太重，成功豁免后惩罚不应等于震慑 |
| **C. 引入反应动作系统** | 概念完整 | 大工程，需要改战斗核心流程 |

**推荐 A**：用"封锁反击"作为"不能反应"的当前版本实现。在技能描述中写"不能使用反应动作（当前实现为不能反击）"，留待未来系统升级。

### 决策 3：恐惧是否限制移动？

| 方案 | 优点 | 缺点 |
|------|------|------|
| **A. 只给攻击惩罚**（推荐） | 简单，不引入复杂路径计算 | 风味稍弱 |
| **B. 限制向恐惧源移动** | 风味正 | 需要记录恐惧源、计算方向、检查每次移动 |

**推荐 A**：先给攻击检定 -2。如果未来需要更完整的恐惧效果，再扩展移动限制。


---

# 深度审查意见（多 Agent 攻击性讨论汇总）

> 以下意见由 4 个独立 Agent 分别从**系统架构**、**数值平衡**、**实现可行性**、**D&D 规则还原度**四个维度进行深度攻击性审查后汇总生成。
> 供后续 Codex 迭代审查使用。

---

## 总体结论

| 审查维度 | Verdict | 关键立场 |
|---------|---------|---------|
| 系统架构 | ❌ **拒绝** | Orchestrator 硬编码破坏配置驱动架构，引入技术债务 |
| 数值平衡 | ❌ **拒绝** | AOE + 远程 + 概率即死 + 低 AP = 经济模型核弹 |
| 实现可行性 | ⚠️ **有条件接受** | 存在 1 个致命 Bug（免疫反被即死）、3 个系统断裂 |
| 规则还原度 | ❌ **拒绝** | 跨版本缝合怪，与任何一版 D&D 均不对齐 |

---

## 一、架构可维护性审查意见

**核心指控**：设计方案选择了"在 Orchestrator 中硬编码一切"的捷径，完全绕过了项目已建立的配置驱动架构。

### 问题 1：Orchestrator 技能 ID 硬编码分支（高严重性）

在 `_handle_skill_command()` 路由层插入 `if skill_id == &"phantasmal_kill"`，开了一个危险先例。每个"有点特殊"的技能都可以复制这个模式，迅速导致 orchestrator 膨胀为上帝类。

**建议**：将逻辑下沉到 `BattleSpecialSkillResolver`，复用现有特殊技能扩展点（如 `_apply_unit_skill_special_effects` 已有技能类型分支模式）。

### 问题 2：`_handle_phantasmal_kill_command` 大量复制 ground skill 代码（高严重性）

该方法复制了 `_handle_ground_skill_command` 的完整骨架：验证、消耗资源、记录行动、构建坐标、应用效果。唯一新增的是"心智过滤"和"四级豁免结算"。当 ground skill 的基础流程后续修复 bug 或添加功能（法术控制、魔法反噬漂移等）时，怪影杀戮的分支将默默遗漏这些变更。

**建议**：复用 `_handle_ground_skill_command` 标准流程，通过预过滤或效果驱动注入特殊逻辑。

### 问题 3：`effect_defs = []` 彻底破坏配置驱动架构（高严重性）

- **AI 评分系统失明**：`BattleAiScoreService._build_target_effect_metrics` 完全依赖 `effect_defs`，为空时 AI 认为零伤害零控制，永远不会使用它。
- **伤害预览系统失效**：`_build_unit_skill_damage_preview` 依赖 `effect_defs` 构建预览。
- **精通/成就系统无法追踪**：`_skill_mastery_service.record_target_result` 依赖 damage resolver 返回的 result。
- **状态语义系统被架空**：状态不通过 `CombatEffectDef` 注册，而是手写 `_set_runtime_status_effect`。

**建议**：保留 `effect_defs` 驱动地位。怪影杀戮的四级豁免差异可通过在 `CombatEffectDef` 中新增 `save_grade_result` 字段实现。

### 问题 4：`_apply_psychic_damage` 直接扣血，绕过 DamageResolver（高严重性）

直接修改 `target.current_hp` 意味着：
- 精神抗性/免疫完全失效
- 护盾不会吸收伤害
- 伤害减免、VajraBody 等状态失效
- 击倒不触发 `_apply_on_kill_gain_resources_effects`、`_collect_defeated_unit_loot` 等标准战后处理

**建议**：构造标准 `CombatEffectDef`（`effect_type = &"damage"`, `damage_tag = &"psychic"`），走 `_runtime._damage_resolver.resolve_effects()` 统一结算。

### 问题 5：豁免分级工具放错类（中严重性）

四级豁免分级是业务规则，不应藏在 orchestrator 的私有方法里。

**建议**：提取到 `BattleSaveResolver` 作为静态方法，或独立 `BattleSaveGradeRules` 类。

### 问题 6：心智生物判断耦合在 Orchestrator 中（中严重性）

职责错误（单位属性判断不应由执行编排器负责）；实现拼凑（免疫 illusion 不等于无心智）；缺乏配置化。

**建议**：在 `BattleUnitState` 或 `enemy_template` 中新增显式 `is_mindless` 标记，技能配置通过 `target_requirement_tags` 过滤。

### 问题 7：AI 评分硬编码在错误位置（中严重性）

`BattleAiService` 只负责决策流程，评分应由 `BattleAiScoreService` 基于 `effect_defs` 自动推导。

**建议**：恢复 `effect_defs` 驱动，AI 评分自动工作；特殊评分通过现有扩展点注入。

### 问题 8：日志文本硬编码破坏报告格式化一致性（中严重性）

`batch.log_lines.append("%s 在怪影中看到了自己的死亡...")` 直接硬编码中文风味文本，导致日志格式不一致、难以本地化。

**建议**：通过标准结果字典传递 `custom_log_lines`，让 `BattleReportFormatter` 统一处理。

### 问题 9：预览系统完全未考虑（中严重性）

`_preview_ground_skill_command` 依赖 `effect_defs` 收集受影响单位。`effect_defs = []` 会导致预览显示"影响 0 个单位、0 伤害"。

**建议**：保留 `effect_defs` 驱动，让标准预览流程自动工作。

### 问题 10：反应封锁使用新状态而非现有 param 机制（低严重性）

现有系统已有 `_has_status_param_bool(unit_state, &"lock_counterattack")` 机制（`doom_sentence` 使用）。设计却为"反应封锁"和"余悸"各创建独立状态 ID。

**建议**：复用现有 param 机制，给 `aftershock` 状态添加 `params = {"lock_counterattack": true}`。

### 问题 11：`stunned` 状态语义不足以实现"跳过回合"（中严重性）

当前系统没有任何状态语义实现 `skip_turn`。`STATUS_PETRIFIED` 注册的也只是 `_build_refresh_timeline_semantic()`。

**建议**：明确 `stunned` 的实际效果边界，如需"无法行动"需定义新的 tick_mode 或行为标记。

### 问题 12：条件即死阈值硬编码在结算逻辑中（中严重性）

`maxi(50, max_hp * 25 / 100)` 和 `max_hp * 35 / 100` 直接写在代码中，平衡调整需改代码重编译。

**建议**：将阈值、伤害骰数、状态持续时间等配置在 `CombatEffectDef.params` 中。

### 问题 13：地面 AOE 友军伤害未在执行层过滤（中严重性）

`target_team_filter = &"any"` 意味着实际会对范围内所有友军施加豁免、伤害和状态。AI 只是"不想"对友军使用，但玩家/脚本施放时友军会真实受伤。

**建议**：明确是否敌我不分；若只影响敌人，设为 `&"enemy"` 或在 effect_def 级别设置 `effect_target_team_filter`。

---

## 二、数值平衡性审查意见

**核心指控**：把本应精密的"高环单体斩杀技"粗暴扩展为"远程 AOE 概率即死地图炮"，同时踩中"超远射程、超大范围、概率批量即死、低 AP 消耗"四个禁忌中的三个半。

### 问题 1：AOE 即死 + 12 格射程 = 风险收益比崩坏（高严重性）

施法者可在绝对安全距离批量删除敌人。

**建议**：射程削减至 6 格，或范围缩减至 3×3，或引入自伤/近身盲区机制。

### 问题 2：5% 强制大失败摧毁属性成长体系（高严重性）

`natural_roll <= 1`（5%）触发强化即死，无论目标豁免加值多高始终有 5% 概率被即死。全豁免装坦克和裸装单位大失败概率相同。

**建议**：彻底移除基于自然骰的大失败即死；或改为 `final_roll - DC <= -10` 才触发大失败。

### 问题 3：成功豁免后完全无效，9 环方差极大（高严重性）

对不反击的敌人（法师、射手），成功豁免 = 零效果。9 环法术下限不能是零。

**建议**：成功豁免时追加 3d6 精神伤害 + frightened 1 轮。

### 问题 4：7×7 敌我不分 AOE 在战术层面不可控（高严重性）

玩家使用时找不到施放角度；AI 使用时成为团灭发动机。AI 评分中友军误伤仅 -40 分，无法阻止自杀式攻击。

**建议**：改为仅影响敌方（`target_team_filter = &"enemy"`）；或友军伤害减半。AI 友军误伤惩罚提升至 -150 分/友军。

### 问题 5：即死阈值 `max(50, 25%)` 对小体型过度残忍（高严重性）

最大 HP ≤ 200 的目标阈值固定为 50，中小怪血量过半即被斩杀。

**建议**：固定值降至 20 或完全移除，采用纯百分比。

### 问题 6：非即死伤害（6d6/10d6）对于 9 环过于可笑（中严重性）

失败分支平均 21 伤害，连低环法术都不如。

**建议**：失败分支提升至 10d6，大失败提升至 16d6；或追加"伤害降至即死阈值以下则直接即死"的斩杀联动。

### 问题 7：AP 消耗为 1 是经济模型崩坏（高严重性）

9 环法术随手丢出，没有机会成本。

**建议**：AP 消耗提升至 3 或全部 AP；或引入过载机制（每额外消耗 1 AP 提升 5% 即死阈值）。

### 问题 8：AI 评分导致友军杀手（中严重性）

3 敌 2 友场景下 AI 仍会施放。

**建议**：友军误伤惩罚提升至 -200 分/友军；或硬性规则：范围内有友军 HP < 50% 时直接禁用。

### 问题 9：大成功/大失败与自然骰绑定，无视豁免 DC 差距（高严重性）

豁免 +20 的 Boss 和豁免 +0 的杂兵，大成功/大失败概率完全相同。

**建议**：大成功条件改为 `final_roll >= DC + 10` 且 `natural_roll >= 18`；大失败改为 `final_roll <= DC - 10` 且 `natural_roll <= 3`。

### 问题 10："余悸"对不反击敌人完全无效（中严重性）

面对远程射手、纯法师 Boss，成功豁免后技能完全无效。

**建议**：余悸重新定义：下回合 AP-1 + 移动距离 -2 + 封锁反击。

### 问题 11：恐惧仅减 2 命中，对非物理敌人无效（中严重性）

依赖技能/法术的敌人几乎不受恐惧影响。

**建议**：恐惧同时提供：攻击 -2、技能 DC -1、移动速度 -1。

### 问题 12：状态持续时间硬编码为 60/120/180 ticks（低严重性）

隐含假设"1 轮 = 60 ticks"，若存在加速/减速机制则失效。

**建议**：引入常量 `TICKS_PER_ROUND = 60`，或以"轮数"为单位传入。

### 问题 13：缺少对"精神伤害免疫/抗性"的应对逻辑（低严重性）

未提及目标免疫精神伤害时的行为。

**建议**：明确检查精神抗性；若免疫则失败分支追加恐惧轮数 +1 或 2d6 物理伤害。

### 问题 14：工作量评估严重低估（低严重性）

实际需跨系统改动 + 平衡性反复测试，至少 2-3 个中等工作量。

**建议**：修正为 2-3 个中等工作量，并单独列出"平衡性测试与数值调优"作为高风险后续工作。

---

## 三、实现可行性审查意见

**核心指控**：存在 1 个致命运行时 Bug、3 个系统级兼容性断裂、多个边界条件漏洞。

### 问题 1：免疫目标被误判为大失败并触发即死（致命 Bug / 高严重性）

`BattleSaveResolver.resolve_save()` 在目标免疫时返回 `"natural_roll": 0`。设计文档的 `_resolve_save_grade` 将 `natural_roll <= 1` 判定为大失败。免疫反而成为即死触发器。

**建议**：在 `_resolve_save_grade` 最开头增加免疫检查：`if bool(save_result.get("immune", false)): return &"critical_success"`。

### 问题 2：AI 评分系统完全失效（高严重性）

`effect_defs = []` 导致 `BattleAiScoreService` 判定为零伤害零控制，AI 永不使用。

**建议**：必须在 `effect_defs` 中配置真实效果（即使 orchestrator 覆盖执行逻辑）。

### 问题 3：预览系统与实际执行严重脱节（高严重性）

`_preview_ground_skill_command()` 依赖 `effect_defs` 计算影响单位。`effect_defs = []` 会导致预览显示"影响 0 个单位、0 伤害"。

**建议**：新增专用 preview 分支，或保留 `effect_defs` 为 dummy 效果让 preview 逻辑自动走通。

### 问题 4：精神伤害直接扣血，绕过整个伤害减免/抗性/护盾系统（高严重性）

同架构审查第 4 点。

**建议**：构造标准 `CombatEffectDef` 调用 `_runtime._damage_resolver.resolve_effects()`。

### 问题 5：`save_tag` 被错误配置在 `CombatSkillDef` 级别，.tres 结构不合法（高严重性）

`CombatSkillDef` 根本没有 `save_tag` / `save_ability` 字段，这两个字段只存在于 `CombatEffectDef` 级别。Godot 加载时会报错或忽略。

**建议**：在 `effect_defs` 中配置 `CombatEffectDef`，将 `save_tag` 和 `save_ability` 放在 effect_def 上。

### 问题 6：即死逻辑 `_apply_instant_death` 未定义，且遗漏击杀链（高严重性）

直接设 `current_hp = 0` 会遗漏：击杀回资源、掉落、战报统计、成就记录。

**建议**：仿照 `_apply_unit_skill_result()` 的击杀后处理，调用完整的击杀链。

### 问题 7：新状态未同步到 `DEBUFF_STATUS_IDS`（中严重性）

`BattleRuntimeModule.DEBUFF_STATUS_IDS` 和 `BattleSkillTurnResolver.DEBUFF_STATUS_IDS` 未追加 `frightened`/`aftershock`/`reaction_lock`/`stunned`，导致 `doom_sentence_verdict` 的"2 个以上 debuff 主技能失效"机制不识别这些状态。

**建议**：在上述两份 debuff 字典中追加新状态 ID。

### 问题 8：`STRONG_ATTACK_DISADVANTAGE_STATUS_IDS` 未添加 `frightened`（中严重性）

`BattleState` 使用 `&"frightened"` 但攻击劣势字典中未注册，恐惧状态的攻击劣势风味缺失。

**建议**：在 `STRONG_ATTACK_DISADVANTAGE_STATUS_IDS` 中追加 `&"frightened": true`。

### 问题 9：Orchestrator 硬编码分支位置与现有架构冲突（中严重性）

行 369 是函数定义开头，不是插入点；且项目中已有 `BattleSpecialSkillResolver` 专门处理特殊技能。

**建议**：将核心逻辑封装到 `BattleSpecialSkillResolver`，orchestrator 只保留一行分发。

### 问题 10：反应动作映射过窄——只封锁反击，不封锁 Guard（中严重性）

`is_unit_guard_locked()` 只检查 `STATUS_BLACK_STAR_BRAND_NORMAL`，未检查 `reaction_lock`/`aftershock`。

**建议**：在 `is_unit_guard_locked()` 中追加新状态检查；或在 `get_skill_cast_block_reason()` 中增加封锁 Guard 的逻辑。

### 问题 11：多个辅助方法不存在（中严重性）

`_get_unit_max_hp()`、`_build_phantasmal_kill_effect_def()`、`_roll_dice()` 等在代码库中均不存在。

**建议**：实现这些辅助方法，或 inline 替换为现有 API（如 `attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)`）。

### 问题 12：`movement_tags` 心智过滤依赖未经验证的数据假设（低严重性）

假设 `&"mindless"`、`&"construct"`、`&"undead"` 等标签存在于 enemy templates 中，但无证据证明。

**建议**：优先使用 `BattleSaveResolver.is_immune(unit, &"illusion")`；若依赖 movement_tags 需同步检查所有 enemy templates。

### 问题 13：工作量估算严重偏低（低严重性）

实际需修改：Orchestrator 执行 + 预览、SpecialSkillResolver、语义表（4 状态）、RuntimeModule、SkillTurnResolver、BattleState、AI 评分、.tres 配置、Headless 回归测试（至少 5 个边界场景）。真实工作量应为中-高。

**建议**：重新排期，预留至少 1.5-2 个中等工作量。

### 问题 14：`target_team_filter = &"any"` 友军误伤的 AI 定位风险（低严重性）

AI 评分系统对 friendly_fire_penalty 的处理基于 `effect_defs` 中 `effect_target_team_filter`。`effect_defs` 为空时 AI 可能低估友军误伤。

**建议**：确保 `effect_defs` 中 `effect_target_team_filter = &"any"`。

### 问题 15：MP 消耗未指定（低严重性）

9 环法术的 `.tres` 配置中 `mp_cost = ...` 为省略号。

**建议**：根据法师 9 环法术梯度补全 MP 消耗。

---

## 四、D&D 规则还原度审查意见

**核心指控**：跨版本缝合怪，同时拿了 Weird 的 9 环 AOE、PF2e 的四级豁免、原创的 HP 条件斩杀、5e 的 psychic 伤害类型，却套了 Phantasmal Killer 的名字。

### 问题 1：环位与法术身份混淆（高严重性）

`Phantasmal Killer` 在所有 D&D 主流版本中均为 **4 环单体法术**；**9 环 AOE 版本叫做 `Weird`**。张冠李戴。

- **3.5e SRD**：PK — Level 4, Target: One living creature；Weird — Level 9, 30 ft. radius。
- **5e PHB**：PK — 4th-level, Target: one creature；Weird — 9th-level, 30-foot-radius sphere。
- **PF1e CRB**：与 3.5e 一致。
- **PF2e CRB**：PK — Spell 4, Targets one living creature。

**建议**：二选一。方案 A：改名 `Weird`（百怪夜行/群魇具现），保持 9 环 AOE，采用 Weird 机制。方案 B：降环至 4 环，范围改为单体，采用 PK 原版机制。

### 问题 2：核心机制完全原创——"条件斩杀"无规则来源（高严重性）

- **3.5e/PF1e**：PK/Weird 是**无条件即死**（Fortitude 失败直接死亡，不检查 HP）。
- **5e**：PK/Weird **根本不是即死法术**，而是持续恐惧 + 每轮伤害。
- **PF2e**：PK 仅在 Critical Failure 后才可能即死，且需二次 Fortitude 豁免，与 HP 百分比无关。

**建议**：
- 3.5e/PF1e 路线：移除 HP 阈值。先 Will disbelief 豁免，失败后再 Fortitude 豁免；Fort 失败则无条件即死，Fort 成功则受固定伤害（3.5e 为 3d6，Weird 为 3d6 + 震慑 1 轮 + 1d4 力量伤害）。
- 5e 路线：移除即死。失败则 frightened，持续时间内每轮结束再次豁免，失败受 4d10 psychic 伤害，成功则法术结束。
- PF2e 路线：移除 HP 阈值。Critical Failure 后追加 Fortitude save，失败才即死；Success 应为 frightened 1 而非余悸。

### 问题 3：双豁免被压缩为单豁免，且滥用四级豁免（高严重性）

- **3.5e/PF1e 核心风味是双豁免链**：先 Will 识破幻象，再 Fortitude 抵抗恐惧致死。
- **四级豁免（Critical Success/Failure）是 PF2e 独有**，3.5e/5e/PF1e 中均不存在。3.5e/PF1e 中 nat 1 是自动失败、nat 20 是自动成功，但**不会附加额外效果**；5e 中 nat 20/1 在豁免检定中不触发自动成功/失败（PHB p.205）。

**建议**：
- 3.5e/PF1e 路线：必须实现**双豁免链**。先 Roll Will save，失败后再 Roll Fortitude save。
- 5e 路线：单次 **Wisdom save**，并移除 nat 20/1 的豁免特殊效果。
- PF2e 路线：可保留四级豁免，但需全面采用 PF2e 的法术 DC、伤害缩放和状态规则。

### 问题 4：成功豁免后仍受负面效果，违背"成功 = 完全无效"原则（高严重性）

所有 D&D 版本中，成功豁免意味着法术对该目标完全无效：
- **3.5e**：Will save 成功则"recognize the image as unreal"，法术完全无效。
- **5e**："On a successful save, the spell ends" — 完全无效。
- **PF2e**：Success 为 frightened 1（PF2e 特定设计，也非"余悸"）。

**建议**：**删除"余悸"（aftershock）状态**。成功豁免后目标完全不受影响。

### 问题 5：目标数量错误——Phantasmal Killer 是单体法术（高严重性）

PK 在 3.5e/5e/PF1e/PF2e 中**全部是单体法术**，只有 Weird 才是 AOE。

**建议**：若技能名为 Phantasmal Killer，必须改为单体目标。若坚持 AOE，请改名为 Weird。

### 问题 6：伤害数值与骰型全面失准（中严重性）

- **3.5e/PF1e**：Fortitude 成功时固定 **3d6** 伤害（非 psychic，为无类型伤害）。
- **5e**：每轮结束失败时 **4d10 psychic** 伤害（非一次性，持续性伤害）。
- **PF2e**：Failure 时 **8d6 mental** 伤害，Critical Failure（Fortitude 成功后）**12d6 mental** 伤害。

**建议**：严格遵循选定规则基线的伤害数值。

### 问题 7："大失败"强化效果在原版 D&D 中不存在（PF2e 除外）（中严重性）

3.5e/5e/PF1e 中，豁免 nat 1 **不会导致法术效果被强化**。Critical Failure 附加更强效果是 **PF2e 专属**。

**建议**：移除大失败分支的强化效果。若坚持 PF2e 四级豁免，必须全面采用 PF2e 规则。

### 问题 8：状态效果与持续时间严重偏离原版（中严重性）

- **3.5e/PF1e**：PK 是 Instantaneous，没有持续 frightened（直接即死或伤害）；Weird 的 Fort 成功目标 stunned for 1 round。
- **5e**：frightened 持续 **1 分钟**（需 Concentration），目标可在**每轮结束时重复豁免**提前结束。
- **PF2e**：frightened 有特定数值（1/2/4），每轮自动减 1。

**建议**：
- 5e 路线：frightened 持续 1 分钟或直至 Concentration 中断，目标每轮结束可重复 Wisdom save 以结束效果。
- 3.5e 路线：PK 无持续状态；Weird 的 Fort 成功目标获得震慑 1 轮（非恐惧）。
- PF2e 路线：frightened 数值应为 1/2/4（随回合递减），非固定轮数。

### 问题 9：豁免属性错误——丢失了 Fortitude 的"恐惧致死"风味（中严重性）

3.5e/PF1e 的 PK/Weird 采用 **Will + Fortitude 双豁免**，叙事逻辑是"先用心智识破幻象，再用身体抵抗恐惧致死"。

**建议**：
- 3.5e 路线：第一豁免用 **Will**，第二豁免用 **Fortitude**。
- 5e 路线：使用 **Wisdom** save（非 willpower）。
- PF2e 路线：主豁免用 **Will**，Critical Failure 后追加 **Fortitude** save。

### 问题 10："余悸"与反应动作封锁是完全没有规则来源的原创惩罚（中严重性）

D&D 任何版本的 PK/Weird 中，**成功豁免都不存在"失去反应动作"或类似惩罚**。5e 甚至没有通用"反应动作池"概念。

**建议**：**彻底删除"余悸"（aftershock）和"reaction_lock"状态**。成功豁免 = 完全无效。

### 问题 11：法术标签错误——擅自添加"即死"类型（低严重性）

3.5e/PF1e 中 PK/Weird 的标签是 `Illusion (Phantasm) [Fear, Mind-Affecting]`，**没有 [Death] 标签**。虽然效果可致死，但机制上是通过恐惧造成的精神崩溃，而非死灵系的 Death Effect。

**建议**：3.5e/PF1e 标准标签应为 **Illusion + Fear + Mind-Affecting**。PF2e 可保留 Death trait，但需全面遵循 PF2e 规则。

### 问题 12：叙事断裂——将"最深恐惧"扁平化为"看到死亡"（低严重性）

原版 PK/Weird 的核心叙事是**"目标看到自己最恐惧的幻象"**（"the most fearsome creature imaginable to the subject" / "deepest fears"），是高度个性化的幻术。设计将所有目标的恐惧扁平化为统一的"死亡"。

**建议**：日志与技能描述应强调"目标看到了**自己最恐惧的幻象**"。若技术允许，可根据敌人类型/职业定制恐惧描述。

### 问题 13：射程未声明折算依据（低严重性）

设计射程 12 格。5e 中 PK/Weird 射程为 120 feet，3.5e 中为 Medium (100 ft. + 10 ft./level)。按 5 尺=1 格折算，12 格仅相当于 60 尺，显著短于原版。

**建议**：在文档中明确声明"12 格 = 60 尺，为适应战棋节奏进行的折算"；或提高至 24 格。

### 问题 14：缺失 Spell Resistance / Concentration（低严重性）

- **3.5e/PF1e**：PK/Weird 均受 **Spell Resistance** 影响（"Spell Resistance: Yes"）。
- **5e**：PK/Weird 均需 **Concentration**（最长 1 分钟），施法者维持专注期间若受伤需进行 Concentration saving throw。

**建议**：
- 3.5e/PF1e 路线：添加对 Spell Resistance / Magic Resistance 的检测。
- 5e 路线：添加 Concentration 机制。

### 问题 15：3.5e Weird 的 Fortitude 成功效果被完全遗漏（中严重性）

若设计意图还原 3.5e 的 Weird（9 环 AOE），其 Fortitude 成功目标的效果不仅是"不受即死"，还应受到 **3d6 伤害 + 震慑 1 轮 + 1d4 临时力量伤害**。

**建议**：若走 3.5e Weird 路线，Fortitude 成功目标应受到 3d6 伤害 + 震慑 1 轮 + 1d4 力量伤害（非 10d6 psychic）。

---

## 五、综合建议（供后续审查使用）

在动工前，设计团队必须回答：

1. **规则基线是什么？** 3.5e / 5e / PF1e / PF2e？选定后严格遵循，禁止跨版本缝合。
2. **技能身份是什么？** `Phantasmal Killer`（4 环单体）还是 `Weird`（9 环 AOE）？
3. **是否接受配置驱动架构？** 如果接受，必须恢复 `effect_defs` 并扩展 `CombatEffectDef`；如果坚持硬编码，需明确声明这是架构例外并承担技术债务。

**最低限度修复清单**（来自实现可行性审查）：
- [ ] 免疫检查前置（致命 Bug）
- [ ] `effect_defs` 恢复真实配置
- [ ] 预览系统兼容
- [ ] 精神伤害走 DamageResolver
- [ ] `.tres` 字段合法化（save_tag/save_ability 移到 effect_def）
- [ ] 即死逻辑补全击杀链
- [ ] 新状态同步 debuff 字典（RuntimeModule + SkillTurnResolver）
- [ ] `frightened` 加入攻击劣势字典（BattleState）
- [ ] 工作量评估修正为 1.5-2 个中等工作量

四个 Agent 已就核心缺陷达成共识：**当前方案不能按原样实施**。建议先回答上述三个关键问题，再基于明确的规则基线和架构策略重做设计文档。
