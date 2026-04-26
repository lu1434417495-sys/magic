# BG3 武器骰整合 + 战斗换装方案讨论

更新日期：`2026-04-26`

## 状态

- 当前状态：`Active Discussion Record`
- 范围：将 `docs/design/weapon_types_damage.md`（BG3 武器类型与基础骰）整合进当前武器系统的方案讨论；以及随之带出的"战斗中支持完整背包换装"的工程拆解。
- 说明：本文件是设计讨论纪要，不代表代码与数据已完成落地。需要 codex 评估方案可行度。

---

## 1. 背景：当前武器系统的接缝

### 数据侧

- `scripts/player/warehouse/item_def.gd` (ItemDef) 已有：
  - `equipment_slot_ids` / `occupied_slot_ids`（双手武器靠后者把 main_hand + off_hand 一起占）
  - `weapon_attack_range`
  - `weapon_physical_damage_tag`（仅 `physical_slash` / `physical_pierce` / `physical_blunt` 三选一）
- 模板继承 `data/configs/items_templates/`：
  - `weapon_melee_base → weapon_melee_{one,two}_handed_base → weapon_{sword,greatsword,axe,dagger,mace}_*_base`
  - 粒度是"武器家族"，**不是 BG3 31 类武器**。
- 具体物品 `data/configs/items/`：仅 5 把概念武器（`bronze_sword` / `iron_greatsword` / `militia_axe` / `watchman_mace` / `scout_dagger`），都只继承家族模板 + 写一些 `attack_bonus` 的 `attribute_modifier`，**完全不携带骰子信息**。

### 战斗侧

- `battle_unit_factory.gd:350` 通过 `character_management_module.get_member_weapon_physical_damage_tag()` 把主手武器的 damage_tag 写入 `BattleUnitState.weapon_physical_damage_tag`。
- 整套武器→战斗的数据流目前**只传 1 个 byte 的物理伤害类型**。
- `battle_damage_resolver.gd:527 _roll_damage_dice()` 从 `effect_def.params.dice_count/dice_sides/dice_bonus` 读骰，**骰子完全是技能配的**。
- `battle_damage_resolver.gd:572 _resolve_damage_tag()` 在 `params.use_weapon_physical_damage_tag = true` 时才用武器 tag 覆盖技能 tag。
- 全工程仅 `data/configs/skills/warrior_heavy_strike.tres` 用上了这条路径（1d8 + 武器 tag）；其他 26 个 warrior/archer/saint 技能都是写死整数 `power = 10/12`。

### 当前公式

`battle_damage_resolver.gd:466-468`：

```
base_damage = effect_def.power + skill_dice_total + skill_dice_bonus
```

武器只贡献 `damage_tag`，**没有任何"武器贡献骰"的路径**。

---

## 2. 核心设计取舍：加性模型

### 问题

BG3 的骰是从**武器**给的，技能给倍数/特效；当前代码是骰从**技能**给，武器只给伤害类型。这两套语义直接互斥。

### 结论：走加性模型

把公式改成：

```
base_damage = weapon_dice (按需)
            + effect_def.power
            + skill_dice
            + skill_dice_bonus
```

每一项可为 0。BG3 的几种典型技能都能自然落进来：

| 技能类型 | weapon_dice | skill_dice | power | 例子 |
|---|---|---|---|---|
| 平砍 / 基础攻击 | ✅ 1d8 长剑 | 0 | 0(+力量) | 长剑挥砍 = 1d8 |
| 武器增益技 | ✅ | +1d8 | 0 | 重击 = 武器骰 + 1d8 |
| 纯加值修正 | ✅ | 0 | +5 | 稳准狠 +5 |
| 替换骰（柄击） | ❌ | 1d4 钝击 | 0 | 不论武器一律 1d4 钝击 |
| 纯法术 | ❌ | 3d6 火焰 | 0 | 火球术只看技能骰 |

实现上只需一个 flag：`params.add_weapon_dice = true/false`（或者按伤害类型自动决定 — physical 默认 true、magic/fire/freeze 默认 false）。`_roll_damage_dice` 旁边加 `_roll_weapon_dice(source_unit, effect_def)`，在 `_resolve_damage_outcome` 把它累加进 `base_damage`。

### `warrior_heavy_strike` 在新模型下的语义修复

- 当前配置 `power = 0, dice 1d8, use_weapon_physical_damage_tag = true` 的语义是 **"无视武器骰、固定甩 1d8、按武器 tag 算"** —— 拿匕首和拿大剑都打 1d8，不符合"重击"语义。
- 加性模型下应改成 `add_weapon_dice = true, dice 1d8`：匕首打 1d4+1d8、大剑打 2d6+1d8。

### 暴击与骰子事件语义

用户结论：

