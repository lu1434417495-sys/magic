# Chain Contingency Data Structure Design

中文名：**连锁应急术数据结构设计**  
适用场景：DND 风格战棋游戏 / 高阶奥术法术系统 / 事件触发型自动施法系统

当前定稿摘要：

```text
1. 采用人物绑定方案，严格自用；owner/caster 都是同一个角色。
2. 预设可战斗外保存并进入存档；保存免费，充能才付费。
3. 充能成本 = 特殊宝石消耗 + charged=true 期间封存最大魔力。
4. 触发储存法术时不扣 AP/MP/冷却，不涨熟练度，不触发另一个应急矩阵。
5. 强攻击、强控制、高风险触发器通过连锁应急术技能升级解锁，不需要额外专精。
6. 不做自然过期；进入释放流程、战斗外清除或未来破阵摧毁后 charged=false，释放封存最大魔力；特殊宝石不返还。
7. 不做兼容；新增存档字段后旧 payload 直接拒绝。
```

---

## 1. 设计目标

**连锁应急术** 和常规法术有本质区别。

常规法术通常是：

```text
选择目标 -> 消耗资源 -> 立即结算效果
```

连锁应急术应该是：

```text
战斗外保存预设 -> 施放连锁应急术为预设充能 -> 战斗中生成应急矩阵实例 -> 监听战斗事件 -> 条件满足时自动释放预设法术
```

所以它不应该强行塞进普通法术结算流程，而应该作为一个独立的 **事件触发器 + 自动施法容器** 来设计。

核心目标：

| 目标 | 说明 |
|---|---|
| 支持预设触发条件 | 例如生命低于30%、受到致死伤害、敌人进入2格内 |
| 支持储存受负载约束的法术 | 例如镜影术、石肤术、闪现术、雷鸣波；强攻击/强控制由技能等级解锁而不是默认开放 |
| 支持自动目标解析 | 自动对自己、触发来源、最近敌人、附近安全格释放 |
| 支持同步或连续释放 | 触发时全部释放，或每轮释放一个 |
| 防止套娃和无限触发 | 自动法术不能再触发另一个应急矩阵 |
| 方便存档 | 只保存法术ID、触发器、参数，不保存完整对象 |
| 明确战前成本 | 预设免费保存，战斗外施放/充能消耗材料，并在充能期间封存最大魔力 |

---

## 2. 推荐架构总览

建议拆成以下层级：

```text
SpellDefinition
 ├─ 普通法术数据
 ├─ effects
 └─ automation profile

ChainContingencyDefinition
 └─ contingencyRules

ContingencySetup
 ├─ enabled / charged
 ├─ trigger
 ├─ release_mode
 ├─ stored_spells
 └─ material / reserved MP cost

ContingencyInstance
 ├─ owner / caster
 ├─ state
 ├─ trigger
 ├─ stored_spells
 └─ runtime queue

BattleRuntimeHooks
 └─ explicit synchronous hook points

BattleContingencySystem
 ├─ receives hook facts
 ├─ matches trigger
 ├─ resolves target
 └─ creates AutoCastRequest

SkillEffectResolver
 └─ resolves AutoCastRequest effects with auto-cast flags
```

一句话总结：

> 普通法术是主动释放的效果；连锁应急术是战前挂在角色自己身上的同步触发器。

当前定稿采用 **人物严格自用**：

```text
owner = 持有该 PartyMemberState 的角色
caster = 战斗中 owner 对应的 BattleUnitState
creator = 不在第一版引入
bearer = 宝石/装备方案概念，不用于人物绑定方案
```

这意味着连锁应急术不是队伍服务，也不是法师给战士外挂高阶法术。储存法术必须来自 owner 自己已学会且当前技能等级允许的技能。

---

## 3. 普通法术定义扩展

普通法术需要增加一个 `automation` 字段，用来说明该法术能否被连锁应急术储存，以及它能使用哪些自动目标解析器。

### 示例：镜影术

```json
{
  "skill_id": "mage_mirror_image",
  "name": "镜影术",
  "level": 2,
  "school": "illusion",
  "target_type": "self",

  "automation": {
    "can_be_stored_in_contingency": true,
    "min_contingency_skill_level": 1,
    "effect_category": "defensive_self_buff",
    "tags": ["defense", "self_buff"],
    "contingency_load_override": -1,
    "allowed_target_resolvers": ["self"],
    "requires_manual_targeting": false
  },

  "effects": [
    {
      "type": "add_status",
      "status": "mirror_image",
      "duration": 3
    }
  ]
}
```

### 推荐字段说明

| 字段 | 类型 | 说明 |
|---|---|---|
| `can_be_stored_in_contingency` | bool | 是否允许被连锁应急术储存 |
| `min_contingency_skill_level` | int | 连锁应急术达到几级后允许储存 |
| `effect_category` | string | 用于区分防护、攻击、强控制、召唤等类别 |
| `tags` | string[] | 用于自动计算储存负载、禁用类别和 UI 解释 |
| `contingency_load_override` | int | 特殊技能可覆盖自动负载；`-1` 表示按等级和标签计算 |
| `allowed_target_resolvers` | string[] | 允许哪些自动目标解析器 |
| `requires_manual_targeting` | bool | 是否必须手动选点，如果是则不适合自动释放 |

不建议只用一个静态白名单把伤害、强控、召唤全部排除掉。更好的规则是：

```text
默认等级：只开放防护、位移、自我解除、侦测、姿态类法术。
技能升级：逐步开放攻击、强控制、高风险触发器和更复杂目标解析。
永久禁止：连锁应急术本身、再次触发类、额外行动类、复活、永久制造、复杂手动选点。
召唤类：默认禁止，若以后开放，必须由连锁应急术等级显式解锁并使用高矩阵负载。
```

这样顶级法师可以把高强度法术编入矩阵，但代价体现在技能等级门槛、矩阵负载、材料成本和最大魔力封存，而不是把顶级能力压成只能触发低级小法术。

---

## 4. 连锁应急术自身定义

连锁应急术不是普通效果法术，所以它的 `castType` 应该是特殊类型。

```json
{
  "id": "spell.chain_contingency",
  "name": "连锁应急术",
  "level": 8,
  "school": ["abjuration", "divination"],
  "tags": ["meta_spell", "contingency", "spell_matrix"],

  "cast_type": "setup_contingency_matrix",

  "contingency_rules": {
    "max_stored_spells": 3,
    "base_matrix_capacity": 8,
    "max_active_per_caster": 1,
    "charge_cost_mode": "special_gem_plus_reserved_max_mp",

    "allowed_release_modes": [
      "burst_release",
      "sequential_release"
    ],

    "forbidden_stored_skill_tags": [
      "contingency",
      "meta_spell",
      "permanent_creation",
      "resurrection",
      "extra_action",
      "retrigger_contingency",
      "complex_manual_target",
      "summon"
    ]
  }
}
```

### 关键规则

| 规则 | 推荐值 |
|---|---:|
| 最多储存法术数 | 3 |
| 矩阵容量 | 由连锁应急术等级决定，默认 8 起 |
| 单个储存法术强度 | 由 `min_contingency_skill_level` 与 `matrix_load` 控制 |
| 每名施法者同时维持数量 | 1 |
| 是否需要专注 | 不需要 |
| 是否消耗特殊宝石 | 充能时消耗，触发/战斗外清除/未来破阵摧毁不返还 |
| 是否封存最大魔力 | 充能期间封存 `reserved_mp_max`，触发/战斗外清除/未来破阵摧毁后释放 |
| 是否自然过期 | V1 不做自然过期，不保留过期字段 |
| 是否可被解除 | V1 默认无法解除；普通解除魔法无效，反魔法领域可临时压制，未来高阶裂解/专门破阵效果可摧毁充能 |

`max_active_per_caster = 1` 不按阵营区分。V1 中玩家、敌人、召唤物或未来 boss 只要作为 caster 使用连锁应急术，都遵守同一限制。敌人默认不走 `PartyMemberState.contingency_matrix_setups` 的持久充能模型；若未来需要敌方或 boss 使用，应通过显式 enemy template / special profile 配置生成 battle-local 矩阵，并仍默认每个 enemy unit 最多 1 个。多矩阵 boss 是未来单独机制，不通过这个字段隐式放宽。

连锁应急术的强度增长来自 **技能升级解锁**，不需要额外专精门槛：

| 技能等级 | matrix_capacity |
|---:|---:|
| 1 | 4 |
| 2 | 5 |
| 3 | 6 |
| 4 | 7 |
| 5 | 8 |
| 6 | 9 |
| 7 | 10 |
| 8 | 11 |
| 9 | 12 |

公式：

```text
matrix_capacity = 3 + skill_level
```

| 技能等级 | 解锁重点 |
|---|---|
| 1-2 | `combat_started`、`hp_below_percent`；`defense`、`self_buff`、`mobility`、`cleanse`；`burst_release` |
| 3-4 | 新增 `status_applied`、`enemy_enter_radius`；`healing`、`shield`、`area`；`sequential_release` |
| 5-6 | 新增 `affected_by_spell`、`incoming_damage_percent`；`damage`、`control`；`trigger_source`、`owner_centered_area` |
| 7-8 | 新增 `fatal_damage_incoming`；`strong_control`；`safe_cell` |
| 9 | 允许最高负载组合；若以后开放召唤，`summon` 只允许 9 级进入；封存效率最佳 |

---

## 5. 应急矩阵配置数据 `ContingencySetup`

玩家在战斗外保存预设时，需要生成一个配置。保存预设本身免费；只有把预设施放为已充能矩阵时，才消耗材料并封存最大魔力。

```json
{
  "setup_id": "contingency_setup_001",
  "display_name": "濒死保命",
  "enabled": true,
  "charged": true,
  "source_skill_id": "mage_chain_contingency",
  "source_skill_level": 5,
  "matrix_load": 6,
  "reserved_mp_max": 12,
  "material_costs": [
    {
      "item_id": "special_contingency_gem",
      "quantity": 1
    }
  ],

  "trigger": {
    "type": "hp_below_percent",
    "subject": "owner",
    "percent": 30,
    "timing": "after_hp_changed"
  },

  "release_mode": "burst_release",

  "stored_spells": [
    {
      "stored_skill_id": "mage_mirror_image",
      "cast_level": 2,
      "order": 1,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    },
    {
      "stored_skill_id": "mage_stoneskin",
      "cast_level": 4,
      "order": 2,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ]
}
```

含义：

```text
当我的生命值低于30%时，自动释放镜影术和石肤术。
```

### 5.1 预设、充能与成本

人物绑定方案把战前流程拆成两个动作：

```text
保存预设：免费，只写入 trigger / release_mode / stored_spells 等配置。
施放充能：战斗外动作，消耗特殊宝石，并在 charged=true 期间封存 reserved_mp_max。
```

推荐成本模型：

```text
charge_cost = material_costs + reserved_mp_max
reserved_mp_max = 基础封存 + ceil(matrix_load * 技能等级系数)
matrix_load = 储存法术负载 + 触发器负载 + 目标解析负载 + 释放模式负载
stored_spell_load = cast_level + tag/effect_category 负载 + optional override
```

关键规则：

```text
1. 触发储存法术时不扣 AP / MP / stamina / aura / cooldown。
2. 自动释放不涨熟练度，不触发普通成就、击杀奖励或另一个应急矩阵。
3. charged=true 时封存最大魔力；角色可用 max MP 使用 effective_mp_max。
4. raw mp_max 永远来自属性/成长/装备系统；连锁应急术不改写 raw mp_max。
5. total_reserved_mp_max = 所有 charged=true setup 的 reserved_mp_max 之和；V1 每名角色最多一个 charged setup。
6. effective_mp_max = max(raw_mp_max - total_reserved_mp_max, 0)。
7. 充能时 current_mp = min(current_mp, effective_mp_max)。
8. 充能期间恢复/休息最多只能恢复到 effective_mp_max。
9. 触发、战斗外清除、未来破阵摧毁后释放封存的最大魔力，但 current_mp 不自动增加。
10. 特殊宝石在充能时消耗；触发、清除、未来破阵摧毁都不返还。
11. 已充能预设不可直接编辑；修改前必须清除充能并二次确认。
12. 读档、进战、战斗开始确认不再次扣特殊宝石，避免重复收费。
13. V1 不做自然过期，不保留过期字段。
```

V1 不保存充能时间字段。充能、清除、战后提交等世界层行为需要审计时，走 `GameSession.log_event` / `GameLogService` 记录；setup payload 本身不保留 `charged_at_world_step`、过期时间或 `-1` 哨兵值。

这个模型保留 DND 风格的“战前施放”：法师提前把魔力编进矩阵里，而不是进战时临时买一次反应。

---

## 6. 运行时实例 `ContingencyInstance`

战斗中真正挂在角色身上的不是 `SpellDefinition`，而是一个运行时实例。

```json
{
  "instance_id": "contingency_001",
  "setup_id": "contingency_setup_001",
  "source_skill_id": "mage_chain_contingency",

  "owner_member_id": "member_mage_01",
  "owner_unit_id": "unit_mage_01",
  "caster_unit_id": "unit_mage_01",

  "state": "armed",
  "matrix_load": 6,
  "reserved_mp_max": 12,

  "trigger": {
    "type": "hp_below_percent",
    "subject": "owner",
    "percent": 30,
    "timing": "after_hp_changed"
  },

  "release_mode": "burst_release",

  "stored_spells": [
    {
      "stored_skill_id": "mage_mirror_image",
      "cast_level": 2,
      "order": 1,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    },
    {
      "stored_skill_id": "mage_stoneskin",
      "cast_level": 4,
      "order": 2,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ]
}
```

运行时实例来自 `PartyMemberState` 中已充能的 `ContingencySetup`。它只存在于战斗本地系统中，用于监听事件、去重、防递归和排队释放；不要把 `armed / triggering / releasing / completed` 这类战斗临时状态写回人物预设。

严格自用约束在运行时也必须保持：

```text
owner_member_id 指向拥有预设的角色。
owner_unit_id 是该角色本场战斗中的单位。
caster_unit_id 必须等于 owner_unit_id。
没有 creator_unit_id，也没有 bearer_unit_id。
```

连锁应急术绑定的是人物身份，而不是当前种族、形态、职业外观、身体模板或 battle-local unit id。`owner_member_id` 是主绑定；`owner_unit_id` 只是本场战斗中承载该人物的当前单位。若未来变形、升华、临时形态或单位替换机制会改变 `unit_id`、体型、占位、属性快照或贴图，只要新的 live unit 保留同一个 `source_member_id`，应急术式就继续跟随该人物。

若找不到任何 live unit 对应 `owner_member_id`，例如 owner 被放逐、暂时离场或本场战斗中不存在，则 live gate 失败，不创建 `release_context`，不进入释放流程，不消耗充能，持久 `charged=true` 保持不变。若矩阵已经进入 `triggering` / `releasing` 后 owner 才消失，则不回滚；已消耗的充能保持消耗，后续依赖 `self` 的预存法术按目标解析失败跳过或中止，并记录日志。

变形后不重新检查 owner 当前是否还能普通施放连锁应急术。充能时已经校验技能等级、负载、材料和可储存法术；触发时只检查 owner 是否存在、是否被反魔法/专门压制、目标是否合法，以及储存法术自身结算是否有效。`source_skill_id` / `stored_skill_id` 找不到或内容契约变坏，仍按存档/内容异常处理，不作为形态变化规则兜底。

### 状态枚举

```text
armed       已武装，等待触发
triggering  正在触发
releasing   连续释放中
completed   已触发完成
suppressed  被反魔法压制
cancelled   战斗外手动清除
destroyed   未来高阶裂解/专门破阵效果摧毁
```

V1 中普通解除魔法对连锁应急术无效。`suppressed` 只由反魔法领域、专门压制效果，或未来高阶破阵/裂解的压制模式造成。压制不释放 `reserved_mp_max`，不返还特殊宝石，不改变持久 `charged=true`。

压制状态机：

```text
armed -> suppressed：
    暂时不能响应新事件；压制结束后恢复 armed。

queued / 未进入释放流程：
    轮到该矩阵时若被压制，则 live gate 失败，不消耗充能。

burst_release 已进入释放流程：
    不被后来的压制回溯中断；已经开始的 burst 继续按快速顺序结算。

sequential_release 的 releasing：
    每个后续法术释放前检查压制；压制中暂停队列，不跳过、不额外消耗。
    压制结束后恢复 releasing，在下一次 owner_turn_started 前继续检查并释放。
```

V1 不做自然过期，因此压制状态不存在“过期计时暂停”。UI 和日志必须显示“被反魔法压制，暂不可触发/暂缓释放”。

普通控制和施法成分限制不阻止矩阵自动释放。连锁应急术是战前完成的预施法矩阵，触发时不是让角色重新执行一次普通施法；因此沉默、眩晕、麻痹、睡眠、恐惧、无法行动、无法说话或无法做姿势，都不阻止进入释放流程，也不阻止储存法术结算。真正能阻止矩阵的是反魔法、专门压制、owner 死亡/离场/不存在，或触发/目标/法术本身的合法性失败。

---

## 7. 触发条件数据结构

触发条件不要使用自由文本，而应该使用枚举和参数。

### V1 玩家可选触发类型

```text
combat_started
hp_below_percent
incoming_damage_percent
fatal_damage_incoming
status_applied
enemy_enter_radius
affected_by_spell
```

玩家界面中“被法术影响”对应内部两个匹配来源：被直接指定为目标、被区域效果波及。默认两者都触发，高级设置可细分，但不暴露为两个独立触发器。

触发主体统一规则：

```text
1. subject = owner 永远只匹配 owner 当前 live unit。
2. 召唤物、宠物、盟友、临时单位都不是 owner 的代理主体；它们受伤、被控、被法术影响或进入危险区域，不触发 owner 的矩阵。
3. source_unit_id 可以是召唤物。敌方召唤物攻击、施法或进入 owner 半径时，按普通敌对来源处理。
4. 召唤物若未来拥有 summoner_unit_id / summoner_member_id，这些字段只用于归因、AI、日志或召唤控制，不用于把召唤物事件改写成 summoner 本人事件。
5. V1 储存法术仍禁止 summon；这里讨论的是战场上已经存在的召唤单位作为事件来源或事件对象。
```

因此，敌方召唤物伤害 owner 可以触发 `incoming_damage_percent` / `fatal_damage_incoming`，敌方召唤物进入 owner 半径可以触发 `enemy_enter_radius`，敌方召唤物施法影响 owner 可以触发 `affected_by_spell`。但召唤物自身被伤害、被控制、被法术影响，不会触发其 summoner 或 owner 的连锁应急术。

### 内部触发类型

```text
owner_turn_started
```

`owner_turn_started` 只服务于 `sequential_release` 的后续队列释放，不出现在玩家选择界面。

