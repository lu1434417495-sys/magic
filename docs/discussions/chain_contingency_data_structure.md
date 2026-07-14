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
8. V1 发布门槛是完整触发器集合、战斗外 UI、headless 命令、真实 `mage_chain_contingency` 技能资源、quantity-aware 仓库 API、战斗结算失败回滚一起完成；内部可按依赖顺序实施，但不切一个“非伤害触发限定版”上线。
9. 已存 setup 的内容契约在读档流程中校验；content catalog 加载后发现技能、resolver、binding 或允许储存规则非法时，本次读档视为异常，不延后到进战时静默失败。
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

运行时实例来自 `PartyMemberState` 中已充能的 `ContingencySetup`。它只存在于战斗本地系统中，用于监听事件、去重、防递归和排队释放；不要把 `armed / triggering / releasing / depleted` 这类战斗临时状态写回人物预设。

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
triggering  已创建 release_context，正在进入自动释放
releasing   连续释放中
depleted    已消耗，等待战后提交或本场移除
```

V1 中普通解除魔法对连锁应急术无效。压制不是主状态，而是战斗本地覆盖层：

```text
suppressed: bool
suppressed_reason: StringName
resume_state: armed / releasing
```

压制只由反魔法领域、专门压制效果，或未来高阶破阵/裂解的压制模式造成。压制不释放 `reserved_mp_max`，不返还特殊宝石，不改变持久 `charged=true`。战斗外手动清除和未来摧毁效果也不进入 battle-local 主状态；它们直接改变持久 setup 的 charged / reserved / material 字段，或作为未来专门规则处理。

压制状态机：

```text
armed + suppressed=true：
    暂时不能响应新事件；压制结束后恢复 armed。

queued / 未进入释放流程：
    轮到该矩阵时若被压制，则 live gate 失败，不消耗充能。

burst_release 已进入释放流程：
    不被后来的压制回溯中断；已经开始的 burst 继续按快速顺序结算。

sequential_release 的 releasing + suppressed=true：
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
```

V1 不允许 value 是 Dictionary，即使是 flat Dictionary 也不进入首版。需要多字段模式参数时，应拆成多个显式 binding key，或等未来为该技能新增 typed 子结构和专门校验器后再开放。

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

连续释放的后续队列只在内部 `owner_turn_started` 调度点推进，`owner_turn_started` 不作为 V1 玩家可选触发器。同一个 `owner_turn_started` tick 内，先推进已有 `releasing` 队列，再进入普通回合开始流程；本 tick 不重新评估该矩阵的新触发。

推荐执行顺序：

```text
单位成为 active unit
-> BattleContingencySystem.OwnerTurnStarted(owner)
    -> 若该 owner 有 releasing 矩阵，检查 owner 存活/在场/未被压制
    -> 释放 remaining_queue 的下一个 stored spell
    -> 队列清空则 state = depleted
    -> 本 tick 不再让该矩阵参与 trigger evaluation
-> trait turn_start
-> turn timer / turn_start statuses
-> AP / move points 分配
-> 玩家 / AI 输入
```

若 owner 被反魔法或专门压制，队列暂停，不消耗新的预存法术；若 owner 死亡或离场，队列中止，已进入 `release_context` 的矩阵不返还。

---

## 11. 战斗同步 Hook 系统

当前项目战斗逻辑以同步调用链为主。V1 不新增通用事件总线，也不新增 `BattleEventDispatcher`；连锁应急术通过 `BattleContingencySystem` 暴露固定同步 hook，由现有战斗流程在关键结算点显式调用。

### 固定 Hook 点

```text
OnBattleConfirmed
BeforeSpellEffectResolved
BeforeDamageResolved
AfterHpChanged
AfterStatusApplied
AfterPositionChanged
OwnerTurnStarted
```

这些 hook 是战斗运行时内部契约，不是玩家可配置事件。`before_damage_resolved` 不是只读观察点，必须能向当前伤害链写入 per-owner 修正，例如取消 owner 的本次伤害、修改伤害或标记本次伤害已被矩阵处理；闪现取消伤害就通过这个 hook 完成。

`before_damage_resolved` 对所有即将结算的伤害调用，包括预计会被护盾完全吸收的伤害。这样闪现、位移或其他防护可以在护盾吸收前取消整次伤害。但 V1 的 `incoming_damage_percent` / `fatal_damage_incoming` 只按 `projected_hp_damage_after_shield` 判定；若预计 HP 伤害为 0，则这两个触发器不触发。未来若需要响应护盾破裂或护盾损耗，应新增 `shield_break_incoming` / `shield_damage_percent` 等触发器，不混入 HP 保命触发。

因此，damage resolver 在调用 `before_damage_resolved` 前，必须先做一次无副作用伤害投影。投影只计算本次伤害预计会扣多少护盾、多少 HP、是否致死、是否破盾，不修改 `target_unit.current_shield_hp`、`target_unit.current_hp` 或任何状态。hook 读取这份 projected facts 决定是否触发矩阵。若 hook 返回 `modified_resolved_damage`，resolver 必须基于修改后的伤害重新投影，再进入实际护盾吸收和 HP 写入。

hook 输入至少包含：

```text
source_event_id / action_instance_id
damage_event_id                    # 仅当当前 hook 是某个 target 上的单次 damage effect 时存在
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

若 `cancel_damage=true`，resolver 在扣盾扣血前直接返回零伤害结果：本次伤害对该 owner 不扣护盾、不扣 HP，也不再产生该 `damage_event_id` 对应的 `hp_below_percent` crossing；已经进入 `release_context` 的矩阵仍消耗充能。取消当前 damage effect 不影响 `resolve_effects` 继续处理同一技能的后续 effect。若只是修改伤害，则使用修改后的伤害继续进入护盾吸收和 HP 写入，后续 crossing 按实际 HP 变化判断。

取消边界只覆盖当前 damage effect 的盾 / HP / HP crossing 写入，不回滚 hook 调用前已经完成的前置步骤，例如命中、豁免、减伤骰、状态消耗、黑星护卫忽略次数、AI blackboard 记录或其它 pre-damage 资源消耗。若未来要取消这些前置消耗，必须把 hook 提前到对应步骤之前，不能让 `cancel_damage` 反向撤销已经提交的事实。

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

`release_context` 是明确的 battle-local 运行时对象，不是临时字典，也不进入存档。它至少冻结以下内容：

```text
release_context_id
owner_member_id / owner_unit_id / setup_id
source_event_id
trigger_type / timing
frozen_trigger_facts
target_resolver_inputs
release_mode
release_queue
current_release_index
entered_release_flow_at_tick
```

爆发释放可以在同一调用栈内消费完整 `release_queue`；连续释放必须把 `release_context` 留在 `BattleContingencySystem` sidecar 中，由 `owner_turn_started` 推进。进入 `release_context` 后才算正式消耗充能；未能创建 `release_context` 的匹配失败、live gate 失败、压制阻挡或 source event 取消都不消耗。

### 同一伤害事件的触发仲裁

伤害事件必须有稳定的 `damage_event_id`，并从伤害投影、伤害写入、HP 变化到死亡/倒地结算全程传递。对同一个 owner，同一个 `damage_event_id` 最多只能让一个应急矩阵进入释放流程。

V1 不新增 `damage_event_group` / `separate_damage_events` 等技能配置字段，也不让 `damage_event_id` 表达复杂技能语义。`damage_event_id` 按当前伤害结算粒度生成：一次 damage effect 对一个 target 生成一个 `damage_event_id`。多 effect / 多段法术天然可能产生多个 `damage_event_id`，因此会逐段打开触发检查窗口。若前一段已经让矩阵进入 `release_context`，后续段因为矩阵不再是 `armed` 而不会再次触发；若前一段未触发，后一段仍可触发。

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

`source_event_id` 表示一次逻辑来源事件，例如一次主动施法、一次 AoE 展开、一次位移或一次状态施加；`damage_event_id` 只表示该来源事件下“某个 damage effect 对某个 target 的一次伤害应用”。AoE / 多目标事件不能把单个 top-level `damage_event_id` 当成整个来源事件 ID。`source_event_facts` 至少应冻结：

```text
source_event_id
source_unit_id / source_member_id / source_faction_id
skill_id / cast_variant_id / action_instance_id
anchor_coord / area_shape / affected_coords
affected_unit_ids
每个 owner 的 affected_reason：direct_target / area_included
每个 owner 的原始坐标、预计 HP 伤害、护盾吸收、是否满足 fatal / damage_percent
每个 owner / effect 的 damage_event_id 映射；非伤害触发没有 damage_event_id
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

注意：这些 flag 只作用于储存法术触发瞬间。连锁应急术本体的成本已经在战斗外充能时支付，包含特殊宝石消耗和最大魔力封存。自动释放仍使用储存技能自身的内容查找、目标合法性、命中、豁免、抗性、免疫、护盾、减伤、special profile 和效果结算；反魔法压制、无合法目标、法术内容非法等仍可阻止或使释放无效。

自动施法不提供必中，也不提供免豁免。技能定义要求命中就正常命中，要求豁免就正常豁免；技能自身不需要命中或豁免时，也不因为 auto-cast 额外增加。`force_hit_no_crit` / `no_attack_roll` 只有在储存技能自身定义了才生效，不能由 `AutoCastRequest` 赋予。

实现上不要把 `AutoCastRequest` 包装成普通 `IssueCommand()`。应在技能执行编排层提供内部入口，例如 `ExecuteAutoCast(request, batch)`，复用底层效果解析，但跳过玩家行动阶段、成本扣除、冷却写入、熟练度和成就统计。

`ExecuteAutoCast()` 不能调用普通玩家施法的 `IssueCommand()` / `GetSkillCastBlockReason()` / `ConsumeSkillCosts()` 主路径后再用 flag 事后抵消。它应在请求构建时完成内容、目标和储存法术合法性校验，然后只复用底层效果解析和 commit helper。必须明确绕过的普通施法门槛包括：当前行动阶段、active unit / turn owner、AP、MP、stamina、aura、cooldown、racial / identity charge、petrified / weapon lock / main skill lock / misfortune / spell-control 等成本、次数、行动或失控门槛。必须保留的规则包括：技能内容存在、储存白名单、目标解析结果合法、射程 / LOS / 目标类型、命中、豁免、抗性、免疫、护盾、减伤、special profile 和实际效果结算。

统计语义固定为：自动释放不增加 caster 的主动技能熟练度、不计普通 skill-used 成就、不计资源消耗、不增加主动行动评分或施法刷量贡献。由实际效果自然造成的战斗事实仍保留，例如目标侧防御/受击触发、HP 变化、死亡、掉落、任务目标或击杀归因；这些事实的 report entry 必须带 `source_kind=contingency`，以后若需要限制“自动击杀成就”，应由成就规则按 origin 单独判断，不能在自动施法入口静默吞掉死亡事实。

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

实现上不能只在 `AutoCastRequest` 上存这个 bool。所有由自动施法派生出的 hook facts / source_event_facts / damage_event_facts 都必须携带 origin，例如：

```text
BattleEffectOrigin:
    source_kind = contingency
    source_matrix_id
    source_setup_id
    can_trigger_contingencies = false