- 暴击时翻倍 `weapon dice + skill dice`，不翻倍 `power` 这类 flat 伤害。
- 暴击的实现口径应优先按"额外再掷一组相同骰子"处理，而不是简单把第一次骰子结果乘 2。
- 暴击会直接满足相关骰子事件判定；非暴击时，不同事件使用不同判定口径。

推荐伤害公式：

```text
normal:
base_damage = weapon_dice + skill_dice + skill_dice_bonus + power

critical_hit:
base_damage = weapon_dice
            + critical_extra_weapon_dice
            + skill_dice
            + critical_extra_skill_dice
            + skill_dice_bonus
            + power
```

骰子事件字段拆成 3 类，不能继续共用单一 `damage_dice_is_max`：

```text
damage_dice_high_total_roll
  用于金刚不坏这类"承受高额骰伤害"事件。
  - critical_hit 且存在任意骰组：true
  - 非暴击：weapon_dice_total + skill_dice_total >= 常规骰理论最大值的 80%

skill_damage_dice_is_max
  用于重击这类"技能骰本身打满"事件。
  - critical_hit 且存在 skill dice：true
  - 非暴击：skill_dice_total == skill_dice_max_total
  - 不看 weapon dice

weapon_damage_dice_is_max
  用于武器精通被动技能提供熟练度。
  - critical_hit 且存在 weapon dice：true
  - 非暴击：weapon_dice_total == weapon_dice_max_total
  - 不看 skill dice
```

对应 reason 字段：

```text
damage_dice_high_total_roll_reason = "critical_hit" / "dice_threshold"
skill_damage_dice_is_max_reason = "critical_hit" / "skill_dice_max"
weapon_damage_dice_is_max_reason = "critical_hit" / "weapon_dice_max"
```

边界规则：

- 如果某次攻击没有对应骰子组，相关字段必须为 `false`，不能因为 `0 == 0` 触发。
- `damage_dice_high_total_roll` 的非暴击阈值只看常规 `weapon_dice + skill_dice`，不包含暴击额外骰；暴击分支已经直接满足。
- `skill_damage_dice_is_max` 与 `weapon_damage_dice_is_max` 在非暴击时分别只看自己的骰组，避免重击、金刚不坏和武器精通被动互相污染。
- 多段 `damage` effect 每段独立判定：同一次 attack 的 `critical_hit` 可以传给所有 damage effect，但上述 3 类骰子事件字段必须按每个 `damage_event` 单独计算。
- 金刚不坏的熟练度触发还必须要求该段 `damage_event` 实际扣到 HP；只被护盾吸收、固定减伤吸收、免疫或其他 mitigation 压到 `hp_damage == 0` 时不给熟练度。
- 顶层 result 只做 OR 汇总，便于现有奖励 / 日志入口读取：

```text
result.damage_dice_high_total_roll = any(event.damage_dice_high_total_roll)
result.skill_damage_dice_is_max = any(event.skill_damage_dice_is_max)
result.weapon_damage_dice_is_max = any(event.weapon_damage_dice_is_max)
```

这样多段伤害不会互相污染。例如第一段武器骰没投高、第二段技能骰投高时，只有第二段 `damage_event` 的 `skill_damage_dice_is_max` 为 true；顶层 result 只表达"本次结算至少有一段满足"。

金刚不坏熟练度判定读取每段 event：

```text
vajra_body_mastery_event =
  event.damage_dice_high_total_roll == true
  and event.hp_damage > 0
```

对应实现注意：

- `_collect_vajra_body_mastery_source_ids()` 不能只看顶层 `critical_hit`；必须确认至少一个 damage event 满足上面的 `vajra_body_mastery_event`。
- `_count_vajra_body_mastery_hits()` 应按满足条件的 event 计数。
- 重击和武器精通仍按各自骰组表现判断，不要求实际扣 HP，除非后续单独改变对应熟练度口径。

### 预览伤害口径

用户结论：

- 战斗预览只需要计算命中率。
- 预计伤害只展示**非暴击情况下的基础伤害理论上下限**。
- 预览文案使用 `伤害 X-Y`；如果上下限相同，则使用 `伤害 X`。
- 多段 damage effect 在预览里汇总成总范围，不逐段展示。
- 预览和 AI 评分不允许调用正式掷骰结算，不能消耗 `TrueRandomSeedService`。
- `伤害 X-Y` 不考虑目标抗性 / 免疫 / 易伤、护盾、格挡、固定减伤、最低伤害保底、`offense_multiplier`、`damage_ratio_percent` 等正式结算修正；这些只在真实命中后的 damage resolver 中生效。

非暴击范围公式：

```text
min_damage = weapon_dice_count
           + skill_dice_count
           + skill_dice_bonus
           + power

max_damage = weapon_dice_count * weapon_dice_sides
           + skill_dice_count * skill_dice_sides
           + skill_dice_bonus
           + power
```

多段 damage effect：

```text
preview_min_damage = sum(effect_min_damage)
preview_max_damage = sum(effect_max_damage)
```