### 不进入 V1 的触发类型

```text
targeted_by_spell      合并进 affected_by_spell
round_started          回合/轮次边界对玩家不直观，且容易和 TU 系统冲突
manual_keyword         V1 不做战斗内手动触发
entered_dangerous_tile 需要完整危险地形事件事实，后续再评估
```

### V1 timing 枚举

`timing` 不是玩家可自由选择的参数，而是触发器绑定到战斗 hook 的实现契约。V1 只允许以下枚举：

```text
after_battle_confirmed
before_spell_effect_resolved
before_damage_resolved
after_hp_changed
after_status_applied
after_position_changed
owner_turn_started
```

对应关系：

| trigger type | timing |
|---|---|
| `combat_started` | `after_battle_confirmed` |
| `affected_by_spell` | `before_spell_effect_resolved` |
| `incoming_damage_percent` | `before_damage_resolved` |
| `fatal_damage_incoming` | `before_damage_resolved` |
| `hp_below_percent` | `after_hp_changed` |
| `status_applied` | `after_status_applied` |
| `enemy_enter_radius` | `after_position_changed` |
| `owner_turn_started` | `owner_turn_started` |

旧式泛称 `after_event`、`after_movement` 不进入 V1。

---

### 7.1 战斗开始

```json
{
  "type": "combat_started",
  "subject": "owner",
  "timing": "after_battle_confirmed"
}
```

时机：玩家确认进入战斗后、首个单位行动前。  
如果矩阵在该事件发生时被反魔法压制，事件不会延迟补触发。

---

### 7.2 生命值低于百分比

```json
{
  "type": "hp_below_percent",
  "subject": "owner",
  "percent": 30,
  "crossing_only": true,
  "timing": "after_hp_changed"
}
```

适合：保命矩阵。  
只在 HP 从高于等于阈值跌到低于阈值时触发；已经低血进入战斗不会触发。若矩阵尚未消耗，之后回血超过阈值再跌破，可以再次满足条件。

---

### 7.3 即将受到大额伤害

```json
{
  "type": "incoming_damage_percent",
  "subject": "owner",
  "damage_percent": 30,
  "damage_basis": "max_hp",
  "damage_amount_mode": "projected_hp_damage_after_shield",
  "timing": "before_damage_resolved"
}
```

时机：命中、豁免、减伤、护盾等投影完成后，真正扣血前。  
阈值按最大生命计算，比较预计会打到 HP 的伤害，不含已经被护盾吸收的部分。若同一伤害事件同时满足致死触发，则 `fatal_damage_incoming` 优先。

---

### 7.4 受到致死伤害前

```json
{
  "type": "fatal_damage_incoming",
  "subject": "owner",
  "timing": "before_damage_resolved"
}
```

这个触发点非常关键。  
如果是保命矩阵，必须在伤害结算前触发，否则角色已经死亡。触发后的闪现或位移若让 owner 脱离当前伤害的有效命中条件，则取消当前伤害事件；否则重新投影后继续结算。

---

### 7.5 敌人进入半径范围

```json
{
  "type": "enemy_enter_radius",
  "center": "owner",
  "radius": 2,
  "radius_metric": "manhattan",
  "source_team": "hostile",
  "timing": "after_position_changed"
}
```

适合：反近身、反刺客。  
只有敌人从范围外进入范围内才触发；原本就在范围内不会重复触发。普通移动、冲锋、传送、推拉和召唤出现，只要形成“进入 owner 半径”的事件事实，都按进入处理。

V1 不设计“同时进入范围”的批量事件。位置变化按真实事件顺序逐个派发；第一个从范围外进入范围内、且成功让矩阵创建 `release_context` 的敌人就是本次 `trigger_source`。矩阵进入 `triggering` / `releasing` 后立即消耗，后续敌人再进入时该矩阵已不再是 `armed`，不会补触发。若先进入的事件在进入释放流程前失败，例如 owner 不在场、矩阵被压制或无法创建 `release_context`，则不消耗，后续进入事件仍可触发。不增加每轮冷却、批量聚合或同回合去重窗口。

---

### 7.6 被施加控制状态

```json
{
  "type": "status_applied",
  "subject": "owner",
  "status_tags": [
    "stun",
    "paralyze",
    "fear",
    "charm",
    "silence"
  ],
  "application_match": "new_status_only",
  "timing": "after_status_applied"
}
```

适合：反控制矩阵。
只在 owner 新获得指定状态或状态组时触发；刷新持续时间、增加叠层不触发。

---

### 7.7 被法术影响

```json
{
  "type": "affected_by_spell",
  "subject": "owner",
  "source_team": "hostile",
  "spell_match": "any",
  "timing": "before_spell_effect_resolved"
}
```

适合：反法术防护、预判闪避、受法术影响前自保。  
`spell_match` 可取 `any`、`direct_target`、`area_included`。默认只响应敌方法术；友方法术、地形残留和召唤物普通攻击不触发。触发事实来自最终目标或最终区域，而不是玩家原始点选。

---

## 8. 目标解析器 `TargetResolver`

普通施法由玩家手动选择目标；连锁应急术需要自动找目标，所以必须提前定义目标解析逻辑。

### 推荐目标解析器

```text
self
trigger_source
trigger_target
nearest_enemy_to_owner
nearest_enemy_to_trigger_cell
owner_centered_area
fixed_cell
attacker_cell
empty_cell_near_owner
```

第一版不开放 `bound_ally`。人物绑定方案严格自用，矩阵只能由 owner 自己维持并以 owner 为 caster。攻击、强控制或反击类法术可以通过 `trigger_source`、`owner_centered_area` 等解析器作用于敌人，但不能把矩阵挂给队友，也不能替队友承担触发条件。

---

### 8.1 对自己释放

```json
{
  "type": "self"
}
```

适合：镜影术、石肤术、防护能量。

`self` 解析为 owner 在当前战斗中的 live `BattleUnitState`，即当前形态下的坐标、体型、占位、属性快照、HP/MP、状态、抗性和可受影响性。变形成其他形态后，`self` 不回指充能时的旧形态，也不保存旧单位快照。若当前找不到 owner 的 live unit，解析失败；未进入释放流程时不消耗充能，已进入释放流程时按该预存法术目标解析失败处理。

---

### 8.2 对触发来源释放

```json
{
  "type": "trigger_source"
}
```

适合：反击类法术，比如对攻击自己的敌人释放虚弱术。

---

### 8.3 以自己为中心释放区域法术

```json
{
  "type": "owner_centered_area"
}
```

适合：雷鸣波、烟雾术、护盾爆发。

---

### 8.4 闪现到附近安全格

```json
{
  "type": "empty_cell_near_owner",
  "preference": "away_from_trigger_source",
  "max_distance": 4
}
```

适合：闪现术、短距传送。

安全格解析必须产出最优合法候选，而不是找不到完美安全格就失败：

```text
硬合法条件：空格、可站立、可放置、不被阻挡。
评分项：离当前伤害范围外更高分、远离伤害来源更高分、不在危险地形更高分、不邻接敌人更高分、靠近盟友更高分、距离原位置适中更高分。
若没有完美安全格，仍选择最高分合法格。
若连合法格都没有，位移部分失败，但矩阵仍继续释放其他储存法术。
```

在 `fatal_damage_incoming` 中，如果位移后的格子已经脱离当前伤害有效条件，则当前伤害事件取消；如果仍处于有效命中条件内，则当前伤害继续结算。

---

### 不建议允许的目标逻辑

以下目标逻辑太智能，容易让连锁应急术变成自动AI法术，不建议开放：

```text
最危险的敌人
最有价值的目标
血量最低的敌人
最优落点
最适合当前局势的位置
```

---

## 9. 储存法术条目 `StoredSpellEntry`

每个储存法术建议这样设计：

```json
{
  "stored_skill_id": "mage_thunderwave",
  "cast_level": 1,
  "order": 1,

  "target_resolver": {
    "type": "owner_centered_area"
  },

  "parameter_bindings": {
    "element": "thunder"
  },

  "fallback_policy": "skip_if_invalid"
}
```

### 字段说明

| 字段 | 说明 |
|---|---|
| `stored_skill_id` | 储存哪个技能/法术 |
| `cast_level` | 以几环释放 |
| `order` | 连续释放时的顺序 |
| `target_resolver` | 触发时如何找目标 |
| `parameter_bindings` | 元素、方向、模式等预设参数；无参数时显式存 `{}` |
| `fallback_policy` | 目标非法时怎么处理 |

### `parameter_bindings` 规则

`parameter_bindings` 用于战斗外提前选择储存法术的模式参数，不用于保存目标、坐标队列或战斗现场状态。

```text
target_resolver = 对谁 / 对哪格释放
parameter_bindings = 以什么模式释放
```

每个 `stored_skill_id` 必须由技能定义声明自己允许哪些 binding key。未声明的 key 直接拒绝，key 必须是字符串 / StringName，value 必须符合该 key 的类型和枚举约束。无参数时也必须显式保存 `{}`。

允许的 value 类型应限制为稳定可序列化的小型数据：

```text
bool
int
float
String / StringName
Array[StringName]
小型 flat Dictionary，但必须由该技能显式声明 schema
```

禁止在 `parameter_bindings` 中保存：

```text
target_unit_id
owner_member_id
runtime unit id
坐标队列或动态路径
节点、脚本实例、函数、对象引用
未声明的自由字典或任意嵌套结构
```

示例：`mage_energy_resistance` 可以声明 `element` 只允许 `fire`、`cold`、`lightning`、`acid`。此时 `{ "element": "fire" }` 合法，`{ "element": 123 }`、`{ "element": "holy_nuke" }`、`{ "target_unit_id": "enemy_001" }` 都必须拒绝。

### `fallback_policy` 推荐值

```text
skip_if_invalid       目标非法则跳过该法术
retarget_self         目标非法则改为自身
retarget_trigger      目标非法则改为触发来源
fail_matrix           任意法术失败则整个矩阵失败
```

推荐默认：

```text
skip_if_invalid
```

否则玩家体验会比较差。

---

## 10. 释放模式

### 10.1 爆发释放

```json
{
  "release_mode": "burst_release"
}
```

触发后，所有储存法术立刻按顺序结算。敌人不能在本次爆发释放的法术之间插入行动；不做原子回滚，前一个法术成功、后一个失败时，已成功效果保留。

适合：

| 用途 | 示例 |
|---|---|
| 濒死保命 | 镜影术 + 石肤术 |
| 反刺客 | 雷鸣波 + 闪现术 |
| 反控制 | 解除魔法 + 自由行动 |

---

### 10.2 连续释放

```json
{
  "release_mode": "sequential_release"
}
```

触发时立即释放队列第一个法术。之后每当 owner 获得行动机会 / turn_started 时，在 owner 可输入行动前释放下一个法术。

运行时需要增加队列：

```json
{
  "state": "releasing",
  "remaining_queue": [
    {
      "stored_skill_id": "mage_haste",
      "cast_level": 3,
      "order": 2,
      "target_resolver": {"type": "self"},
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ],
  "next_release_turn_owner_unit_id": "unit_mage_01"
}
```

适合：开战预案、持续防护、逐轮强化。连续释放可使用较低 `release_mode_load`，作为延迟节奏和较低负载的选择。

---

## 11. 战斗同步 Hook 系统

当前项目战斗逻辑以同步调用链为主。V1 不新增通用事件总线，也不新增 `battle_event_dispatcher.gd`；连锁应急术通过 `BattleContingencySystem` 暴露固定同步 hook，由现有战斗流程在关键结算点显式调用。

### 固定 Hook 点

```text
on_battle_confirmed
before_spell_effect_resolved
before_damage_resolved
after_hp_changed
after_status_applied
after_position_changed
owner_turn_started
```

这些 hook 是战斗运行时内部契约，不是玩家可配置事件。`before_damage_resolved` 不是只读观察点，必须能向当前伤害链写入 per-owner 修正，例如取消 owner 的本次伤害、修改伤害或标记本次伤害已被矩阵处理；闪现取消伤害就通过这个 hook 完成。

`before_damage_resolved` 对所有即将结算的伤害调用，包括预计会被护盾完全吸收的伤害。这样闪现、位移或其他防护可以在护盾吸收前取消整次伤害。但 V1 的 `incoming_damage_percent` / `fatal_damage_incoming` 只按 `projected_hp_damage_after_shield` 判定；若预计 HP 伤害为 0，则这两个触发器不触发。未来若需要响应护盾破裂或护盾损耗，应新增 `shield_break_incoming` / `shield_damage_percent` 等触发器，不混入 HP 保命触发。

hook 输入至少包含：

```text
source_event_id / damage_event_id / action_instance_id
source_unit_id / target_unit_id
skill_id / effect_id
resolved_damage
projected_shield_absorbed
projected_hp_damage_after_shield
would_be_fatal
damage_tags / element / bypass_shield
```

hook 输出或 mutable context 至少支持：

```text
cancel_damage: bool
modified_resolved_damage: int
reason_id: StringName
report_entries: Array[Dictionary]
```

若 `cancel_damage=true`，本次伤害对该 owner 不扣护盾、不扣 HP，也不再产生该 `damage_event_id` 对应的 `hp_below_percent` crossing；已经进入 `release_context` 的矩阵仍消耗充能。若只是修改伤害，则使用修改后的伤害继续进入护盾吸收和 HP 写入，后续 crossing 按实际 HP 变化判断。

### 触发流程

```text
1. 现有战斗结算函数到达固定 hook 点。
2. BattleContingencySystem 接收 hook facts / source_event_facts。
3. 按 trigger_type 索引查找相关 ContingencyInstance。
4. 判断 trigger 是否匹配，并执行 owner / 压制 / source event 等 live gate。
5. 若匹配，创建 release_context，设置 state = triggering，并立刻在 battle-local 标记本次充能已消耗、释放 MP 上限封存。
6. 根据 release_mode 通过内部 auto-cast 路径执行储存法术。
7. 触发完成后删除或标记 depleted，并记录本场提交时需要把 charged 写回为 false。
```

消耗边界按“是否进入释放流程”判断，而不是按最终法术效果是否成功判断：

```text
未进入释放流程：事件不匹配、矩阵被压制、owner 不合法、触发过滤失败、无法创建 release_context，均不消耗充能。
进入释放流程：state 进入 triggering / releasing 后立刻消耗充能；后续储存法术即使全部被免疫、目标无效或没有实际收益，也不返还充能。
```

不引入额外的 `matrix_fallback_policy`。整体矩阵不按“全部储存法术是否成功”判断是否返还；唯一边界是是否已经创建 `release_context` 并进入释放流程。若所有储存法术都因目标解析失败、免疫、非法目标或 `skip_if_invalid` 没有产生实际效果，仍视为已触发并已消耗，日志必须明确记录“矩阵已触发，但储存法术全部未产生有效效果”。

### 同一伤害事件的触发仲裁

伤害事件必须有稳定的 `damage_event_id`，并从伤害投影、伤害写入、HP 变化到死亡/倒地结算全程传递。对同一个 owner，同一个 `damage_event_id` 最多只能让一个应急矩阵进入释放流程。

伤害触发器优先级：

```text
fatal_damage_incoming
> incoming_damage_percent
> hp_below_percent
```

解决顺序：

```text
1. 先完成命中、豁免、减伤、护盾后的伤害投影。
2. 如果预计会致死，优先检查 fatal_damage_incoming。
3. 如果不致死但预计 HP 伤害超过阈值，检查 incoming_damage_percent。
4. 如果没有任何 pre-damage 触发进入释放流程，才写入 HP。
5. HP 写入后，若从高于等于阈值跌到低于阈值，检查 hp_below_percent。
6. hp_below_percent 发生在死亡/倒地最终结算前，因此即使该次伤害会把 HP 打到 0 以下，也可以触发。
7. 一旦某个触发器进入释放流程，后续同一 damage_event_id 的 HP crossing 不再触发另一个矩阵。
8. 如果只是触发评估失败、未进入释放流程，不占用该 damage_event_id。
```

如果 `fatal_damage_incoming` 中的闪现或位移取消了原伤害，则该 `damage_event_id` 结束，不再产生对应的 HP crossing。

### 多 owner 瞬发事件的触发队列

AoE / 多目标瞬发事件打开反应窗口时，必须先生成不可变的 `source_event_facts`，再基于这份事实收集触发队列。不要在每个 owner 的矩阵释放后，重新计算 AoE 几何、遮挡或目标列表。

`source_event_facts` 至少应冻结：

```text
source_event_id / damage_event_id
source_unit_id / source_member_id / source_faction_id
skill_id / cast_variant_id / action_instance_id
anchor_coord / area_shape / affected_coords
affected_unit_ids
每个 owner 的 affected_reason：direct_target / area_included
每个 owner 的原始坐标、预计 HP 伤害、护盾吸收、是否满足 fatal / damage_percent
排序键：timeline_order / unit_id / owner_member_id
```

触发资格和最终结算必须分层：

```text
触发资格：只看 frozen source_event_facts。
释放门槛：owner 轮到自己时读取 live state。
最终伤害/效果：读取当前单位状态，并应用 source event 上的显式 per-owner 修正。
```

因此，前一个 owner 的矩阵造成的位移、护盾、造墙、杀死施法者或改变地形，不会移除后一个 owner 对同一原始事件的触发资格，也不会新增原本未被波及 owner 的触发资格。owner 轮到自己时仍要检查：owner 是否存在/存活/在场，矩阵是否仍可释放，是否被反魔法/专门压制，source event 是否已被显式取消，目标解析是否还有合法目标。

闪现取消伤害不通过“重新计算 AoE 范围”实现，而应写入该 owner 对本次 source event 的结算修正：

```text
source_event.target_exclusions[owner_unit_id] = true
或 source_event.damage_cancelled_for[owner_unit_id] = true
或 source_event.damage_modifiers[owner_unit_id] = ...
```

示例：

```text
火球原始范围覆盖 A 和 B。
A 的矩阵先触发并闪现离开，A 写入 damage_cancelled_for[A]。
B 仍按原始 source_event_facts 获得触发资格。
B 最终是否受伤，取决于 B 的 live state 和 B 自己的 per-owner 修正。
```

如果 source event 被显式全局取消，尚未进入释放流程的队列项可以因 `source_event_cancelled` 跳过，且不消耗充能；已经进入释放流程的矩阵不回滚。杀死原施法者不自动取消已经发出的瞬发事件，除非效果显式写入 `cancel_source_event`。

这条只适用于瞬发 source event。持续区域、毒云、火墙、延迟爆炸、多段传播等效果，必须为每个 tick / 阶段重新生成 `source_event_facts`，除非技能文本明确声明使用初始快照。

### 伪代码