```

任何 contingency hook 在扫描候选矩阵前先检查 origin；`can_trigger_contingencies=false` 时直接记录 `recursive_contingency_blocked` 或静默跳过，不得进入触发匹配。这样自动法术造成的新伤害、状态、位移、死亡或 AoE 命中都不会绕过请求级 flag 重新触发另一套应急术。

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

已存矩阵也必须满足当前内容定义的硬契约。若内容定义变更导致存档中的 setup 变为非法，例如法术不再允许储存、关键 tags / `min_contingency_skill_level` 与已存配置冲突、目标解析器不再被允许，读档时按存档异常处理；不自动清除、降级、返还、迁移、静默跳过或延后到进战时报错。玩家修改预设或重新充能时，也必须按当前内容定义重新校验。

储存法术合法性必须按固定顺序校验：

```text
1. can_be_stored_in_contingency 必须为 true；默认 false，默认禁止储存。
2. 若 spell.tags 与 forbidden_stored_skill_tags 任一相交，则拒绝储存；ANY 匹配即禁止。
3. 若白名单与禁止 tag 冲突，禁止 tag 优先。
4. 再检查 min_contingency_skill_level、matrix_load、allowed_target_resolvers 和目标类型。
```

`damage`、`control` 不作为默认禁止 tag；它们由连锁应急术等级、矩阵负载和法术自身 `min_contingency_skill_level` 控制。`summon`、`extra_action`、`retrigger_contingency`、`complex_manual_target` 等禁止 tag 命中时必须在 UI 中显示拒绝原因。

负载默认按 `cast_level + tags/effect_category` 自动计算，特殊技能可使用 `contingency_load_override` 覆盖。技能若缺少应急储存配置，默认不能储存。

内容配置必须是 typed 契约，而不是自由字典。推荐在 `SkillDef` 或 `CombatSkillDef` 上挂一个明确的 `ContingencyAutomationDef` / typed field，声明：

```text
can_be_stored_in_contingency
min_contingency_skill_level
contingency_load_override
allowed_target_resolvers
allowed_parameter_bindings
forbidden_stored_skill_tags
automation_profile_id
```

`SkillContentRegistry` / content validator 负责把这些字段投成 typed catalog 并校验 allowlist、binding key、binding value 类型和枚举集合。`PartyMemberState.FromDictionary()` 只做 exact schema / 类型 / 必填字段校验；技能 ID 是否存在、stored spell 是否仍可储存、resolver 与技能目标是否匹配，必须在 content catalog 已加载后由 contingency validator 处理，不能把内容注册表依赖塞进 state parser。读档流程必须在 content catalog 可用后运行该 validator；validator 失败时本次 save load 失败，而不是允许非法 setup 进入 runtime。

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
    "version": 6,
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
8. 不做兼容：新增字段时基于当前 root save version 9 / PartyState.version 5 做下一次破坏性升级；当前落地目标是 root save version 10、PartyState.version 6，若实现前版本再次变化则以实现时当前版本 +1 为准。旧 payload 缺字段直接拒绝。
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

属性快照必须显式区分三个值，且运行时规则统一把 `mp_max` 视为 effective 可用上限：

```text
mp_max_unreserved = 属性、成长、装备和临时修正算出的未封存 raw 上限
reserved_mp_max = 当前 charged setup 封存的最大魔力总量
mp_max = effective_mp_max = max(mp_max_unreserved - reserved_mp_max, 0)
```

持久层只在每个 charged setup 上保存 `reserved_mp_max`，不保存 member 级别总和。属性构建时由 `CharacterManagementModule` 汇总该角色所有 charged setup，把临时 `reserved_mp_max` 放入 `AttributeSourceContext`；`AttributeService._build_snapshot()` 先完成 raw `mp_max` 的全部加法、乘法和百分比修正，再在最后一步写入：

```text
mp_max_unreserved = raw_mp_max
reserved_mp_max = total_reserved_mp_max
mp_max = max(raw_mp_max - total_reserved_mp_max, 0)
```

现有所有资源规则、恢复、进战、战斗中刷新和战后回写继续读取 `AttributeService.MP_MAX` / `snapshot.get_value("mp_max")`，因此它们看到的是可用上限。`mp_max_unreserved` 只用于 UI 展示、日志、调试和解释封存来源，不参与恢复、扣费、技能前置或资源上限判定。这不是普通属性 modifier，也不得写进 `UnitBaseAttributes.custom_stats["mp_max"]`。

战斗中若某个 setup 已进入释放流程，但本场战斗尚未提交结算，持久 `PartyMemberState` 仍保持 `charged=true`。因此战斗内属性刷新不能只按持久 charged setup 重新计算封存，否则换装、形态刷新或其他 `BattleUnitState` 属性重建会把已经释放的 MP 上限再次封回去。V1 使用 battle-local overlay 记录本场战斗已经消耗的 setup，并在战斗内属性快照上覆盖本场有效封存值：

```text
persistent_reserved_mp_max = sum(setup.reserved_mp_max for persistent charged setup)
battle_released_mp_max = sum(instance.reserved_mp_max for consumed setup in this battle)
battle_reserved_mp_max = max(persistent_reserved_mp_max - battle_released_mp_max, 0)

snapshot.reserved_mp_max = battle_reserved_mp_max
snapshot.mp_max = max(snapshot.mp_max_unreserved - battle_reserved_mp_max, 0)
```

这个 overlay 不是兼容层、不是存档迁移，也不是持久回滚机制；它只是战斗 runtime 在正式结算前持有的临时事实。战斗未提交时 overlay 随 runtime 丢弃；战斗提交时只把 `consumed_setup_ids` 写回存活成员的持久 setup。

所有会写入或刷新 MP 的存活成员路径都要 clamp 到 `effective_mp_max`：修炼成长、休息/恢复、装备变化、充能、清除、战斗中释放封存和战后资源回写。需要逐一检查的现有入口包括 `BattleUnitFactory` 的单位构建和属性刷新、战斗内装备投影刷新、settlement / item / rest 类资源恢复、`PracticeGrowthService.ApplyDailyGrowthToMember()`、战后 `CommitBattleResources()` 以及所有直接根据属性快照恢复 MP 的命令路径。死亡提交不走 MP 上限释放/恢复逻辑，直接按死亡规则把 `current_mp` 归零。UI 和日志展示 MP 上限时应同时显示未封存上限、封存值和可用上限，避免玩家误以为 raw 属性被永久扣减。

这样后续修改法术数值时，存档仍能读取最新的技能定义，同时不会重复扣材料或偷偷兼容旧结构。

---

## 19. Godot 落地建议

在当前 Godot 项目中，建议把数据、配置服务、战斗运行时和自动施法策略分开。

### 建议持久状态类

```text
scripts/player/progression/ContingencyMatrixSetupState.cs
scripts/player/progression/ContingencyTriggerState.cs
scripts/player/progression/ContingencyTargetResolverState.cs
scripts/player/progression/ContingencyStoredSpellEntryState.cs
scripts/player/progression/ContingencyMaterialCostState.cs
```

`PartyMemberState` 新增 `contingency_matrix_setups: Godot.Collections.Array<ContingencyMatrixSetupState>` 字段，并把这些状态类纳入 `ToDictionary()` / `FromDictionary()` 的 exact fields。`PartyState` 只负责成员集合和整体版本，不额外维护一份按成员 id 分组的应急矩阵字典。

持久状态使用 per-type exact schema：顶层 setup、stored spell、material cost 使用固定字段；trigger 与 target resolver 按 `type` 选择对应字段集合。不得使用通用 `params` 保存未声明字段，也不得让某种 trigger / resolver 携带其他 type 的字段。多字段、少字段、未知字段、未知 type、错误类型都按坏 payload 拒绝，不做兼容或自动修正。

状态 parser 只负责结构和类型，不负责内容存在性。`FromDictionary()` 不查询 skill catalog、item catalog 或 trigger/resolver allowlist；这些依赖当前内容表的检查必须在 `GameContentCatalog` / typed registry 已加载后，由 `PartyContingencySetupService` 或专门 validator 执行。读档入口必须把该 validator 纳入 save load 接受条件：内容契约失败即读档失败。这样 state 类不会反向依赖 session/content，也不会在 catalog 尚未加载时产生假失败。

`ContingencyMatrixSetupState` 字段固定为：

```text
setup_id
enabled
charged
source_skill_id
source_skill_level
matrix_load
reserved_mp_max
material_costs
trigger
release_mode
stored_spells
```

`ContingencyTriggerState` 按 type 精确字段：

```text
combat_started:
type, subject, timing

hp_below_percent:
type, subject, percent, crossing_only, timing

incoming_damage_percent:
type, subject, damage_percent, damage_basis, damage_amount_mode, timing

fatal_damage_incoming:
type, subject, timing

enemy_enter_radius:
type, center, radius, radius_metric, source_team, timing

status_applied:
type, subject, status_tags, application_match, timing

affected_by_spell:
type, subject, source_team, spell_match, timing
```

`ContingencyTargetResolverState` 按 type 精确字段：

```text
self:
type

trigger_source:
type

trigger_target:
type

nearest_enemy_to_owner:
type

nearest_enemy_to_trigger_cell:
type

owner_centered_area:
type

attacker_cell:
type