建议实现：

- 新增纯预览服务或纯函数，例如 `BattleDamagePreviewService.build_non_critical_base_damage_range(source_unit, effect_defs)`。
- 该服务只读 `effect_def.params`、weapon profile、`power`、`dice_bonus` 等静态 / 当前状态字段。
- 不调用 `BattleDamageResolver.resolve_effects()`。
- 不掷骰、不改 target、不触发 status / shield / mastery / report。
- UI 的命中率仍由 hit preview 链计算；伤害范围服务不需要读取 target 状态。
- UI 输出例子：

```text
命中率 65%
伤害 2-12
```

### 对 26 个老技能的影响

- 默认 `add_weapon_dice = false`，老技能逐字保持原行为。
- 把它当成"愿意接武器熟练系的技能就开 flag、平衡时把 power 调小"，按职业小批量迁。

### 技能参数与运行时 effect 复制结论

用户结论：

- `add_weapon_dice` 必须显式配置；不按 physical damage 自动默认开启。
- 连击 / 多段 attack 允许重复计算武器骰。也就是每段 stage effect 如果配置了 `add_weapon_dice = true`，每段都各自加入当前武器骰。
- 冲锋路径伤害不能手动 new 半残 `CombatEffectDef`；运行时临时 effect 必须完整复制原 effect，再只追加运行时字段。

推荐实现口径：

```gdscript
func duplicate_for_runtime() -> CombatEffectDef:
	var copy := duplicate(true) as CombatEffectDef
	if copy == null:
		return null
	if copy.params == null:
		copy.params = {}
	else:
		copy.params = copy.params.duplicate(true)
	return copy
```

- `BattleChargeResolver` 的 path step AOE 使用 `path_step_aoe_effect.duplicate_for_runtime()`，保留 `params.add_weapon_dice`、`params.use_weapon_physical_damage_tag`、`dice_count/dice_sides/dice_bonus`、`damage_ratio_percent`、`bonus_condition` 等字段。
- `BattleRepeatAttackResolver` 的 stage effect 也使用同一个 helper；如果需要追加 `runtime_pre_resistance_damage_multiplier`，只写入复制体的 `params`。
- 规则边界：resolver 只负责"这一段打到谁 / 重复几次"，不负责重写伤害 effect 的内容。

---

## 3. Versatile（两用武器）：B 模式（动态决定）

### 问题

BG3 中长剑 / 战斧 / 战锤 / 长矛 / 三叉戟是 versatile，骰记作 `1d8 / 1d10` —— 副手有盾时按 1H、副手空时按 2H。

### 三种方案与选择

- **A 静态决定**：factory 开战时定死哪套骰，整场不变。最简单。
- **B 动态决定**：unit_state 同时存 1H 和 2H 两套，结算时按当前副手状态选。
- **C 完全无视 versatile**：每把武器只配一套骰。

**结论：选 B**，因为后面要支持完整战斗换装（详见 §4），握法切换/卸盾在战斗中要能立刻反映到骰上。

### `BattleUnitState` 字段设计

```gdscript
var weapon_damage_dice_count_one_handed: int = 0
var weapon_damage_dice_sides_one_handed: int = 0
var weapon_damage_dice_count_two_handed: int = 0   # 仅 versatile 填，否则与 1H 同
var weapon_damage_dice_sides_two_handed: int = 0
var weapon_is_versatile: bool = false
var weapon_uses_two_hands: bool = false            # 当前生效的握法
var weapon_attack_range: int = 1                   # 当前武器 profile 投影出的基础射程
```

- `weapon_physical_damage_tag` 维持原样（一手二手 tag 不变）。
- `weapon_attack_range` 不再继续依赖 `attribute_snapshot.weapon_attack_range` 作为战斗读取真相源；它来自当前武器 profile 的 battle projection。
- `_resolve_damage_outcome` 只看 `weapon_uses_two_hands` 选骰，不关心 versatile —— 解算路径上只有一个 bool。
- 序列化要扩 `to_dictionary()` / `from_dictionary()`（参考 `battle_unit_state.gd:230,278` 现有那一对 weapon_physical_damage_tag 的写法）。

---

## 4. 战斗换装范围：(a) 完整背包换装

### 三种范围比较

- **(a) 完整战斗换装**：战斗中开装备界面，任意槽位换任意物品（含护甲/饰品）。
- **(b) 仅 versatile 切握法**：长剑装盾时单手、空时双手；只是翻 `weapon_uses_two_hands` 的 bool。
- **(c) 武器预设切换**：战前配 1-2 套预设（剑+盾 / 双手大剑），战中 AP 切换。

**用户决策：选 (a)**。

### (a) 的成本量级

(a) 比 dice 整合大一个数量级，必须分 PR 做。它意味着：