```text
onContingencyHook(hook_facts, mutable_context):
    for matrix in activeByTriggerType[hook_facts.trigger_type]:
        if matrix.state != armed:
            continue

        if not triggerMatches(matrix.trigger, hook_facts):
            continue

        if not can_enter_release_flow(matrix, hook_facts):
            continue

        matrix.state = triggering
        matrix.consumed_charge = true
        release_reserved_mp_max_in_battle(matrix)
        executeContingency(matrix, hook_facts, mutable_context)
```

```text
executeContingency(matrix, hook_facts, mutable_context):
    if matrix.release_mode == burst_release:
        for stored_spell in matrix.stored_spells ordered by order:
            auto_cast(stored_spell, matrix, hook_facts, mutable_context)
        matrix.state = completed
        remove matrix

    if matrix.release_mode == sequential_release:
        matrix.state = releasing
        matrix.remaining_queue = matrix.stored_spells
        release_next_spell(matrix, hook_facts, mutable_context)
```

---

## 12. 自动施法请求 `AutoCastRequest`

触发后不要直接调用玩家手动施法流程，而是生成一个自动施法请求。

```json
{
  "is_auto_cast": true,
  "source_kind": "contingency",
  "source_matrix_id": "contingency_001",

  "caster_unit_id": "unit_mage_01",
  "stored_skill_id": "mage_mirror_image",
  "cast_level": 2,

  "target": {
    "type": "unit",
    "unit_id": "unit_mage_01"
  },

  "ignore_action_phase": true,
  "ignore_ap_cost": true,
  "ignore_resource_cost": true,
  "ignore_cooldown": true,
  "ignore_identity_charge": true,
  "ignore_mastery_gain": true,
  "ignore_skill_used_achievement": true,
  "skip_spell_control": true,
  "spent_mp": 0,
  "can_trigger_other_contingencies": false
}
```

### 关键字段

| 字段 | 作用 |
|---|---|
| `is_auto_cast` | 标记这是自动施法请求，不走玩家手动命令入口 |
| `source_kind` | 固定为 `contingency`，用于日志、报告和防递归 |
| `ignore_action_phase` | 自动触发不要求处于可输入行动阶段 |
| `ignore_ap_cost` | 自动触发不消耗 AP |
| `ignore_resource_cost` | 储存法术触发时不扣 MP / stamina / aura 等资源 |
| `ignore_cooldown` | 储存法术触发时不写入普通技能冷却 |
| `ignore_identity_charge` | 自动触发不消耗种族、身份、职业等技能次数 |
| `ignore_mastery_gain` | 自动释放不涨熟练度 |
| `ignore_skill_used_achievement` | 自动释放不计普通技能使用成就、评分或刷量 |
| `skip_spell_control` | 不参与魔力失控、施法失控、失败保护、额外抽耗或返还类机制 |
| `spent_mp` | 固定为 `0`，避免后续统计把自动法术当作主动消费 |
| `can_trigger_other_contingencies` | 防止套娃连锁 |
| `source_matrix_id` | 用于日志、回放、调试 |

注意：这些 flag 只作用于储存法术触发瞬间。连锁应急术本体的成本已经在战斗外充能时支付，包含特殊宝石消耗和最大魔力封存。自动释放仍使用普通法术的内容查找、目标合法性、免疫、抗性、护盾、豁免和效果结算；反魔法压制、无合法目标、法术内容非法等仍可阻止或使释放无效。

实现上不要把 `AutoCastRequest` 包装成普通 `issue_command()`。应在技能执行编排层提供内部入口，例如 `execute_auto_cast(request, batch)`，复用底层效果解析，但跳过玩家行动阶段、成本扣除、冷却写入、熟练度和成就统计。

---

## 13. 防止套娃和无限触发

必须加以下规则：

```text
1. 连锁应急术触发的自动法术，不能触发任何连锁应急术。
2. 同一个事件中，每个 owner 最多触发自己的一个矩阵；不同 owner 可以同时触发。
3. 每名施法者最多维持一个连锁应急术。
4. 新的连锁应急术会覆盖旧的。
5. 已触发矩阵不能再次触发。
6. 应急矩阵不能储存应急类法术。
7. 矩阵严格自用，caster 必须是 owner。
8. 触发完成后清除 charged 状态并释放 reserved_mp_max。
9. 自身递归不需要额外状态：矩阵必须先进入 triggering / releasing，再释放预存法术；因此自动施法产生的派生事件不会让同一矩阵再次从 armed 触发。
```

否则可能出现：

```text
矩阵A触发法术 -> 法术触发矩阵B -> 矩阵B触发法术 -> 法术又触发矩阵A
```

这类跨矩阵循环必须从数据层和运行时层同时禁止。`AutoCastRequest.can_trigger_other_contingencies` 固定为 `false`，V1 不允许配置成 `true`。自动施法产生的状态、伤害、位移、法术影响或 AoE 命中事件仍可进入普通结算、动画、战斗日志和结构化报告，但 `BattleContingencySystem` 必须把它们视为不可触发应急术式的派生事件。

---

## 14. 配置校验器

配置完成时必须跑一次校验。

```text
validateContingencySetup(setup):
    检查 owner 是否拥有连锁应急术
    检查储存法术是否来自 owner 自己已学会的技能
    检查连锁应急术等级是否满足解锁条件
    检查储存法术数量
    检查矩阵总负载
    检查单个法术负载
    检查是否禁止储存
    检查触发条件是否合法
    检查目标解析器是否合法
    检查触发器与目标解析器是否在交叉白名单内
    检查法术目标类型是否匹配
    检查特殊宝石与 reserved_mp_max 是否可支付
```

### 校验规则

```text
储存法术数量 ≤ 3
矩阵总负载 ≤ 当前连锁应急术等级提供的 matrixCapacity
单个储存法术必须满足 min_contingency_skill_level
强攻击、强控制、高风险触发器必须由连锁应急术等级解锁
不能储存连锁应急术
不能储存复活大法术
不能储存永久制造法术
不能储存需要复杂手动选点的法术
不能储存额外行动、再次触发、递归触发类法术
召唤类默认禁止；若以后开放，必须显式等级解锁并使用高负载
所有存档、运行时状态和文档示例统一使用 snake_case
所有技能/法术引用统一使用 skill_id 体系，skill_id 是持久化契约，发布后不得随意重命名或复用
```

`skill_id` 是存档持久契约。`source_skill_id`、`stored_skill_id` 只保存 ID，不保存完整法术定义；读档和配置校验必须能在当前内容注册表中找到对应 `skill_id`。找不到时按坏 payload / 非法 setup 处理，不做别名表、不做语义哈希、不按名称或标签猜测替代技能。如果未来确实需要重命名已发布技能，必须作为显式数据迁移单独确认，不能由连锁应急术系统内置兼容逻辑自动处理。

已存矩阵也必须满足当前内容定义的硬契约。若内容定义变更导致存档中的 setup 变为非法，例如法术不再允许储存、关键 tags / `min_contingency_skill_level` 与已存配置冲突、目标解析器不再被允许，读档或进入战斗时按存档异常处理；不自动清除、降级、返还、迁移或静默跳过。玩家修改预设或重新充能时，也必须按当前内容定义重新校验。

储存法术合法性必须按固定顺序校验：

```text
1. can_be_stored_in_contingency 必须为 true；默认 false，默认禁止储存。
2. 若 spell.tags 与 forbidden_stored_skill_tags 任一相交，则拒绝储存；ANY 匹配即禁止。
3. 若白名单与禁止 tag 冲突，禁止 tag 优先。
4. 再检查 min_contingency_skill_level、matrix_load、allowed_target_resolvers 和目标类型。
```

`damage`、`control` 不作为默认禁止 tag；它们由连锁应急术等级、矩阵负载和法术自身 `min_contingency_skill_level` 控制。`summon`、`extra_action`、`retrigger_contingency`、`complex_manual_target` 等禁止 tag 命中时必须在 UI 中显示拒绝原因。

负载默认按 `cast_level + tags/effect_category` 自动计算，特殊技能可使用 `contingency_load_override` 覆盖。技能若缺少应急储存配置，默认不能储存。

---

## 15. 完整示例一：濒死保命矩阵

```json
{
  "setup_id": "contingency_life_guard",
  "display_name": "濒死保命",
  "enabled": true,
  "charged": true,
  "source_skill_id": "mage_chain_contingency",
  "source_skill_level": 4,
  "matrix_load": 6,
  "reserved_mp_max": 12,
  "material_costs": [
    {
      "item_id": "special_contingency_gem",
      "quantity": 1
    }
  ],

  "trigger": {
    "type": "hp_below_percent",
    "subject": "owner",
    "percent": 30,
    "timing": "after_hp_changed"
  },

  "release_mode": "burst_release",

  "stored_spells": [
    {
      "stored_skill_id": "mage_mirror_image",
      "cast_level": 2,
      "order": 1,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    },
    {
      "stored_skill_id": "mage_stoneskin",
      "cast_level": 4,
      "order": 2,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ]
}
```

效果：

```text
当施法者生命值低于30%时，自动释放镜影术和石肤术。
```

---

## 16. 完整示例二：反近身矩阵

```json
{
  "setup_id": "contingency_anti_melee",
  "display_name": "反近身逃脱",
  "enabled": true,
  "charged": true,
  "source_skill_id": "mage_chain_contingency",
  "source_skill_level": 5,
  "matrix_load": 8,
  "reserved_mp_max": 16,
  "material_costs": [
    {
      "item_id": "special_contingency_gem",
      "quantity": 1
    },
    {
      "item_id": "resonance_dust",
      "quantity": 2
    }
  ],

  "trigger": {
    "type": "enemy_enter_radius",
    "center": "owner",
    "radius": 2,
    "timing": "after_position_changed"
  },

  "release_mode": "burst_release",

  "stored_spells": [
    {
      "stored_skill_id": "mage_thunderwave",
      "cast_level": 1,
      "order": 1,
      "target_resolver": {
        "type": "owner_centered_area"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    },
    {
      "stored_skill_id": "mage_blink_step",
      "cast_level": 2,
      "order": 2,
      "target_resolver": {
        "type": "empty_cell_near_owner",
        "preference": "away_from_trigger_source",
        "max_distance": 4
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ]
}
```

效果：

```text
敌人进入2格内时，自动释放雷鸣波，然后向远离敌人的空格闪现。
```

---

## 17. 完整示例三：致死伤害保命矩阵

```json
{
  "setup_id": "contingency_fatal_guard",
  "display_name": "致死逃生",
  "enabled": true,
  "charged": true,
  "source_skill_id": "mage_chain_contingency",
  "source_skill_level": 7,
  "matrix_load": 10,
  "reserved_mp_max": 22,
  "material_costs": [
    {
      "item_id": "perfect_contingency_gem",
      "quantity": 1
    }
  ],

  "trigger": {
    "type": "fatal_damage_incoming",
    "subject": "owner",
    "timing": "before_damage_resolved"
  },

  "release_mode": "burst_release",

  "stored_spells": [
    {
      "stored_skill_id": "mage_blink_step",
      "cast_level": 2,
      "order": 1,
      "target_resolver": {
        "type": "empty_cell_near_owner",
        "preference": "safe_cell",
        "max_distance": 4
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    },
    {
      "stored_skill_id": "priest_cure_wounds",
      "cast_level": 4,
      "order": 2,
      "target_resolver": {
        "type": "self"
      },
      "parameter_bindings": {},
      "fallback_policy": "skip_if_invalid"
    }
  ]
}
```

致死伤害触发时，流程应该是：

```text
1. 伤害即将结算
2. 系统检测到该伤害会致死
3. 连锁应急术触发
4. 自动闪现 + 自动治疗
5. 如果闪现后 owner 已脱离当前伤害有效命中条件，则取消当前伤害事件
6. 如果闪现失败或仍处于有效命中条件内，则继续结算原本伤害
7. 如果治疗后仍然被打到0，正常倒地
```

这里触发点必须是：

```text
before_damage_resolved
```

不能是：

```text
after_damage_resolved
```

否则角色已经死亡，保命逻辑无法成立。

---

## 18. 存档数据结构

人物绑定方案需要进存档。正式真相源应放在角色成员状态上，而不是放在队伍顶层的 `contingency_setups_by_member_id` 之类平行字典。

存档时不要保存完整法术对象，只保存 ID、触发器、目标解析、负载和充能成本参数。

```json
{
  "party_state": {
    "version": 4,
    "member_states": {
      "member_mage_01": {
        "member_id": "member_mage_01",
        "contingency_matrix_setups": [
          {
            "setup_id": "contingency_life_guard",
            "enabled": true,
            "charged": true,
            "source_skill_id": "mage_chain_contingency",
            "source_skill_level": 4,
            "matrix_load": 6,
            "reserved_mp_max": 12,
            "material_costs": [
              {
                "item_id": "special_contingency_gem",
                "quantity": 1
              }
            ],
            "trigger": {
              "type": "hp_below_percent",
              "subject": "owner",
              "percent": 30,
              "timing": "after_hp_changed"
            },
            "release_mode": "burst_release",
            "stored_spells": [
              {
                "stored_skill_id": "mage_mirror_image",
                "cast_level": 2,
                "order": 1,
                "target_resolver": {
                  "type": "self"
                },
                "parameter_bindings": {},
                "fallback_policy": "skip_if_invalid"
              }
            ]
          }
        ]
      }
    }
  }
}
```

重要约束：

```text
1. owner_member_id 不需要重复存；外层 PartyMemberState 就是 owner。
2. charged / material_costs / reserved_mp_max 必须显式存，不能靠读档后重新推断。
3. 读档不重新扣材料，只恢复 charged 状态和最大魔力封存。
4. 触发并进入释放流程、战斗外清除、未来破阵摧毁时，把 charged 改为 false，并释放 reserved_mp_max；current_mp 不自动增加。
5. V1 不做自然过期，不保存过期字段，也不保留 expired 状态。
6. 不允许战斗中存档，因此不保存 battle-local active matrix 的 triggering / releasing / queue 状态。
7. 不保存 charged_at_world_step；需要审计或显示最近操作时，读取世界层日志，不从 setup payload 推断。
8. 不做兼容：新增字段后 root save 版本升到 8，PartyState.version 升到 4；旧 payload 缺字段直接拒绝。
```

字段不变量：

```text
1. PartyMemberState.contingency_matrix_setups 是唯一持久真相源。
2. setup payload 不保存 owner_member_id；导入、复制、UI 调试都必须从外层 owner 上下文取得 owner。
3. setup_id 在同一 PartyMemberState 内唯一。
4. V1 每名角色最多一个 charged=true 的 setup；多个 charged setup 直接视为非法 payload。
5. enabled=false && charged=true 非法。
6. charged=false 时 reserved_mp_max 必须为 0，material_costs 必须为空数组。
7. material_costs 是已消耗材料的收据，不是仓库锁定，不允许读档时重新扣费。
```

战斗外手动清除充能是非战 UI / headless 命令中的自由操作，不消耗世界时间、行动或轮次。清除后：

```text
charged = false
reserved_mp_max = 0
material_costs = []
```

清除会释放 MP 上限，但 `current_mp` 不自动增加；特殊宝石不返还。任何编辑已充能配置的操作都必须先走清除流程，并弹出二次确认，明确提示“宝石不返还，MP 上限释放但当前 MP 不恢复”。战斗中不允许手动清除，也不允许手动触发；战斗中已有 battle-local instance / queue 时，不允许通过 UI 清除影响它，只能等战斗提交回写。

MP 上限封存不修改 raw `mp_max`。raw `mp_max` 仍由属性、成长、装备和临时修正规则产生；连锁应急术只通过人物状态上的 charged setup 计算可用上限：

```text
total_reserved_mp_max = sum(setup.reserved_mp_max for setup in contingency_matrix_setups if setup.charged)
effective_mp_max = max(raw_mp_max - total_reserved_mp_max, 0)
```

所有限制 `current_mp` 或恢复 MP 的路径必须使用 `effective_mp_max`，包括充能后 clamp、休息恢复、修炼恢复、进战单位生成、战斗中属性刷新和战后资源回写。属性成长、装备修正、技能前置、内容校验、raw 属性展示和存档读取仍使用 raw `mp_max`。

属性快照必须显式区分三个值：

```text
mp_max_unreserved / raw_mp_max = 属性、成长、装备和临时修正算出的未封存上限
reserved_mp_max = 当前 charged setup 封存的最大魔力总量
effective_mp_max / snapshot.mp_max = max(mp_max_unreserved - reserved_mp_max, 0)
```

持久层只在每个 charged setup 上保存 `reserved_mp_max`，不保存 member 级别总和。属性构建时由 `CharacterManagementModule` 汇总该角色所有 charged setup，把临时 `reserved_mp_max` 放入 `AttributeSourceContext`；`AttributeService._build_snapshot()` 先完成 raw `mp_max` 的全部加法、乘法和百分比修正，再在最后一步计算 `effective_mp_max`。这不是普通属性 modifier，也不得写进 `UnitBaseAttributes.custom_stats["mp_max"]`。

所有会写入或刷新 MP 的路径都要 clamp 到 `effective_mp_max`：修炼成长、休息/恢复、装备变化、充能、清除、战斗中释放封存、死亡提交和战后资源回写。UI 和日志展示 MP 上限时应同时显示未封存上限、封存值和可用上限，避免玩家误以为 raw 属性被永久扣减。

这样后续修改法术数值时，存档仍能读取最新的技能定义，同时不会重复扣材料或偷偷兼容旧结构。

---

## 19. Godot 落地建议

在当前 Godot 项目中，建议把数据、配置服务、战斗运行时和自动施法策略分开。

### 建议持久状态类

```text
scripts/player/progression/contingency_matrix_setup_state.gd
scripts/player/progression/contingency_trigger_state.gd
scripts/player/progression/contingency_target_resolver_state.gd
scripts/player/progression/contingency_stored_spell_entry_state.gd
scripts/player/progression/contingency_material_cost_state.gd
```

`PartyMemberState` 新增 `contingency_matrix_setups: Array[ContingencyMatrixSetupState]` 字段，并把这些状态类纳入 `to_dict()` / `from_dict()` 的 exact fields。`PartyState` 只负责成员集合和整体版本，不额外维护一份按成员 id 分组的应急矩阵字典。

建议在 `PartyMemberState` 提供纯计算 helper：

```gdscript
func get_total_reserved_mp_max() -> int
func get_effective_mp_max(raw_mp_max: int) -> int
```

这两个 helper 只读取已充能 setup 并返回数值，不负责扣宝石、校验储存法术、校验容量或修改 charged 状态；这些行为必须留在配置服务或战斗提交服务中。

### 建议运行时类

```text
scripts/systems/progression/party_contingency_setup_service.gd
scripts/systems/battle/runtime/battle_contingency_system.gd
scripts/systems/battle/runtime/auto_cast_request.gd
```

