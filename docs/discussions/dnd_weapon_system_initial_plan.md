# 战棋项目 DND 武器系统初版落地方案

> 源需求：DND 风格武器系统初版设计
> 分析范围：CU-10、CU-12、CU-14、CU-15、CU-16

## Problem

当前战棋战斗希望把“普通攻击”切到 DND 风格，但不能直接把整个战斗系统重写成纯 5E、纯 2E、或纯 3.5。

这次方案的目标是：

- 先做可玩闭环，只把武器普通攻击做成 `d20` 攻击检定 + 武器骰伤害
- 内部统一一套命中与伤害流水线，避免 2E / 3.5 双引擎并行
- 保留后续技能系统继续走综合规则体系的空间
- 保留 BG3 风味中的日志可读性、地形价值、武器手感差异

非目标：

- 首版不把全部战技、法术、AOE 技能都改成 DND 攻击检定
- 首版不完整覆盖 THAC0 全细则
- 首版不完整覆盖 3.5 专长、借机、擒抱与全套动作经济
- 首版不同时落地 flank / facing / cover / backstab 的完整战场规则

## Current Ownership

### 当前状态真相源

- 武器和装备内容在 `scripts/player/warehouse/item_def.gd` 与 `data/configs/items/*.tres`
- 角色属性快照在 `scripts/systems/attribute_service.gd`
- 队伍成员到战斗单位的桥接在 `scripts/systems/character_management_module.gd`
- 战斗单位构建在 `scripts/systems/battle_unit_factory.gd`
- 战斗执行编排在 `scripts/systems/battle_runtime_module.gd`
- 伤害结算在 `scripts/systems/battle_damage_resolver.gd`
- 战斗选择和技能交互在 `scripts/systems/game_runtime_battle_selection.gd`

### 当前行为现状

- 武器目前只体现为 `equipment_type_id + attribute_modifiers`
- 装备武器会提高 `physical_attack / hit_rate`，但没有独立武器伤害骰、伤害类型、暴击范围、攻击来源
- `BattleUnitState` 目前不存武器运行时档案，也不存 AC、攻击规则来源、武器标签
- `BattleRuntimeModule` 当前天然是“技能驱动”，不是“武器动作驱动”
- 大部分技能仍走固定效果链，`BattleDamageResolver` 用的是固定伤害公式，不是攻击检定链
- 现有项目中只有 `repeat_attack` 分支会显式读取 `hit_rate - evasion`

### 当前不变约束

- 不应把战斗规则继续塞回 `GameRuntimeFacade` 或 UI
- 不能让 battle runtime 在执行期回查 party 装备状态，避免破坏现有 battle 夹具与测试独立性
- 不能直接套用标准 DND 3-18 属性修正公式，因为当前仓库的基础属性是小整数标度

## Options

### Option A：3.5 主干 + 2E 桥接，但只先落地武器普攻

- 内部统一为 `d20 + attack_bonus vs ascending AC`
- 2E 只保留为 `THAC0 -> attack_bonus` 的来源桥
- 武器普通攻击进入新命中链
- 旧技能继续走当前固定效果链

优点：

- 改动面最可控
- 可以在当前 repo 中形成最小可玩闭环
- 不会强行重写现有大量技能内容和 battle regression

风险：

- 首版会出现“普攻走新链、旧技能走旧链”的混合态
- 需要用清晰字段隔离两条执行路径，避免后续逻辑互相污染

### Option B：全量切换战斗命中内核

- 所有单体伤害技能全部改成 DND 命中检定
- `BattleDamageResolver` 从固定效果链改成统一攻击链

优点：

- 规则口径最纯
- 后续长期维护最整齐

风险：

- 现有 `CombatSkillDef`、AI、预览、日志、测试面会一起爆炸
- 不符合“先做武器普攻闭环”的切片目标

### Option C：只给武器加表现字段，不改战斗命中链

- 武器新增 `damage_dice` 等配置，但实际战斗仍走固定公式

优点：

- 上线较快

风险：

- 玩家体感不够 DND
- 后续技能体系复用价值低
- 规则和日志语义会出现名不副实

## Recommended Design

选择 **Option A**。

核心原则：

- 规则来源允许 `3.5 / 2E bridge / hybrid`
- 战斗底层只跑一套统一攻击流水线
- 首版只让“武器普通攻击”进入新流水线
- 现有主动技能默认继续走旧链，避免重构面失控