- 战斗中要有一条 mutate `BattleUnitState` 装备视图的命令。
- 装备一改，`attribute_modifiers`（`attack_bonus`、`armor_class` 等）都跟着武器走，整个 `attribute_snapshot` 都得重算。
- 这条流水线现在不存在 —— `attribute_snapshot` 只在 `battle_unit_factory.gd:67-180` 一次性建好就不动了。
- 战斗 UI 要新开装备面板。
- 序列化、回放确定性都要扩。

---

## 5. PR 拆分建议

### PR1：`WeaponProfileDef` + 武器 dice 数据落地（约 1-3 天）

用户决策：采用方案 C，新增 `WeaponProfileDef`，`ItemDef` 只持有 `weapon_profile`。不走 `ItemDef` 顶层裸字段过渡。

- 新增：
  - `WeaponProfileDef`
  - `WeaponDamageDiceDef`
- `WeaponProfileDef` 持有：
  - `weapon_type_id: StringName` — `shortsword` / `greatsword` / `handaxe` / `mace` / `natural_weapon` / `unarmed` …
  - `training_group: StringName` — `simple` / `martial` / `natural` / `unarmed`
  - `range_type: StringName` — `melee` / `ranged` / `thrown`
  - `family: StringName` — `sword` / `axe` / `mace` / `natural` / `unarmed` …
  - `damage_tag: StringName` — `physical_slash` / `physical_pierce` / `physical_blunt`
  - `attack_range: int`
  - `one_handed_dice: WeaponDamageDiceDef`
  - `two_handed_dice: WeaponDamageDiceDef`
  - `properties_mode`
  - `properties: Array[StringName]` — `light` / `finesse` / `thrown` / `versatile` / `two_handed` / `reach`
- 模板分层重做：
  - 顶层：`weapon_{simple,martial}_{melee,ranged}_base.tres` 4 个。
  - 中层：按 BG3 31 类**实际会用到的子集**各一个 `weapon_<id>_base.tres`，把骰、damage_tag、properties 一次写死。
- 现有武器按 BG3 重映射并扩字段：

  | 现物 | BG3 类型 | 骰 | tag | properties |
  |---|---|---|---|---|
  | bronze_sword | Shortsword | 1d6 | physical_pierce | finesse, light |
  | iron_greatsword | Greatsword | 2d6 | physical_slash | two_handed |
  | militia_axe | Handaxe | 1d6 | physical_slash | light, thrown |
  | watchman_mace | Mace | 1d6 | physical_blunt | — |
  | scout_dagger | 移除 | — | — | — |

  > 用户决策：`bronze_sword` 定为 Shortsword；`militia_axe` 定为 Handaxe；`scout_dagger` 直接从种子武器中移除。

- `BattleUnitState` 加 §3 那 6 个字段。
- factory 开战时按当前装备初始化所有武器派生字段（含 versatile + off_hand 空 → uses_two_hands=true 的判定）。
- `_resolve_damage_outcome` 加 `add_weapon_dice` fallback。
- `warrior_heavy_strike` 改成加性样板 (`add_weapon_dice = true`)。
- 测试：扩 `tests/warehouse/run_item_template_inheritance_regression.gd`、`tests/equipment/run_party_equipment_regression.gd`；新增 `run_battle_weapon_dice_regression.gd` 验证 add_weapon_dice fallback。

**PR1 完全不碰战斗换装**。但把 (a) 需要的"武器派生数据放在 unit_state 上"的接口先备好。在 PR1 完成而 PR2 未上线的中间窗口期，versatile 武器只能按 factory 初始决定的握法用一辈子 —— 作为中间状态可接受。

### PR1 补充：敌方也接入武器系统

用户决策：

- 敌人也要接入同一套武器骰 / 武器 profile / battle projection 体系，不能继续只靠 `attribute_overrides.weapon_attack_range` 与 `weapon_physical_damage_tag` 这类散字段。
- 野兽类敌人拥有"天生武器"，不从共享仓库或装备实例读取。
- 野兽天生武器伤害暂定为 `1D6`。

推荐口径：

- `EnemyTemplateDef` 持有敌方武器来源。
  - 非野兽 / humanoid / armed enemy：必须显式携带一件真实攻击装备；这不是纯 `weapon_profile` 数值引用，而是可进入装备 / 缴械 / 掉落 / 战斗投影语义的装备对象。
  - 带 `beast` 标签的敌人：若未显式配置 weapon profile，则 registry / roster build 阶段给一份默认 `natural_weapon` profile。
- 默认野兽 natural weapon：
  - `weapon_type_id = natural_weapon`
  - `family = natural`
  - `range_type = melee`
  - `damage_tag = physical_blunt`
  - `attack_range = 1`
  - `one_handed_dice = 1D6`
  - `two_handed_dice = 1D6`
  - `properties = []`
  - `weapon_is_versatile = false`
  - `weapon_uses_two_hands = false`