V1 不新增 `battle_event_dispatcher.gd` 或通用事件总线。`auto_cast_request.gd` 可以是轻量类，也可以先用严格字段字典，但必须有单一构造/校验入口。自动施法执行入口放在现有技能执行编排层内部，例如 `execute_auto_cast(request, batch)`，不要复用玩家 `issue_command()`。

配置路径：

```text
队伍 UI / headless 命令
    -> PartyContingencySetupService
        -> 校验 owner 技能、负载、材料、最大魔力封存
            -> 修改 PartyMemberState.contingency_matrix_setups
                -> GameSession 保存
```

充能事务必须是单入口、全成功才提交：

```text
1. 若当前处于战斗或 battle lock，拒绝。
2. 校验 owner 已学会连锁应急术、储存法术、等级解锁、负载、trigger / resolver 白名单和内容注册表。
3. 校验特殊宝石和 reserved_mp_max 可支付。
4. 在候选 PartyState 上设置 setup charged=true、写入 material_costs、写入 reserved_mp_max。
5. 扣除特殊宝石。
6. 重新计算 effective_mp_max，并 clamp current_mp。
7. 提交候选状态并写世界层日志。
```

任一步失败都不得修改仓库、人物状态或 current_mp。读档、进战和战斗开始确认都不能重新扣材料。

战斗路径：

```text
BattleRuntimeModule.start_battle 建好单位
    -> BattleContingencySystem 从 active member 的 charged setup 建 battle-local instance
        -> 玩家确认开战后调用 on_battle_confirmed
            -> 伤害、HP、状态、位移、法术影响等现有流程显式调用对应 hook
                -> BattleContingencySystem 创建 AutoCastRequest
                    -> execute_auto_cast 跳过 AP / 资源 / 冷却 / 熟练度 / 成就统计
```

战斗运行时边界：

```text
1. 只有 active member 的 charged setup 会生成 battle-local ContingencyInstance；reserve member 不生成实例，但仍承受 MP 封存。
2. 桥接发生在战斗单位生成和落位之后、`on_battle_confirmed` 之前；实例用 `source_member_id` 绑定 live unit，战斗开始时找不到 owner live unit 视为数据异常。
3. ContingencyInstance 只存在于 BattleRuntimeModule / BattleContingencySystem 的 sidecar 中，不保存完整 setup 到 BattleUnitState，不进入 BattleUnitState.to_dict()，也不写入存档。
4. 进入释放流程时，battle runtime 记录 consumed_setup_id，不在战斗中途直接改持久 `PartyMemberState`。
5. 进入释放流程时，战斗本地必须立刻释放该 setup 的 MP 上限封存，并刷新 owner 的 BattleUnitState / 属性快照；`current_mp` 不自动恢复。
6. 战斗胜利、逃跑提交或其他明确提交型结算时，才把 consumed setup 回写为 charged=false 并释放持久 reserved_mp_max。
7. 失败重试、结算失败、战斗中保存锁期间不得持久化 consumed 状态。
8. 永久死亡提交会清除该成员的 charged setup：charged=false、reserved_mp_max=0、material_costs=[]，并释放封存；未触发且非永久死亡的 setup 保持 charged。
9. 回写 charged=false 必须发生在 commit_battle_resources clamp current_mp 之前；释放封存只提高 MP 上限，不自动恢复 current_mp。
```

战斗结算生命周期：

| 战斗结果 | 未触发 charged setup | 已进入释放流程的 setup | MP 封存 |
|---|---|---|---|
| 胜利 / 正常提交 | 保持 `charged=true` | 回写 `charged=false` | 未触发继续封存；已触发释放 |
| 逃跑 / 提交型撤退 | 保持 `charged=true` | 回写 `charged=false` | 未触发继续封存；已触发释放 |
| 失败但读档 / 重试 | 不写回，回到战前存档状态 | 不写回，回到战前存档状态 | 跟随战前存档 |
| 永久死亡提交 | 清除该成员所有 charged setup | 清除 | 释放封存，但 `current_mp` 不恢复 |
| 战斗异常中断 / 结算失败 | 不提交 contingency 回写 | 不提交 contingency 回写 | 不污染存档 |

`consumed_setup_id` 只有在明确提交型结算时才写回。战斗中仍禁止保存 `triggering` / `releasing` / queue 等 battle-local 状态。

### 同步 Hook 流程

```text
BattleRuntimeModule / damage resolver / status resolver / movement resolver 到达固定 hook
    -> BattleContingencySystem 接收 hook facts
        -> 判断触发条件与 live gate
            -> 生成 AutoCastRequest
                -> execute_auto_cast 复用效果结算底层处理自动法术
```

示意：

```gdscript
func on_before_damage_resolved(hook_facts, damage_context):
    for matrix in active_by_trigger_type.get(hook_facts.trigger_type, []):
        if matrix.state != ContingencyState.ARMED:
            continue
        if not trigger_matches(matrix.trigger, hook_facts):
            continue
        trigger_matrix(matrix, hook_facts, damage_context)
```

特殊注意：

```text
1. 自动施法不能直接复用普通 issue_command()。
2. combat_started 不能在 start_battle() 中直接触发，应等玩家确认开战后触发。
3. fatal_damage_incoming 需要 pre-damage interception，不能等死亡落地后补救。
4. status_applied 与 movement 类触发需要由对应同步 hook 提供统一事实，否则容易漏路径。
5. UI 不能直接改 PartyMemberState，必须走配置服务并在 mutate 前检查 battle lock。
```

### UI、日志与结构化报告

连锁应急术的反馈分为三层，不能互相混用：

```text
1. source_event_facts
   - battle-local 规则事实，只用于触发判断和队列构建。
   - 不是玩家日志，不进入存档，也不交给 UI 解析。

2. BattleEventBatch.log_lines / report_entries
   - 战斗内反馈通道。
   - log_lines 是玩家可见的中文摘要。
   - report_entries 是 headless、回放和测试断言使用的结构化事件。

3. GameSession.log_event / GameLogService
   - 世界层运行日志。
   - 只记录战斗外充能、清除、战后提交等世界层事件。
   - 战斗中每个 hook 不直接写入 GameLogService。
```

玩家侧术语统一为：

| 内部概念 | 玩家可见文案 |
|---|---|
| matrix / contingency instance | 应急术式 |
| setup | 预设 |
| stored spell | 预存法术 |
| trigger / trigger_type | 触发条件 |
| target_resolver | 目标方式 |
| source_event | 触发原因 |
| release_mode | 释放方式 |

战斗外状态只显示 `未充能` / `已充能`。战斗内状态只显示 `待命` / `被压制` / `释放中` / `已耗尽`。避免在玩家 UI 中使用“已武装”“矩阵实例”“source event”等工程词。

玩家可见反馈规则：

```text
1. 触发时显示一次角色浮动文字，例如“应急术式触发”。
2. 战斗日志记录一条触发摘要，说明触发条件和将要释放的预存法术列表。
3. 每个预存法术的成功、跳过或无效原因进入日志，但同一矩阵释放应合并展示，避免刷屏。
4. 压制和压制解除只在状态变化时记录；持续处于反魔法领域不重复刷日志。
5. 战斗外清除充能必须提示“宝石不返还，MP 上限释放但当前 MP 不恢复”。
```

推荐中文日志模板：

```text
{角色} 的「{预设名}」已充能：封存 {reserved_mp_max} 最大魔力。
{角色} 的应急术式触发：{触发条件}。
应急术式开始释放：{法术列表}。
预存法术生效：{法术名}。
预存法术跳过：{法术名}（{原因}）。
{角色} 的应急术式被反魔法压制，暂不可触发。
{角色} 的应急术式压制解除，重新待命。
{角色} 的应急术式释放完成，充能已耗尽。
{角色} 的应急术式中止：{原因}。
已清除「{预设名}」：宝石不返还，{reserved_mp_max} 最大魔力已释放。
```

`report_entries` 不解析中文 `text`，必须使用稳定字段断言。V1 只使用两个入口类型：

```text
entry_type = "contingency_matrix"
entry_type = "contingency_auto_cast"
```

生命周期和结果写入 `decision`：

```text
triggered
release_started
stored_spell_cast
stored_spell_skipped
release_completed
release_aborted
suppressed
restored
live_gate_blocked
charge_consumed
```

通用字段：

```json
{
  "entry_type": "contingency_matrix",
  "decision": "triggered",
  "reason_id": "trigger_matched",
  "event_tags": ["contingency"],
  "text": "阿莱娜的应急术式触发：即将受到致死伤害。",
  "setup_id": "emergency_self_01",
  "contingency_instance_id": "battle_contingency_0001",
  "owner_member_id": "member_001",
  "owner_unit_id": "unit_ally_001",
  "source_skill_id": "chain_contingency",
  "trigger_type": "fatal_damage_incoming",
  "timing": "before_damage_resolved",
  "release_mode": "burst_release",
  "entered_release_flow": true,
  "consumed_charge": true
}
```

条件字段：

| 字段 | 使用场景 |
|---|---|
| `source_event_id` | 来自战斗事件事实的触发、压制、live gate 判断 |
| `damage_event_id` | 伤害链触发，例如 `incoming_damage_percent` / `fatal_damage_incoming` |
| `stored_skill_id` | `contingency_auto_cast` 条目 |
| `stored_spell_order` | 预存法术顺序 |
| `target_resolver_type` | 自动目标解析方式 |
| `target_unit_ids` | 解析到的单位目标；没有目标时为空数组 |
| `target_coord` | 解析到的格子目标；不适用时为 `null` |
| `cast_result` | `cast` / `skipped` / `no_effect` / `aborted` |
| `skip_reason` | 预存法术跳过原因 |
| `suppressed_reason` | 压制原因 |

稳定 `reason_id` 枚举：

```text
trigger_matched
release_started
matrix_completed
matrix_suppressed_antimagic
matrix_suppressed_dedicated
suppression_restored
owner_missing
owner_dead_or_absent
source_event_cancelled
duplicate_damage_event
release_context_failed
target_resolver_no_target
target_invalid
stored_skill_invalid
stored_skill_immune_or_no_effect
all_stored_spells_no_effect
recursive_contingency_blocked
already_consumed
```

以下内容不进入存档：

```text
BattleState.log_entries
BattleEventBatch.report_entries
source_event_facts
source_event_id
damage_event_id
ContingencyInstance
release_context
triggering / releasing / queue
skip_reason / suppressed_reason 日志
```

存档仍只保存 `PartyMemberState.contingency_matrix_setups` 中的预设和充能状态。结构化报告字段用于测试、调试、回放和玩家战斗日志生成，不参与读档恢复。

---

## 20. 最终设计结论

连锁应急术的数据结构重点不是“法术效果”，而是：

```text
人物绑定预设 + 战前充能成本 + 触发条件 + 储存法术 + 自动目标解析 + 同步 hook + 自动施法请求
```

它和普通法术的关系应该是：

| 系统 | 作用 |
|---|---|
| 普通法术系统 | 负责法术具体效果 |
| 连锁应急术系统 | 负责什么时候自动释放哪些法术 |
| 战斗同步 hook | 负责在关键结算点把稳定事实交给矩阵 |
| 目标解析器 | 负责自动选目标 |
| 校验器 | 负责防止非法配置和强度漏洞 |
| 充能成本系统 | 负责材料消耗与最大魔力封存 |

最终定位：

> 连锁应急术是高阶奥术师战前写在自己身上的应急保险。它可以随着技能等级解锁强力反制手段，但必须通过材料消耗、矩阵负载和最大魔力封存支付代价，让玩家体现出“我早就预料到了这种情况”，而不是每场战斗白送一次额外行动。

---

## 21. 已确认审查结论

本节记录对 DeepSeek 附录审查意见的已确认裁决。附录原文保留为外部审查记录，不在本节中改写。

### A. 系统设计与平衡

| 编号 | 裁决 |
|---|---|
| A1 | 封存最大 MP 时立刻 clamp 当前 MP；休息/恢复只能回到封存后的上限；释放封存不自动回蓝。 |
| A2 | 允许多法术爆发释放，不额外加 debuff 或槽位惩罚；平衡依赖 9 环学习难度、特殊宝石、矩阵负载与最大 MP 封存。 |
| A3 | `fatal_damage_incoming` 中位移可以取消当前伤害；若位移后脱离当前伤害有效条件，则该伤害事件取消。 |
| A4 | `combat_started` 可以触发强 buff，不扣第一回合动作，不禁 Haste 类法术。 |
| A5 | V1 不做自然过期；充能可长期保留，但持续封存最大 MP，且充能必须消耗特殊宝石。 |
| A6 | 沉默、眩晕、麻痹、睡眠、恐惧等普通控制不阻止矩阵触发；反魔法/专门压制效果可以压制。 |
| A7 | V1 默认无法解除；普通解除魔法无效，反魔法领域可临时压制，未来高阶裂解/专门破阵效果可摧毁充能。 |
| A8 | `hp_below_percent` 按伤害前后跨过阈值触发，不要求伤害后仍存活。 |
| A9 | 保留连续释放，用于延迟节奏、长期预案与较低负载。 |
| A10 | 储存法术自动释放不涨熟练度；只有战斗外施放/充能连锁应急术本体可按普通规则处理熟练度。 |
| A11 | 每级都要有明确奖励，由技能升级解锁触发器、法术类别、容量、连续释放和封存效率。 |
| A12 | 保留负载公式，但必须提供完整数值表，并在 UI 展示负载明细。 |
| A13 | `safe_cell` 按评分选择最优合法格；没有完美安全格也必须选择最高分合法格，只有无合法格时位移部分失败。 |
| A14 | 玩家侧展示为“被法术影响”；内部拆成直接目标和区域波及，默认两者都触发，高级设置可细分。 |
| A15 | V1 每名角色严格 1 个充能矩阵；可以保存多个未充能预设，但同一时间只能充能一个。 |

### B. 工程实现与数据结构

| 编号 | 裁决 |
|---|---|
| B1 | 全文、存档和运行时统一 `snake_case`，统一使用 `skill_id` 体系。 |
| B2 | 运行时状态字段与存档字段同名，不做 camelCase/snake_case 映射。 |
| B3 | 储存法术负载默认按 `cast_level + tags/effect_category` 自动计算，特殊技能可 `contingency_load_override`；禁用类 tag 直接禁止。 |
| B4 | 不做兼容；旧版本、缺字段或坏字段 payload 直接拒绝。 |
| B5 | `skill_id` 是持久契约；发布后不得随意重命名或复用；缺失 ID 直接拒绝，不做语义哈希。 |
| B6 | 爆发触发开始后不被回溯中断；连续释放每次后续释放前检查压制，压制中暂停。 |
| B7 | 战斗外可手动清除充能，释放最大 MP 封存，特殊宝石不返还。 |
| B8 | 同一事件中，每个 owner 最多触发自己的一个矩阵；不同 owner 可同时触发。 |
| B9 | 不允许战斗中存档，所以不做战斗中触发进度持久化；进入释放流程后战后回写 `charged=false`。 |
| B10 | 顺序释放队列只存在 battle-local runtime，不做跨存档追踪。 |
| B11 | 不做法术 ID 别名表，依赖冻结 `skill_id`。 |
| B12 | 触发器和目标解析器必须做交叉白名单校验，同时还要满足技能自身允许的 resolver。 |
| B13 | 保留 `parameter_bindings`，用于预设法术模式/元素/偏好；无参数时显式存 `{}`。 |
| B14 | 连续释放队列保存完整 `StoredSpellEntry`，不能只存字符串 ID。 |
| B15 | 移除 `forbidden_in_contingency`，只保留 `can_be_stored_in_contingency` + 禁止 tags。 |
| B16 | 移除 `can_trigger`，只由 `state` 决定能否触发。 |
| B17 | V1 不做过期；不保存 `charged_at_world_step`，也不保留任何过期/充能时间字段。显示与审计改走世界层日志。 |
| B18 | 移除 `expired` 状态。 |
| B19 | `suppressed` 是临时压制，结束后恢复 `armed` 或继续 `releasing`。 |
| B20 | 不加读档标记；扣特殊宝石只发生在唯一充能服务入口，读档永不扣费。 |
| B21 | 爆发释放是快速顺序结算，不做回滚，敌人不能在矩阵内插入行动。 |
| B22 | 连续释放首次触发立即释放第一个，后续法术在 owner 每次行动开始前结算。 |
| B23 | `BattleContingencySystem` 按 `trigger_type` 建 active matrix 索引，事件只扫描相关矩阵。 |
| B24 | `source_event_facts` 只作为 battle-local 规则事实，不是日志也不进存档；战斗反馈走 `BattleEventBatch.log_lines` / `report_entries`，世界层充能、清除、战后提交才走 `GameSession.log_event` / `GameLogService`。 |

### C. 玩家体验与 UI

| 编号 | 裁决 |
|---|---|
| C1 | 移除过期字段和 `-1` 示例；V1 不做自然过期。 |
| C2 | 特殊宝石不返还，但充能前 UI 必须明确提示。 |
| C3 | 已充能配置不可直接编辑；修改前必须清除充能，特殊宝石不返还，必须有确认弹窗。 |
| C4 | 触发、跳过、压制、清除都必须有日志/状态反馈；触发至少有一次浮动文字和一条战斗日志摘要，具体逐步细节进入结构化 `report_entries`。 |
| C5 | 用推荐模板和首次引导降低复杂度，不做规则层面的简单模式。 |
| C6 | 充能时 clamp 当前 MP；充能期间恢复上限为封存后的 max MP；释放封存不自动回蓝。 |
| C7 | 底层保留预设/充能两步，默认 UI 合并成“施放连锁应急术”向导，并明确显示未充能不会触发。 |
| C8 | UI 用“目标方式”和中文选项，内部仍用 `target_resolver`。 |
| C9 | 所有已充能配置修改都必须先清除充能，不区分小改/大改。 |
| C10 | 允许多个命名预设，但同一时间只能充能一个。 |
| C11 | 消耗点是进入释放流程，而不是储存法术最终是否生效；未创建 `release_context` 的触发拒绝不消耗，进入 `triggering` / `releasing` 后即使所有储存法术最终无效也消耗充能。 |
| C12 | 不适用，V1 禁止战斗中存档。 |
| C13 | 触发器 UI 用向导式流程，并按触发器类型切换参数面板。 |
| C14 | 严格自用；不开放队友绑定；UI 明确提示只能保护施法者本人。 |
| C15 | 不用“同时释放”，改为“爆发释放”。 |
| C16 | 任何会清除/覆盖已充能矩阵的操作都必须二次确认，并提示宝石不返还、MP 封存释放。 |
| C17 | 用 tags/effect_category 客观定义强度，并在 UI 显示需求和负载原因。 |
| C18 | 不做模拟触发；V1 只做必要配置预览/风险提示。 |
| C19 | V1 不做战斗内手动触发，也不做战斗内手动解除；只允许战斗外清除充能。 |
| C20 | 玩家可见名称使用“连锁应急术”；代码内部可继续使用 contingency/matrix 命名表达结构。 |
| C21 | “连锁”指触发条件到多个预设法术的链式释放，不允许连锁应急术触发连锁应急术。 |
| C22 | `fallback_policy` 作为高级选项可见，默认 `skip_if_invalid`，有失败风险时显示提示。 |
| C23 | 战斗内外都要显示连锁应急术状态；战斗外为 `未充能` / `已充能`，战斗内为 `待命` / `被压制` / `释放中` / `已耗尽`，玩家 UI 不使用“已武装”等工程词。 |
| C24 | 玩家可见术语统一为“应急术式、预设、预存法术、触发条件、目标方式、触发原因、释放方式”；中文 `text` 只用于展示，headless / 测试只能断言稳定字段、`decision` 和 `reason_id`。 |