empty_cell_near_owner:
type, preference, max_distance
```

V1 不支持 `fixed_cell` 目标解析器。连锁应急术是战斗外预设，不能保存 battle-local 坐标、格子列表或复杂选点结果；闪现、传送和区域放置类法术必须通过 `empty_cell_near_owner`、`owner_centered_area`、`trigger_source` 等当前战斗事实动态解析。

`ContingencyStoredSpellEntryState` 字段固定为：

```text
stored_skill_id
cast_level
order
target_resolver
parameter_bindings
fallback_policy
```

`ContingencyMaterialCostState` 字段固定为：

```text
item_id
quantity
```

`quantity` 是正式成本数量。V1 必须先为 `PartyWarehouseService` 增加 quantity-aware typed API，再由 contingency 充能事务调用该 API 扣除材料。禁止把 `quantity=N` 展开成 N 条 `{ item_id = StringName }` 作为正式实现；也禁止把带 `quantity` 的字典直接塞进现有 batch path 后假定会扣 N 个，那会导致材料 under-deduct。

`parameter_bindings` 必须是 flat Dictionary，由储存技能定义声明允许的 key、类型和枚举值；无参数时显式 `{}`。V1 的 value 只允许 bool、int、float、String/StringName、Array[StringName]；不得保存目标、坐标、unit id、owner、节点/脚本/函数/对象引用、Dictionary value 或任意嵌套结构。

建议在 `PartyMemberState` 提供纯计算 helper：

```csharp
public int GetTotalReservedMpMax();
public int CalculateEffectiveMpMaxFromRaw(int rawMpMax);
```

这两个 helper 只读取已充能 setup 并返回数值，不负责扣宝石、校验储存法术、校验容量或修改 charged 状态；这些行为必须留在配置服务或战斗提交服务中。

### 建议运行时类

```text
scripts/systems/progression/PartyContingencySetupService.cs
scripts/systems/battle/runtime/BattleContingencySystem.cs
scripts/systems/battle/core/AutoCastRequest.cs
```

V1 不新增 `BattleEventDispatcher` 或通用事件总线。`AutoCastRequest` 使用 C# typed value object，必须有单一构造/校验入口。自动施法执行入口放在现有技能执行编排层内部，例如 `ExecuteAutoCast(request, batch)`，不要复用玩家 `IssueCommand()`。

V1 必须同时新增真实连锁应急术技能内容资源，例如 `mage_chain_contingency` 对应的 `SkillDef` / `CombatSkillDef` 或等价资源配置。测试 fixture 只能作为回归辅助，不能替代正式技能资源。正式资源必须接入 content catalog、等级曲线、负载/容量解锁和 automation schema 校验。

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
2. 捕获仓库状态、成员 contingency 状态和 `current_mp` 快照；当前 C# 主线没有通用 party transaction 入口。
3. 校验 owner 已学会连锁应急术、储存法术、等级解锁、负载、trigger / resolver 白名单和内容注册表。
4. 校验特殊宝石和 reserved_mp_max 可支付，可先用 `PartyWarehouseService` 的 batch swap 预检/提交路径模拟材料消耗。
5. 扣除特殊宝石：通过 `PartyWarehouseService` batch swap 提交。
6. 写 setup charged=true、material_costs=已消耗收据、reserved_mp_max=计算值。
7. 重新计算 effective_mp_max，并 clamp current_mp。
8. 全部步骤成功后保留状态；提交成功后由 runtime command layer 记录成功状态 / 世界层日志。
```

任一步失败都不得修改仓库、人物状态或 current_mp。实现上由 `PartyContingencySetupService` 自己持有仓库、成员 setup 和 `current_mp` 快照；失败时恢复快照，成功时保留结果，不新增通用事务框架，也不在 `PartyWarehouseService` 内新增信号暂缓、dirty 标记或日志暂缓机制。`PartyWarehouseService` 的 batch swap 当前只做仓库状态副本提交，没有信号、dirty、日志或持久化副作用；失败回滚返回稳定 `error_code`。失败回滚不写“充能成功”日志；命令失败日志按现有 command result 日志路径处理，不能污染持久状态。读档、进战和战斗开始确认都不能重新扣材料。战斗外清除不属于充能事务：只释放 MP 上限、清空 charged / material_costs / reserved_mp_max，不返还宝石。

战斗路径：

```text
BattleRuntimeModule.StartBattle 建好单位
    -> BattleContingencySystem 从 active member 的 charged setup 建 battle-local instance
        -> 玩家确认开战后调用 OnBattleConfirmed
            -> 伤害、HP、状态、位移、法术影响等现有流程显式调用对应 hook
                -> BattleContingencySystem 创建 AutoCastRequest
                    -> ExecuteAutoCast 跳过 AP / 资源 / 冷却 / 熟练度 / 成就统计
```

战斗运行时边界：

```text
1. 只有 active member 的 charged setup 会生成 battle-local ContingencyInstance；reserve member 不生成实例，但仍承受 MP 封存。
2. 桥接发生在战斗单位生成和落位之后、`OnBattleConfirmed` 之前；实例用 `source_member_id` 绑定 live unit，战斗开始时找不到 owner live unit 视为数据异常。
3. ContingencyInstance 只存在于 BattleRuntimeModule / BattleContingencySystem 的 sidecar 中，不保存完整 setup 到 BattleUnitState，不进入 `BattleUnitState.ToDictionary()`，也不写入存档。
4. 进入释放流程时，battle runtime 记录 consumed_setup_id，不在战斗中途直接改持久 `PartyMemberState`。
5. 进入释放流程时，战斗本地必须立刻释放该 setup 的 MP 上限封存，并刷新 owner 的 BattleUnitState / 属性快照；`current_mp` 不自动恢复。
6. 战斗胜利、逃跑提交或其他明确提交型结算时，才把 consumed setup 回写为 charged=false 并释放持久 reserved_mp_max。
7. 失败重试、结算失败、战斗中保存锁期间不得持久化 consumed 状态。
8. 死亡提交不做 contingency 特殊回写、封存释放或 MP clamp 处理；直接沿用现有死亡规则，`current_mp=0` 并按死亡流程移出可用队伍。
9. 存活成员的回写 charged=false 必须发生在 `CommitBattleResources()` clamp current_mp 之前；释放封存只提高 MP 上限，不自动恢复 current_mp。
```

#### 战斗临时事实层（battle-local overlay）

battle-local overlay 的职责是把“本场战斗已经释放了哪个持久 setup”这件事覆盖到战斗属性刷新里，同时不提前修改存档层。它只存在于 `BattleContingencySystem`，不写入 `PartyMemberState`、`BattleUnitState.ToDictionary()`、战斗存档或世界日志。

`BattleContingencySystem` 维护以下 battle-local 状态：

```text
instances_by_member_id:
  member_id -> Array[ContingencyInstance]

instances_by_setup_key:
  "{member_id}:{setup_id}" -> ContingencyInstance

consumed_setup_ids_by_member_id:
  member_id -> Dictionary[setup_id, true]
```

`ContingencyInstance` 至少保存以下运行时字段：

```text
owner_member_id
owner_unit_id
setup_id
source_skill_id
source_skill_level
reserved_mp_max
trigger
release_mode
stored_spells
state
consumed_charge
consumed_source_event_id
consumed_damage_event_id
release_queue
```

这些字段来自开战时的持久 setup 快照。`reserved_mp_max` 必须复制进 instance，因为释放封存、战斗内属性刷新和战后提交都需要稳定知道本场战斗消耗的是哪一笔封存。instance 可以保存触发器、释放方式和预存法术的 battle-local 副本；但它们仍不进入任何持久 payload。

进入释放流程时的顺序固定为：

```text
1. hook facts 通过触发条件匹配。
2. live gate 通过：owner 仍有对应 live BattleUnitState，矩阵 state 仍可触发，且未 consumed。
3. 创建 release_context，并确定本次 setup 正式进入释放流程。
4. BattleContingencySystem.mark_setup_consumed(owner_member_id, setup_id, source_event_facts)。
5. 标记 instance.consumed_charge=true，state 进入 triggering / releasing / depleted 路径。
6. 立刻刷新 owner 的 battle-local 属性快照，使该 setup 的 reserved_mp_max 不再封存本场 MP 上限。
7. 构建并执行 AutoCastRequest / release_queue。
```

第 4 步是“消耗充能”的唯一战斗内切点。触发匹配失败、live gate 失败、owner 找不到、矩阵被压制且不能进入 release_context 时，不调用 `mark_setup_consumed()`，不释放封存，也不战后回写。只要第 3 步已经成立，后续即使所有预存法术因目标解析失败、免疫、非法目标或效果无效而没有实际收益，也保持 consumed，不回滚、不返还、不重新封存。

MP overlay 的计算必须通过单一 helper 完成，避免各个战斗系统自己减封存：

```csharp
public IReadOnlyList<StringName> GetConsumedSetupIds(StringName memberId);
public int GetBattleReservedMpMax(StringName memberId, int persistentReservedMpMax);
public void ApplyMpReservationOverlay(StringName memberId, AttributeSnapshot snapshot);
```

`ApplyMpReservationOverlay()` 读取 `snapshot.mp_max_unreserved` 和 `snapshot.reserved_mp_max`。其中 `snapshot.reserved_mp_max` 是 `CharacterManagementModule` 按持久 charged setup 算出的封存总量；overlay 再扣除本场已 consumed instance 的 `reserved_mp_max`，最后覆盖 `snapshot.reserved_mp_max` 与 `snapshot.mp_max`。如果正式属性快照缺少 `mp_max_unreserved` / `reserved_mp_max`，这是实现错误或测试 fixture 不完整，不能用旧字段猜测兼容。

`BattleUnitFactory` 是战斗内应用 overlay 的统一入口。所有玩家战斗单位属性构建和刷新都必须在拿到 `CharacterManagementModule.GetMemberAttributeSnapshot*()` 返回值后，先调用 contingency overlay，再写入 `BattleUnitState.attribute_snapshot`：

```text
BattleUnitFactory.BuildMemberAttributeSnapshot(...)
    -> characterGateway.GetMemberAttributeSnapshotForEquipmentView(...)
        -> snapshot 内含持久层 reserved_mp_max
    -> BattleContingencySystem.ApplyMpReservationOverlay(member_id, snapshot)
        -> snapshot 内含本场战斗 effective reserved_mp_max
    -> BattleUnitState.attribute_snapshot = snapshot
```

因此以下路径都会看到同一套 battle-local MP 上限事实：

```text
1. 开战构建 ally unit：无 consumed setup，overlay 不改变结果。
2. setup 触发后刷新 owner：consumed setup 释放封存，mp_max 立刻提高，current_mp 不恢复。
3. 战斗内换装刷新属性：先按持久层重建 raw / persistent reserved，再由 overlay 扣掉本场 consumed setup，避免重复封存。
4. 其他战斗内属性刷新：同样必须走 BattleUnitFactory 或同一个 overlay helper。
```

刷新资源时沿用现有 clamp 语义：如果新 `mp_max` 低于旧上限，`current_mp` clamp 到新上限；如果新 `mp_max` 高于旧上限，只保证 `current_mp >= 0`，不自动恢复 MP。连锁应急术触发释放封存通常属于上限提高，因此当前 MP 保持原值。

这个 overlay 只影响战斗内属性快照，不负责世界层恢复、充能、清除或战后写回。战后写回仍然只使用 `consumed_setup_ids`：

```text
存活成员：
  CommitContingencyConsumedSetups(member_id, consumed_setup_ids)
  GetMemberAttributeSnapshot(member_id)  # 此时持久 charged=false 已生效
  CommitBattleResources(member_id, current_hp, current_mp, current_aura)

死亡成员：
  不读取 consumed_setup_ids
  不释放持久封存
  CommitBattleDeath(member_id)
```

若战斗 runtime 异常结束、失败重试、未提交结算或 battle save lock 未释放，overlay 直接丢弃；持久 setup 仍保持战前状态。若提交型结算中 `CommitContingencyConsumedSetups()` 返回异常，`EndBattle()` 必须中止该成员后续 `CommitBattleResources()`，并把结算视为提交失败，不能出现“MP 资源已提交但 consumed setup 未回写”的半提交状态。

`battle_save_lock` 只能保证磁盘不在战斗中途保存，不能保证内存 mutation 自动回滚。`GameRuntimeFacade.FinalizeBattleResolution()` 这类结算入口在调用 `EndBattle()` 前必须捕获可恢复的 runtime / party 快照；只要 consumed 写回、资源提交、party state set、world persist 或 `FlushGameState()` 任一步失败，就要恢复到结算前内存状态，并保留同一份 battle result 供重试或报错。否则会出现磁盘仍是战前状态、内存却已经释放封存或提交资源的半提交。