- 具体野兽模板可以覆写 natural weapon 的 `damage_tag`：
  - 狼咬 / 毒刺 / 角顶：`physical_pierce`
  - 爪击 / 撕裂：`physical_slash`
  - 拍击 / 冲撞 / 踩踏：`physical_blunt`
- `EncounterRosterBuilder / BattleUnitFactory` 不再只写敌方 `attribute_snapshot.weapon_attack_range`，而是把敌方 weapon profile 投影到 `BattleUnitState` 的武器字段：
  - `weapon_damage_dice_count_one_handed`
  - `weapon_damage_dice_sides_one_handed`
  - `weapon_damage_dice_count_two_handed`
  - `weapon_damage_dice_sides_two_handed`
  - `weapon_is_versatile`
  - `weapon_uses_two_hands`
  - `weapon_attack_range`
  - `weapon_physical_damage_tag`
- 敌方 AI、技能射程、伤害结算与玩家单位读取同一组 `BattleUnitState` weapon projection 字段。
- 空手攻击：
  - 玩家与非野兽敌人都允许在无武器时空手攻击。
  - 内容校验层仍要求非野兽敌人携带攻击装备；缺武器应作为配置错误暴露。
  - 若运行时真的出现无武器状态（例如后续缴械、装备损坏、测试夹具刻意构造），可以降级为空手攻击。
  - 空手攻击 profile：
    - `weapon_type_id = unarmed`
    - `family = unarmed`
    - `range_type = melee`
    - `damage_tag = physical_blunt`
    - `attack_range = 1`
    - `one_handed_dice = 1D4`
    - `two_handed_dice = 1D4`
    - `properties = []`
    - `weapon_is_versatile = false`
    - `weapon_uses_two_hands = false`
- 空手攻击只能支撑普通攻击 / 不要求武器的技能；不能满足 `requires weapon` 或当前由 `use_weapon_physical_damage_tag` 推导出的"需要武器才能施展"技能条件。
- 空手攻击不参与"武器熟练 / 武器精通"事件。
- 天生武器不参与"武器熟练 / 武器精通"事件。PR1 只让它参与伤害骰，不触发玩家武器精通类奖励。
- `EnemyTemplateDef.attribute_overrides.weapon_attack_range` 在 PR1 一次性移除；敌人射程只从 weapon profile 投影到 `BattleUnitState.weapon_attack_range`。
- 非野兽敌人的真实攻击装备是敌人的装备实例，可被缴械、可影响战斗投影，但死亡不自动掉落；战斗掉落仍只看 `EnemyTemplateDef.drop_entries`。

### PR1 补充：模板继承字段语义的 3 个方案

当前 `ItemContentRegistry.merge_with_template()` 是手写字段合并，不是 Godot 原生继承。新增武器骰、武器属性、分类字段时，不能只在 `ItemDef` 上加字段；否则会出现模板字段没有流到实例、`0` 无法区分"继承"和"显式清空"、数组属性被错误合并等问题。

#### 方案 A：继续在 `ItemDef` 上加裸字段，补齐合并规则

形态：

```gdscript
@export var weapon_damage_dice_count := 0
@export var weapon_damage_dice_sides := 0
@export var weapon_damage_dice_count_two_handed := 0
@export var weapon_damage_dice_sides_two_handed := 0
@export var weapon_properties: Array[StringName] = []
@export var weapon_category: StringName = &""
@export var weapon_family: StringName = &""
@export var weapon_range_type: StringName = &""
```

合并规则：

- 数值字段沿用现状：实例非 0 覆盖，否则继承模板。
- `weapon_properties` 非空覆盖，否则继承模板。
- 标量 StringName 非空覆盖，否则继承模板。

优点：

- 改动最小。
- `.tres` 结构直观，PR1 最容易快速落地。

主要问题：

- `0` 不能表达"显式无骰 / 显式无射程"，只能表达继承。
- `weapon_properties` 无法表达"显式清空"。
- `ItemDef` 顶层继续膨胀，后续投掷、熟练、特殊武器规则都会继续散落。
- 合并逻辑会变得越来越脆，容易漏字段。

结论：只适合临时过渡，不推荐作为长期方案。

#### 方案 B：保留 `ItemDef` 顶层字段，但给骰子与覆盖语义加结构

形态：

```gdscript
@export var weapon_damage_dice_one_handed: WeaponDamageDiceDef = null
@export var weapon_damage_dice_two_handed: WeaponDamageDiceDef = null
@export var override_weapon_properties := false
@export var weapon_properties: Array[StringName] = []
@export var override_weapon_attack_range := false
@export_range(0, 99, 1) var weapon_attack_range := 0
```

`WeaponDamageDiceDef`：

```gdscript
class_name WeaponDamageDiceDef
extends Resource

@export_range(0, 99, 1) var count := 0
@export_range(0, 99, 1) var sides := 0
```

合并规则：