### D. 系统整合与完整性

| 编号 | 裁决 |
|---|---|
| D1 | 补完整负载与封存数值表，公式数据驱动，UI 展示明细。 |
| D2 | 不做充能有效期，不保留过期字段。 |
| D3 | `matrix_capacity = 3 + skill_level`，1-9 级容量为 4 到 12。 |
| D4 | 不使用层级命名，只按技能等级范围解锁：1-2 基础防护/位移，3-4 反控/反近身/连续释放，5-6 法术影响/伤害/控制，7-8 致死触发/强控制/safe_cell，9 最高负载组合与未来 summon。 |
| D5 | V1 玩家可选触发器固定为 `combat_started`、`hp_below_percent`、`incoming_damage_percent`、`fatal_damage_incoming`、`status_applied`、`enemy_enter_radius`、`affected_by_spell`；`owner_turn_started` 仅用于连续释放内部调度；`targeted_by_spell` 合并进 `affected_by_spell`，`round_started`、`manual_keyword`、`entered_dangerous_tile` 不进入 V1。 |
| D6 | `contingency_matrix_setups` 正式落在 `PartyMemberState`，类型为 `Array[ContingencyMatrixSetupState]`，纳入 strict exact `to_dict()` / `from_dict()`；新增 trigger、resolver、stored spell、material cost 等显式状态类；setup 不存 owner，owner 来自外层 `PartyMemberState`；raw `mp_max` 不被改写，MP 封存通过 `effective_mp_max = max(raw_mp_max - total_reserved_mp_max, 0)` 投影到恢复、进战、刷新和战后回写；战斗中进入 release_context 后 battle-local 立即释放 MP 上限封存并记录 consumed setup，持久 `PartyMemberState` 到提交结算时才回写 `charged=false`。 |
| D7 | 连锁应急术是战前预施法矩阵，自动释放储存法术时绕过言语/姿势/材料成分和沉默、眩晕、麻痹、睡眠、恐惧等普通行动限制；反魔法/专门压制、owner 死亡/离场/不存在、目标非法或法术结算规则仍可阻止或使释放无效。 |
| D8 | 同一伤害事件必须携带稳定 `damage_event_id`；同一 owner 同一 `damage_event_id` 最多一个矩阵进入释放流程；伤害触发优先级为 `fatal_damage_incoming > incoming_damage_percent > hp_below_percent`；HP 写入后的 `hp_below_percent` 仍发生在死亡/倒地最终结算前，但若前置伤害触发已进入释放流程，则不再补触发。 |
| D9 | 多 owner 响应同一 AoE / 多目标瞬发事件时，先冻结 `source_event_facts` 并按 timeline / `unit_id` / `owner_member_id` 构建稳定触发队列；触发资格只看 frozen facts，不因前一个矩阵位移、造墙、护盾、杀死施法者或改变地形而重算；owner 轮到自己时再做 live gate；最终伤害/效果通过当前状态和 per-owner source event modifier 结算。持续区域、延迟爆炸、多段传播按 tick / 阶段重新生成 facts。 |
| D10 | 不新增 `matrix_fallback_policy`；矩阵是否消耗只看是否创建 `release_context` 并进入释放流程，进入后即使所有储存法术因目标解析失败、免疫、非法目标或 `skip_if_invalid` 没有实际效果，也消耗充能且不回滚；未进入释放流程的 live gate / 触发评估失败才不消耗。 |
| D11 | `suppressed` 只由反魔法、专门压制或未来高阶破阵/裂解压制模式造成；普通解除无效；压制不释放 MP 封存、不返还宝石、不改变 `charged=true`；`armed` 压制结束恢复 `armed`，队列中未进入释放流程则 live gate 失败不消耗，`burst_release` 开始后不回溯中断，`sequential_release` 压制中暂停队列并在解除后继续。 |
| D12 | 手动清除充能只允许战斗外通过 UI / headless 命令执行，是自由操作；清除后 `charged=false`、`reserved_mp_max=0`、`material_costs=[]`，释放 MP 上限但不自动恢复 current MP，特殊宝石不返还；编辑已充能配置前必须先清除并二次确认；战斗中不允许手动清除或手动触发。 |
| D13 | 储存法术采用默认禁止 + 显式白名单：`can_be_stored_in_contingency=true` 才可进入候选；随后检查 `forbidden_stored_skill_tags`，任一 tag 命中即拒绝，且禁止 tag 优先于白名单；`summon`、额外行动、再次触发、复活、永久制造、复杂手动选点等默认禁止，伤害/控制通过技能等级、负载和 `min_contingency_skill_level` 开放。 |
| D14 | 当前项目没有专注机制，V1 不实现、不校验、不保存任何专注相关字段；连锁应急术和储存法术暂不处理专注冲突，未来若引入专注机制再单独设计。 |
| D15 | 战斗生命周期按提交型结算处理：胜利/正常提交/逃跑提交时，未触发 charged setup 保持充能，已进入释放流程的 setup 回写 `charged=false` 并释放封存；失败读档/重试与异常结算失败不写回 contingency 状态；永久死亡提交清除该成员所有 charged setup，释放封存但不恢复 current MP；battle-local `triggering` / `releasing` / queue 永不存档。 |
| D16 | `skill_id` 是持久契约，发布后不得随意重命名、复用或改变语义；存档只保存 `source_skill_id` / `stored_skill_id`，读档找不到 ID 时拒绝 payload 或 setup；不做 alias table、semantic hash、名称/标签猜测替代，也不在连锁应急术系统内置自动迁移。 |
| D17 | 已存矩阵必须满足当前内容定义；若内容变更导致 setup 不再合法，例如法术不再允许储存、关键 tags / `min_contingency_skill_level` 冲突、目标解析器不再允许，读档或进入战斗时按存档异常处理；不自动清除、降级、返还、迁移或静默跳过。 |
| D18 | 保留 `parameter_bindings`，无参数时显式 `{}`；每个技能定义声明允许的 binding key、类型和枚举值，未声明 key 或错误 value 直接拒绝；该字段只用于法术模式选择，不允许保存目标、坐标队列、runtime unit id、owner、节点/脚本/函数/对象引用或任意嵌套结构。 |
| D19 | V1 `timing` 固定为实现契约，不由玩家自由选择；仅允许 `after_battle_confirmed`、`before_spell_effect_resolved`、`before_damage_resolved`、`after_hp_changed`、`after_status_applied`、`after_position_changed`、`owner_turn_started`，并由 trigger type 固定映射；旧式 `after_event`、`after_movement` 不进入 V1。 |
| D20 | 新增连锁应急术存档字段属于 save schema break：root save version 升到 8，`PartyState.version` 升到 4；明确不做兼容、不写迁移、不补默认字段、不支持旧 payload、不做 soft fallback；旧版本或缺字段 payload 加载失败并作为存档版本/结构不兼容处理。 |
| D21 | 删除 `charged_at_world_step`；V1 不保存充能时间、过期时间或 `-1` 哨兵值。若以后需要显示“最近充能/清除”，从 `GameSession.log_event` / `GameLogService` 查询世界层日志，不把时间快照放入 setup payload。 |
| D22 | 连锁应急术绑定 `owner_member_id` 人物身份，不绑定当前形态、种族、职业外观、身体模板或 battle-local `unit_id`；`self` 解析为 owner 当前 live `BattleUnitState`，使用当前形态的坐标、体型、属性和状态；若找不到 owner live unit，则未进入释放流程时 live gate 失败且不消耗，已进入释放流程后不回滚，后续 `self` 法术按目标解析失败跳过或中止。 |
| D23 | 原审查意见中的“自身矩阵递归触发”在当前状态机下无效：矩阵必须先从 `armed` 进入 `triggering` / `releasing` 才释放预存法术，已触发矩阵不能再次触发；不新增专门的自身递归处理。保留全局防跨矩阵规则：连锁应急术自动施法产生的派生事件不能触发任何连锁应急术，`AutoCastRequest.can_trigger_other_contingencies` 固定为 `false`。 |
| D24 | `max_active_per_caster = 1` 不按阵营区分；V1 玩家、敌人、召唤物或未来 boss 只要作为 caster 使用连锁应急术，都统一最多 1 个 active / charged 矩阵。敌人默认不使用持久充能矩阵；若未来需要敌方或 boss 使用，必须通过显式 enemy template / special profile 预装 battle-local 矩阵，不让 AI 临场感知该机制并做资源分配；仍默认每个 enemy unit 最多 1 个。多矩阵 boss 作为未来单独机制设计。 |
| D25 | 已由 D15 生命周期覆盖：成员永久死亡只有在提交型结算时才清除该成员所有 charged setup，写成 `charged=false`、`reserved_mp_max=0`、`material_costs=[]`，释放 MP 上限但不恢复 `current_mp`，特殊宝石不返还；失败重试或结算失败不写回。 |
| D26 | 原审查意见中的“多个单位同时进入范围”在 V1 事件模型下无效：位置变化按真实时间顺序逐个派发，不存在同时进入批量事件；`enemy_enter_radius` 按先来后到处理，首个从范围外进入范围内且成功创建 `release_context` 的敌人成为 `trigger_source` 并消耗矩阵，后续进入事件不会补触发；若先进入事件未进入释放流程则不消耗，后续进入仍可触发；不新增每轮冷却、批量聚合或同回合去重窗口。 |
| D27 | 召唤物、宠物、盟友、临时单位都不是 owner 的代理主体；它们受伤、被控、被法术影响或进入危险区域，不触发 owner 的矩阵。召唤物可以作为普通 `source_unit_id`：敌方召唤物伤害 owner、进入 owner 半径或施法影响 owner 时，可按对应触发器触发；未来 `summoner_unit_id` / `summoner_member_id` 只用于归因、AI、日志或召唤控制，不把召唤物事件改写成 summoner 本人事件。 |

### E. 代码架构可行性审查

| 编号 | 裁决 |
|---|---|
| F1 | MP 封存不修改 raw `mp_max`，也不写 `UnitBaseAttributes.custom_stats["mp_max"]`；属性快照显式区分 `mp_max_unreserved`、`reserved_mp_max`、`effective_mp_max`。持久层只保存 setup 级 `reserved_mp_max`，member 级总和运行时汇总；`AttributeSourceContext` 增加 transient `reserved_mp_max`，`AttributeService._build_snapshot()` 在所有属性修正后计算 effective max。充能立刻 clamp current MP；战斗中释放封存立刻刷新 BattleUnitState 上限但不恢复 current MP；战后提交先释放封存再提交资源 clamp。 |
| F2 | 自动施法不走 `issue_command()`；新增 battle-local `AutoCastRequest` 和内部 `execute_auto_cast()` 路径。固定 flags：`is_auto_cast`、`source_kind=contingency`、`ignore_action_phase`、`ignore_ap_cost`、`ignore_resource_cost`、`ignore_cooldown`、`ignore_identity_charge`、`ignore_mastery_gain`、`ignore_skill_used_achievement`、`skip_spell_control`、`spent_mp=0`、`can_trigger_other_contingencies=false`。保留内容查找、目标、抗性、护盾、豁免和效果结算。 |
| F3 | 不做通用事件总线，不新增 `battle_event_dispatcher.gd`。改为 `BattleContingencySystem` 暴露固定同步 hook：`on_battle_confirmed`、`before_damage_resolved`、`after_hp_changed`、`after_status_applied`、`after_position_changed`、`before_spell_effect_resolved`、`owner_turn_started`。`before_damage_resolved` 必须能写入取消或修改伤害的 per-owner 修正；source event id / damage event id 使用 battle-local serial，不进存档。 |
| F4 | 战斗中进入 release_context 后，battle-local 立刻消耗 setup 并释放 MP 上限封存；持久 `PartyMemberState` 不在战斗中途修改。正式战后提交时先把 consumed setup 写成 `charged=false` 并释放封存，再提交资源；失败重试、结算失败和战斗保存锁不写回。永久死亡提交清除该成员所有 charged setup，宝石不返还，current MP 按死亡规则处理。 |
| F5 | `PartyMemberState.contingency_matrix_setups` 使用 strict exact fields；新增 root save version 8 和 `PartyState.version=4`。旧 payload、缺字段、坏字段直接拒绝，不补默认、不迁移、不做兼容。 |
| F6 | 战斗桥接从 `PartyMemberState` 到 `BattleContingencySystem`，发生在单位生成落位之后、战斗确认 hook 之前。实例使用 `source_member_id` 绑定 live unit；不把完整 setup 存进 `BattleUnitState` 或 `BattleUnitState.to_dict()`。战斗开始找不到 owner live unit 是数据异常，不静默跳过。 |
| F7 | 持久状态类落在 `scripts/player/progression/`：setup、trigger、target resolver、stored spell entry、material cost 五类。运行时新增 `party_contingency_setup_service.gd`、`battle_contingency_system.gd`，可选 `auto_cast_request.gd`；不新增 `battle_event_dispatcher.gd`。 |
| F8 | 保存格式破坏性升级：root save version 升到 8，`SaveSerializer` 默认版本同步到 8，`PartyState.version` 升到 4；save index 版本不因正文存档字段变化而升级，除非索引 schema 另有变化。 |
| F9 | 命名统一 snake_case。`skill_id` 是技能定义持久契约；`source_skill_id` 表示创建矩阵的连锁应急术技能，`stored_skill_id` 表示自动释放的预存技能。玩家 UI 使用“爆发释放”；战斗内部状态为 `armed`、`suppressed`、`releasing`、`depleted`，不把 `triggering` 暴露给玩家。 |
| F10 | 复用现有 `BattleEventBatch.report_entries` 通道，必要时加公开 helper，不直接调用私有 `_append_report_entry_to_batch`。新增结构化条目使用 `entry_type`，不使用旧式 `type`。 |
| F11 | 现有 battle save lock 可以作为战斗不存档基础；新增要求是 contingency 回写顺序必须在 `commit_battle_resources()` clamp 之前。若崩溃、结算失败或 lock 未释放，存档保持战前状态；不新增 dirty flag。 |
| F12 | 世界层新增 `PartyContingencySetupService` 作为充能/清除唯一入口。充能事务先拒绝战斗态，再校验技能、内容、材料和封存，候选状态写 charged/material/reserved，扣宝石，重算 effective max 并 clamp current MP，最后提交；任一步失败都不产生部分 mutation。所有世界层 MP 恢复与 clamp 路径使用 effective max。 |
| F13 | `charged_at_world_step` 直接删除；不使用 `-1` 哨兵，不保存充能时间或过期时间。需要显示或审计最近充能/清除时走世界层日志，不把该字段放回 setup payload。 |
| F14 | 当前不做专注机制，不讨论连锁应急术与专注的交互。V1 不新增专注字段、校验、状态、日志或测试。 |
| F15 | V1 不做敌人连锁应急术；未来敌人 / boss 的应急连锁术由 enemy template 或 special profile 预装为 battle-local instance，不读取 `PartyMemberState.contingency_matrix_setups`，不走宝石 / MP 封存持久成本，也不要求 `enemy_ai_service` 感知该机制并做分配。默认仍每个 caster 最多 1 个矩阵；多矩阵 boss 以后单独设计。 |

### G. 第二轮代码验证审查

| 编号 | 裁决 |
|---|---|
| G1 | `before_damage_resolved` 是可修改 hook，不是观察 hook。它对所有即将结算的伤害调用，包括预计完全被护盾吸收的伤害；但 `incoming_damage_percent` / `fatal_damage_incoming` 只按 `projected_hp_damage_after_shield` 判定，预计 HP 伤害为 0 时不触发。hook 必须支持 per-owner `cancel_damage` / `modified_resolved_damage`；`cancel_damage=true` 时不扣护盾、不扣 HP、不产生对应 HP crossing，已进入 release_context 的矩阵仍消耗。 |

---

# 附录：对抗性检视报告

以下从四个维度对本文档进行对抗性检视，按严重程度标出所有发现。

---

## A. 系统设计与平衡视角

### A1. [CRITICAL] MP封存不扣当前MP → 休息后零成本
成本模型仅封存 `reservedMpMax`（最大魔力上限），但充能时当前MP不受影响。法师充能后在营地休息至满，等于白嫖3个法术。
**建议：** 充能时必须同时扣除当前MP，或休息恢复不能超过 `MaxMP − reservedMpMax`。

### A2. [CRITICAL] 同步释放等于免费获得3个回合的行动
3个法术瞬间释放，无视AP/MP/冷却。战士需要3轮自buff，法师用一个被动的反应动作完成——行动经济严重失衡。
**建议：** 触发后给施法者施加N轮debuff（过载），或限制为每触发最多释放1个法术（高等级再解锁多法术）。

### A3. [CRITICAL] 致死伤害触发 + 闪现 + 治疗 = 不死之身
`fatal_damage_incoming` + `before_damage_resolved` + 闪现传送 + 治疗 → 角色无法被单次攻击击杀。
**建议：** 该类触发每长休限一次，或治疗在伤害结算后触发（作为稳定手段而非免死金牌）。

### A4. [HIGH] 开战触发 = 永久战前buff
`combat_started` 触发让法师每战零动作自动buff 3个法术，战士/盗贼还没行动。
**建议：** 开战触发应消耗第一回合动作，或禁止储存Haste类法术。

### A5. [HIGH] 无过期 → 一次性投资永久保险
所有示例的 `expiresAtWorldStep = -1`，一份材料+一次封存覆盖整个战役。
**建议：** 增加充能有效期（世界步数/天数限制），或要求周期性小额维护费用。

### A6. [HIGH] 矩阵穿透沉默/眩晕/CC释放
自动施法绕过所有花费检查，暗示即使沉默/眩晕也会释放。矩阵成为所有控制效果的硬反制。
**建议：** 明确定义哪些角色状态会阻止矩阵触发。至少沉默应压制矩阵。