战斗结算生命周期：

| 战斗结果 | 未触发 charged setup | 已进入释放流程的 setup | MP 封存 |
|---|---|---|---|
| 胜利 / 正常提交 | 保持 `charged=true` | 回写 `charged=false` | 未触发继续封存；已触发释放 |
| 逃跑 / 提交型撤退 | 保持 `charged=true` | 回写 `charged=false` | 未触发继续封存；已触发释放 |
| 失败但读档 / 重试 | 不写回，回到战前存档状态 | 不写回，回到战前存档状态 | 跟随战前存档 |
| 永久死亡提交 | 不做 contingency 特殊回写 | 不做 contingency 特殊回写 | 直接走死亡规则，`current_mp=0` |
| 战斗异常中断 / 结算失败 | 不提交 contingency 回写 | 不提交 contingency 回写 | 不污染存档 |

`consumed_setup_id` 只有在明确提交型结算时才写回。战斗中仍禁止保存 `triggering` / `releasing` / queue 等 battle-local 状态。

战后提交需要新增一个明确入口，由 `CharacterManagementModule` 负责写回存活成员的已消耗 setup：

```csharp
public ContingencyCommitResult CommitContingencyConsumedSetups(
    StringName memberId,
    IReadOnlyList<StringName> consumedSetupIds
);
```

该入口只处理存活成员；死亡成员不调用。它只做三件事：

```text
1. 找到 member_state.contingency_matrix_setups 中对应 setup_id。
2. 对 consumed setup 写 charged=false、reserved_mp_max=0、material_costs=[]。
3. 返回提交结果，供 `BattleRuntimeModule.EndBattle()` 决定是否继续资源提交。
```

`BattleRuntimeModule.EndBattle()` 中的存活成员提交顺序固定为：

```text
consumedIds = BattleContingencySystem.GetConsumedSetupIds(memberId)
CommitContingencyConsumedSetups(memberId, consumedIds)
CommitBattleResources(memberId, currentHp, currentMp, currentAura)
```

如果 `consumed_setup_ids` 为空，直接返回 ok。若 member 不存在、setup_id 找不到、setup 未 charged 或字段异常，视为战斗提交异常 / 数据异常；不得静默跳过、返还材料或做兼容修复。

### 同步 Hook 流程

```text
BattleRuntimeModule / damage resolver / status resolver / movement resolver 到达固定 hook
    -> BattleContingencySystem 接收 hook facts
        -> 判断触发条件与 live gate
            -> 生成 AutoCastRequest
-> ExecuteAutoCast 复用效果结算底层处理自动法术
```

示意：

```csharp
void BeforeDamageResolved(HookFacts hookFacts, DamageContext damageContext)
{
    foreach (var matrix in activeByTriggerType[hookFacts.TriggerType])
    {
        if (matrix.State != ContingencyState.Armed)
            continue;
        if (!TriggerMatches(matrix.Trigger, hookFacts))
            continue;
        TriggerMatrix(matrix, hookFacts, damageContext);
    }
}
```

特殊注意：