- dice resource 为 `null` 表示继承；非 null 表示实例覆盖。
- `override_weapon_properties = true` 时用实例数组，即使为空；否则非空覆盖，空继承。
- `override_weapon_attack_range = true` 时允许显式写 0；否则非 0 覆盖，0 继承。

优点：

- 能解决最关键的 `0` 值歧义。
- 比方案 A 更安全，迁移成本仍可控。
- 不必一次性重构所有武器读取方。

主要问题：

- 顶层字段仍然分散在 `ItemDef`。
- 合并规则分散在 `ItemContentRegistry`，新增武器字段仍需要逐个记得补。
- 武器 profile 不是一个可传递、可验证、可投影的整体，PR2 仍需要再聚合一次。

结论：可以作为中间方案，但如果确定要做 PR2 战斗换装，不如直接上方案 C。

#### 方案 C：新增 `WeaponProfileDef`，让武器 profile 自己负责合并

形态：

```gdscript
class_name WeaponProfileDef
extends Resource

@export var weapon_type_id: StringName = &""        # shortsword / greatsword
@export var training_group: StringName = &""        # simple / martial
@export var range_type: StringName = &""            # melee / ranged / thrown
@export var family: StringName = &""                # sword / axe / bow
@export var damage_tag: StringName = &""            # physical_slash / pierce / blunt
@export_range(0, 99, 1) var attack_range := 0

@export var one_handed_dice: WeaponDamageDiceDef = null
@export var two_handed_dice: WeaponDamageDiceDef = null
@export var properties_mode := PropertyMergeMode.INHERIT
@export var properties: Array[StringName] = []
```

`ItemDef` 只持有：

```gdscript
@export var weapon_profile: WeaponProfileDef = null
```

`WeaponProfileDef.merge(template_profile, instance_profile)` 负责 profile 内部合并：

- 标量字段：实例非空 / 非 0 覆盖，否则继承模板。
- dice：实例 dice 非 null 覆盖，否则继承模板。
- properties：不默认合并，按 mode 明确处理：
  - `INHERIT`：继承模板 properties。
  - `REPLACE`：使用实例 properties，即使为空。
  - `ADD`：模板 + 实例去重。
  - `REMOVE`：从模板中移除实例列出的 properties。

推荐数据形态：

```text
data/configs/items_templates/
  weapon_base.tres
  weapon_simple_melee_base.tres
  weapon_martial_melee_base.tres
  weapon_type_shortsword_base.tres
  weapon_type_greatsword_base.tres

data/configs/items/
  bronze_sword.tres -> base_item_id = weapon_type_shortsword_base
```

优点：

- 武器定义成为一个完整 profile，可直接给 `PartyEquipmentService`、`CharacterBattleProjectionService`、战斗 UI 和 headless snapshot 使用。
- 合并语义集中在 `WeaponProfileDef`，不再让 `ItemContentRegistry.merge_with_template()` 继续膨胀。
- PR1 的武器骰、PR2 的战斗换装、后续熟练 / AI / 投掷 / 盾牌联动都能复用同一份结构。
- 可以逐步把旧顶层 `weapon_physical_damage_tag` / `weapon_attack_range` 迁入 profile，避免双真相源。

主要问题：

- PR1 初始改动最大。
- 需要一次性迁移现有武器模板与测试。
- 如果不保留旧字段 fallback，所有旧资源必须同步更新；按当前兼容策略，这是可接受但必须明确的破坏性资源 schema 更新。

结论：**已选方案 C**。不走方案 A / B 的过渡路径；PR1 直接把武器数据收束成 `WeaponProfileDef`。

### PR1 补充：`weapon_attack_range` 迁移结论（采用射程方案 B）

这里的"方案 B"指射程迁移方案：**射程从 `attribute_snapshot` 迁出，改由武器 profile / battle projection 投影到 `BattleUnitState.weapon_attack_range`**。这不是上一节模板继承方案 B。

#### 旧方案 A：继续写入 `attribute_snapshot.weapon_attack_range`

做法：

- `ItemDef.get_attribute_modifiers()` 继续把武器射程伪装成属性 modifier。
- 开战和战斗换装后都重算 `attribute_snapshot`，现有读取方继续读 `attribute_snapshot.weapon_attack_range`。

问题：

- 武器射程本质是装备 profile 的派生字段，不是角色属性；继续放在 snapshot 会扩大 `AttributeService` 的职责。
- 战斗换装后如果只更新 weapon profile / dice 而漏重算 snapshot，射程会静默过期。
- 后续同一武器 profile 还要服务 damage tag、dice、properties、熟练、AI 评分和 UI 展示；只有射程继续绕到 attribute，会形成双路径。

#### 采用方案 B：射程进入 battle weapon projection

做法：