### 1. 统一攻击流水线

每次武器普通攻击都走：

1. `build_attack_profile()`
2. `roll_hit()`
3. `roll_damage()`
4. `apply_crit_and_resistance()`
5. `emit_battle_log()`

其中统一攻击档案建议包含：

```text
rule_source: core_35 | legacy_2e | hybrid_bg3
attack_rule_mode: weapon_attack
attack_bonus_total: int
target_ac: int
weapon_dice_count: int
weapon_dice_sides: int
damage_type: slashing | piercing | bludgeoning | ...
crit_range_min: int
crit_multiplier: int
damage_bonus_static: int
situational_notes: [string]
```

### 2. 命中口径

统一命中判定：

```text
roll = d20
attack_total = roll + attack_bonus_total

if roll == 1: miss
elif roll == 20: hit and crit_candidate
elif attack_total >= target_ac: hit
else: miss
```

3.5 来源：

```text
attack_bonus_total =
    base_attack_bonus
    + ability_mod
    + proficiency
    + enhancement
    + situational
```

2E 桥接来源：

```text
attack_bonus_from_thac0 = 20 - thac0

attack_bonus_total =
    attack_bonus_from_thac0
    + ability_mod
    + proficiency_delta
    + enhancement
    + situational
```

说明：

- 2E 差异只在构建 `attack_bonus_total` 时消化
- 进入命中判定后不再分叉
- 日志可以显示“2E bridge / THAC0 映射”，但执行只用统一攻击值

### 3. 防御口径

内部只维护升序 AC。

```text
target_ac =
    10
    + armor
    + shield
    + dex_or_agi_bonus
    + natural_armor
    + deflection
    + situational
```

说明：

- 不建议现在把 repo 全量切回旧设计文档中的降序 AC
- 当前落地目标是“统一攻击流水线”，不是重做整套角色成长体系
- 如需保留 2E 语义，放在日志和来源字段，不直接拆成第二套运行时 AC

### 4. 伤害口径

统一伤害模型：

```text
final_damage =
    (weapon_dice_roll * crit_dice_factor + static_bonus)
    * resistance_factor
```

首版规则：

- 暴击时只翻倍武器骰
- 静态加值不翻倍
- 抗性先支持 `immune / resist / vulnerable`
- 伤害类型先支持 `slashing / piercing / bludgeoning`

### 5. 武器数据模型

建议直接扩 `ItemDef`，不首版拆 `WeaponProfile`。

新增字段：

- `weapon_category`
- `damage_dice_count`
- `damage_dice_sides`
- `damage_type`
- `crit_range_min`
- `crit_multiplier`
- `attack_ability_mode`
- `damage_ability_mode`
- `range_normal`
- `range_max`
- `weapon_tags`
- `enchantment_bonus`
- `proficiency_group`
- `rule_source`

原因：

- 当前武器种子内容很少
- 首版以减少资源和读取方改动为先
- 等后续出现更多投掷、弹药、复杂派生后，再考虑拆 `WeaponProfile`

### 6. 运行时挂接方式

不新增新的 `BattleCommand` 类型。

首版采用：

- 继续走现有 `TYPE_SKILL`
- 新增通用技能定义：
  - `weapon_basic_attack_melee`
  - `weapon_basic_attack_ranged`
- 这两个技能声明 `attack_rule_mode = weapon_attack`
- `BattleRuntimeModule` 看到该模式时，转交新的武器攻击 resolver

这样可以直接复用：

- 战斗 HUD
- 目标选择
- AI 单位技能动作
- headless / snapshot / regression 协议

### 7. 战斗单位状态设计

`BattleUnitState` 需要新增“武器运行时快照”，由 `BattleUnitFactory` 在建 ally unit 时一次性注入。

不建议：

- 在攻击执行期回查 `PartyEquipmentService`
- 在 UI 或 facade 层动态拼武器规则

原因：

- battle runtime 需要保持独立夹具可测
- battle state 应该包含本轮战斗所需的完整只读攻击数据

### 8. 首版可落的 BG3 风味

可以做：

- 高打低 +2、低打高 -2
- 详细战斗日志
- 暴击武器骰翻倍
- 武器手感差异明显

不建议首版做：

- flank / backstab
- 基于 facing 的侧击 / 背击
- cover AC

原因：