```text
1. 自动施法不能直接复用普通 `IssueCommand()`。
2. combat_started 不能在 `StartBattle()` 中直接触发，应接在 `GameRuntimeFacade.CommandConfirmBattleStartTyped()` 成功分支之后、timeline 解冻和首个单位 turn_start 之前触发，并把 batch/report 纳入本次确认开战的 UI/headless 快照。
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
| D6 | `contingency_matrix_setups` 正式落在 `PartyMemberState`，类型为 `Godot.Collections.Array<ContingencyMatrixSetupState>`，纳入 strict exact `ToDictionary()` / `FromDictionary()`；新增 trigger、resolver、stored spell、material cost 等显式状态类；setup 不存 owner，owner 来自外层 `PartyMemberState`；raw `mp_max` 不被改写，MP 封存通过 `effective_mp_max = max(raw_mp_max - total_reserved_mp_max, 0)` 投影到恢复、进战、刷新和战后回写；战斗中进入 release_context 后 battle-local 立即释放 MP 上限封存并记录 consumed setup，持久 `PartyMemberState` 到提交结算时才回写 `charged=false`。 |
| D7 | 连锁应急术是战前预施法矩阵，自动释放储存法术时绕过言语/姿势/材料成分和沉默、眩晕、麻痹、睡眠、恐惧等普通行动限制；反魔法/专门压制、owner 死亡/离场/不存在、目标非法或法术结算规则仍可阻止或使释放无效。 |
| D8 | 同一伤害事件必须携带稳定 `damage_event_id`；同一 owner 同一 `damage_event_id` 最多一个矩阵进入释放流程；伤害触发优先级为 `fatal_damage_incoming > incoming_damage_percent > hp_below_percent`；HP 写入后的 `hp_below_percent` 仍发生在死亡/倒地最终结算前，但若前置伤害触发已进入释放流程，则不再补触发。 |
| D9 | 多 owner 响应同一 AoE / 多目标瞬发事件时，先冻结 `source_event_facts` 并按 timeline / `unit_id` / `owner_member_id` 构建稳定触发队列；触发资格只看 frozen facts，不因前一个矩阵位移、造墙、护盾、杀死施法者或改变地形而重算；owner 轮到自己时再做 live gate；最终伤害/效果通过当前状态和 per-owner source event modifier 结算。持续区域、延迟爆炸、多段传播按 tick / 阶段重新生成 facts。 |
| D10 | 不新增 `matrix_fallback_policy`；矩阵是否消耗只看是否创建 `release_context` 并进入释放流程，进入后即使所有储存法术因目标解析失败、免疫、非法目标或 `skip_if_invalid` 没有实际效果，也消耗充能且不回滚；未进入释放流程的 live gate / 触发评估失败才不消耗。 |
| D11 | 压制是 battle-local overlay，不是主状态；主状态保持 `armed` / `triggering` / `releasing` / `depleted`，另存 `suppressed`、`suppressed_reason`、`resume_state`。压制只由反魔法、专门压制或未来高阶破阵/裂解压制模式造成；普通解除无效；压制不释放 MP 封存、不返还宝石、不改变 `charged=true`；`armed` 压制结束恢复可触发，队列中未进入释放流程则 live gate 失败不消耗，`burst_release` 开始后不回溯中断，`sequential_release` 压制中暂停队列并在解除后继续。 |
| D12 | 手动清除充能只允许战斗外通过 UI / headless 命令执行，是自由操作；清除后 `charged=false`、`reserved_mp_max=0`、`material_costs=[]`，释放 MP 上限但不自动恢复 current MP，特殊宝石不返还；编辑已充能配置前必须先清除并二次确认；战斗中不允许手动清除或手动触发。 |
| D13 | 储存法术采用默认禁止 + 显式白名单：`can_be_stored_in_contingency=true` 才可进入候选；随后检查 `forbidden_stored_skill_tags`，任一 tag 命中即拒绝，且禁止 tag 优先于白名单；`summon`、额外行动、再次触发、复活、永久制造、复杂手动选点等默认禁止，伤害/控制通过技能等级、负载和 `min_contingency_skill_level` 开放。 |
| D14 | 当前项目没有专注机制，V1 不实现、不校验、不保存任何专注相关字段；连锁应急术和储存法术暂不处理专注冲突，未来若引入专注机制再单独设计。 |
| D15 | 战斗生命周期按提交型结算处理：胜利/正常提交/逃跑提交时，存活成员未触发 charged setup 保持充能，已进入释放流程的 setup 回写 `charged=false` 并释放封存；失败读档/重试与异常结算失败不写回 contingency 状态；死亡提交不做 contingency 特殊回写、封存释放或 MP clamp，直接走死亡规则；battle-local `triggering` / `releasing` / queue 永不存档。 |
| D16 | `skill_id` 是持久契约，发布后不得随意重命名、复用或改变语义；存档只保存 `source_skill_id` / `stored_skill_id`，读档找不到 ID 时拒绝 payload 或 setup；不做 alias table、semantic hash、名称/标签猜测替代，也不在连锁应急术系统内置自动迁移。 |
| D17 | 已存矩阵必须满足当前内容定义；若内容变更导致 setup 不再合法，例如法术不再允许储存、关键 tags / `min_contingency_skill_level` 冲突、目标解析器不再允许，读档时按存档异常处理；不自动清除、降级、返还、迁移、静默跳过或延后到进入战斗时报错。 |
| D18 | 保留 `parameter_bindings`，无参数时显式 `{}`；每个技能定义声明允许的 binding key、类型和枚举值，未声明 key 或错误 value 直接拒绝；V1 value 只允许 bool、int、float、String/StringName、Array[StringName]；该字段只用于法术模式选择，不允许保存目标、坐标队列、runtime unit id、owner、节点/脚本/函数/对象引用、Dictionary value 或任意嵌套结构。 |
| D19 | V1 `timing` 固定为实现契约，不由玩家自由选择；仅允许 `after_battle_confirmed`、`before_spell_effect_resolved`、`before_damage_resolved`、`after_hp_changed`、`after_status_applied`、`after_position_changed`、`owner_turn_started`，并由 trigger type 固定映射；旧式 `after_event`、`after_movement` 不进入 V1。 |
| D20 | 新增连锁应急术存档字段属于 save schema break：基于当前 root save version 9 / `PartyState.version` 5 做下一次破坏性升级；当前落地目标是 root save version 10、`PartyState.version` 6，若实现前版本再次变化则以实现时当前版本 +1 为准。明确不做兼容、不写迁移、不补默认字段、不支持旧 payload、不做 soft fallback；旧版本或缺字段 payload 加载失败并作为存档版本/结构不兼容处理。 |
| D21 | 删除 `charged_at_world_step`；V1 不保存充能时间、过期时间或 `-1` 哨兵值。若以后需要显示“最近充能/清除”，从 `GameSession.log_event` / `GameLogService` 查询世界层日志，不把时间快照放入 setup payload。 |
| D22 | 连锁应急术绑定 `owner_member_id` 人物身份，不绑定当前形态、种族、职业外观、身体模板或 battle-local `unit_id`；`self` 解析为 owner 当前 live `BattleUnitState`，使用当前形态的坐标、体型、属性和状态；若找不到 owner live unit，则未进入释放流程时 live gate 失败且不消耗，已进入释放流程后不回滚，后续 `self` 法术按目标解析失败跳过或中止。 |
| D23 | 原审查意见中的“自身矩阵递归触发”在当前状态机下无效：矩阵必须先从 `armed` 进入 `triggering` / `releasing` 才释放预存法术，已触发矩阵不能再次触发；不新增专门的自身递归处理。保留全局防跨矩阵规则：连锁应急术自动施法产生的派生事件不能触发任何连锁应急术，`AutoCastRequest.can_trigger_other_contingencies` 固定为 `false`，且所有派生 hook facts / source_event_facts / damage_event_facts 都必须携带 `BattleEffectOrigin.can_trigger_contingencies=false`。 |
| D24 | V1 只支持玩家 active party member 的持久 charged setup；敌人、召唤物和 boss 不读取 `PartyMemberState.contingency_matrix_setups`，不走宝石 / MP 封存持久成本。若未来需要敌方或 boss 使用，必须通过显式 enemy template / special profile 预装 battle-local profile/template，不让 AI 临场感知该机制并做资源分配；默认每个 enemy unit 最多 1 个，多矩阵 boss 作为未来单独机制设计。 |
| D25 | 已由 D15 生命周期覆盖：死亡提交不做 contingency 特殊回写、封存释放或 MP clamp，直接走死亡规则，`current_mp=0` 并按现有死亡流程移出可用队伍；失败重试或结算失败不写回。 |
| D26 | 原审查意见中的“多个单位同时进入范围”在 V1 事件模型下无效：位置变化按真实时间顺序逐个派发，不存在同时进入批量事件；`enemy_enter_radius` 按先来后到处理，首个从范围外进入范围内且成功创建 `release_context` 的敌人成为 `trigger_source` 并消耗矩阵，后续进入事件不会补触发；若先进入事件未进入释放流程则不消耗，后续进入仍可触发；不新增每轮冷却、批量聚合或同回合去重窗口。 |
| D27 | 召唤物、宠物、盟友、临时单位都不是 owner 的代理主体；它们受伤、被控、被法术影响或进入危险区域，不触发 owner 的矩阵。召唤物可以作为普通 `source_unit_id`：敌方召唤物伤害 owner、进入 owner 半径或施法影响 owner 时，可按对应触发器触发；未来 `summoner_unit_id` / `summoner_member_id` 只用于归因、AI、日志或召唤控制，不把召唤物事件改写成 summoner 本人事件。 |

### E. 代码架构可行性审查

| 编号 | 裁决 |
|---|---|
| F1 | MP 封存不修改 raw `mp_max`，也不写 `UnitBaseAttributes.custom_stats["mp_max"]`；属性快照显式区分 `mp_max_unreserved`、`reserved_mp_max`、`effective_mp_max`。持久层只保存 setup 级 `reserved_mp_max`，member 级总和运行时汇总；`AttributeSourceContext` 增加 transient `reserved_mp_max`，`AttributeService._build_snapshot()` 在所有属性修正后计算 effective max。充能立刻 clamp current MP；战斗中释放封存立刻刷新 BattleUnitState 上限但不恢复 current MP；战后提交先释放封存再提交资源 clamp。 |
| F2 | 自动施法不走 `IssueCommand()` / `GetSkillCastBlockReason()` / `ConsumeSkillCosts()` 主路径；新增 battle-local `AutoCastRequest` 和内部 `ExecuteAutoCast()` 路径。固定 flags：`is_auto_cast`、`source_kind=contingency`、`ignore_action_phase`、`ignore_ap_cost`、`ignore_resource_cost`、`ignore_cooldown`、`ignore_identity_charge`、`ignore_mastery_gain`、`ignore_skill_used_achievement`、`skip_spell_control`、`spent_mp=0`、`can_trigger_other_contingencies=false`。保留内容查找、目标、抗性、护盾、豁免和效果结算；派生事件必须携带 origin 抑制。 |
| F3 | 不做通用事件总线，不新增 `BattleEventDispatcher`。改为 `BattleContingencySystem` 暴露固定同步 hook：`OnBattleConfirmed`、`BeforeDamageResolved`、`AfterHpChanged`、`AfterStatusApplied`、`AfterPositionChanged`、`BeforeSpellEffectResolved`、`OwnerTurnStarted`。`BeforeDamageResolved` 必须能写入取消或修改伤害的 per-owner 修正；`source_event_id` 是逻辑来源事件，`damage_event_id` 是单个 target 上的单次 damage effect，二者使用 battle-local serial，不进存档。 |
| F4 | 战斗中进入 release_context 后，battle-local 立刻消耗 setup 并释放 MP 上限封存；持久 `PartyMemberState` 不在战斗中途修改。正式战后提交时，存活成员先把 consumed setup 写成 `charged=false` 并释放封存，再提交资源；失败重试、结算失败和战斗保存锁不写回。死亡提交不做 contingency 特殊回写，直接按死亡规则处理。 |
| F5 | `PartyMemberState.contingency_matrix_setups` 使用 strict exact fields；实现时基于当前 root save version 9 / `PartyState.version=5` 再做破坏性版本升级，当前落地目标为 root save version 10 和 `PartyState.version=6`。旧 payload、缺字段、坏字段直接拒绝，不补默认、不迁移、不做兼容。 |
| F6 | 战斗桥接从 `PartyMemberState` 到 `BattleContingencySystem`，发生在单位生成落位之后、战斗确认 hook 之前。实例使用 `source_member_id` 绑定 live unit；不把完整 setup 存进 `BattleUnitState` 或 `BattleUnitState.ToDictionary()`。战斗开始找不到 owner live unit 是数据异常，不静默跳过。 |
| F7 | 持久状态类落在 `scripts/player/progression/`：`ContingencyMatrixSetupState.cs`、`ContingencyTriggerState.cs`、`ContingencyTargetResolverState.cs`、`ContingencyStoredSpellEntryState.cs`、`ContingencyMaterialCostState.cs`。运行时新增 `PartyContingencySetupService.cs`、`BattleContingencySystem.cs`，可选 `AutoCastRequest.cs`；不新增 `BattleEventDispatcher`。 |
| F8 | 保存格式破坏性升级：root save / `SaveSerializer` 默认版本从当前 9 升到下一版本，`PartyState.version` 从当前 5 升到下一版本；当前落地目标为 root save version 10、`SaveSerializer` 默认 version 10、`PartyState.version` 6。save index 版本不因正文存档字段变化而升级，除非索引 schema 另有变化。 |
| F9 | 命名统一 snake_case。`skill_id` 是技能定义持久契约；`source_skill_id` 表示创建矩阵的连锁应急术技能，`stored_skill_id` 表示自动释放的预存技能。玩家 UI 使用“爆发释放”；战斗内部主状态为 `armed`、`triggering`、`releasing`、`depleted`，压制使用 overlay 字段，不把 `triggering` 暴露给玩家。 |
| F10 | 复用现有 `BattleEventBatch.report_entries` 通道，必要时加公开 helper，不直接调用私有 `_append_report_entry_to_batch`。新增结构化条目使用 `entry_type`，不使用旧式 `type`。 |
| F11 | 现有 battle save lock 可以作为战斗不存档基础；新增要求是 contingency 回写顺序必须在 `CommitBattleResources()` clamp 之前。若崩溃、结算失败或 lock 未释放，存档保持战前状态；不新增 dirty flag。 |
| F12 | 世界层新增 `PartyContingencySetupService` 作为充能/清除唯一入口。充能事务先拒绝战斗态，再校验技能、内容、材料和封存，候选状态写 charged/material/reserved，扣宝石，重算 effective max 并 clamp current MP，最后提交；任一步失败都不产生部分 mutation。所有世界层 MP 恢复与 clamp 路径使用 effective max。 |
| F13 | `charged_at_world_step` 直接删除；不使用 `-1` 哨兵，不保存充能时间或过期时间。需要显示或审计最近充能/清除时走世界层日志，不把该字段放回 setup payload。 |
| F14 | 当前不做专注机制，不讨论连锁应急术与专注的交互。V1 不新增专注字段、校验、状态、日志或测试。 |
| F15 | V1 不做敌人连锁应急术；未来敌人 / boss 的应急连锁术由 enemy template、`EnemyAiBrainDef` 或 special profile 预装为 battle-local profile/template，不读取 `PartyMemberState.contingency_matrix_setups`，不走宝石 / MP 封存持久成本，也不要求现有敌方 AI 决策层感知该机制并做分配。默认仍每个 enemy unit 最多 1 个矩阵；多矩阵 boss 以后单独设计。 |

### G. 第二轮代码验证审查

| 编号 | 裁决 |
|---|---|
| G1 | `before_damage_resolved` 是可修改 hook，不是观察 hook。它对所有即将结算的伤害调用，包括预计完全被护盾吸收的伤害；但 `incoming_damage_percent` / `fatal_damage_incoming` 只按 `projected_hp_damage_after_shield` 判定，预计 HP 伤害为 0 时不触发。hook 必须支持 per-owner `cancel_damage` / `modified_resolved_damage`；`cancel_damage=true` 时不扣护盾、不扣 HP、不产生对应 HP crossing，已进入 release_context 的矩阵仍消耗。 |
| G2 | 战后提交时，只有存活成员需要先应用 contingency writeback，再重建 `AttributeSnapshot`，最后调用 `CommitBattleResources()` clamp HP / MP / aura。顺序固定为：`consumed setup -> charged=false / reserved_mp_max=0 / material_costs=[]`，然后 `GetMemberAttributeSnapshot(member_id)`，再提交资源。死亡提交不做这些 contingency 特殊处理，直接走死亡规则。 |
| G3 | 新增 `CharacterManagementModule.CommitContingencyConsumedSetups(member_id, consumed_setup_ids)`，专门把 battle-local consumed setup 写回 `PartyMemberState`。该入口只处理存活成员；死亡成员不调用。空列表直接 ok；member 不存在、setup_id 找不到、setup 未 charged 或字段异常都视为战斗提交异常 / 数据异常，不静默跳过、不返还材料、不做兼容修复。`EndBattle()` 必须先调用该入口，再调用 `CommitBattleResources()`。 |
| G4 | 自动施法只跳过行动阶段、AP/MP/资源、冷却、熟练度、成就和魔力失控等成本/统计机制，不改变储存技能自身的命中、豁免、抗性、免疫、护盾、减伤、special profile 和效果结算规则。技能要求豁免就正常豁免，要求命中就正常命中；技能本身不要求命中/豁免时不额外增加。`force_hit_no_crit` / `no_attack_roll` 只能来自储存技能自身定义，不能由 `AutoCastRequest` 赋予。 |
| G5 | `owner_turn_started` 在 V1 中只用于连续释放队列推进，不作为玩家可选触发器。同一个 `owner_turn_started` tick 内，先推进已有 `releasing` 队列，再进入普通回合开始流程；本 tick 不重新评估该矩阵的新触发。推荐插入点是单位成为 active unit 后、trait turn_start / AP 分配 / 玩家或 AI 输入前。owner 被压制则队列暂停，死亡或离场则队列中止，已进入 release_context 的矩阵不返还。 |
| G6 | 战斗外充能必须由 `PartyContingencySetupService` 作为唯一入口执行跨域事务，覆盖仓库扣特殊宝石、`PartyMemberState.contingency_matrix_setups` 写 charged/material/reserved、`current_mp` clamp 和世界层日志。当前 C# 主线没有通用 party transaction helper；V1 应在服务内显式捕获仓库状态、成员 contingency 状态和 `current_mp`，失败时恢复这些快照并返回稳定 `error_code`。读档、进战和战斗开始确认永不扣材料；战斗外清除不返还宝石。 |
| G7 | V1 不新增 `damage_event_group` / `separate_damage_events` 等技能字段，也不让 `damage_event_id` 承载复杂技能语义。`damage_event_id` 按现有伤害结算粒度生成：一次 damage effect 对一个 target 一个 ID；多 effect / 多段法术会逐段打开触发检查窗口。同一 owner、同一 `damage_event_id` 最多一个矩阵进入 `release_context`。若前一段已触发并消耗矩阵，后续段不会再次触发；若前一段未触发，后一段可以触发。AoE 的触发资格仍由 frozen `source_event_facts` 保证，不靠 `damage_event_id` 回头重算。 |

### H. 第三轮代码验证审查

| 编号 | 裁决 |
|---|---|
| H1 | `before_damage_resolved` 调用前，damage resolver 必须先进行无副作用伤害投影，把 `resolved_damage`、`projected_shield_absorbed`、`projected_hp_damage_after_shield`、`would_be_fatal`、`shield_would_break` 注入 hook context。投影阶段不得修改护盾、HP 或状态。若 hook 返回 `modified_resolved_damage`，必须按修改后的伤害重新投影，再进入实际扣盾扣血。 |
| H2 | `cancel_damage=true` 的实现边界是局部提前返回零伤害结果：不扣护盾、不扣 HP、不产生对应 `hp_below_percent` crossing，并记录取消原因 / 结构化报告。取消当前 damage effect 不影响上层 `resolve_effects` 循环继续处理同一技能的后续 effect。 |
| H3 | 原附录提出的“仓库快照回滚需暂缓信号 / side effects”不采纳为实现项。代码复核确认 `PartyWarehouseService` 的 batch swap 事务当前主要做仓库状态副本提交；世界层日志由 command handler 在命令返回后统一记录。由于当前 C# 主线没有通用 party transaction helper，V1 不应假设已有外层事务，而应在 `PartyContingencySetupService` 内显式完成快照、提交、失败恢复和稳定错误码。 |
| H4 | 持久状态采用 per-type exact schema：setup / stored spell / material cost 为固定字段，trigger / target resolver 按 `type` 精确字段校验；未知字段、缺字段、未知 type 和错误类型均拒绝。V1 移除 `fixed_cell`，不保存 battle-local 坐标、格子列表或复杂选点结果。 |
| H5 | 运行时规则中 `mp_max` 永远表示 effective 可用上限。属性快照额外写 `mp_max_unreserved` 和 `reserved_mp_max` 供 UI / 日志 / 调试展示；raw 上限不参与恢复、扣费、技能前置或资源上限判定。`reserved_mp_max` 由 `AttributeSourceContext` 传入，`AttributeService._build_snapshot()` 最后一步覆盖 `mp_max=max(mp_max_unreserved-reserved_mp_max,0)`。 |
| H6 | 战斗中释放封存使用 battle-local overlay：`BattleContingencySystem` 记录本场 consumed setup ids 和对应 instance 的 `reserved_mp_max`，持久 `PartyMemberState` 战斗中不改；`BattleUnitFactory` 每次从 `CharacterManagementModule` 重建玩家属性快照后调用 overlay helper，按 `persistent_reserved_mp_max - consumed_reserved_mp_max` 覆盖 `snapshot.reserved_mp_max` 与 `snapshot.mp_max`。触发进入 release_context 后立刻 mark consumed 并刷新 owner 属性，上限提高不恢复 current MP；战后只对存活成员先写回 consumed setup，再提交资源。 |

---

### I. 当前 C# 主线校准（2026-06-23）

本节是对当前 C# 代码的最新落地校准。后续实现以本节引用为准，早期审查中的历史行号和版本号不再作为实现依据。

| 编号 | 当前代码事实 | 落地影响 |
|---|---|---|
| I1 | 保存层当前是 `scripts/systems/persistence/GameSession.cs` 的 `SaveVersion = 9`、`scripts/systems/persistence/SaveSerializer.cs` 的 `_save_version = 9`、`scripts/player/progression/PartyState.cs` 的 `version = 5`。 | 实现 contingency 持久字段必须在当前版本基础上再次破坏性升级；当前落地目标是 root save version 10、party version 6，不做旧 payload 兼容。 |
| I2 | `scripts/player/progression/PartyMemberState.cs` 仍是 exact fields，当前没有 `contingency_matrix_setups`，字段数组也不是旧审查里的 44 项。 | 新字段必须进入 `TO_DICT_FIELDS`、`ToDictionary()`、`FromDictionary()`、`DuplicateState()`；相关测试 fixture 需要同步重建。 |
| I3 | `scripts/systems/attributes/AttributeSourceContext.cs` 没有 `reserved_mp_max`；`scripts/systems/attributes/AttributeService.cs` 的 `BuildSnapshot()` 直接写 `mp_max`，没有 raw/effective 分层。 | MP 封存仍是实现阻塞点。必须先加 `mp_max_unreserved`、`reserved_mp_max`、effective `mp_max` 的统一计算点，再改世界恢复、战斗生成和战后 clamp。 |
| I4 | `scripts/systems/battle/runtime/BattleUnitFactory.cs` 从 character gateway 取得 `AttributeSnapshot` 后直接用于单位构建，没有 contingency overlay。 | 战斗中释放封存需要新增 overlay helper，并保证所有玩家快照刷新入口都走同一 helper。 |
| I5 | `scripts/systems/battle/runtime/BattleRuntimeModule.cs` 的战斗结束只提交资源/死亡；`scripts/systems/progression/CharacterManagementModule.cs` 的 `CommitBattleResources()` 会重新构建 snapshot 后 clamp 资源。 | 已 consumed setup 的写回必须在 `CommitBattleResources()` 前完成，否则 MP 上限释放不会参与最终 clamp。 |
| I6 | `scripts/systems/battle/rules/BattleDamageResolver.cs` 的伤害、护盾吸收和 HP 扣减仍在同一结算段内；护盾吸收比例来自 `DamageApplicationInput.ShieldAbsorptionPercent`，不再是旧审查假设的固定 1.0。 | `BeforeDamageResolved` 不能复制护盾公式；必须抽无副作用投影 helper，hook 和实际扣盾扣血共用同一计算。 |
| I7 | 玩家施法入口仍由 `BattleRuntimeModule.IssueCommand()` 校验 active unit / turn state 后进入 `BattleSkillExecutionOrchestrator._handle_skill_command()`；成本在 `BattleRuntimeSkillTurnResolver.cs` 消耗，熟练度和成就统计散落在 orchestrator / runtime。 | 自动施法不能复用 `IssueCommand()`。需要内部 `ExecuteAutoCast(AutoCastRequest, BattleEventBatch)`，显式绕过 AP/MP/cooldown/mastery/achievement，同时保留命中、豁免、抗性、护盾和 effect 结算。 |
| I8 | 当前没有通用 party transaction helper；可见的是 `GameRuntimeWarehouseHandler.cs` 的私有仓库快照/回滚和 `PartyWarehouseService.cs` 的仓库 batch swap。 | 充能服务不能引用不存在的通用事务。`PartyContingencySetupService` 必须自己捕获仓库、成员 setup、`current_mp` 快照，失败时恢复，成功后再由命令层记录日志。 |
| I9 | `scripts/systems/battle/core/BattleEventBatch.cs`、`scripts/systems/battle/runtime/BattleSkillOutcomeCommitter.cs`、`scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs`、`BattleRuntimeModule` 已经使用 `report_entries`。 | 结构化战报不是阻塞点；contingency 只需要复用现有 `report_entries` 通道，避免直接写私有 batch helper。 |
| I10 | 当前 skill content 没有 contingency 专用的 stored spell allowlist、parameter binding schema、automation profile。 | 配置层仍缺内容契约。没有这层，UI/headless 不能安全保存 `parameter_bindings`，自动施法也无法判断哪些技能允许被预存。 |

当前硬阻塞不止 I6 / I8。真正会决定能否安全落地的前置项是：I3 effective MP / MP 封存统一计算点，I6 伤害投影和可修改 hook，I7 自动施法内部入口与 origin 抑制，I8 跨域充能事务，I10 内容 automation schema。缺任一项都只能写出局部 demo，无法保证存档、战斗结算、自动释放和防递归行为一致。

---

### J. 阻塞点详细实现设计

本节细化会阻塞首版落地的基础设施。目标是先补出可验证的 typed 契约、MP 封存、事务、hook 和自动施法边界，再让后续 contingency 主功能接入；不要在这里顺手实现完整连锁应急术。

#### J1. 跨域充能事务

**问题边界**

充能一次 contingency setup 会同时修改：

```text
PartyState.warehouse_state       # 扣特殊宝石
PartyMemberState.contingency_*   # 写 charged/material/reserved
PartyMemberState.current_mp      # 按 effective mp max clamp
```

当前只有 `PartyWarehouseService` 自己的 batch swap 事务和 `GameRuntimeWarehouseHandler` 的私有快照/回滚；没有可复用的 party-wide transaction。实现不应新增泛化事务框架，也不应让 runtime facade 承载具体 contingency 规则。

**前置 owner / API**

J1 不是第一个可落地改动。充能事务依赖以下 owner/API 已存在，否则只能做空壳测试：

```text
1. PartyMemberState.contingency_matrix_setups exact schema 已由前序 schema slice 落地。
2. 保存/编辑预设命令已存在；充能命令只引用 existing SetupId，不携带 Trigger / StoredSpells 直接改配置。
3. effective MP owner 已定义：
   - AttributeSourceContext.reserved_mp_max
   - AttributeService.BuildSnapshot() 最后一步写 mp_max_unreserved / reserved_mp_max / effective mp_max
   - CharacterManagementModule 只负责汇总持久 charged setup 后传入 AttributeSourceContext，不另建第二套 GetEffectiveMpMax 规则源