- `WeaponProfileDef.attack_range` 是静态真相源。
- `CharacterBattleProjectionService` 输出当前装备投影，其中包含 `weapon_profile` 与 `weapon_attack_range`。
- `BattleUnitFactory` 开战时把投影结果写入 `BattleUnitState.weapon_attack_range`。
- 战斗换装命令重新投影当前 battle-local equipment view，并更新 `BattleUnitState.weapon_attack_range`、weapon dice、damage tag、properties 与 `attribute_snapshot`。
- 当前读取 `attribute_snapshot.weapon_attack_range` 的战斗链路改读 `BattleUnitState.weapon_attack_range`：
  - `BattleRuntimeModule`
  - `BattleHudAdapter`
  - `GameRuntimeBattleSelection`
  - `BattleSpawnReachabilityService`

状态 / buff 口径：

- 武器 profile 给的是基础射程。
- 状态、地形、职业或技能提供的射程加成不要重新塞回武器 profile；它们应作为战斗规则层的额外修正叠加在 `unit.weapon_attack_range` 之后。
- 因此最终射程读取建议封装成 `BattleRangeService.get_effective_attack_range(unit, battle_state, skill_def)`，避免 UI、AI、可达性各自手写 `base + status_bonus`。

结论：

- PR1/PR2 按射程方案 B 做。
- `attribute_snapshot.weapon_attack_range` 作为战斗读取真相源应被移除；如果仍需角色面板展示射程，也应从 battle/character projection 读取，不再由 `AttributeService` 注入。
- 不加旧字段 fallback；现有武器资源和测试应随 `WeaponProfileDef.attack_range` 一次性迁移。

### PR1 补充：需要武器的技能判定

用户决策：

- 新增显式 `params.requires_weapon = true` 表达技能必须持有武器才能施展。
- `params.use_weapon_physical_damage_tag = true` 只负责"伤害类型用武器 tag 覆盖"，不再隐含"需要武器才能施展"。
- 空手攻击、天生武器、未持有效武器的单位不能满足 `requires_weapon`。
- 迁移现有技能时，当前依赖 `use_weapon_physical_damage_tag` 阻断无武器施展的技能，需要同步补 `requires_weapon = true`。

### PR2：战斗换装管线（约 1-2 周）

依赖 PR1 落地。建议再细分：

- **PR2-a：battle-local 装备 / 队伍共享背包状态与投影服务**。建立 `BattleUnitState/BattleState` 内的 `equipment_view`、队伍共享背包 view、装备实例身份、属性重算、武器 projection 与战后回写链路，但可以先不接 UI。
- **PR2-b：战斗换装命令 + UI / headless 接入**。支持所有槽位换装、自动联动、2 AP 消耗、HP/MP/stamina clamp、战斗日志与刷新。

用户决策：

- 战斗中所有装备槽都允许换装，不再限制为武器 / 副手 / 盾。
- 战斗中换装目标只能是当前行动单位自己；不能替其他友方单位换装。
- 战斗换装状态住在 `BattleUnitState/BattleState` 的 battle-local view，战斗结束再写回 `PartyMemberState`；战斗中不要直接 mutate party 装备状态。
- 战斗中打开队伍当前共享背包；队伍共享背包与据点共享仓库是两个不同概念，不能继续混成一个"共享仓库"语义。
- 队伍共享背包暂时复用现有 `WarehouseState` 结构，但语义上改名为队伍共享背包；据点共享仓库以后新增独立状态。
- 战斗开始时复制一份 battle-local 队伍共享背包 view；战中换装只修改这份 battle-local view，战后统一回写。
- 战斗中不支持存档。
- 换装统一消耗 `2 AP`。
- 换装后如果 `current_ap <= 0`，立刻结束当前行动单位行动。
- 换装导致 `hp_max` 变化时：
  - 如果 `current_hp > new_hp_max`，把 `current_hp` clamp 到 `new_hp_max`。
  - 如果 `current_hp <= new_hp_max`，当前 HP 保持不变，不按最大生命变化做比例缩放或治疗。

---

## 6. PR2 架构决策

以下为已拍板口径，直接决定 PR2 的代码形态。

### 6.1 战斗中装备状态住哪？

- **(I) 镜像到 BattleUnitState/BattleState，只在战斗结束写回 PartyMemberState** —— 与现行 `current_hp` / `attribute_snapshot` 模式一致（参考 `battle_unit_factory.gd:180`）。回退干净。
- **(II) 直接 mutate PartyMemberState** —— 实现简单，但战斗中途读档语义混乱（战斗存档要包含原装备快照）。

**结论：选 (I)**。`BattleUnitState / BattleState` 新增 battle-local `equipment_view` 与队伍共享背包 view，代替战斗中回查 / 直改 `character_gateway` 的装备状态。战斗开始时复制 party 当前队伍共享背包为 battle-local view，战中只改这份 view，战斗结束时 facade 把 diff 回写 party state。

注意：`equipment_view` 不能只存 `slot_id -> item_id`；当前装备有 `instance_id`，队伍共享背包 view 也需要保留装备实例身份，否则会丢耐久、稀有度和后续实例字段。