### A7. [HIGH] 解除魔法一击抹消全部投入
敌方一个3环解除魔法可摧毁8环连锁应急术的投入（材料丢失、封存释放但当前MP不回）。
**建议：** 基于技能等级提供解除抵抗，或将"解除"改为"压制"（战后恢复）。

### A8. [HIGH] hp_below_percent 被爆发伤害跳过
一次攻击将HP从35%直接打到0% → 条件从未在"低于30%且存活"窗口触发。
**建议：** 增加 `hp_crossed_percent_threshold` 触发器类型，检查伤害前后HP跨越。

### A9. [MEDIUM] 连续释放没有机制优势
同时释放严格优于连续释放——需要保护的是"现在"而非分3轮。
**建议：** 给连续释放实质好处：更低负载、更低MP封存、或后续法术+1有效施法等级。

### A10. [MEDIUM] 零熟练度增长产生逆向激励
触发施法不涨熟练度 → 玩家故意不把未熟练法术放进矩阵 → 只在法术刷满后才用系统。
**建议：** 给予50%熟练度（防止挂机刷，但不惩罚正常使用）。

### A11. [MEDIUM] 技能升级有很多"死等级"
仅有4个模糊层级（初级/中级/高级/顶级），中间等级无实质性收益。
**建议：** 提供每级具体奖励表——每级解锁新触发类型/法术类别/+1位置/容量提升/封存比例降低。

### A12. [MEDIUM] 矩阵负载公式有5个隐藏税变量
`触发器税 + 目标解析税 + 效果类别税 + 释放模式税` —— 全无数值。
**建议：** 在UI中展示完整负载明细，让玩家理解每项贡献。

### A13. [MEDIUM] `safe_cell` 解析器未定义
"安全"格没有精确定义——可能传送到陷阱、岩浆或孤立位置。
**建议：** 明确定义：无人、距盟友N格内、不邻接敌人、在视线内、非危险地形。无满足条件则落回自身。

### A14. [LOW] `targeted_by_spell` 与AoE模糊
火球中心3格外、施法者在溅射范围内是否算"被锁定"？
**建议：** 区分"被直接指定为目标"和"落入法术区域"两种触发器。

### A15. [LOW] 1矩阵限制不与投入成正比
20级大法师和1级学徒只能维持1个矩阵。高投入无回报。
**建议：** 高级技能等级允许第二矩阵位，或通过天赋解锁。

---

## B. 工程实现与数据结构视角

### B1. [CRITICAL] 标识符命名三重不一致
`spellId`(263行)、`skill_id`(1088行存档)、`source_skill_id`(1067行存档) 三种键名对应同一逻辑字段。`mage_chain_contingency` 与 `spell.chain_contingency` 是不同的ID字符串，查找必然失败。
**建议：** 全局统一 `snake_case` 命名，后缀 `_id` 而非 `Id`。

### B2. [CRITICAL] 存档格式与运行时配置的字段命名分裂
第5节用 `camelCase`(`chargedAtWorldStep`、`reservedMpMax`)，第18节用 `snake_case`(`charged_at_world_step`、`reserved_mp_max`)。无映射表→加载时数据必然丢失。
**建议：** 全文统一为 `snake_case`，与GDScript `to_dict()`/`from_dict()` 对齐。

### B3. [CRITICAL] 成本公式无具体值 → 不可实现
`reservedMpMax = 基础封存 + ceil(matrixLoad * 技能等级系数)` 中，7个变量全无数值。三个示例的数据也无一致算术关系。
**建议：** 贴出带注释的具体数值表，或将 `reservedMpMax` 直接写入配置（由策划手工调整）。

### B4. [CRITICAL] "不做兼容"无用户体验和回滚策略
旧存档被拒绝时用户表现未定义：静默崩溃？错误弹窗？存档被孤立？若玩家在 `charged=true` 时存档，拒绝意味着材料和封存同时丢失。
**建议：** 定义具体行为（错误消息 + 禁止加载），提供版本跳转逻辑，至少给一个软着陆期。

### B5. [CRITICAL] 仅存ID但法术定义可能语义变更
镜影术若被重做为完全不同效果，已充能矩阵将产生意外行为。存档依赖加载时解析定义但无版本控制/哈希校验。
**建议：** 对加载法术结构做哈希校验，或对影响已存矩阵的定义变更做版本控制。

### B6. [CRITICAL] `triggering`/`releasing`状态中对驱散无防御
伪代码假设进入 `triggering` 后无阻碍运行至完成。敌人在此过程中驱散矩阵→行为未定义。
**建议：** 添加 `if matrix.state in [DISPELED, SUPPRESSED]: abort_cast_queue()`。

### B7. [CRITICAL] 没有"手动清除"状态
摘要和成本规则多次提到"手动清除"，但状态枚举中无对应值。
**建议：** 添加 `cancelled` 状态（玩家主动清除，不写完成痕迹），成本语义同驱散。

### B8. [CRITICAL] 规则13.2与伪代码冲突
规则写"同一事件最多触发一个应急矩阵"，伪代码遍历所有实例并触发每个匹配。两个施法者都有 `combat_started` → 都触发。
**解决：** 规则应为"对同一施法者最多触发一个"，伪代码不需改，规则文本需修正。

### B9. [CRITICAL] 触发中保存/读档导致双重触发
`triggering`状态不写入存档 → 重新加载后 `charged=true` → 矩阵重建为 `armed` → 同事件可重新触发。
**建议：** 在触发开始时原子写入 `charged=false`，或将 `consumed_charge` 标志同步持久化。

### B10. [HIGH] 多法术顺序释放中战斗崩溃 → 部分消耗无法追踪
顺序释放完成2/3后崩溃，存档显示 `charged=true`（队列未写入存档），等于无代价重置。
**建议：** 将 `released_spell_count` 写入存档结构，仅战斗完全结束后做 `charged=true→false`。

### B11. [HIGH] 法术重命名破坏全部存档
`spell.mirror_image` → `spell.illusion.mirror_image` → 所有已充能矩阵悬空引用。
**建议：** 实现法术ID别名表，或冻结初始发布后的法术ID。

### B12. [HIGH] 验证器不检查触发器与目标解析器的配对
`combat_started` 无触发源，但 `targetResolver.type = "trigger_source"` 会通过验证。
**建议：** 为每个触发器添加 `allowed_target_resolvers` 白名单，验证器逐项执行交叉合规检查。

### B13. [MEDIUM] 实例的 `storedSpells` 缺少 `parameterBindings`
第9节定义了 `parameterBindings` 字段，第6节实例JSON中缺失。
**建议：** 将 `parameterBindings` 加入实例JSON或明确声明实例构造时拒收/允许该字段。

### B14. [MEDIUM] `remainingQueue` 存字符串而非完整对象
顺序释放队列存纯字符串列表，其他地方存储的是带 `castLevel`/`targetResolver` 等的完整对象。
**建议：** 将 `remainingQueue` 定义为 `StoredSpellEntry` 对象的完整列表。

### B15. [MEDIUM] `canBeStoredInContingency` 与 `forbiddenInContingency` 冗余
两者共存允许自相矛盾的状态。
**建议：** 移除 `forbiddenInContingency`，仅保留 `canBeStoredInContingency`。

### B16. [MEDIUM] `canTrigger` 字段未被解释
实例有 `canTrigger: true`，全文无说明其含义和与 `state == armed` 的区别。
**建议：** 补充定义或移除冗余字段。

### B17. [MEDIUM] 全局步进重置会破坏过期时间
`world_step` 若因补丁/故障而重置，过期和充能时间全面失效。
**建议：** 使用单调世界时钟（带纪元标识符），或存校验和/代际哈希。

### B18. [MEDIUM] `expired` 转换无事件驱动
状态枚举含 `expired`，但战斗事件列表中无"按世界步进检查过期"事件。`armed → expired` 永远不会触发。
**建议：** 在 `RoundStarted` 添加 `check_expiry` 步骤。

### B19. [MEDIUM] `suppressed → armed` 恢复路径未定义
反魔法场移除后，已压制的矩阵恢复还是永久禁用？
**建议：** 要么定义 `suppressed` 为不可逆（同驱散），要么定义检查反魔法退出的恢复转换。

### B20. [MEDIUM] 无"读档"标记阻止重复扣材料
规则说"读档、进战不再次扣材料"，但存档中无标志位告诉系统"这是恢复，不重新计费"。
**建议：** 增加一个 `is_restored_from_save` 上下文标识。

### B21. [LOW] 同步释放排序——原子性未明确
按顺序结算——如果2号法术对象依赖1号法术结果，作为原子事务还是独立多事件处理？
**建议：** 明确："每个法术在前一个完全结算后作为独立自动施法提交"，不尝试原子回滚。

### B22. [LOW] 顺序释放回合边界——效果何时生效
第N轮开始时释放法术，同轮的回合内效果在半轮结算前还是后生效？
**建议：** 明确自动施法在 `round_started` 事件监听器主列表中的触发点。

### B23. [LOW] 事件循环O(NxM)性能
每个战斗事件遍历所有武装矩阵。8个队员 x 每回合几百事件 → 显著CPU开销。
**建议：** 建立事件类型到受影响矩阵的哈希映射表。

---

## C. 玩家体验与UX视角

### C1. [CRITICAL] `expiresAtWorldStep: -1` 从未解释
每个示例都使用 `-1`（永不过期），但"世界步进"是什么？矩阵能否过期？玩家完全不知道。
**建议：** 新增章节解释世界步进、过期策略、`-1`的含义。

### C2. [CRITICAL] 材料消耗后永不返还（即使从未触发）
充能消耗材料，触发/清除/解除/过期均不返还。战斗中从未低于30%HP→永久损失材料→零收益。
**建议：** 至少允许手动清除/解除时部分退款，或将材料保留到矩阵实际触发时再消耗。

### C3. [CRITICAL] 编辑已充能预设 → 全部材料损失，零警告
从30%改成25%需要重付全部材料。惩罚实验和误操作太过严厉。无撤销、无宽限期、无部分退款。
**建议：**
- 最优：允许编辑所有内容，仅重新计算负载差异
- 良好：小改动（阈值、退路策略）免费；大改动（换法术）重新计费
- 可接受：任何编辑先展示成本预览："此变更消耗：奥术水晶x1, +3封存MP，继续？"

### C4. [CRITICAL] 触发器反馈零规范
文档描述内部状态机但未定义玩家看到/听到/读到什么。同时释放3个法术若无视觉/音效/日志反馈，玩家完全不知道矩阵已触发。
**建议：** 必须要求以下反馈：
- 独特视觉特效（符文圈、魔法符文爆炸）
- 独特音效
- 战斗日志："[应急矩阵] HP<30% -> 镜影术, 石肤术"
- 角色上方浮动文字："应急矩阵触发!"
- 触发法术施放的buff图标上角标，表明来自矩阵

### C5. [CRITICAL] 零入门引导——系统是全有或全无的复杂壁垒
新玩家需同时理解：充能/预设两步操作、5个隐藏税项、目标解析器、退路策略、零熟练度增长、永久MP削减。无渐进式引导。
**建议：** 添加NPC导师引导首矩阵；提供"推荐"预构建矩阵；考虑"简单模式"（隐藏目标解析器和退路策略，默认自我目标+跳过无效）。

### C6. [HIGH] `reservedMpMax` 对当前MP的影响未定义
说"角色可用 max MP 应扣掉"，但当前MP呢？当前MP=5、封存=12 → 角色昏迷？
**建议：** 明确定义充能/触发/解除/过期时对最大MP和当前MP的行为。最好做一张场景表。

### C7. [HIGH] "预设 vs 充能"两步操作不直观
先存预设（免费）再充能（付费）。玩家可能存了预设就以为已保护→死了才发现从未充能。
**建议：** 合并为单按钮"施放应急术"（自动存储预设作为充能的一部分）。高级切换可分离，但默认合并。

### C8. [HIGH] "目标解析器"是程序员术语
`trigger_source`、`owner_centered_area`、`empty_cell_near_owner` —— 玩家读不懂。
**建议：** 映射到中文描述："目标：[我] [触发此阵的敌人] [附近安全格]"。尽量提供可视化预览。

### C9. [HIGH] 编辑清充能规则——所有编辑一视同仁
改阈值%和换法术受完全相同惩罚。
**建议：** 见C3。

### C10. [HIGH] 无预设库/配置系统
只能维持1个充能矩阵 → 想换用其他方案需手动重建整个配置。
**建议：** 增加命名预设系统，可存无限预设，充能时选哪个预设激活。

### C11. [HIGH] 无声失败（`skip_if_invalid` 无通知）
储存法术找不到有效目标静默跳过。玩家设计的3法术矩阵只释放2个，不知原因→死亡。
**建议：** 战斗日志记录每次跳过："[应急矩阵] 雷鸣波跳过——未找到有效目标。"

### C12. [HIGH] 战斗中途存档/读档行为未定义
战斗中保存（矩阵已触发但效果未完全结算）——读档后重武装还是正确标记为已完成？
**建议：** 存档一个 `matrix_triggered_this_battle` 标志。若已设置，读档后 `charged=false` 不重武装。

### C13. [HIGH] 触发器配置UI高度异质
每个触发器需完全不同输入（滑块、范围选择器、标签云），统一UI设计难度大。
**建议：** 向导式UI：(1)"何时触发？"→视觉分类选择，(2)"配置细节"→选中触发器上下文面板，(3)"触发后做什么？"→法术配置。

### C14. [HIGH] 严格自用限制破坏了支援法师幻想
DND文化中法师用应急术保护队友是经典玩法。文本禁止了 `bound_ally` 解析器和盟友绑定。
**建议：** 考虑"高级连锁应急术"作为单独高阶法术允许一个盟友绑定矩阵（更高成本）。V1即使不自用，也在工具提示写明："仅能保护自己。高级版本请学习高级连锁应急术"——将限制变预告。

### C15. [MEDIUM] "同时释放"实际是顺序释放
说"同时"但实现是按顺序 `for` 循环。法术1结束后才开始法术2。
**建议：** 改名为"爆发释放"或"快速顺序释放"，说明敌人不能在矩阵内法术间行动。

### C16. [MEDIUM] 覆盖旧矩阵无保护
"新覆盖旧"——一键误操作无声删除已充能矩阵（材料+封存全损失）？
**建议：** 弹出确认对话框："你已有充能矩阵。创建新矩阵将销毁它，材料不返还。继续？"

### C17. [MEDIUM] "强攻击/强控制"无客观定义
什么决定"强"？法术等级？标签？效果数值？玩家不知为何A法术3级可用、B法术需7级。
**建议：** 在法术工具提示中展示 `effectCategory` 和 `minContingencySkillLevel`。

### C18. [MEDIUM] 无"试射"或模拟模式
玩家无法验证矩阵是否正常工作→战斗触发时才知错误→致命伤害触发尤甚。
**建议：** 矩阵编辑器中添加"模拟触发"按钮，展示施放法术/目标/顺序/效果。

### C19. [MEDIUM] 无法手动触发或战斗内解除
想提早手动触发或想等更好时机——无法控制。
**建议：** 添加"解除矩阵"动作（自由动作）和"强制触发"动作（完整动作）。

### C20. [MEDIUM] "应急矩阵"（矩阵）的科幻味与奇幻游戏相悖
**建议：** 考虑"应急法阵"、"应急结界"、"应急术式"。

### C21. [MEDIUM] "连锁应急术"暗示级联但系统禁止级联
"连锁"强烈暗示级联（A触发B触发C），但第13节明确禁止。
**建议：** 考虑"预设应急术"或"触发应急术"，或重新定义"连锁"为"条件到法术的链接"。

### C22. [LOW] `fallbackPolicy` 需在编辑器中有可见性
玩家在配置矩阵时看不出"万一目标无效怎么办"。
**建议：** 在编辑器中设为"高级"可见项，对任何可能失败的目标解析器旁显示警告图标。

### C23. [LOW] 无战斗内武装状态视觉指示
玩家不知矩阵处于待命还是已释放。
**建议：** 角色头像上添加持久状态图标和buff栏条目："应急矩阵：武装（HP<30% -> 镜影术 + 石肤术）"。

---

## D. 系统整合与完整性视角

### D1. [CRITICAL] `matrixLoad` 成本公式几乎完全未定义
7个税项目中0个有具体值，不可实现（同B3）。三个示例值（load=6->MP=12, load=8->MP=16, load=10->MP=22）无推导关系。
**建议：** 提供每个税类别的完整数值表、`baseMatrixLoad`+`castLevel`->储存负载的映射公式、各技能层级对应 `基础封存` 和 `技能等级系数`。

### D2. [CRITICAL] 充能有效期完全缺失
全部示例用 `-1`，未定义：基础持续时长、持续时间单位（世界步/秒/轮次/休整）、是否受技能等级/材料品质影响。
**建议：** 说明V1不支持过期（始终用 `-1`），或定义 `baseDurationWorldSteps` 常量。

### D3. [CRITICAL] `matrixCapacity` 等级进度表未定义
"由连锁应急术等级决定，默认8起" —— 无等级->容量映射表。
**建议：** 提供具体表（1->4, 3->6, 5->8, 7->10, 9->12），或公式。

### D4. [CRITICAL] 无具体技能等级->层级映射
初级/中级/高级/顶级的标签无法映射到具体等级数（技能等级4算初级还是中级？）。`minContingencySkillLevel` 无法实现。
**建议：** 提供具体表：

| 技能等级 | 层级 | 容量 | 允许类别 | 允许触发器 | 释放模式 |
|---|---|---|---|---|---|
| 1-2 | 初级 | 4 | 防护、位移 | hp_below_percent, combat_started | 同时 |
| 3-4 | 中级 | 6 | +反控、反近身 | +enemy_enter_radius, status_applied | 同时 |
| 5-6 | 高级 | 8 | +攻击、强控 | +fatal_damage_incoming | 同时+连续 |
| 7+ | 顶级 | 10+ | +召唤（高负载） | +targeted_by_spell | 同时+连续 |

### D5. [CRITICAL] 触发器类型一半无结构定义
10个类型中仅5个有JSON定义。`manual_keyword` 未解释。`entered_dangerous_tile` 无对应地格标签系统。
**建议：** 为全部10类型提供完整JSON schema和参数说明，或将未定义项移出V1范围。

### D6. [CRITICAL] `PartyMemberState` 字段定义太模糊
`contingency_matrix_setups` 无类型声明、无 `to_dict()`/`from_dict()` 交互说明、存档用 `snake_case` 而内存用 `camelCase`、无 `reserved_mp_max` 如何影响 `max_mp` 的说明。
**建议：** 声明具体GDScript类型、声明序列化格式、添加 `get_effective_max_mp()` 方法。