4. CommitBattleResources() 和所有 MP 恢复 / clamp 路径已改用 effective mp max。
5. 战后 consumed 写回有可失败返回值：
   - CharacterManagementModule.CommitContingencyConsumedSetups(...)
   - IBattleRuntimeCharacterGateway 对应方法
   - BattleRuntimeModule.EndBattle() 能在写回失败时 abort 后续 resource commit，并向 GameRuntimeFacade.FinalizeBattleResolution() 暴露失败结果。
```

第 5 点是半提交防线。如果 `EndBattle()` 仍只能调用 void `CommitBattleResources()` / `CommitBattleDeath()`，则文档中“consumed 写回失败时中止资源提交”的要求无法表达。`IBattleRatingCharacterGateway` 只服务评分 / 成就，不是 battle resource / contingency writeback 的正确桥。

**推荐所有权**

```text
PartyContingencySetupService
    owning state: PartyState + PartyWarehouseService + skill/content catalog
    owns: charge/clear validation, in-memory mutation rollback

GameRuntime command handler / headless command
    owns: battle-state rejection, PersistPartyState(), command result, log/status
    owns: persistence failure rollback snapshot
```

`PartyContingencySetupService` 保证“校验和内存 mutation 原子”。runtime command handler 保证“持久化失败时恢复到命令前状态”。不要让服务直接调用 `GameSession` 或写日志。

**新增窄类型**

```csharp
internal sealed class ContingencyChargeRequest
{
    public StringName MemberId;
    public StringName SetupId;
}