### 6.2 `attribute_snapshot` 重算策略

- 每次换装都重建 snapshot —— 把 `battle_unit_factory.gd:323` 的 `_build_member_attribute_snapshot` 重构成接受 `equipment_view: Dictionary` 而非读 member_state，然后换装命令调它。
- snapshot 重算后要对 `current_hp/mp/stamina` 做 clamp。
- `hp_max` 变化后的 HP 口径：
  - `current_hp > new_hp_max` 时 clamp 到 `new_hp_max`。
  - `current_hp <= new_hp_max` 时保持当前 HP 不变。
  - 不按最大生命变化做比例缩放，不因换装提高 `hp_max` 而自动治疗。

### 6.3 换装命令的 AP/TU 经济

- 所有槽位换装统一消耗 `2 AP`。
- 如果 AP 不足，换装命令失败，不产生部分换装。
- 换装命令只能由当前行动单位对自己执行，消耗当前行动单位的 AP。
- 换装命令结算后如果 `current_ap <= 0`，立刻结束当前单位行动。

### 6.4 战中能看到什么背包？

- 战中打开队伍当前共享背包，不再使用出战锁定的 `carried_inventory`。
- 队伍共享背包属于当前队伍随身资源；据点共享仓库属于据点 / 世界服务资源，二者必须在状态、UI 文案和服务入口上区分。
- 队伍共享背包暂时复用现有 `WarehouseState` 数据结构；代码 / UI / 文案语义改为队伍共享背包。据点共享仓库以后新增独立状态，不复用队伍背包状态。
- 战斗开始时复制一份 battle-local 队伍共享背包 view；战中换装只改 battle-local view，不直接 mutate party state。
- 战中不能直接访问据点共享仓库。
- 战后把 battle-local 装备状态与队伍共享背包 view 回写到 `PartyMemberState` / 队伍共享背包状态。
- 战后回写不应出现实例冲突、容量变化或数据不一致；若出现，视为内部状态错误而不是玩法分支。
- 战利品容量问题不在 battle-local 装备回写阶段处理；战利品结算界面负责处理容量约束，队伍共享背包满时无法放入的战利品自动丢失。
- 战斗中换装 / 卸装如果需要把装备写入队伍共享背包但背包已满，则命令失败并回滚，状态不变；玩家主动管理装备时不静默丢弃装备。

### 6.5 双手武器 / off_hand 联动

- 主手装双手剑 → 自动卸副手到 inventory_view，扣双倍 AP？
- 副手装盾 → versatile 武器自动切 1H 握法？
- 这些自动行为算一个命令的副作用，还是要求玩家两步？

**结论**：自动联动，按一个换装命令计费，统一消耗 `2 AP`。自动卸下的装备进入 battle-local 队伍共享背包 view；如果背包容量或实例写入失败，整个命令失败并回滚。

### 6.6 序列化 / 回放确定性

- 本轮不支持战斗中存档；当前 battle save lock 继续生效。
- 换装事件要进入 battle log / report，供 UI、headless 和调试观察。
- 不扩 `save_serializer.gd` 的 battle 段；如果未来要支持战斗中存档，再单独设计 `BattleState` save payload。

---

## 7. 剩余工程评估问题

1. PR1 / PR2 / PR2-a / PR2-b 的拆法是否合理？是否有可以合并或必须拆得更细的部分？
2. `BattleCommand` 是否新增 `TYPE_EQUIP` / `TYPE_UNEQUIP` 两类命令，还是统一成 `TYPE_CHANGE_EQUIPMENT` 并用 payload 描述 slot / item / instance。
3. 战后回写 diff 必须满足实例一致性不变量；若不满足，应按内部错误暴露并修复代码路径，不作为正常玩法分支处理。

---

## 8. 关键引用

- BG3 武器原始资料：`docs/design/weapon_types_damage.md`
- 现有 ItemDef：`scripts/player/warehouse/item_def.gd`
- 武器物品模板：`data/configs/items_templates/weapon_*.tres`
- 武器具体物品：`data/configs/items/{bronze_sword,iron_greatsword,militia_axe,watchman_mace,scout_dagger}.tres`
- 战斗单位状态：`scripts/systems/battle_unit_state.gd`（参 line 84 的 `weapon_physical_damage_tag`）
- 战斗单位生成：`scripts/systems/battle_unit_factory.gd`（参 line 72/175/350 既有通路）
- 伤害结算：`scripts/systems/battle_damage_resolver.gd`（参 line 466-470 base_damage 公式、line 527 `_roll_damage_dice`、line 572 `_resolve_damage_tag`）
- 装备规则：`scripts/player/equipment/equipment_rules.gd`
- 角色侧装备读取：`scripts/systems/character_management_module.gd:304 get_member_weapon_physical_damage_tag`
- 唯一现有"用武器骰"样板：`data/configs/skills/warrior_heavy_strike.tres`