### D7. [CRITICAL] 沉默/眩晕/麻痹的施法者在触发时行为未定义
应急矩阵是"预施法"——监听器应工作。但：沉默的施法者储存的雷鸣波能否生效（言语成分）？麻痹者的闪现步（姿势成分）？眩晕者的镜影术？
**建议：** 明确声明："应急术自动施法绕过所有成分要求（言语/姿势/材料）和所有动作限制。即使施法者沉默/麻痹/眩晕/以其他方式无法行动，矩阵仍可触发并结算全部储存法术。"

### D8. [CRITICAL] 同一伤害事件的触发器冲突
AoE对施法者命中60%最大HP：
- `hp_below_percent` 成立 -> 按 `after_event` 触发
- `fatal_damage_incoming` 成立 -> 按 `before_damage_resolved` 触发
- 哪个先？哪个触发？治疗是否抑制另一个？

**建议：** 定义触发器优先和解决顺序：每施法者每个源事件最多只触发一个矩阵，最高优先级者胜。

### D9. [CRITICAL] 多施法者同类触发器无排序
A、B两位施法者均有 `combat_started` 触发——什么顺序解决？A的buff是否影响B的后续解决？
**建议：** 定义确定性排序（先攻顺序），所有自动施法请求在战斗轮次开始前排队并按序解决。

### D10. [HIGH] 全部储存法术目标解析失败——行为未定义
3个储存法术 `fallbackPolicy` 均为 `skip_if_invalid`，且全部目标均无效->矩阵变为已完成？消耗充能？释放MP？当"已触发并消耗"还是"已触发但失败"？
**建议：** 为整体矩阵定义 `matrixFallbackPolicy`（`consume_on_trigger_anyway` / `retain_charge_on_total_failure`），并指定状态转换。

### D11. [HIGH] `suppressed` 状态零行为定义
无进入条件、无MP释放规则、无计时行为、无反魔法结束后恢复规则。
**建议：** 定义完整状态机：进入条件=反魔法场/裂解术；MP行为=保留封存；计时=过期计时暂停；恢复=反魔法结束后于施法者下回合开始时重新武装。

### D12. [HIGH] "手动清除"从未定义为动作
如何清除（战斗动作/非战UI按钮）？消耗动作/轮次/时间？可否在战斗中？是否可逆？
**建议：** 定义为非战UI中的自由动作，设置 `charged=false`、释放MP、材料不返还。

### D13. [HIGH] 标签排除逻辑对多标签法术未定义
法术标签=`["summon", "extra_action"]`，`"summon"`在文本禁止列表但在JSON禁止列表中不存在->矛盾。用"ANY匹配即禁止"还是"ALL匹配才禁止"？
**建议：** 定义排除逻辑：
1. 优先检查 `SpellDefinition.forbiddenInContingency`
2. 若该法术任一标签在 `forbiddenStoredSpellTags` 中->阻止
3. 将 `"summon"` 加入 `forbiddenStoredSpellTags`
4. 定义阻止方式为"任一标签相交"

### D14. [HIGH] 储存法术需要专注
应急矩阵自身不需专注，但储存的加速术/飞行术需专注->自动施法后自动展开专注？已有专注怎么办？强制放弃旧专注？
**建议：** (A) 自动施法专注法术仍需专注并遵循正常专注规则；(B) 储存法术无视自身专注要求。推荐(A)并明确文档。

### D15. [HIGH] 无战斗胜利/失败/逃跑状态转换规范
- 胜利 -> 矩阵武装/触发中？销毁。`charged` 保留。MP保留。
- 团灭 -> 保留重试还是清除？
- 逃跑 -> 武装矩阵保留。触发中的矩阵->`charged` 写回了吗？

**建议：** 添加生命周期表：

| 事件 | 战斗本地实例 | 持久 `charged` | MP封存 |
|---|---|---|---|
| 胜利 | 销毁 | 不变 | 不变 |
| 失败(重试) | 销毁 | 不变 | 不变 |
| 失败(永久死亡) | 销毁 | 清除 | 释放 |
| 逃跑 | 销毁 | 原样写回 | 不变 |

### D16. [HIGH] 法术重命名破坏全部存档（同B11）
**建议：** 法术ID别名表，或冻结初始发布后的法术ID。

### D17. [HIGH] 已存矩阵的法术定义在语义上可能变为非法
`minContingencySkillLevel` 从4升到7 -> 已充能矩阵（技能等级4）重新加载后仍有效？
**建议：** 添加 `validate_saved_setup()` 函数，将过期矩阵标记为 `charged=false` 并向用户说明原因。

### D18. [MEDIUM] `parameterBindings` schema 未定义
`{ "element": "thunder" }` 的合法键值未指定。
**建议：** 定义每个法术类别的 `parameterBindings` schema，或委托给单独的法术参数绑定文档。

### D19. [MEDIUM] `timing` 枚举无详尽定义
文档中使用了 `after_event`、`before_damage_resolved`、`after_movement`、`after_status_applied`，但无完整集合。
**建议：** 定义完整 `ContingencyTiming` 枚举并指定各触发器类型可用的时序值。

### D20. [MEDIUM] 非战斗触发场景未涉及
关卡探索模式中，陷阱伤害->`hp_below_percent` 会触发吗？环境异常呢？
**建议：** 明确声明触发仅在战斗中工作，关卡层不触发应急事件。

### D21. [MEDIUM] "世界步进"单位从未定义
`chargedAtWorldStep`/`expiresAtWorldStep` 无上下文。
**建议：** 明确定义世界步进或引用其定义文档。若为关卡层时钟，注明其不前进于战斗。

### D22. [MEDIUM] 施法者变形/转化后充能
法师变形成巨魔后矩阵是否仍属于他？目标解析器 `self` 解析为当前形态？本体被放逐？
**建议：** 声明矩阵跟随 `ownerMemberId` 不论形态，`self` 解析为当前单位状态。若被放逐=当地不存在->矩阵不触发且保留充能。

### D23. [MEDIUM] `status_applied` 可能被自身自动施法重新触发
规则13.1禁止触发"另一个"矩阵，但未禁止触发自身矩阵的*其他*触发条件。
**建议：** 明确："应急矩阵的自动施法绝不能重新触发自身矩阵；即使自动施法产生了匹配该矩阵触发条件的事件也不行。"

### D24. [MEDIUM] 敌对使用矩阵时 `maxActivePerCaster: 1` 未区分
对玩家1个矩阵没问题，敌方AI应放宽限制？
**建议：** 提示是否需要为敌方单独配置限制。

### D25. [LOW] 永久死亡时清理已充能矩阵
施法者永久死亡->MP释放目标不存在。矩阵清理规则未定义。
**建议：** 成员永久死亡时释放 `reservedMpMax` 并清除设置。

### D26. [LOW] 多个单位同时进入范围 -> `enemy_enter_radius` 触发多次
3个敌人传送进入范围->触发3次。
**建议：** 添加每轮冷却或每触发类型去重窗口。

### D27. [LOW] 召唤生物与应急术交互
召唤生物伤害能否触发施法者矩阵？触发器用 `subject: "owner"` 所以大概不会。仍需澄清。
**建议：** 声明全部触发器仅作用于 `owner`/`caster`，召唤物/盟友非触发器主体。

---

## 汇总统计

| 视角 | CRITICAL | HIGH | MEDIUM | LOW |
|---|---|---|---|---|
| A. 系统设计与平衡 | 3 | 5 | 5 | 2 |
| B. 工程实现与数据结构 | 9 | 3 | 9 | 3 |
| C. 玩家体验与UX | 5 | 8 | 8 | 2 |
| D. 系统整合与完整性 | 9 | 8 | 8 | 3 |

**总计: 26 CRITICAL, 24 HIGH, 30 MEDIUM, 10 LOW**

---

## 优先修复建议（Top 10）

1. **D1/A12 — 成本公式缺失数值**（CRITICAL x4次命中）
   必须提供所有税项的完整数值、`基础封存`、`技能等级系数`。

2. **B1/B2 — 命名约定三重冲突**（CRITICAL x2次命中）
   统一为 `snake_case`，修复 `spellId`/`skill_id`/`source_skill_id` 混淆。

3. **B8/D8/D9 — 触发器冲突与排序**（CRITICAL x3次命中）
   定义触发器优先级、同事件多矩阵行为、多施法者排序。

4. **B6/D11 — 状态机漏洞**（CRITICAL x2次命中）
   补充 `triggering`/`releasing`中的中断处理、`suppressed` 完整定义、过期转换、手动清除状态。

5. **B9/B10/D9 — 存档/读档触发器完整性**（CRITICAL x2次命中）
   原子写入 `charged=false` 或持久化 `consumed_charge`，处理顺序释放队列。

6. **C2/C3 — 材料非返还 + 编辑清充能**（CRITICAL x4次命中）
   重做退款规则：至少部分退款、编辑清充能前有确认/预览。

7. **A1/A2/A3 — 核心平衡漏洞**（CRITICAL x3次命中）
   休息绕过MP封存（充能时扣当前MP）、同步释放=免费3回合动作（加debuff）、致命触发=无敌（加每休息限制）。

8. **C1/C4 — 零用户反馈**（CRITICAL x2次命中）
   强制视觉/音效/日志反馈；解释世界步进和过期。

9. **D5 — 缺失的触发类型定义**
   完成 `incoming_damage_percent`、`targeted_by_spell` 等未定义触发器的schema。

10. **C5 — 零引导**
    教程任务、预构建矩阵、"简单模式"、渐进式功能开放。

---

# 附录：代码架构可行性审查

本节基于实际代码库（`E:\game\magic`）对第21节已确认裁决进行工程可行性交叉验证。每条给出裁决要点、代码现状、以及裁决与实现之间的缺口严重程度。

审查时间基准：GameSession.SAVE_VERSION=7, PartyState.version=3, PartyMemberState.TO_DICT_FIELDS=44项，无任何 contingency 相关代码。

---

## 致命缺口（裁决不可落地的结构性障碍）

### F1. MP封存机制在属性系统中无挂载点

**涉及裁决：** A1, C6, D6  
**裁决要求：** `effective_mp_max = max(raw_mp_max - total_reserved_mp_max, 0)`；充能时 clamp 当前 MP；休息上限受封存约束。

**代码现状：**
- `AttributeService._build_snapshot()`（`attribute_service.gd:181`）仅从 `UnitBaseAttributes.custom_stats["mp_max"]` + 各类修正器计算，无封存减法。
- `AttributeSnapshot`（`attribute_snapshot.gd`）是纯值容器，`get_value(MP_MAX)` 直接返回字典值，无派生计算。
- `BattleUnitState`（`battle_unit_state.gd`）无 `reserved_mp` / `effective_mp_max` 字段。
- `battle_unit_factory.gd:253-258` 用 `snapshot.get_value(MP_MAX)` 作为上限 clamp 当前 MP。
- `character_management_module.gd:1196` 战后回写 MP 同样用原始 `snapshot.get_value(MP_MAX)` 做上限。
- 全仓库搜索 `reserved_mp` / `effective_mp` / `total_reserved_mp` — **零匹配**。

**缺口：** 需要至少在三层注入封存逻辑（AttributeService计算层、BattleUnitState存储层、commit_battle_resources回写层），且必须与现有修正器体系一致，避免与种族/职业/装备/被动修正产生双重减扣。裁决声称"raw mp_max 不被改写"，但 attribute_snapshot 内部无此区分能力。

**严重程度：CRITICAL**

---

### F2. 技能管线无任何 cost-bypass 路径

**涉及裁决：** D7, B21, B22, D23  
**裁决要求：** 自动施法绕过言语/姿势/材料成分、不扣 AP/MP/冷却/熟练度；爆发释放快速顺序结算；`AutoCastRequest.can_trigger_other_contingencies = false`。

**代码现状：**
- `battle_skill_turn_resolver.gd:170-210`（施法阻塞检查）：无条件检查 AP 是否足够、资源是否满足、冷却是否就绪、沉默/眩晕等状态是否阻止。无任何 `ignore_*` 参数。
- `battle_skill_turn_resolver.gd:250-279`（消耗扣除）：无条件扣 AP/MP/stamina/aura/cooldown。无法跳过。
- `battle_skill_execution_orchestrator.gd:371` (`_handle_skill_command`)：入口参数只有 `(active_unit, command, batch)`，不接受 ignore flags。
- 全仓库搜索 `AutoCastRequest` / `ignore_action_cost` / `ignore_resource_cost` / `ignore_cooldown` / `ignore_mastery_gain` — **零匹配**。

**缺口：** 要实现裁决，必须在技能管线中开辟第二条执行路径或注入 bypass 参数。这不是"加一个字段"级别的工作——影响 orchestrator → turn_resolver → cost 扣除整条调用链。`AutoCastRequest` 概念完全不存在。

**严重程度：CRITICAL**

---

### F3. 战斗事件系统不存在

**涉及裁决：** B8, B23, D8, D9, D19, D26  
**裁决要求：** 按 trigger_type 建 active matrix 索引；事件只扫描相关矩阵；冻结 source_event_facts；稳定 damage_event_id；多 owner 按 frozen facts 排队；timing 枚举（`after_battle_confirmed` / `before_damage_resolved` 等）。

**代码现状：**
- 全仓库搜索 `BattleEventDispatcher` / `battle_event_dispatcher` — **零匹配**。该文件不存在。
- 全仓库搜索 `CombatStartedEvent` / `DamageIncomingEvent` / `HpChangedEvent` / `StatusAppliedEvent` — **零匹配**。无任何战斗事件类型定义。
- `BattleRuntimeModule.advance()` 和 `issue_command()` 是直接同步函数调用，不经过事件总线。
- `damage_event_id` 概念不存在——伤害结算无稳定事件标识。
- `source_event_facts` 概念不存在。
- `timing` 枚举中列出的值（`after_battle_confirmed` / `before_spell_effect_resolved` / `before_damage_resolved` / `after_hp_changed` / `after_status_applied` / `after_position_changed` / `owner_turn_started`）— **全部不存在**。

**缺口：** 整个触发匹配架构依赖事件驱动，但代码库中战斗逻辑是同步调用链。要实现裁决，要么在现有调用链每个关键点插入触发检查，要么新建事件分发层。无论哪种方式，工作量都等同于引入一个新子系统。

**严重程度：CRITICAL**

---

### F4. 战后回写管线无 contingency 入口

**涉及裁决：** B9, D15  
**裁决要求：** 已进入释放流程的 setup 在战斗胜利/正常提交/逃跑提交时回写 `charged=false` 并释放封存；失败读档/重试不写回；永久死亡提交清除全部 charged setup。

**代码现状：**
- `end_battle()`（`battle_runtime_module.gd:940-960`）仅调用 `commit_battle_resources()` 和 `commit_battle_death()`。
- `commit_battle_resources()`（`character_management_module.gd:1190-1198`）仅写入 `current_hp, current_mp, current_aura, is_dead = false`。
- `character_management_module` 无任何 contingency 相关方法。
- `PartyMemberState` 无 `contingency_matrix_setups` 字段，回写无目标。
- 全仓库搜索 `commit_contingency` / `charged` / `reserved_mp_max` — **零匹配**。

**缺口：** 需要在 `end_battle` → `character_management_module` 之间新增一条完整的 contingency 数据通道——携带 consumed setup 列表、charged 状态、MP 释放指令。还要处理"提交型结算"的各种分支（胜利/逃跑/永久死亡/失败重试）。

**严重程度：CRITICAL**

---

### F5. PartyMemberState 字段添加与 exact-fields 验证冲突

**涉及裁决：** D6, D20  
**裁决要求：** `contingency_matrix_setups: Array[ContingencyMatrixSetupState]` 纳入 `PartyMemberState.TO_DICT_FIELDS` 并参与 strict exact `to_dict()`/`from_dict()`。

**代码现状：**
- `PartyMemberState.TO_DICT_FIELDS`（`party_member_state.gd:12-45`）当前精确 44 个字段。
- `from_dict()` 第188行：`if not _has_exact_fields(data, TO_DICT_FIELDS)` → 返回 `null`。多一个或少一个字段都拒绝。
- `PartyState.from_dict()` 第412行：`if int(version_variant) != 3` → 返回 `null`。
- 添加 `contingency_matrix_setups` 会使 PartyMemberState 字段数从 44 变为 45+（取决于新增子状态类有多少字段进入 to_dict）。
- PartyState 版本从 3 升 4 后，所有旧存档 100% 加载失败（这是设计意图，但需要明确承认影响）。

**缺口：** 这不是"缺口"而是设计选择。但需注意连锁影响：
1. 任何用到 `PartyMemberState.from_dict()` 的测试固件（大量 regression 测试）需要全部重新生成。
2. `PartyState.from_dict()` 在加载 member_states 时为每个成员调用 `PartyMemberState.from_dict()`，版本检查在外层和内层各执行一次。
3. 不存在"灰度升级"或"存量兼容"的中间态——一旦代码合入，所有旧 save 和测试数据即全部失效。

**严重程度：CRITICAL**（非工程障碍，但属于操作风险）

---

## 高危缺口（需要大量新造基础设施）

### F6. 战斗单位工厂无 contingency 数据通路

**涉及裁决：** D6, D22  
**裁决要求：** 战斗开始时从 charged setup 构建 battle-local ContingencyInstance。

**代码现状：**
- `build_ally_units()`（`battle_unit_factory.gd:40-61`）从 `PartyMemberState` 读取身份/属性/资源/技能/装备。不读取任何 contingency 数据。
- `_build_runtime_ally_unit()`（`battle_unit_factory.gd:237-283`）构建 `BattleUnitState` 时仅设置资源、技能、属性快照。
- 无任何 battle-local contingency instance 创建逻辑。

**缺口：** 需要新增从 `PartyMemberState.contingency_matrix_setups` 到 `BattleUnitState` / battle-local system 的数据桥接。至少涉及 factory、BattleUnitState、BattleContingencySystem 三个未创建文件。

**严重程度：HIGH**

---

### F7. 六个新服务/状态类文件全部为空

**涉及裁决：** D6（全部显式状态类）  
**裁决涉及的新文件：**

| 文件 | 状态 |
|---|---|
| `scripts/player/progression/contingency_matrix_setup_state.gd` | 不存在 |
| `scripts/player/progression/contingency_trigger_state.gd` | 不存在 |
| `scripts/player/progression/contingency_target_resolver_state.gd` | 不存在 |
| `scripts/player/progression/contingency_stored_spell_entry_state.gd` | 不存在 |
| `scripts/systems/progression/party_contingency_setup_service.gd` | 不存在 |
| `scripts/systems/battle/runtime/battle_contingency_system.gd` | 不存在 |
| `scripts/systems/battle/runtime/battle_event_dispatcher.gd` | 不存在 |
| `scripts/systems/battle/runtime/auto_cast_request.gd` | 不存在 |