internal sealed class ContingencyChargeMutationSnapshot
{
    public WarehouseState WarehouseState;
    public StringName MemberId;
    public int CurrentMp;
    public Godot.Collections.Array<ContingencyMatrixSetupState> ContingencySetups;
}

internal sealed class ContingencyChargeResult
{
    public bool Ok;
    public StringName ErrorCode;
    public string Message;
    public IReadOnlyList<ContingencyMaterialCostState> MaterialCosts;
    public int ReservedMpMax;
    public int EffectiveMpMax;
}
```

`ContingencyChargeMutationSnapshot` 是 service 内部实现细节，不进存档、不暴露给 UI。若 runtime command handler 需要处理 `PersistPartyState()` 失败，优先在 command handler 侧捕获更粗的 `PartyState.DuplicateState()` / world runtime snapshot，沿用现有 warehouse command 的回滚模式。

`ContingencyChargeRequest` 只引用已保存的 setup。保存 / 编辑预设是独立命令；已 charged 的 setup 不允许编辑，必须先 clear charge。不要让 charge request 同时承载“保存配置”和“扣材料充能”两类职责。

**服务内执行顺序**

```text
1. Resolve member，member 不存在直接失败，不捕获 snapshot。
2. 校验当前不是 battle mutation path；battle lock / battle active 由 runtime command 先拒绝，service 也保留防御性 bool 参数或状态 provider。
3. 按 SetupId 读取 existing setup，校验 source skill、stored skill allowlist、trigger/resolver schema、matrix load、reserved_mp_max。
4. 把 ContingencyMaterialCostState.quantity 转成 quantity-aware 仓库 API 输入；如果仓库 API 还不支持数量，先实现该 typed API，不用 N 条 `{ item_id }` 展开作为 V1 正式实现。
5. 用 PartyWarehouseService.PreviewBatchQuantitySwapTyped(...) 或等价 quantity-aware typed API 预检材料。
6. Capture ContingencyChargeMutationSnapshot。
7. PartyWarehouseService.CommitBatchQuantitySwapTyped(...) 或等价 quantity-aware typed API 扣材料。
8. 写/替换 member.contingency_matrix_setups[setup_id]：
   charged=true
   material_costs=本次扣除收据
   reserved_mp_max=计算值
9. 通过 CharacterManagementModule / AttributeService 计算 effective mp max，clamp member.current_mp。
10. 做 postcondition 校验：setup charged、材料已扣、current_mp <= effective max。
11. 任一步失败或抛异常：Restore snapshot，返回稳定 error_code。
```

**恢复规则**

```text
Restore snapshot:
    partyState.warehouse_state = snapshot.WarehouseState.DuplicateState()
    member.contingency_matrix_setups = DeepDuplicate(snapshot.ContingencySetups)
    member.current_mp = snapshot.CurrentMp
```

不要复用 `PartyWarehouseService` 当前私有 `_set_transaction_warehouse_state()`。如果直接替换 `partyState.warehouse_state` 会破坏 service 内部视图，就给 `PartyWarehouseService` 增加一个很窄的 internal 方法：

```csharp
internal WarehouseState CaptureWarehouseStateForTransaction();
internal void RestoreWarehouseStateForTransaction(WarehouseState snapshot);
```

这两个方法只做状态复制，不发信号、不写日志、不持久化。

**runtime command 持久化失败处理**

```text
runtime command:
    commandSnapshot = CaptureRuntimeCommandSnapshot()
    result = PartyContingencySetupService.ChargeSetup(request)
    if !result.Ok:
        return failure
    persist = PersistPartyState()
    if persist != Ok:
        RestoreRuntimeCommandSnapshot(commandSnapshot)
        return persistence_failure
    write command status/log
    return ok
```

这一步不能省。否则 service 成功、存档失败时会出现内存已扣材料但磁盘未保存的半提交状态。runtime command 的 rollback 必须沿用 `GameRuntimeWarehouseHandler` 的完整模式：恢复 session/runtime party state、world/runtime snapshot 和 selected member 后重新 setup 相关服务引用；不能只替换 `WarehouseState`。

**必须覆盖的测试**

```text
tests/progression/run_contingency_charge_transaction_regression.cs
    - 材料不足：仓库、setup、current_mp 都不变
    - 材料扣除成功但 setup 写入失败：仓库回滚
    - 充能成功：材料扣除、charged=true、current_mp 被 effective max clamp
    - runtime persist failure：command handler 恢复命令前 PartyState
    - charge request 只能引用 existing SetupId，不能直接写 Trigger / StoredSpells
    - CommitBattleResources 使用 effective mp max，不再按 raw mp max clamp
```

#### J2. 伤害投影与 BeforeDamageResolved hook

**问题边界**

`BattleDamageResolver.ApplyDamageToTargetResult()` 现在在同一段里完成：

```text
resolved damage
shield absorption
shield mutation
hp damage
death prevention / fatal trait
```

contingency 的 `BeforeDamageResolved` 需要在扣盾扣血前看到准确的 `projected_hp_damage_after_shield`，并且可以取消或修改本次伤害。由于当前 C# 已支持 `DamageApplicationInput.ShieldAbsorptionPercent`，hook 不能复制护盾公式。

必须区分 live commit 与 preview / AI scoring。当前 `PreviewDamageEffectTyped()` 和 `preview_damage_sequence_typed()` 也会在 clone 上调用 `ApplyDamageToTargetResult()`；如果 hook 不加抑制，UI / AI preview 会提前触发、consume setup 或取消预览伤害。`BeforeDamageResolved` 只允许在 live commit damage 路径触发；preview / scoring 路径必须传 `SuppressDamageApplicationHook=true`，或使用 resolver scoped suppressor。

**新增窄类型**

放在 rules/core 侧，保持普通 C# DTO，不进存档：

```csharp
internal readonly struct DamageApplicationProjection
{
    public readonly int ResolvedDamage;
    public readonly bool BypassShield;
    public readonly double ShieldEfficiency;
    public readonly int ShieldHpBefore;
    public readonly int ProjectedShieldAbsorbed;
    public readonly int ProjectedShieldDrain;
    public readonly bool ShieldWouldBreak;
    public readonly int HpBefore;
    public readonly int ProjectedHpDamageAfterShield;
    public readonly int MinHpAfterDamage;
    public readonly bool BypassDeathPrevention;
    public readonly int ProjectedHpBeforeDeathPrevention;
    public readonly bool WouldEnterDeathResolution;
    public readonly bool WouldBeFatalBeforeDeathPrevention;
}

internal sealed class BeforeDamageResolvedContext
{
    public BattleUnitState SourceUnit;
    public BattleUnitState TargetUnit;
    public DamageApplicationInput Input;
    public DamageApplicationProjection Projection;
    public long DamageEventId;
    public Godot.Collections.Dictionary SourceEventFacts;
    public BattleDamageHookCommitContext CommitContext;
}

internal sealed class BeforeDamageResolvedResult
{
    public bool CancelDamage;
    public int? ModifiedResolvedDamage;
    public bool StateMayHaveChanged;
    public StringName ReasonId;
    public Godot.Collections.Array<Godot.Collections.Dictionary> ReportEntries;
}

internal sealed class BattleDamageHookCommitContext
{
    public BattleEventBatch Batch;
    public IBattleAutoCastExecutor AutoCastExecutor;
    public IBattleReportSink ReportSink;
}
```

**resolver 内顺序**

```text
ApplyDamageToTargetResult(target, input, source):
    if target == null or input.ResolvedDamage <= 0:
        return zero result

    if input.LiveCommit:
        target.NormalizeShieldState()  # 保持旧路径在正伤害提交前 normalize 的副作用时机

    projection = ProjectDamageApplication(target, input)  # 无副作用
    hookResult = input.SuppressDamageApplicationHook
        ? null
        : _damageApplicationHook?.BeforeDamageResolved(context)

    if hookResult != null && hookResult.ModifiedResolvedDamage != null:
        input = input.WithResolvedDamage(max(modified, 0))
        projection = ProjectDamageApplication(target, input)

    if hookResult != null && hookResult.StateMayHaveChanged:
        projection = ProjectDamageApplication(target, input)

    if hookResult != null && hookResult.CancelDamage:
        append report entries / cancellation facts
        return BuildAppliedDamageResult(input, 0, 0, false)

    ApplyProjectedShieldMutation(target, projection)
    ApplyProjectedHpDamageAndDeathPrevention(target, input, projection, source)
    return BuildAppliedDamageResult(input, projection.ProjectedHpDamageAfterShield, projection.ProjectedShieldAbsorbed, projection.ShieldWouldBreak)
```

`DamageApplicationInput.WithResolvedDamage(int)` 必须同步更新 typed field 和 payload 里的 `resolved_damage`，并保留 save、dice、mitigation、shield absorption、min HP 等其它字段。否则后续 report / preview payload 会出现 typed damage 与 payload damage 不一致。

`ProjectDamageApplication()` 不能调用会改状态的方法。live commit 路径仍在投影前调用一次 `NormalizeShieldState()` 以保持旧行为；preview / AI scoring 基于 clone，即使 normalize clone 也不得触发 hook 或影响真实单位。如果当前 shield state 可能不规范，投影 helper 必须用纯读方式解释：

```text
effectiveShieldHp = target.HasShield() ? max(target.current_shield_hp, 0) : 0
shieldEfficiency = input.ShieldAbsorptionPercent / 100.0
projectedShieldAbsorbed = min(resolvedDamage, ceil(effectiveShieldHp * shieldEfficiency))
projectedShieldDrain = ceil(projectedShieldAbsorbed / shieldEfficiency)
projectedHpDamageAfterShield = max(resolvedDamage - projectedShieldAbsorbed, 0)
projectedHpBeforeDeathPrevention = target.current_hp - projectedHpDamageAfterShield
wouldEnterDeathResolution = projectedHpDamageAfterShield > 0
    && projectedHpBeforeDeathPrevention <= input.MinHpAfterDamage