- 当前 `BattleUnitState` 没有 facing
- 当前 edge / low wall 还没有正式 cover 规则 owner
- 这些规则一进首版，会把 battle grid 与 edge 设计一起扩大

### 9. repo 现有属性标度的桥接约束

当前项目的基础属性是小整数，不适合直接套标准 DND `floor((score - 10) / 2)`。

因此首版需要：

- 在 `AttributeService` 中单独定义 repo 适配的 `strength_mod / agility_mod / perception_mod`
- 命中与 AC 底盘沿用 repo 内部可接受的小数值标度
- 不把角色属性体系一起重构成原版 DND 属性尺

否则会出现：

- 绝大多数角色能力修正为负数
- 武器参数和命中区间完全失真

## Minimal Slice

### M1：武器普攻 DND 化

- 新增武器字段
- 新增武器基础攻击技能
- 新增武器攻击 resolver
- 近战 / 远程普攻接入 `d20 + attack_bonus vs AC`
- 伤害改为 `weapon_dice + static_bonus`

### M2：首批 6 把武器种子

- 长剑：`1d8 slashing`
- 匕首：`1d4 piercing`，`finesse / light / thrown`
- 战斧：`1d8 slashing`
- 戟：`1d10 slashing`，`reach / heavy / two_handed`
- 短弓：`1d6 piercing`
- 重弩：`1d10 piercing`，`loading`

### M3：日志增强

至少输出：

- 规则来源
- `d20`
- 攻击总值
- 目标 AC
- 是否暴击候选
- 武器骰结果
- 抗性倍率
- 最终伤害

### M4：跨版本一致性对拍

- 固定随机种子
- 同一配置分别跑 `rule_35` 与 `rule_2e_bridge`
- 检查命中率和期望伤害差异在允许区间

## Files To Change

### 必改

- `scripts/player/warehouse/item_def.gd`
- `scripts/player/warehouse/item_content_registry.gd`
- `data/configs/items/*.tres`
- `scripts/systems/attribute_service.gd`
- `scripts/systems/party_equipment_service.gd`
- `scripts/systems/character_management_module.gd`
- `scripts/systems/battle_unit_state.gd`
- `scripts/systems/battle_unit_factory.gd`
- `scripts/systems/battle_runtime_module.gd`

### 建议新增

- `scripts/systems/battle_attack_resolver.gd`

### 少量适配

- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/progression_content_registry.gd`
- `scripts/systems/game_runtime_battle_selection.gd`
- `scripts/ui/battle_hud_adapter.gd`

## Tests To Add Or Run

建议新增：

- `tests/battle_runtime/run_weapon_attack_regression.gd`

测试点：

1. `attack_total == AC` 时命中
2. 天然 `1 / 20` 必失 / 必中
3. 暴击只翻倍武器骰
4. 抗性 / 易伤倍率正确
5. 高低差修正正确
6. `finesse / reach / loading` 生效
7. `rule_35` 与 `rule_2e_bridge` 的结果差异受控
8. 未切换到新模式的旧技能结果不变

建议回归：

```bash
godot --headless --script tests/equipment/run_party_equipment_regression.gd
godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/battle_runtime/run_game_runtime_battle_selection_regression.gd
```

若 basic attack 注入影响战斗构建，再补跑：

```bash
godot --headless --script tests/battle_runtime/run_battle_runtime_ai_regression.gd
```

## Project Context Units Impact

- 主要影响：CU-10、CU-12、CU-14、CU-15、CU-16
- 轻量联动：CU-06、CU-18

当前 `docs/design/project_context_units.md` 主体结构仍有效。

若后续正式实现本方案，建议只补充一句：

- CU-16 已引入“武器普通攻击 sidecar + 统一 d20 命中链 + 2E THAC0 兼容桥”

## 结论

这次武器系统初版最稳的做法不是重写整个战斗系统，而是：

- 用 `ItemDef` 扩字段承载武器语义
- 用 `BattleUnitFactory` 把当前武器快照注入 battle unit
- 用 `TYPE_SKILL + attack_rule_mode` 复用现有交互链
- 用独立 `BattleAttackResolver` 承载新的 DND 普攻流水线
- 只先改“武器普攻”，不连带改全技能

这样可以在当前仓库里以最小耦合拿到一个可玩的 DND 武器闭环，并为后续技能体系逐步接入 2E / 3.5 混合规则留出干净扩展点。