每个都需要 `class_name`、`TO_DICT_FIELDS`、`to_dict()`/`from_dict()` exact-fields 校验，与其他状态类一致。

**严重程度：HIGH**

---

### F8. SaveSerializer 版本升 8 的连锁影响

**涉及裁决：** D20  
**裁决要求：** root save version 升到 8，PartyState.version 升到 4。

**代码现状：**
- `GameSession.SAVE_VERSION = 7`（`game_session.gd:41`）
- `SaveSerializer._save_version = 7`（`save_serializer.gd:179/189`，构造器默认值）
- `SaveSerializer` 第262行：`if save_version != _save_version: return {"error": ERR_INVALID_DATA}`
- 全仓库约有 15+ 个 regression 测试包含硬编码的 save version 检查和 payload 构造。

**缺口：** 修改 SAVE_VERSION 会连锁影响：
1. `SaveSerializer` 中的所有版本检查分支
2. `GameSession` 中引用 `SAVE_VERSION` 的代码
3. 所有 regression 测试中的 save payload 固件
4. 任何外部工具/脚本依赖 save version 7 的假设

**严重程度：HIGH**

---

## 中等缺口（裁决与现有机制有摩擦但可解决）

### F9. 多个现有枚举/命名与裁决冲突

**涉及裁决：** B1, C15, C23, C24  
**裁决要求：** 统一 `snake_case`；"同时释放"改"爆发释放"；战斗内状态用"待命/被压制/释放中/已耗尽"；代码内部保持 `contingency/matrix` 命名。

**代码现状：**
- 代码库已统一 `snake_case`——这个无冲突。文档中残留的 `camelCase` 示例（第5节JSON）与新裁决指示一致，需要更新正文示例。
- "爆发释放"是新术语，未在任何现有代码中冲突。
- 战斗内状态显示名不需要进代码，只需要 UI 映射层。

**缺口：** 主要是文档本身残留的旧术语（`charged_at_world_step` 的旧示例仍用 camelCase）。文档正文中的旧 JSON 示例需要与裁决对齐。

**严重程度：MEDIUM**

---

### F10. report_entries 通道存在但从未使用

**涉及裁决：** C4, B24, 第19节整节  
**裁决要求：** 结构化 `report_entries` 写入 `BattleEventBatch.report_entries`。

**代码现状：**
- `BattleEventBatch` 已有 `report_entries: Array[Dictionary] = []`（`battle_event_batch.gd:21`）
- 当前无任何代码向 `report_entries` 写入数据。
- `BattleEventBatch.clear()` 第34行会清空它。
- `GameSession.log_event()` / `GameLogService` 存在，可用于世界层日志。

**缺口：** 基础设施存在但从未使用。只需在 contingency 系统正确写入 `report_entries`（entry_type + decision + reason_id + 条件字段），不需要新建字段或类。

**严重程度：MEDIUM**（基础设施就绪，只需写入端）

---

### F11. battle_save_lock 与 deferred save 的 charged 一致性

**涉及裁决：** B9, C12  
**裁决要求：** 不允许战斗中存档，因此不做战斗中触发进度持久化。

**代码现状：**
- `GameSession._battle_save_lock_enabled` 存在且工作。
- `save_game_state()` 在锁定时标记 `_battle_save_dirty = true` 而不是实际写入。
- 战斗结束后锁释放，deferred save 执行。
- 如果 contingency 在战后回写 `charged=false`，deferred save 会正确反映最终状态。

**缺口：** 需要验证的边界条件：
1. 如果战斗崩溃/异常退出，`_battle_save_lock_enabled` 仍为 `true`，deferred save 永不执行 → `charged` 保留为战前值（正确行为）。
2. 如果 `end_battle()` 被调用但提交失败（异常），charged 写回可能未执行但锁已释放 → 需要确认异常处理路径。
3. `_runtime_save_dirty` 和 `_battle_save_dirty` 是两个独立标志——需要确认战后 flush 时哪个标志触发 contingency 状态持久化。

**严重程度：MEDIUM**

---

### F12. 战前充能的 MP clamp 无服务入口

**涉及裁决：** A1, C6  
**裁决要求：** 充能时 clamp 当前 MP 到封存后的上限。

**代码现状：**
- 充能是战斗外操作（世界层）。
- `PartyMemberState.current_mp` 的写入路径只有：
  - 战后 `commit_battle_resources()`（唯一规范的写入点）
  - 直接字段赋值（无服务层保护）
- 战斗外 MP 恢复机制（旅店/物品/休息）通过 `character_management_module` 的 `restore_resources()` 等方法——这些方法以 attribute snapshot 的满值作为恢复目标，无 `effective_mp_max` 概念。

**缺口：** 充能操作需要一个世界层的服务入口来安全修改 `PartyMemberState.current_mp`。但当前 `PartyContingencySetupService` 文件不存在，且现有的资源恢复路径不知道封存上限。

**严重程度：MEDIUM**

---

## 低危缺口（工程细节或V1可推迟）

### F13. `charged_at_world_step` 存 -1 但 world_step 是 non-negative int

**涉及裁决：** D21, D12  
**裁决要求：** `charged_at_world_step` 直接引用 `world_data["world_step"]`，非负 int，`charged=false` 时为 `-1`。

**代码现状：**
- `world_data["world_step"]` 的验证在 `get_world_data_step_validation_error()` 中要求 `int >= 0`。
- 使用 `-1` 作为"未充能"标记是哨兵值模式，与 world_step 自身约束不一致（world_step 本身不可能为 -1）。
- 但如果 `charged_at_world_step` 存储在 `PartyMemberState` 中而不是 `world_data` 中，这个冲突不成立——它只是在 save payload 中的另一个字段。

**缺口：** 需要确保 type validation 允许 -1 作为合法值。当前 PartyMemberState 的 `from_dict()` 对所有 int 字段做范围校验（如 `body_size >= 1 and body_size <= 6`），如果 `charged_at_world_step` 加了 `>= 0` 的校验就会冲突。

**严重程度：LOW**

---

### F14. D14（无专注机制）是对现有代码的正确判断

**涉及裁决：** D14  
**裁决要求：** 当前项目没有专注机制，V1 不实现。

**代码验证：** 全仓库搜索 `concentration` / `专注` — 仅在 `dispel_magic_logic.gd` 和 `status_effect_scripts/` 中有驱散相关引用，无专注系统。

**确认：** 裁决正确。

---

### F15. D24（敌人矩阵限制）需要敌方AI框架感知

**涉及裁决：** D24  
**裁决要求：** `max_active_per_caster = 1` 不按阵营区分；敌人默认不使用持久充能矩阵。

**代码现状：**
- 敌方 AI 通过 `enemy_ai_service` 决策，使用 `EnemyAIProfile` 中的行为配置。
- 如果未来敌人使用矩阵，需要在 `enemy template` 或 `special profile` 中新增配置字段。
- 当前 `EnemyAIProfile` 无 contingency 相关内容。

**缺口：** V1 无影响。未来扩展时需要新增 AI 决策模块。

**严重程度：LOW**

---

## 汇总：裁决与实现就绪度矩阵

| 裁决编号 | 裁决描述 | 实现就绪度 | 新造量 |
|---|---|---|---|
| D6 (PartyMemberState字段) | 新增 contingency_matrix_setups | 需新造字段+子状态类+to_dict/from_dict | 8个新文件 |
| D7 (自动施法绕过限制) | 技能管线 bypass | 需贯穿 orchestrator→turn_resolver 的新路径 | 修改4个现有文件 |
| F3关联 (事件系统) | 触发匹配 | 全新子系统 | 1-2个新文件 + 分散埋点 |
| F4关联 (战后回写) | charged写回 | 新数据通道 | 修改2个现有文件 |
| D8/D9 (伤害事件ID/frozen facts) | 触发器冲突解决 | 新概念植入伤害/移动结算 | 修改多个现有函数 |
| D19 (timing枚举) | 固定触发点 | 新增枚举 + 各触发点的钩子调用 | 1个枚举文件 + 多处钩子 |
| A1/C6 (MP封存) | effective_mp_max | 贯穿属性→单位→战后三层的链 | 修改3-4个现有文件 |
| D15 (生命周期表) | 提交型结算 | end_battle 扩展 | 修改1个文件 |
| D3 (容量公式) | matrix_capacity = 3 + skill_level | 纯数据公式 | 新常量/函数 |
| D11 (suppressed状态机) | 压制/恢复 | BattleContingencySystem 内部逻辑 | 新文件内实现 |
| B23 (按trigger_type索引) | 性能优化 | 在 BattleContingencySystem 内部 | 新文件内实现 |
| C4 (日志反馈) | report_entries 写入 | 使用现有 BattleEventBatch.report_entries | 新文件内实现 |

**结论：** 裁决的合理性（作为设计决策）与代码库的可实现性之间存在显著断层。最关键的三个缺口——MP封存层（F1）、技能 bypass（F2）、事件系统（F3）——都需要在现有架构中开"竖井"而非简单扩展。建议在开工前先完成这三项的 spike 实现以验证可行性。

---

# 附录：第二轮对抗性审查（E节裁决 × 代码验证）

本节基于对 `battle_damage_resolver.gd`、`battle_skill_execution_orchestrator.gd`、`attribute_snapshot.gd`、`battle_unit_factory.gd`、`character_management_module.gd` 等核心文件的逐行审查，对第21节E组裁决进行工程可行性二次对抗。

审查结论：F组15项裁决均 **可落地**，但存在7个新发现的细节缺口需要补充裁决。

---

## 新缺口 G1：`before_damage_resolved` hook 需支持伤害取消/修正

**涉及裁决：** F3, A3, D8  
**裁决要求：** `fatal_damage_incoming` 触发后位移可以取消当前伤害；`before_damage_resolved` hook 必须能写入取消或修改伤害的 per-owner 修正。

**代码现状：**
- `_apply_damage_to_target()`（`battle_damage_resolver.gd:2174`）计算 `normalized_damage` 后直接进入护盾吸收（2184行），无 hook 点，无取消机制。
- 函数签名不接收外部 cancellation callback。
- `normalized_damage` 一旦算出就必然至少推进到护盾吸收阶段。

**缺口：** hook 若只是"观察者"（读取伤害但不修改），则 A3 的"位移取消伤害"无法实现。需要 hook 能返回 `{cancel: true}` 或修改 `normalized_damage`，且 `_apply_damage_to_target` 的调用链（`_resolve_damage_outcome` → `resolve_effects`）需要能消费这个返回值并中断后续处理。

**严重程度：HIGH**（不可推迟——致命伤害触发是最核心的卖点之一）

---

## 新缺口 G2：`commit_battle_resources` 重建 snapshot 会丢失封存释放信息

**涉及裁决：** F4, D6, D15  
**裁决要求：** 战后提交时先释放封存再提交资源 clamp，`effective_mp_max` 影响 clamp 上限。

**代码现状：**
- `commit_battle_resources()`（`character_management_module.gd:1194-1201`）在 clamp 前调用 `get_member_attribute_snapshot(member_id)` 重新构建 snapshot。
- 新 snapshot 基于当前 `PartyMemberState` 的持久字段（progression/race/equipment）计算 `MP_MAX`。
- 如果在 `commit_battle_resources` 之前将 `reserved_mp_max` 归零（释放封存），新 snapshot 仍然从 `AttributeSourceContext` 获取 `reserved_mp_max`——如果 `reserved_mp_max` 的来源是 `PartyMemberState.contingency_matrix_setups`，则需要在调用 snapshot 构建前先写入持久层。

**缺口：** 不存在"先释放封存到持久层，再让 snapshot 感知到释放"的原子操作。必须明确定义释放的顺序：
1. 修改 `PartyMemberState.contingency_matrix_setups[i].charged = false` + `reserved_mp_max = 0`
2. **然后**调用 `get_member_attribute_snapshot()`（此时 snapshot 会通过 `AttributeSourceContext` 读取到新的 `reserved_mp_max=0`）
3. **然后**用 `effective_mp_max`（此时等于 `raw_mp_max`）做 clamp

当前 `end_battle` 没有这个分步逻辑。

**严重程度：HIGH**

---

## 新缺口 G3：`end_battle` 无 contingency 状态的持久化写入路径

**涉及裁决：** F4, D15  
**裁决要求：** 战后提交时把 consumed setup 写成 `charged=false`。

**代码现状：**
- `end_battle()` 仅调用 `commit_battle_resources()` 和 `commit_battle_death()`。
- `character_management_module` 无 `commit_contingency_state()` 或类似方法。
- `PartyMemberState.contingency_matrix_setups` 字段尚不存在，写入目标不存在。

**缺口：** 需要新增一个方法（如 `commit_contingency_charges()`），在 `end_battle` 的资源提交循环中（第956-970行之间）调用，直接修改 `PartyMemberState` 的 contingency 字段。这与现有的 `commit_battle_resources` 模式一致（直接字段写入），工程上无阻碍，但需要明确定义方法签名和错误处理。

**严重程度：MEDIUM**（工作量明确，无架构障碍）

---

## 新缺口 G4：自动施法路径未定义命中结算策略

**涉及裁决：** F2, D7  
**裁决要求：** `execute_auto_cast()` 保留"内容查找、目标、抗性、护盾、豁免和效果结算"。

**代码现状：**
- 技能管线有两条命中路径：
  - **普通路径**：通过 `battle_hit_resolver` 掷命中骰，可能 miss/graze/hit/crit
  - **必中路径**：`battle_skill_resolution_rules.is_force_hit_no_crit_skill()` 用于特殊技能（如 `black_contract_push`）
- 自动施法走哪条路径？裁决未说明。

**缺口：** 如果走普通路径，自动施法的 buff（镜影术、石肤术）可能因命中失败而浪费，违背"战前保险"的定位。自目标法术（`target_resolver.self`）自然不需要命中检查，但 `owner_centered_area` 的雷鸣波、`trigger_source` 的反击法术需要。建议为自动施法明确枚举命中策略：self-target 跳过命中检查，hostile-target 走普通命中。

**严重程度：MEDIUM**

---

## 新缺口 G5：顺序释放与 `owner_turn_started` hook 的执行顺序冲突

**涉及裁决：** B22, F3  
**裁决要求：** B22 说"后续法术在 owner 每次行动开始前结算"；F3 说 `owner_turn_started` hook 在行动开始前触发。

**代码现状：**
- `_activate_next_ready_unit()`（`battle_timeline_driver.gd:334-388`）中，turn_start 是一个线性序列：phase 切换 → 重置 per-turn 标志 → trait hook → turn timer → action point 分配。
- 当前序列无 contingency 插入点。

**缺口：** 如果顺序释放的队列推进和新的触发评估都在 `owner_turn_started` 点发生：
- 顺序释放先执行 → 可能改变 HP/状态 → 新触发条件可能匹配 → 但矩阵已在 releasing 状态无法再次触发 → OK
- 但若顺序释放最后一个法术后矩阵变为 `depleted`，同一 turn_start 点不应再评估新触发 → 需要状态机保证释放完成后的同帧不再重新评估

**严重程度：LOW**（状态机设计问题，非代码障碍）

---

## 新缺口 G6：跨域事务（仓库扣材料 + PartyMemberState 写充能）无原子保障

**涉及裁决：** F12  
**裁决要求：** 充能事务先拒绝战斗态，再校验技能/内容/材料/封存，候选状态写 charged/material/reserved，扣宝石，重算 effective max 并 clamp current MP，最后提交；任一步失败不产生部分 mutation。

**代码现状：**
- 仓库操作通过 `party_warehouse_service.remove_item()` → `commit_batch_swap()` 原子化。
- PartyMemberState 字段写入是直接赋值，无事务封装。
- 两者之间无协调器。`submit_item_objective`（`character_management_module.gd:400-470`）采用手动快照回滚模式：先 snapshot `warehouse_state_before`，调用 batch swap，若后续失败则恢复快照。

**缺口：** 需要一个充能协调器，按以下顺序执行：
1. Snapshot warehouse + PartyMemberState contingency 状态
2. 校验全部前置条件
3. 扣材料（commit_batch_swap）
4. 写 PartyMemberState（charged=true 等）
5. Clamp current_mp
6. 任一步失败 → 回滚 snapshot

现有代码中无现成的跨域事务框架，但快照回滚模式可复用。需要明确回滚时是否也需要 restore warehouse snapshot。

**严重程度：MEDIUM**（模式已知，实现量明确）

---

## 新缺口 G7：多 effect 法术的 `damage_event_id` 粒度问题

**涉及裁决：** D8, D9  
**裁决要求：** 同一伤害事件携带稳定 `damage_event_id`；同一 owner 同一 `damage_event_id` 最多一个矩阵进入释放流程。

**代码现状：**
- `resolve_effects()`（`battle_damage_resolver.gd:258`）按 effect_def 逐个循环处理。一个法术可有多个 effect（damage + status + forced_move）。
- 每个 effect 独立调用 `_resolve_damage_outcome()` 和 `_apply_damage_to_target()`。
- 当前 damage outcome dict 无 event_id 字段。

**缺口：** 如果 `damage_event_id` 在单个 effect 级别生成，多 effect 法术会产生多个 ID。如果 contingency 的触发评估只应在"整次施法"级别发生一次，则 `damage_event_id` 必须在 orchestrator 层生成并下传，而非在 resolver 层。这与 F3 的 `before_spell_effect_resolved` hook 存在同一问题——该 hook 会在每个 effect 上触发一次。

**严重程度：MEDIUM**

---

## 汇总：第二轮缺口

| 编号 | 严重度 | 问题 | 需要补充裁决的方向 |
|---|---|---|---|
| G1 | HIGH | `before_damage_resolved` hook 需支持伤害取消，`_apply_damage_to_target` 当前无此机制 | hook 返回值规范：`{cancel: bool, modified_damage: int}` |
| G2 | HIGH | `commit_battle_resources` 重建 snapshot，无法感知战前释放的封存 | 明确三步顺序：写持久层 → 重建 snapshot → clamp |
| G3 | MEDIUM | `end_battle` 无 contingency 写入路径 | 新增 `commit_contingency_charges()` 方法签名 |
| G4 | MEDIUM | 自动施法未定义命中策略（self 跳过？hostile 正常掷骰？） | 按 target_resolver 类型明确定义命中策略 |
| G5 | LOW | 顺序释放与 turn_start hook 的执行顺序未定义 | 明确：顺序释放先执行，释放结束后不重新评估同帧触发 |
| G6 | MEDIUM | 跨域事务（仓库+PartyMemberState）无原子保障框架 | 指定快照回滚模式或二阶段提交模式 |
| G7 | MEDIUM | 多 effect 法术的 event_id 粒度 | 明确 ID 在 orchestrator 层生成，每法术一个，非每 effect 一个 |