```

实际写入只发生在 `ApplyProjectedShieldMutation()`，并且必须使用 projection 里的 `ProjectedShieldDrain` / `ShieldWouldBreak`，不能重新计算一遍。

**hook 接线**

`BattleDamageResolver` 不直接依赖 `BattleContingencySystem`。增加一个窄接口，由 `BattleRuntimeModule` 在 setup 时注入：

```csharp
internal interface IBattleDamageApplicationHook
{
    BeforeDamageResolvedResult BeforeDamageResolved(BeforeDamageResolvedContext context);
}
```

`BattleContingencySystem` 实现该接口。没有 contingency system 时 hook 为 null，伤害行为必须与现状一致。

hook 只允许在 live commit damage 路径执行，并且必须拿到 `BattleDamageHookCommitContext`。该 context 由 runtime / orchestrator 提供，包含当前 `BattleEventBatch`、自动施法执行器和结构化 report sink。`BattleContingencySystem` 可以通过 `CommitContext.AutoCastExecutor` 同步执行 release_context 中的自动法术，但不能在 hook 内绕开 batch/report sink 直接写任意战斗状态；所有可见战斗 mutation 都要走既有 effect commit 路径。preview / AI scoring / clone path 必须 suppress hook，且 `CommitContext` 为空。

只要自动施法可能改变目标或环境状态，例如加盾、治疗、位移、施加免疫、制造地形、杀死来源单位或取消 source event，hook result 必须置 `StateMayHaveChanged=true`。resolver 在继续当前 damage effect 前必须重新投影；不能只在 `ModifiedResolvedDamage` 时重算，否则自动施法造成的新盾、新 HP 或位置变化不会影响即将落地的伤害。

hook 抑制必须是输入级或 scoped 级显式状态，不得靠“target 是 clone”推断。推荐：

```csharp
internal sealed class DamageApplicationInput
{
    public bool SuppressDamageApplicationHook;
    public DamageApplicationInput WithResolvedDamage(int resolvedDamage);
}
```

**取消语义**

```text
CancelDamage=true:
    - 不扣 shield
    - 不扣 HP
    - 不触发本次 HP crossing
    - 不阻止同一技能后续 effect 继续结算
    - 已进入 release_context 的 setup 仍 consumed
    - 仅 live commit path 可产生；preview / AI scoring path 必须 suppress hook
    - 不回滚 hook 调用前已经发生的命中、豁免、减伤、状态消耗、守护次数或 AI 记录
```

取消结果要写入结构化 report，不要只写 log line。V1 推荐让 hook 通过 `BattleDamageHookCommitContext.ReportSink` 写入当前 `BattleEventBatch.report_entries`，或把 report entries 返回给 runtime 层统一 append；不要为了 contingency 重新发明战报通道。

```text
Preferred: hook / runtime append 到 BattleEventBatch.report_entries。
Fallback: AppliedDamageResult / AttackEffectResolutionResult 增加 report_entries 数组。
```

只有在当前调用栈确实拿不到 batch/report sink 时才使用 fallback；一旦给 result 增加数组字段，必须同步更新所有现有 result appender，避免只读取单个 `report_entry`。

**修改伤害语义**

```text
ModifiedResolvedDamage:
    - 只改当前 damage effect 的 resolved damage
    - 必须重算 projection
    - 不改变原 skill def / effect def
    - 不跳过命中、豁免、抗性、免疫等前置步骤
```

`fatal_damage_incoming` 的投影判定发生在死亡预防、death_ward、last stand、fatal trait 等最终落地逻辑之前。它读取的是 `WouldBeFatalBeforeDeathPrevention`，因此“致死前闪现 / 护盾 / 治疗”有机会阻止本次 HP 写入；若没有进入 release_context，后续死亡预防机制仍按原顺序执行。

**必须覆盖的测试**

```text
tests/battle_runtime/rules/run_damage_application_projection_regression.cs
    - 无 hook 时，新 projection 路径与旧行为一致
    - shield_absorption_percent=50/100 时，投影值与实际扣盾扣血一致
    - CancelDamage 不扣盾、不扣 HP、返回 hp_damage=0
    - ModifiedResolvedDamage 后重新投影，shield drain 不使用旧值
    - 自动施法加盾 / 治疗 / 位移后，StateMayHaveChanged 触发重新投影
    - preview/scoring path suppress hook，不触发 consumed / cancel
    - death_ward / fatal trait / last stand mastery / min_hp_after_damage 语义不变

tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs
    - hook null 行为不变
    - hook cancel 不中断后续 effect
    - hook 产生 report_entries，且 ReportSink / batch append 路径能被 headless snapshot 读到
    - damage=0 时不误触发 on-hit/status/mastery
```

#### J3. 实施顺序

```text
1. 先做内容与持久 schema：
   ContingencyAutomationDef / stored spell allowlist / parameter binding schema / material cost schema
   PartyMemberState.contingency_matrix_setups exact schema
   state parser 只校验结构；content validator 在 catalog 加载后校验内容。
2. 再做 effective MP API：
   AttributeSourceContext.reserved_mp_max
   AttributeService.BuildSnapshot() 写 mp_max_unreserved / reserved_mp_max / effective mp_max
   所有 MP 恢复、刷新、进战、战后 clamp 路径使用 effective mp max。
3. 再做战后提交防线：
   CharacterManagementModule.CommitContingencyConsumedSetups(...)
   IBattleRuntimeCharacterGateway 对应方法
   BattleRuntimeModule.EndBattle() 返回可失败结果
   GameRuntimeFacade.FinalizeBattleResolution() 在 persist/flush 失败时恢复结算前内存快照。
4. 再做战斗外 setup 编辑 / 清除 / headless 命令：
   保存预设、编辑预设、清除充能、查询状态
   战斗外 UI 与 headless 命令都必须落地；headless 不能替代玩家 UI。
   输出稳定 code / reason_id / charged / reserved_mp_max / material quantity 字段，不依赖中文日志断言。
5. 再做 J1：
   PartyContingencySetupService charge transaction
   quantity-aware 仓库 API
   失败恢复仓库、setup、current_mp 和 runtime command snapshot。
6. 再做 battle-local instance / release_context：
   从 active member charged setup 建 sidecar
   OnBattleConfirmed / owner_turn_started 的最小可测闭环
   consumed overlay 刷新 owner 属性，但不提前写 PartyMemberState。
7. 再做 J2：
   抽 damage projection helper + hook suppress，hook 为空时 live/preview 行为完全一致
   接入 BeforeDamageResolved commit context、report sink 和 StateMayHaveChanged 重新投影。
8. 最后接入完整 BattleContingencySystem、AutoCastRequest、origin 抑制和 damage/status/position/spell hooks。
```

J2 的无 hook 投影重构可以作为独立安全重构更早执行，但它不能替代 schema、effective MP、EndBattle 写回和 content automation 这些前置条件。内部实现仍可先用 `combat_started` / `owner_turn_started` 建立非伤害闭环来降低调试风险，但这不是 V1 可发布范围；V1 发布门槛是所有玩家可选触发器、自动施法 origin 抑制、伤害/status/position/spell hooks、战斗外 UI 和 headless 命令全部完成并通过回归。

`docs/design/project_context_units.md` 当前仍可作为装载索引使用；这次只是 discussion 文档细化，不需要更新上下文单元。等实际新增 `PartyContingencySetupService` / `BattleContingencySystem` 文件后，再把相关 read set 补进 CU-11 / CU-12 / CU-15 / CU-16。

---

## 22. V1 不做与延期项

本节只保留仍然有效的范围边界。已经被正文和第 21 节裁决吸收的问题不再重复列为历史意见。

### V1 明确不做

```text
1. 不做旧存档兼容、字段默认补齐、alias table、semantic hash、技能 ID 自动迁移或旧 payload fallback。
2. 不做自然过期、维护费、charged_at_world_step、过期时间、world_step 哨兵值或 expired 状态。
3. 不允许战斗中存档；不持久化 battle-local ContingencyInstance、release_context、triggering/releasing queue。
4. 不做战斗内手动触发、战斗内手动清除或战斗内解除；清除只允许战斗外 UI / headless 命令。
5. 不开放 bound_ally / 队友绑定；V1 严格自用，owner 就是 caster。
6. 每名玩家角色同一时间只能有一个 charged setup；不做第二矩阵位或多 active 矩阵。
7. 不做敌人、召唤物或 boss 的持久 charged setup；它们不读取 PartyMemberState.contingency_matrix_setups。
8. 不开放 summon 作为 stored spell；extra_action、retrigger_contingency、resurrection、permanent_creation、complex_manual_target 默认禁止。
9. 不支持 fixed_cell 目标解析器；不保存 battle-local 坐标、路径、格子列表或复杂选点结果。
10. 不把 targeted_by_spell 单独作为 V1 trigger；合并进 affected_by_spell。
11. 不做 round_started、manual_keyword、entered_dangerous_tile、非战斗触发或世界事件触发。
12. 不做专注机制，不保存专注字段，不校验连锁应急术与专注冲突。
13. 不新增通用 BattleEventDispatcher / 战斗事件总线。
14. 不新增通用 party-wide transaction framework；充能事务由 PartyContingencySetupService 自己做窄快照和恢复。
15. 不新增 matrix_fallback_policy；进入 release_context 后即消耗，不因全部 stored spell 无效而返还。
16. 不做爆发释放的原子回滚；前一个 stored spell 成功、后一个失败时，已成功效果保留。
17. 不做配置“试射”或战斗模拟模式；V1 只提供配置预览、风险提示和结构化 headless 断言。
18. parameter_bindings 不允许 Dictionary value 或任意嵌套结构；V1 只允许 bool、int、float、String/StringName、Array[StringName]。
19. 不新增 damage_event_group / separate_damage_events 等技能语义字段；damage_event_id 按单个 damage effect 对单个 target 生成。
20. 普通解除魔法不能摧毁或解除连锁应急术；V1 只支持反魔法 / 专门压制作为临时压制来源。
```

### 延期评估项

```text
1. 敌人 / boss 连锁应急术：
   未来只能通过 enemy template、EnemyAiBrainDef 或 special profile 预装 battle-local profile/template；不走玩家宝石和 MP 封存持久成本。

2. 多矩阵 boss / 第二矩阵位：
   未来作为单独机制设计，不通过 max_active_per_caster 隐式放宽。

3. 召唤类 stored spell：
   若以后开放，必须由高等级显式解锁，使用高负载，并重新审查召唤物事件归因。

4. 专门破阵 / 裂解 / 永久摧毁：
   未来单独定义摧毁效果、抵抗、日志和材料不返还规则；不复用普通解除魔法。

5. 护盾相关触发器：
   若以后需要响应护盾破裂或护盾损耗，新增 shield_break_incoming / shield_damage_percent，不混入 HP 保命触发。

6. 非战斗触发和危险地形触发：
   需要完整世界事件事实、危险地形语义和 runtime hook 后再评估。

7. 更复杂的 parameter binding：
   若某些技能确实需要多字段模式参数，新增 typed 子结构和专门校验器后再开放。

8. 仓库 API 泛化范围：
   V1 必须新增 contingency 所需的 quantity-aware typed warehouse API；是否把它进一步扩展为通用 UI/其它系统的批量交易 API，留到以后评估。

9. 战斗中存档：
   只有在 battle-local instance、release_context、source_event_facts、damage_event_id、队列和 overlay 都有完整序列化方案后，才能重新评估。

10. 技能 ID 重命名：
    已发布 skill_id 需要重命名时，必须作为显式数据迁移单独确认；不能由连锁应急术系统内置兼容逻辑自动处理。
```
