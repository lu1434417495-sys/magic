# 帝国战争十轮战役人物可直接配置清单

> 把 `docs/design/quests/quest_mechanics_full_200.md` 第一部分的阵营人物细化到可直接落进 Godot `.tres` 配置的程度。本文档只覆盖 **Q101–Q125 帝国战争十轮战役** 中出现的人物与兵种身份。

---

## 1. 目标与范围

### 1.1 目标
- 为帝国战争线的核心人物建立 **可直接配置的身份定义**，包括：
  - 有名有姓的 NPC（皇帝、公爵、和谈使者、走私商、第三方守望者等）
  - 敌方精英/首领模板（私掠舰队司令、先锋将军、政变领袖等）
  - 通用兵种身份（霜铠师、永冻院法师、流星团、七塔元素法师等）
- 给出每个角色对应的 **代码资源 ID、复用模板、新建资源清单**，便于后续直接生成 `.tres` 文件。

### 1.2 范围
- 覆盖任务：**Q101 征兵令** 到 **Q125 永恒者之谜**。
- 不覆盖主世界任务（Q001–Q100）、种族线、龙线、神线、不死线。
- 不实现新系统（阵营选择 UI、战争进度条、分支对话树等），只把人物映射到 **当前代码已支持** 的资源形态：`EnemyTemplateDef`、`FacilityNpcConfig`、`TraitDef`、`SkillDef`。
- 对于设计稿中超出当前 runtime 的机制（如阵营标签、战争进度、多结局），在文档中标注为 **“需后续系统扩展”**，并给出当前可行的替代落点。

---

## 2. 代码约束摘要

本清单基于对当前代码系统的探索，关键约束如下：

| 系统 | 当前能力 | 对人物细化的影响 |
|---|---|---|
| 任务系统 (`QuestDef`) | 仅支持三种目标：`defeat_enemy`、`submit_item`、`settlement_action` | 人物“选择”必须通过 **接取不同任务/提交不同物品/与不同 settlement_action 交互** 间接表达 |
| 敌人定义 (`EnemyTemplateDef`) | 完全数据化：`template_id`、`brain_id`、`skill_ids`、`attribute_overrides`、`trait_ids` 等 | 所有敌方人物都可以配置为 EnemyTemplateDef，无需硬编码 |
| NPC 定义 (`FacilityNpcConfig`) | 据点服务 NPC：`npc_id`、`display_name`、`service_type`、`interaction_script_id` | 有名 NPC 都落为据点 NPC；任务对话通过 `QuestDef.accept_dialogue_text` 表达 |
| AI 大脑 (`EnemyAiBrainDef`) | 状态机 + transition rule + generation slot |  reuse 现有 `frontline_bulwark`、`ranged_archer`、`healer_controller`、`mage_controller` 即可覆盖大部分人物 |
| 特性 (`TraitDef`) | 被动标记/抗性/属性修正 | 用新建 faction/identity trait 表达“霜铠师纪律”“流星团机动”等阵营特色 |
| 持久化 | 无阵营/战争进度/称号字段 | 阵营选择、战争进度、多结局需后续扩展；当前可用 **quest 完成状态 + settlement reputation + 任务奖励差异** 模拟 |

---

## 3. ID 命名约定

统一前缀，避免与现有 `wolf_*`、`mist_*` 冲突，同时保持可读性。

| 类型 | 命名模式 | 示例 |
|---|---|---|
| 帝国 NPC | `npc_empire_<role>` | `npc_empire_vlakis_iii` |
| 联邦 NPC | `npc_federation_<role>` | `npc_federation_council_herald` |
| 第三方/中立 NPC | `npc_ashen_<role>` / `npc_neutral_<role>` | `npc_ashen_ember_watcher` |
| 敌方模板（帝国） | `empire_<unit>[_<variant>]` | `empire_frost_legionnaire` |
| 敌方模板（联邦） | `federation_<unit>[_<variant>]` | `federation_meteor_rider` |
| 敌方模板（第三方） | `ashen_<unit>[_<variant>]` | `ashen_balancer` |
| 阵营特性 | `faction_<faction>_<theme>` | `faction_empire_frost_discipline` |
| 据点交互 ID | `service_<location>_<action>` / `npc_<role>_dialogue` | `service_empire_throne`、`npc_duke_manor` |

---

## 4. 核心人物档案

每位人物包含：
- **叙事身份**：一句话定位、动机、在战役中的作用。
- **任务出场**：出现的 quest_id（按设计稿暂定）。
- **代码形态**：是 `EnemyTemplateDef`、`FacilityNpcConfig`，还是两者兼有。
- **具体配置**：可直接填进 `.tres` 的字段值。
- **复用/新建资源**：哪些用现成，哪些需要新增。

---

### 4.1 弗拉基斯三世 / 永恒者（Emperor Vlakis III, the Eternal）

#### 叙事身份
霜烬帝国皇帝，通过“记忆之棺”继承历代皇帝记忆，已统治两百年。战役最终抉择的核心人物：玩家可选择支持他、揭露他、刺杀他或取代他。

#### 任务出场
- `main_imperial_edict`（Q115 皇帝的密令）—— 任务终点 NPC
- `main_eternal_mystery`（Q125 永恒者之谜）—— 最终对话与结局分支

#### 代码形态
**`FacilityNpcConfig`（据点 NPC）**。皇帝不进入战斗，只在密室中作为 settlement_action 交互对象。

#### 具体配置

```ini
; data/configs/world_map/shared/main_world_default_settlement_bundle.tres 或专用皇城据点
[npc_empire_vlakis_iii]
npc_id = "npc_empire_vlakis_iii"
display_name = "弗拉基斯三世"
service_type = "剧情"
interaction_script_id = "service_empire_throne"
local_slot_id = "throne_room"
```

#### 任务配置建议
由于当前无分支对话树，最终抉择通过 **多个互斥的尾任务** 表达：

| 结局意向 | QuestDef 配置思路 |
|---|---|
| 支持永恒者 | `main_eternal_mystery_support`：完成条件 `settlement_action(service_empire_throne, 1)`，奖励 `imperial_crown` |
| 揭露真相 | `main_eternal_mystery_expose`：完成条件 `settlement_action(service_empire_throne, 1)`，奖励不同 |
| 刺杀/摧毁记忆之棺 | `main_eternal_mystery_assassinate`：完成条件 `submit_item(calamity_shard, 5)` + `settlement_action(service_empire_throne, 1)` |
| 成为永恒者 | `main_eternal_mystery_usurp`：接取要求 `quest_completed(main_imperial_edict)` + 拥有 `calamity_shard`×5 |
| 新帝国线 | `main_eternal_mystery_renew`：接取要求完成“第三方外交线”相关任务 |

> **系统扩展备注**：真正互斥的“单选题”需要后续在 `QuestState` 或 `PartyState` 增加 `chosen_ending_id` 字段，当前用 **多任务 + 接取条件** 模拟。

#### 复用/新建资源
- **新建**：`npc_empire_vlakis_iii` 的 `FacilityNpcConfig` 子资源
- **新建交互 ID**：`service_empire_throne`（若不走通用 `service_contract_board`）
- **复用**：最终奖励饰品用现有 `ItemDef` 框架扩展

---

### 4.2 第三公爵（Third Duke）

#### 叙事身份
帝国贵族，被指控叛国。真相是他与灰烬交界的“余火守望者”交易，用三千活人换界火碎片，只为治愈得了绝症的女儿。玩家的处理方式会开启“第三方隐藏线”。

#### 任务出场
- `main_traitor_duke`（Q108 叛国者）—— 任务终点 NPC

#### 代码形态
**`FacilityNpcConfig`（据点 NPC）**。击败其卫队后，在书房中作为 settlement_action 交互对象。

#### 具体配置

```ini
[npc_empire_third_duke]
npc_id = "npc_empire_third_duke"
display_name = "第三公爵"
service_type = "剧情"
interaction_script_id = "npc_duke_manor"
local_slot_id = "duke_study"
```

#### 任务配置建议
把 Q108 的三种处理结果拆为 **完成后的分支任务奖励/解锁**：

| 玩家选择 | 任务后续 |
|---|---|
| A. 逮捕 | 直接完成 `main_traitor_duke`，奖励帝国声望（待系统扩展），解锁后续帝国任务 |
| B. 处决 | 直接完成 `main_traitor_duke`，奖励不同，后续触发公爵家族追杀（用新任务/遭遇表达） |
| C. 听他说 | 不直接完成任务，而是接取新任务 `main_traitor_duke_truth`（见下文详细设计） |

> **系统扩展备注**：当前 `QuestDef` 无分支选择字段，需用 **任务链替换** 实现。即选择 C 后，原 `main_traitor_duke` 不再推进，玩家接取新任务 `main_traitor_duke_truth`。

#### `main_traitor_duke_truth` 详细设计

**剧情背景**
公爵女儿艾莲诺患的是罕见的“血蚀症”，传统医术无法治愈。余火守望者提出用界火碎片续命，代价是三千活人。公爵在绝望中接受，但被帝国发现后冠以叛国罪。玩家选择“听他说”后，决定从余火守望者手中夺回契约，切断公爵与守望者的绑定，从而让公爵转为内应。

**任务流程**
1. 在公爵书房接取任务。
2. 进入 19×11 `canyon` 地图“灰鸦峡谷”，中央河流分隔两岸。
3. 玩家从南岸出发，需渡河到达北岸灰鸦营地。
4. 清理北岸营地：击败 10 名 `ashen_cultist`。
5. 击败营地指挥官 `ashen_ember_warden` ×1。
6. 进入祭坛区：击败契约守护者 `ashen_ember_warden` ×1 + `mist_beast` ×2 + 仆从 ×2。
7. 在祭坛 `objective_marker` 处提交 `sealed_dispatch`（封印密契）。
8. 返回公爵庄园复命；公爵成为内应，解锁第三方外交线。

**战场设计**
- 地图：19×11 `canyon`，中央河流将地图分为南北两岸。
- 地形：南岸为玩家出生点（森林/陆地），北岸为灰鸦营地（陆地+祭坛区），河流需游泳或绕行浅滩。
- 敌人：
  - 营地外围：`ashen_cultist` ×4 巡逻
  - 营地内部：`ashen_cultist` ×4 + `ashen_ember_warden` ×1
  - 祭坛区：`ashen_ember_warden` ×1（契约守护者）+ `mist_beast` ×2 + `ashen_cultist` ×2
- 增援机制：若触发警报，每 2 回合从北岸边缘增援 `ashen_cultist` ×2，共 3 波。
- 目标点：祭坛区放置 `objective_marker` prop，击败契约守护者后可交互提交 `sealed_dispatch`。
- 失败条件：全队阵亡。

**复用/新增资源**
- **新建敌方模板**：`ashen_cultist`（基于 `cultist_acolyte`）、`ashen_ember_warden`（基于 `mist_weaver`）
- **复用敌方模板**：`mist_beast`
- **任务目标**：`defeat_enemy(ashen_cultist, 10)`、`defeat_enemy(ashen_ember_warden, 2)`、`defeat_enemy(mist_beast, 2)`、`submit_item(sealed_dispatch, 1)`

#### 复用/新建资源
- **新建**：`npc_empire_third_duke`
- **新建交互 ID**：`npc_duke_manor`
- **新建任务**：`main_traitor_duke_truth`（替代 C 路线）

---

### 4.3 公爵之女艾莲诺（Duke's Daughter Elinor）

#### 叙事身份
第三公爵的女儿，身患绝症。她是公爵叛国的唯一动机，也是玩家选择“治愈替代方案”的关键触发点。

#### 任务出场
- `main_traitor_duke_truth`（Q108 C 路线后续）

#### 代码形态
**`FacilityNpcConfig`（据点 NPC）**。卧床不起的剧情 NPC，无战斗。

#### 具体配置

```ini
[npc_empire_duke_daughter]
npc_id = "npc_empire_duke_daughter"
display_name = "艾莲诺"
service_type = "剧情"
interaction_script_id = "npc_duke_manor"
local_slot_id = "duke_bedroom"
```

#### 复用/新建资源
- **新建**：`npc_empire_duke_daughter`
- **复用交互 ID**：`npc_duke_manor`

---

### 4.4 私掠舰队司令（Privateer Fleet Commander）

#### 叙事身份
星陨联邦私掠舰队指挥官，Q106 潜入/斩首路线的可选 Boss。擅长“预见”玩家下回合技能类型并提前获得抗性，但每 3 回合会“计算过载”。

#### 任务出场
- `main_fleet_assassination`（Q106 联邦路线）

#### 代码形态
**`EnemyTemplateDef`**（精英法师）。基于 `mist_weaver` 扩展，但技能组偏向“预见/反制”。

#### 具体配置

```ini
; data/configs/enemies/templates/federation_privateer_commander.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_privateer_commander"
display_name = "私掠舰队司令"
battle_sprite_texture = ExtResource("mist_weaver_sprite")  ; 复用雾沼织咒者贴图，后续可换
brain_id = &"healer_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"federation", &"mage", &"commander"])
attack_equipment_item_id = &"watchman_mace"
base_attribute_overrides = {
    &"strength": 4,
    &"agility": 6,
    &"constitution": 6,
    &"perception": 7,
    &"intelligence": 10,
    &"willpower": 8,
}
skill_ids = Array[StringName]([&"mage_ice_lance", &"mage_glacial_prison", &"mage_temporal_rewind", &"mage_blink"])
skill_level_map = {
    &"mage_ice_lance": 4,
    &"mage_glacial_prison": 4,
    &"mage_temporal_rewind": 4,
    &"mage_blink": 3,
}
target_rank = &"elite"
attribute_overrides = {
    &"hp_max": 38,
    &"mp_max": 160,
    &"action_points": 2,
    &"attack_bonus": 7,
    &"deflection_bonus": 5,
}
```

#### “预见”机制落地说明
设计稿中的“预见”需要新代码。当前可用以下折中方案：
- 给司令一个 **被动 trait**：`federation_privateer_commander_precognition`，提供全伤害抗性 +2 或 `damage_resistances` 条目。
- 每 3 回合的“计算过载”用 **技能 CD/状态效果** 模拟：给司令一个自 debuff trait，每 3 回合触发 `silence_orb` 或清空抗性。
- 精确到“玩家最可能使用的技能类型”需要新增 AI 评分逻辑，属于后续扩展。

#### 复用/新建资源
- **新建模板**：`federation_privateer_commander.tres`
- **复用 brain**：`healer_controller`
- **新建/复用 trait**：`federation_privateer_commander_precognition`（建议新建，也可用 `damage_resistance` 占位）
- **复用贴图**：`mist_weaver` 贴图（后续换联邦指挥官专属贴图）

---

### 4.5 联邦先锋将军（Federation Vanguard General）

#### 叙事身份
联邦陆军先锋指挥官，Q105 要塞之围中作为第八波次登场，骑乘 `wolf_alpha`。重甲骑兵领袖，代表联邦地面精锐。

#### 任务出场
- `main_frost_fortress_siege`（Q105 要塞之围）

#### 代码形态
**`EnemyTemplateDef`**（精英近战）。由于当前没有人类骑兵模板，基于 `wolf_vanguard` 扩展，改名并加联邦标签与精英技能。

#### 具体配置

```ini
; data/configs/enemies/templates/federation_vanguard_general.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_vanguard_general"
display_name = "联邦先锋将军"
battle_sprite_texture = ExtResource("wolf_alpha_sprite")  ; 骑乘状态复用 wolf_alpha 贴图
brain_id = &"frontline_bulwark"
initial_state_id = &"engage"
action_threshold = 40
tags = Array[StringName]([&"humanoid", &"federation", &"cavalry", &"elite"])
attack_equipment_item_id = &"militia_spear"  ; 或新建骑兵长枪
base_attribute_overrides = {
    &"strength": 9,
    &"agility": 6,
    &"constitution": 8,
    &"perception": 6,
    &"intelligence": 4,
    &"willpower": 6,
}
skill_ids = Array[StringName]([&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard_break"])
target_rank = &"elite"
attribute_overrides = {
    &"hp_max": 55,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 8,
}
```

> **备注**：将军“骑乘 wolf_alpha”在当前系统中可以表达为：战斗中有两个单位——将军本人（`federation_vanguard_general`）+ 坐骑（`wolf_alpha`），或简化为一个精英单位。推荐后者以减少复杂度。

#### 复用/新建资源
- **新建模板**：`federation_vanguard_general.tres`
- **复用 brain**：`frontline_bulwark`
- **复用贴图**：`wolf_alpha_board.png`（临时）
- **复用/新建武器**：`militia_spear` 或新建 `federation_lance`

---


### 4.6 政变领袖 / 联邦前魔法顾问（Coup Leader / Former Magic Advisor）

#### 叙事身份
星陨联邦前魔法顾问，Q116 议会政变的发起者。擅长“政治幻术”制造镜像，并能短暂操控友方 NPC 阵营。

#### 任务出场
- `main_council_coup`（Q116 议会政变）

#### 代码形态
**`EnemyTemplateDef`**（Boss 法师）。基于 `mist_weaver`，增加镜像相关技能。

#### 具体配置

```ini
; data/configs/enemies/templates/federation_coup_leader.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_coup_leader"
display_name = "政变领袖·前魔法顾问"
battle_sprite_texture = ExtResource("mist_weaver_sprite")
brain_id = &"healer_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"federation", &"mage", &"boss"])
attack_equipment_item_id = &"watchman_mace"
base_attribute_overrides = {
    &"strength": 4,
    &"agility": 6,
    &"constitution": 7,
    &"perception": 7,
    &"intelligence": 11,
    &"willpower": 9,
}
skill_ids = Array[StringName]([&"mage_ice_lance", &"mage_glacial_prison", &"mage_temporal_rewind", &"mage_mirror_image"])
skill_level_map = {
    &"mage_ice_lance": 4,
    &"mage_glacial_prison": 4,
    &"mage_temporal_rewind": 4,
    &"mage_mirror_image": 3,
}
target_rank = &"boss"
attribute_overrides = {
    &"hp_max": 60,
    &"mp_max": 200,
    &"action_points": 2,
    &"attack_bonus": 8,
    &"deflection_bonus": 6,
}
```

#### “镜像 / 阵营操控”机制落地说明
- **镜像**：复用 `mage_mirror_image` 技能，召唤 1HP 镜像。如需“镜像复制本体下一个法术”，需要新建技能或扩展 `mage_mirror_image` 的 combat effect，属于后续内容工作。
- **阵营操控**：当前无友方阵营切换机制，可用 **对友方单位释放控制/混乱状态** 模拟，例如 `mage_confusion_wave` 或 `mage_mass_hypnosis`。

#### 复用/新建资源
- **新建模板**：`federation_coup_leader.tres`
- **复用 brain**：`healer_controller`
- **复用/新建技能**：`mage_mirror_image`（已存在，可复用）

---

### 4.7 和谈使者（Peace Envoy）

#### 叙事身份
盲人老太婆，Q110 护送目标。必须存活到达中立区，是和谈能否成功的关键。自身无战斗能力，每回合自动向目标移动。

#### 任务出场
- `main_peace_envoy`（Q110 和谈使者）

#### 代码形态
**临时队伍成员（战斗内）+ `FacilityNpcConfig`（剧情 NPC）**。战斗中将使者作为临时可控单位加入玩家队伍；战斗外作为剧情 NPC 存在。

#### 具体配置

**战斗内临时成员：**

```ini
; data/configs/enemies/templates/neutral_peace_envoy.tres
; 本模板用于 encounter roster 生成临时友方单位，实际操控权交给玩家
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"neutral_peace_envoy"
display_name = "和谈使者"
battle_sprite_texture = ExtResource("civilian_sprite")  ; 需 civilian 贴图，暂用占位
brain_id = &"civilian_escort"  ; 备用 AI：玩家未操控时向目标点移动
initial_state_id = &"escort"
action_threshold = 9999
tags = Array[StringName]([&"humanoid", &"neutral", &"civilian", &"escort_target"])
base_attribute_overrides = {
    &"strength": 3,
    &"agility": 4,
    &"constitution": 5,
    &"perception": 8,
    &"intelligence": 6,
    &"willpower": 7,
}
skill_ids = Array[StringName]([])
attribute_overrides = {
    &"hp_max": 25,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 0,
}
```

**据点 NPC：**

```ini
[npc_neutral_peace_envoy]
npc_id = "npc_neutral_peace_envoy"
display_name = "和谈使者"
service_type = "剧情"
interaction_script_id = "service_contract_board"
local_slot_id = "embassy"
```

#### “护送”机制落地说明
- **临时队伍成员**：Q110 开战时，通过 encounter roster 的 ally slot 将 `neutral_peace_envoy` 加入玩家可操控单位列表。战斗结束后从队伍中移除。
- **玩家操控**：每回合玩家可消耗使者的 2AP 进行移动；使者无攻击技能，不能攻击或使用物品。
- **敌人目标优先级**：刺客敌人（`federation_meteor_rider`、`empire_frost_legionnaire`、`ashen_balancer`）通过 brain 目标选择器优先攻击 `escort_target` 标签单位。
- **失败条件**：使者 HP ≤ 0 时任务失败，在 battle finalization 中回写 `QuestState.status_id = failed`。
- **搀扶机制**：当前无专门“搀扶”动作，可简化为：玩家单位与使者相邻时，使者本回合移动力 +1（通过 aura trait 实现）。

#### 复用/新建资源
- **新建模板**：`neutral_peace_envoy.tres`
- **新建 brain**：`civilian_escort`（备用 AI，玩家未操控时自动向出口移动）
- **新建 NPC**：`npc_neutral_peace_envoy`
- **可选新建 aura trait**：`peace_envoy_escort_aura`（相邻友方使使者移动力 +1）

---

### 4.8 走私商 / 商人公会代理人（Smuggler / Merchant Guild Agent）

#### 叙事身份
同时向帝国和联邦出售裂隙武器的商人。Q114 中玩家可曝光、要挟或加入他；Q019 走私者巢穴中也出现同类角色。

#### 任务出场
- `main_weapon_smuggling`（Q114 武器走私）
- `side_smugglers_den`（Q019 走私者巢穴，主世界任务，风格一致可复用）

#### 代码形态
**`FacilityNpcConfig`（交易/剧情 NPC）**。

#### 具体配置

```ini
[npc_merchant_smuggler_guild]
npc_id = "npc_merchant_smuggler_guild"
display_name = "商人公会代理人"
service_type = "交易"
interaction_script_id = "service_local_trade"
local_slot_id = "smuggler_den"
```

#### 任务配置建议
把 Q114 的三种处理结果表达为 **不同任务奖励 + 后续任务解锁**：

| 选择 | 当前可落点 |
|---|---|
| 曝光 | 完成任务 `main_weapon_smuggling_expose`，奖励金币，解锁后续遭遇“被追杀的商人” |
| 利用 | 完成任务 `main_weapon_smuggling_leverage`，奖励情报类 trait 或后续任务难度降低 |
| 加入 | 生成新任务 `main_weapon_smuggling_partner`，每轮战役给予金币奖励（用 repeatable quest 模拟） |

> **系统扩展备注**：“每轮战役 +200 金”需要周期性奖励系统，当前可用 **repeatable quest**（`is_repeatable = true`）或 **settlement action 奖励** 模拟。

#### 复用/新建资源
- **新建 NPC**：`npc_merchant_smuggler_guild`
- **复用交互 ID**：`service_local_trade`
- **新建任务**（可选）：`main_weapon_smuggling_expose`、`main_weapon_smuggling_leverage`、`main_weapon_smuggling_partner`

---

### 4.9 余火守望者（Ember Watcher）

#### 叙事身份
灰烬交界势力代表，向第三公爵提出“三千活人换界火碎片”的交易。是开启“第三方外交线”的关键 NPC。

#### 任务出场
- `main_traitor_duke_truth`（Q108 C 路线后续）
- `main_ashen_diplomacy`（第三方外交线，需新增任务链）

#### 代码形态
**`FacilityNpcConfig`（据点 NPC）**。

#### 具体配置

```ini
[npc_ashen_ember_watcher]
npc_id = "npc_ashen_ember_watcher"
display_name = "余火守望者"
service_type = "剧情"
interaction_script_id = "service_contract_board"
local_slot_id = "ashen_rift"
```

#### 复用/新建资源
- **新建 NPC**：`npc_ashen_ember_watcher`

---

### 4.10 春泉村熟人（Spring Village Acquaintance）

#### 叙事身份
玩家在春泉村认识的 NPC，Q124 地下抵抗中出现。玩家可选择镇压抵抗（杀死/逮捕熟人）或放走他（熟人加入队伍助战）。

#### 任务出场
- `main_underground_resistance`（Q124 地下抵抗）

#### 代码形态
**`FacilityNpcConfig`（剧情 NPC）**。熟人不进入战斗，战斗主体始终是玩家小队。

#### 具体配置

```ini
[npc_resistance_spring_village_acquaintance]
npc_id = "npc_resistance_spring_village_acquaintance"
display_name = "春泉村的熟人"
service_type = "剧情"
interaction_script_id = "npc_underground_resistance"
local_slot_id = "resistance_hideout"
```

#### “熟人加入队伍助战”落地说明
熟人不作为战斗单位登场。玩家选择“放走他”后，通过以下方式表达后续影响：
- **剧情反馈**：Q124 任务完成对话中提及熟人安全离开。
- **奖励差异**：选择放走获得独特称号或 trait；选择镇压则无。
- **后续任务解锁**：熟人存活可解锁一条隐藏支线（由其他任务承接）。
- **后续系统扩展**：若未来实现 NPC 队友/助战系统，可再让熟人以非战斗 companion 形式出现。

#### 复用/新建资源
- **新建 NPC**：`npc_resistance_spring_village_acquaintance`
- **新建交互 ID**：`npc_underground_resistance`

---

## 5. 通用兵种模板表

设计稿中用 `wolf_vanguard`、`wolf_raider`、`mist_weaver` 等野兽模板作为“帝国/联邦士兵”的占位。为避免叙事错位，本清单为各阵营兵种建立 **身份明确的模板 ID**，通过复用现有 brain + 调整技能/属性/特性来实现。

| 阵营 | 兵种 | 模板 ID | 复用基础 | Brain | 关键技能/特性 | 出现任务 |
|---|---|---|---|---|---|---|
| 霜烬帝国 | 霜铠师（重甲步兵） | `empire_frost_legionnaire` | `wolf_vanguard` | `frontline_bulwark` | `warrior_taunt`、`warrior_guard`、`warrior_shield_bash`；特性 `faction_empire_frost_discipline` | Q101–Q125 多场 |
| 霜烬帝国 | 永冻院法师（冰霜法师） | `empire_eternal_frost_mage` | `mist_weaver` | `healer_controller` | `mage_ice_lance`、`mage_glacial_prison`、`mage_temporal_rewind`；特性 `faction_empire_frost_affinity` | Q105、Q115、Q125 |
| 霜烬帝国 | 禁卫军（皇宫精锐） | `empire_imperial_guard` | `wolf_vanguard` | `frontline_bulwark` | 高 AC/HP，`warrior_guard`、`warrior_taunt`；特性 `faction_empire_shield_wall` | Q115 |
| 星陨联邦 | 流星团轻骑兵 | `federation_meteor_rider` | `wolf_raider` | `ranged_archer` | `archer_aimed_shot`、`archer_multishot`、`charge`；特性 `faction_federation_cavalry_mobility` | Q101–Q125 多场 |
| 星陨联邦 | 七塔元素法师 | `federation_tower_elementalist` | `mist_weaver` | `mage_controller` | `mage_fireball`、`mage_chain_lightning`、`mage_ice_lance`；特性 `faction_federation_elemental_versatility` | Q105、Q116 |
| 星陨联邦 | 私掠水手 | `federation_privateer_marine` | `wolf_raider` | `melee_aggressor` 或 `ranged_archer` | `basic_attack`、`charge` | Q102、Q117 |
| 灰烬交界/第三方 | 平衡者 | `ashen_balancer` | `mist_weaver` | `healer_controller`（改目标选择器） | `mage_confusion_wave`、`mage_ice_lance`、`mage_temporal_rewind`；特性 `faction_ashen_balance` | Q110 |
| 平民/友方 | 村民/民兵 | `neutral_militia` / `neutral_villager` | `militia`（seed） | 待定 | `basic_attack` | Q109、Q010 等 |

### 5.1 霜铠师示例配置

```ini
; data/configs/enemies/templates/empire_frost_legionnaire.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"empire_frost_legionnaire"
display_name = "霜铠师"
battle_sprite_texture = ExtResource("wolf_vanguard_sprite")  ; 临时复用，后续换帝国重甲贴图
brain_id = &"frontline_bulwark"
initial_state_id = &"engage"
action_threshold = 50
tags = Array[StringName]([&"humanoid", &"empire", &"frost_legion", &"heavy_infantry"])
base_attribute_overrides = {
    &"strength": 8,
    &"agility": 4,
    &"constitution": 8,
    &"perception": 5,
    &"intelligence": 3,
    &"willpower": 6,
}
skill_ids = Array[StringName]([&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard"])
attribute_overrides = {
    &"hp_max": 48,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 6,
}
```

### 5.2 特性建议

以下特性用于表达阵营身份，需新建为 `TraitDef` `.tres`：

| 特性 ID | 显示名 | 效果建议 |
|---|---|---|
| `faction_empire_frost_discipline` | 霜铠纪律 | `damage_resistances` 增加 `cold` 抗性；相邻友方 ≥2 时 `deflection_bonus` +1 |
| `faction_empire_frost_affinity` | 永冻亲和 | 冰霜技能 MP 消耗 -10%；`damage_resistances` 增加 `cold` |
| `faction_empire_shield_wall` | 盾墙 | 相邻友方 ≥2 且同为 `empire` 标签时，AC +2 |
| `faction_federation_cavalry_mobility` | 流星机动 | 移动力 +1；首回合 `charge` 技能无 CD |
| `faction_federation_elemental_versatility` | 七塔元素 | 火/电/冰伤害 +1 |
| `faction_ashen_balance` | 平衡之秤 | 每回合自动攻击 HP 最高的一方 |

> **实现备注**：上述效果若当前 `TraitDef` 字段无法直接表达，可在实现阶段转为 `attribute_modifiers` 或 `damage_resistance_entries` 等已有字段的等价形式。

---

## 6. 新增资源清单

### 6.1 必须新建的 `FacilityNpcConfig`

| NPC ID | 显示名 | 所属任务 |
|---|---|---|
| `npc_empire_vlakis_iii` | 弗拉基斯三世 | Q115、Q125 |
| `npc_empire_third_duke` | 第三公爵 | Q108 |
| `npc_empire_duke_daughter` | 艾莲诺 | Q108 |
| `npc_neutral_peace_envoy` | 和谈使者 | Q110 |
| `npc_merchant_smuggler_guild` | 商人公会代理人 | Q114、Q019 |
| `npc_ashen_ember_watcher` | 余火守望者 | Q108、第三方线 |
| `npc_resistance_spring_village_acquaintance` | 春泉村的熟人 | Q124 |

### 6.2 必须新建的 `EnemyTemplateDef`

| 模板 ID | 显示名 | 复用基础 |
|---|---|---|
| `federation_privateer_commander` | 私掠舰队司令 | `mist_weaver` |
| `federation_vanguard_general` | 联邦先锋将军 | `wolf_vanguard` |
| `federation_coup_leader` | 政变领袖·前魔法顾问 | `mist_weaver` |
| `neutral_peace_envoy` | 和谈使者 | 新建平民模板 |
| `empire_frost_legionnaire` | 霜铠师 | `wolf_vanguard` |
| `empire_eternal_frost_mage` | 永冻院法师 | `mist_weaver` |
| `empire_imperial_guard` | 禁卫军 | `wolf_vanguard` |
| `federation_meteor_rider` | 流星团轻骑兵 | `wolf_raider` |
| `federation_tower_elementalist` | 七塔元素法师 | `mist_weaver` |
| `federation_privateer_marine` | 私掠水手 | `wolf_raider` |
| `ashen_balancer` | 平衡者 | `mist_weaver` |
| `ashen_ember_warden` | 余火守望者精英 | `mist_weaver` |
| `ashen_cultist` | 余火守望者仆从 | `cultist_acolyte` |

### 6.3 建议新建的 `EnemyAiBrainDef`

| Brain ID | 用途 | 复杂度 |
|---|---|---|
| `civilian_escort` | 和谈使者、村民等无战斗单位的自动移动 | 中 |

### 6.4 建议新建的 `TraitDef`

| Trait ID | 显示名 | 用途 | 建议字段 |
|---|---|---|---|
| `faction_empire_frost_discipline` | 霜铠纪律 | 霜铠师阵营特色 | `damage_resistance_entries`: `cold` +5；相邻友方加成需战斗规则支持 |
| `faction_empire_frost_affinity` | 永冻亲和 | 永冻院法师特色 | `damage_resistance_entries`: `cold` +10；`attribute_modifiers`: `mp_max` +10% |
| `faction_empire_shield_wall` | 盾墙 | 禁卫军盾墙 | 相邻 ≥2 名 `empire` 友方时 AC +2（需战斗规则支持） |
| `faction_federation_cavalry_mobility` | 流星机动 | 流星团机动 | `movement` +1；首回合 `charge` 无 CD（用技能/装备能力近似） |
| `faction_federation_elemental_versatility` | 七塔元素 | 七塔元素法师 | `damage_resistance_entries`: `fire`/`lightning`/`cold` +5 |
| `faction_ashen_devotion` | 余火虔信 | 余火仆从/精英 | `save_advantage_tags`: `will`；`damage_resistance_entries`: `force` +5 |
| `faction_ashen_balance` | 平衡之秤 | 平衡者 | 需新建 brain 目标选择器，攻击优势方 |
| `federation_privateer_commander_precognition` | 海战预见 | 私掠舰队司令 | `damage_resistance_entries`: 全伤害 +2；周期性“过载”用技能/状态模拟 |

### 6.6 建议新建或确认的 `ItemDef`

上文化身模板中引用了若干武器/装备 ID。若仓库中不存在，可复用现有近似装备，或新建以下条目：

| 物品 ID | 类型 | 用途 | 绑定兵种 |
|---|---|---|---|
| `empire_longsword` | 武器（单手剑） | 霜铠师主手 | `empire_frost_legionnaire` |
| `imperial_tower_sword` | 武器（单手剑） | 禁卫军主手 | `empire_imperial_guard` |
| `federation_shortbow` | 武器（短弓） | 流星团远程 | `federation_meteor_rider` |
| `federation_lance` | 武器（长柄） | 先锋将军/骑兵 | `federation_vanguard_general` |
| `cutlass` | 武器（弯刀） | 私掠水手 | `federation_privateer_marine` |
| `ashen_dagger` | 武器（匕首） | 余火仆从 | `ashen_cultist` |
| `ashen_mace` | 武器（钉头锤） | 余火精英 | `ashen_ember_warden` |
| `sealed_dispatch` | 任务物品 | 灰鸦契约 | `main_traitor_duke_truth` |

> 实现备注：若仓库中已有功能等价的武器（如 `iron_sword`、`militia_axe`、`watchman_mace`），可直接复用，避免新增物品。新建物品仅用于强化阵营视觉身份。

### 6.5 建议新建的任务 ID

为表达设计稿中的分支，建议把单一结局任务拆成多个互斥任务：

| 任务 ID | 来源 | 说明 |
|---|---|---|
| `main_traitor_duke_truth` | Q108 C 路线 | 公爵真相线，目标 `submit_item(moonfern_sample, 3)` |
| `main_eternal_mystery_support` | Q125 | 支持永恒者结局 |
| `main_eternal_mystery_expose` | Q125 | 揭露真相结局 |
| `main_eternal_mystery_assassinate` | Q125 | 刺杀结局 |
| `main_eternal_mystery_usurp` | Q125 | 成为永恒者结局 |
| `main_eternal_mystery_renew` | Q125 | 新帝国线结局 |
| `main_weapon_smuggling_expose` | Q114 | 曝光商人 |
| `main_weapon_smuggling_leverage` | Q114 | 要挟商人 |
| `main_weapon_smuggling_partner` | Q114 | 成为合伙人（repeatable 奖励） |

---

## 7. 任务-人物映射表

| 任务 | 敌方模板 | 友方/中立 NPC | 关键 settlement NPC |
|---|---|---|---|
| Q101 征兵令 | `federation_meteor_rider`×3 | 哨站民兵（`militia`） | 征兵官（可复用 `npc_command_master` 或新建 `npc_empire_recruiter`） |
| Q102 商路封锁 | `federation_privateer_marine`、`mist_harrier`、`federation_privateer_commander` | 帝国弩炮手 | — |
| Q103 谍影重重 | `federation_meteor_rider`（巡逻）、`mist_weaver`（镜卫） | — | — |
| Q105 要塞之围 | `federation_meteor_rider`、`empire_frost_legionnaire`、`federation_vanguard_general`、`wolf_shaman` | 要塞守军、冰霜法师 | — |
| Q106 刺杀行动 | `federation_privateer_commander`、`federation_privateer_marine` | — | — |
| Q107 炼金武器 | `cultist_acolyte`（裂隙研究员）、`mist_weaver`（首席研究员） | — | — |
| Q108 叛国者 | `empire_frost_legionnaire`（公爵卫队）；`main_traitor_duke_truth` 中用 `ashen_cultist`×10、`ashen_ember_warden`×2、`mist_beast`×2 | — | `npc_empire_third_duke`、`npc_empire_duke_daughter` |
| Q109 焦土政策 | `federation_meteor_rider`、`empire_frost_legionnaire` | 村民 NPC | — |
| Q110 和谈使者 | `federation_meteor_rider`、`empire_frost_legionnaire`、`ashen_balancer` | `neutral_peace_envoy`（临时可控队伍成员） | — |
| Q112 逃兵营 | `empire_frost_legionnaire`（逃兵老兵） | 平民 NPC | — |
| Q114 武器走私 | `federation_meteor_rider`（护卫） | 车队 NPC | `npc_merchant_smuggler_guild` |
| Q115 皇帝的密令 | `empire_imperial_guard`、`empire_eternal_frost_mage` | — | `npc_empire_vlakis_iii` |
| Q116 议会政变 | `federation_coup_leader`、`federation_meteor_rider`、`empire_frost_legionnaire` | 保皇/政变 NPC | — |
| Q117 海军决战 | `mist_harrier`、`wolf_alpha`、`mist_beast` | — | — |
| Q118 战俘营 | `empire_frost_legionnaire`（守卫） | 六名囚犯 NPC | — |
| Q124 地下抵抗 | `empire_frost_legionnaire`（占领军/抵抗战士） | 平民 NPC | `npc_resistance_spring_village_acquaintance` |
| Q125 永恒者之谜 | — | — | `npc_empire_vlakis_iii`（最终抉择） |

---

## 8. 落地检查清单

### 8.1 第一阶段：最小可运行集（MVP）
- [ ] 创建所有核心 NPC 的 `FacilityNpcConfig` 子资源，并放入合适的 `SettlementConfig`。
- [ ] 创建 `federation_privateer_commander`、`federation_vanguard_general`、`federation_coup_leader` 三个首领模板。
- [ ] 创建 `empire_frost_legionnaire`、`federation_meteor_rider` 两个阵营基础兵种模板。
- [ ] 把 Q101、Q105、Q106、Q108、Q115 的 `defeat_enemy` 目标从 `wolf_*` 替换为新的阵营模板 ID。
- [ ] 运行 `QuestContentValidator` 与 `EnemyContentRegistry` 校验，确保模板 → brain → skill 引用无误。

### 8.2 第二阶段：体验补全
- [ ] 创建 `neutral_peace_envoy` 模板 + `civilian_escort` brain，完成 Q110 护送机制。
- [ ] 创建阵营特性 `TraitDef`，给兵种增加身份差异。
- [ ] 拆分 Q108、Q114、Q125 的结局/分支任务，用任务链替代单选分支。
- [ ] 为最终抉择 NPC 增加专用 `interaction_script_id`（如 `service_empire_throne`）。

### 8.3 第三阶段：系统扩展（超出本文档）
- [ ] 实现阵营标签/战争进度持久化字段。
- [ ] 实现真正的分支对话树或 choice modal。
- [ ] 实现 NPC 队友/助战系统。
- [ ] 实现任务内波次/阶段切换 runtime。

---

## 9. 回归测试建议

- 使用 `tests/text_runtime/headless/run_headless_game_test_session_regression.cs` 风格的 headless 脚本：
  - 加载包含新建 enemy template 的 `GameContentCatalog`，断言 `GetEnemyTemplateDef("empire_frost_legionnaire")` 不为 null。
  - 模拟接取 `main_traitor_duke`，检查 `npc_empire_third_duke` 的 `interaction_script_id` 可达。
  - 对 `federation_privateer_commander` 进行一场最小战斗快照，确认技能列表可被 AI 正确解析。
- 不进入 battle simulation/balance 跑分（按 `AGENTS.md` 常规回归排除）。

---

## 10. 备注

- 所有 **新建 .tres 文件** 应放入对应目录：
  - `data/configs/enemies/templates/*.tres`
  - `data/configs/traits/*.tres`
  - `data/configs/world_map/shared/main_world_default_settlement_bundle.tres`（NPC 子资源）
- 贴图资源当前可复用 `wolf_*`、`mist_weaver` 等占位；美术迭代后替换 `battle_sprite_texture` 即可，不影响配置结构。
- 本文档只解决“人物身份可配置”，不解决“剧情分支系统”。若需实现真正的阵营/战争进度/多结局，应再开一份系统扩展 spec。

### 5.3 帝国兵种详细配置

#### 霜铠师 `empire_frost_legionnaire`

```ini
; data/configs/enemies/templates/empire_frost_legionnaire.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"empire_frost_legionnaire"
display_name = "霜铠师"
battle_sprite_texture = ExtResource("placeholder_human_heavy")  ; 临时占位，后续换帝国重甲贴图
brain_id = &"frontline_bulwark"
initial_state_id = &"engage"
action_threshold = 50
tags = Array[StringName]([&"humanoid", &"empire", &"frost_legion", &"heavy_infantry"])
attack_equipment_item_id = &"iron_sword"  ; 或新建 empire_longsword
base_attribute_overrides = {
    &"strength": 9,
    &"agility": 4,
    &"constitution": 9,
    &"perception": 5,
    &"intelligence": 3,
    &"willpower": 6,
}
skill_ids = Array[StringName]([&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard"])
attribute_overrides = {
    &"hp_max": 52,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 7,
}
```

> 装备说明：若 `iron_sword` 不存在，可新建 `empire_longsword`（单手剑，1d8 斩击）并绑定 `warrior_*` 技能可用性。

#### 永冻院法师 `empire_eternal_frost_mage`

```ini
; data/configs/enemies/templates/empire_eternal_frost_mage.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"empire_eternal_frost_mage"
display_name = "永冻院法师"
battle_sprite_texture = ExtResource("mist_weaver_sprite")  ; 临时占位
brain_id = &"healer_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"empire", &"frost_legion", &"mage"])
attack_equipment_item_id = &"watchman_mace"  ; 临时，后续换 staff
base_attribute_overrides = {
    &"strength": 3,
    &"agility": 5,
    &"constitution": 5,
    &"perception": 6,
    &"intelligence": 10,
    &"willpower": 8,
}
skill_ids = Array[StringName]([&"mage_ice_lance", &"mage_glacial_prison", &"mage_temporal_rewind", &"mage_frost_bolt"])
skill_level_map = {
    &"mage_ice_lance": 3,
    &"mage_glacial_prison": 3,
    &"mage_temporal_rewind": 3,
    &"mage_frost_bolt": 3,
}
attribute_overrides = {
    &"hp_max": 28,
    &"mp_max": 140,
    &"action_points": 2,
    &"attack_bonus": 6,
    &"deflection_bonus": 4,
}
```

#### 禁卫军 `empire_imperial_guard`

```ini
; data/configs/enemies/templates/empire_imperial_guard.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"empire_imperial_guard"
display_name = "禁卫军"
battle_sprite_texture = ExtResource("placeholder_human_heavy")  ; 临时占位
brain_id = &"frontline_bulwark"
initial_state_id = &"engage"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"empire", &"imperial_guard", &"heavy_infantry", &"elite"])
attack_equipment_item_id = &"iron_sword"  ; 或新建 imperial_tower_sword
target_rank = &"elite"
base_attribute_overrides = {
    &"strength": 11,
    &"agility": 4,
    &"constitution": 11,
    &"perception": 6,
    &"intelligence": 3,
    &"willpower": 7,
}
skill_ids = Array[StringName]([&"charge", &"warrior_shield_bash", &"warrior_taunt", &"warrior_guard"])
attribute_overrides = {
    &"hp_max": 70,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 9,
}
```

---

### 5.4 联邦兵种详细配置

#### 流星团轻骑兵 `federation_meteor_rider`

```ini
; data/configs/enemies/templates/federation_meteor_rider.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_meteor_rider"
display_name = "流星团轻骑兵"
battle_sprite_texture = ExtResource("placeholder_human_cavalry")  ; 临时占位
brain_id = &"ranged_archer"
initial_state_id = &"pressure"
action_threshold = 35
tags = Array[StringName]([&"humanoid", &"federation", &"meteor_cavalry", &"light_cavalry"])
attack_equipment_item_id = &"shortbow"  ; 或新建 federation_shortbow
base_attribute_overrides = {
    &"strength": 6,
    &"agility": 9,
    &"constitution": 5,
    &"perception": 7,
    &"intelligence": 4,
    &"willpower": 5,
}
skill_ids = Array[StringName]([&"charge", &"archer_aimed_shot", &"archer_multishot"])
attribute_overrides = {
    &"hp_max": 32,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 6,
}
```

#### 七塔元素法师 `federation_tower_elementalist`

```ini
; data/configs/enemies/templates/federation_tower_elementalist.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_tower_elementalist"
display_name = "七塔元素法师"
battle_sprite_texture = ExtResource("mist_weaver_sprite")  ; 临时占位
brain_id = &"mage_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"federation", &"seven_towers", &"mage"])
attack_equipment_item_id = &"watchman_mace"  ; 临时，后续换 staff
base_attribute_overrides = {
    &"strength": 3,
    &"agility": 6,
    &"constitution": 5,
    &"perception": 6,
    &"intelligence": 10,
    &"willpower": 7,
}
skill_ids = Array[StringName]([&"mage_fireball", &"mage_chain_lightning", &"mage_ice_lance", &"mage_blink"])
skill_level_map = {
    &"mage_fireball": 3,
    &"mage_chain_lightning": 3,
    &"mage_ice_lance": 3,
    &"mage_blink": 2,
}
attribute_overrides = {
    &"hp_max": 26,
    &"mp_max": 140,
    &"action_points": 2,
    &"attack_bonus": 6,
    &"deflection_bonus": 4,
}
```

#### 私掠水手 `federation_privateer_marine`

```ini
; data/configs/enemies/templates/federation_privateer_marine.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_privateer_marine"
display_name = "私掠水手"
battle_sprite_texture = ExtResource("placeholder_human_marine")  ; 临时占位
brain_id = &"melee_aggressor"
initial_state_id = &"engage"
action_threshold = 40
tags = Array[StringName]([&"humanoid", &"federation", &"privateer", &"marines"])
attack_equipment_item_id = &"militia_axe"  ; 或新建 cutlass
base_attribute_overrides = {
    &"strength": 7,
    &"agility": 6,
    &"constitution": 6,
    &"perception": 5,
    &"intelligence": 3,
    &"willpower": 5,
}
skill_ids = Array[StringName]([&"charge", &"warrior_heavy_strike", &"basic_attack"])
attribute_overrides = {
    &"hp_max": 36,
    &"mp_max": 0,
    &"action_points": 2,
    &"attack_bonus": 6,
}
```

#### 联邦随军萨满 `federation_field_shaman`

```ini
; data/configs/enemies/templates/federation_field_shaman.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"federation_field_shaman"
display_name = "联邦随军萨满"
battle_sprite_texture = ExtResource("wolf_shaman_sprite")  ; 临时复用 wolf_shaman 贴图
brain_id = &"healer_controller"
initial_state_id = &"support"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"federation", &"shaman", &"support"])
attack_equipment_item_id = &"watchman_mace"
base_attribute_overrides = {
    &"strength": 5,
    &"agility": 6,
    &"constitution": 5,
    &"perception": 6,
    &"intelligence": 7,
    &"willpower": 8,
}
skill_ids = Array[StringName]([&"mage_ice_lance", &"mage_temporal_rewind", &"mage_blood_rage_aura"])
skill_level_map = {
    &"mage_ice_lance": 3,
    &"mage_temporal_rewind": 3,
    &"mage_blood_rage_aura": 2,
}
attribute_overrides = {
    &"hp_max": 26,
    &"mp_max": 140,
    &"action_points": 2,
    &"attack_bonus": 6,
}
```

> 技能说明：`mage_blood_rage_aura` 可用现有 `mage_*` 范围 buff 技能近似，或新建一个每 3 回合全场友方伤害 +2/受伤 +1 的 aura 技能。

---

### 5.5 第三方兵种详细配置

#### 余火仆从 `ashen_cultist`

```ini
; data/configs/enemies/templates/ashen_cultist.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"ashen_cultist"
display_name = "余火仆从"
battle_sprite_texture = ExtResource("cultist_acolyte_sprite")  ; 临时复用 cultist 贴图
brain_id = &"mage_controller"
initial_state_id = &"pressure"
action_threshold = 40
tags = Array[StringName]([&"humanoid", &"ashen", &"cultist", &"ember_watcher"])
attack_equipment_item_id = &"dagger"  ; 或新建 ashen_dagger
base_attribute_overrides = {
    &"strength": 5,
    &"agility": 6,
    &"constitution": 5,
    &"perception": 6,
    &"intelligence": 7,
    &"willpower": 7,
}
skill_ids = Array[StringName]([&"mage_ember_mine", &"mage_shadow_bolt", &"basic_attack"])
attribute_overrides = {
    &"hp_max": 24,
    &"mp_max": 80,
    &"action_points": 2,
    &"attack_bonus": 5,
}
```

#### 平衡者 `ashen_balancer`

```ini
; data/configs/enemies/templates/ashen_balancer.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"ashen_balancer"
display_name = "平衡者"
battle_sprite_texture = ExtResource("mist_weaver_sprite")  ; 临时占位
brain_id = &"healer_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"ashen", &"balancer", &"ember_watcher"])
attack_equipment_item_id = &"watchman_mace"
base_attribute_overrides = {
    &"strength": 4,
    &"agility": 6,
    &"constitution": 6,
    &"perception": 7,
    &"intelligence": 9,
    &"willpower": 8,
}
skill_ids = Array[StringName]([&"mage_confusion_wave", &"mage_ice_lance", &"mage_temporal_rewind"])
attribute_overrides = {
    &"hp_max": 30,
    &"mp_max": 150,
    &"action_points": 2,
    &"attack_bonus": 6,
    &"deflection_bonus": 4,
}
```

> 目标选择器：当前 `healer_controller` 默认攻击最低 HP 敌人。实现“攻击最强一方”需要调整 brain 中的 target_selector 为 `highest_threat_enemy` 或新建 brain `ashen_balancer`。属于后续内容/AI 调整。

#### 余火精英 `ashen_ember_warden`

```ini
; data/configs/enemies/templates/ashen_ember_warden.tres
[resource]
script = ExtResource("EnemyTemplateDef")
template_id = &"ashen_ember_warden"
display_name = "余火守望者"
battle_sprite_texture = ExtResource("mist_weaver_sprite")  ; 临时占位
brain_id = &"healer_controller"
initial_state_id = &"pressure"
action_threshold = 45
tags = Array[StringName]([&"humanoid", &"ashen", &"ember_warden", &"elite"])
attack_equipment_item_id = &"watchman_mace"  ; 或新建 ashen_mace
target_rank = &"elite"
base_attribute_overrides = {
    &"strength": 5,
    &"agility": 5,
    &"constitution": 8,
    &"perception": 7,
    &"intelligence": 10,
    &"willpower": 9,
}
skill_ids = Array[StringName]([&"mage_ice_lance", &"mage_glacial_prison", &"mage_temporal_rewind", &"mage_ember_mine"])
skill_level_map = {
    &"mage_ice_lance": 4,
    &"mage_glacial_prison": 4,
    &"mage_temporal_rewind": 4,
    &"mage_ember_mine": 3,
}
attribute_overrides = {
    &"hp_max": 45,
    &"mp_max": 180,
    &"action_points": 2,
    &"attack_bonus": 7,
    &"deflection_bonus": 5,
}
```

---

### 5.6 阵营特性详细设计

以下特性为 faction identity trait，需新建 `TraitDef`：

| 特性 ID | 显示名 | 建议字段 | 备注 |
|---|---|---|---|
| `faction_empire_frost_discipline` | 霜铠纪律 | `damage_resistance_entries`: `cold` +5；`attribute_modifiers`: 相邻 ≥2 友方时 `deflection_bonus` +1 | 相邻判定需战斗规则支持，若不可行则仅保留 cold 抗性 |
| `faction_empire_frost_affinity` | 永冻亲和 | `damage_resistance_entries`: `cold` +10；`attribute_modifiers`: `mp_max` +10% | MP 消耗减免在 TraitDef 中无直接字段，可用 MP 上限加成近似 |
| `faction_empire_shield_wall` | 盾墙 | `attribute_modifiers`: 相邻 ≥2 名 `empire` 标签友方时 `armor_class` +2 | 相邻判定需战斗规则支持 |
| `faction_federation_cavalry_mobility` | 流星机动 | `attribute_modifiers`: `movement` +1；首回合 `charge` 无 CD 用技能初始冷却字段或装备能力近似 | 若技能 CD 不可调，则改为 `action_points` +0 或移动力加成 |
| `faction_federation_elemental_versatility` | 七塔元素 | `damage_resistance_entries`: `fire` +5、`lightning` +5、`cold` +5 | 元素伤害 +1 用抗性/伤害加成近似 |
| `faction_ashen_devotion` | 余火虔信 | `save_advantage_tags`: `will`；`damage_resistance_entries`: `force` +5 | 表达被界火能量加持的意志与抗性 |
| `faction_ashen_balance` | 平衡之秤 | 无直接字段，需新建 brain 目标选择器 | 设计目标：每回合评估双方 HP 总和，攻击优势方 |
| `federation_privateer_commander_precognition` | 海战预见 | `damage_resistance_entries`: 全伤害类型 +2；周期性“过载”用技能/状态效果模拟 | 精确预见需新 AI 逻辑 |

---

## 11. 帝国战争线任务战斗级设计（Q101–Q125）

> 本节将帝国战争十轮战役设计稿中出现的全部任务细化到可直接配置的战斗级别：地图/地形、敌人配置、任务目标、特殊机制。所有地图尺寸与地形类型均选自当前 `BattleTerrainGenerator` 支持的正式 profile。

---

### 11.1 Q101 征兵令

**剧情定位**：战争序章，玩家首次面对骑射敌人，同时选择阵营。

**地图**：11×9 `default`（边境哨站外围，草地+少量森林+栅栏 prop）

**敌人**：
- `federation_meteor_rider` ×3（骑射骚扰队）

**友方**：
- `militia` ×2（哨站民兵，前排步兵，会主动挡在玩家前面）

**任务目标**：
1. `defeat_enemy(federation_meteor_rider, 3)`
2. `settlement_action(service_contract_board, 1)` — 报告并选择阵营

**特殊机制**：
- 骑射 AI：`federation_meteor_rider` 使用 `ranged_archer` brain，射击后移动，保持 3-5 格距离。
- 民兵挡箭：民兵有较高概率移动至玩家与敌人之间（`frontline_bulwark` brain 的 `guard` 行为）。
- 阵营选择：通过 settlement_action 接取不同的尾任务/奖励来模拟（需后续系统扩展真正的阵营标签）。

---

### 11.2 Q102 商路封锁（帝国路线）

**剧情定位**：帝国正面突破联邦私掠舰队封锁。

**地图**：21×13 `canyon`（中央河流表示海峡，两岸陆地表示三艘战舰拼接的甲板）

**敌人**：
- 阶段1：`mist_harrier` ×4（敌方飞行侦察单位，优先攻击弩炮操作位）
- 阶段2：`federation_privateer_marine` ×6（登船肉搏型，从北岸向南岸冲锋）
- 阶段3：`federation_privateer_commander` ×1（私掠舰队司令）+ `federation_privateer_marine` ×2

**任务目标**：
1. `defeat_enemy(mist_harrier, 4)`
2. `defeat_enemy(federation_privateer_marine, 6)`
3. `defeat_enemy(federation_privateer_commander, 1)`
4. `settlement_action(service_contract_board, 1)`

**特殊机制**：
- 战舰甲板：河岸两侧放置 `spike_barricade` prop 表示拒马/船舷。
- 弩炮：南岸放置 2 个 `objective_marker` prop，玩家单位站在相邻格可操作（通过 settlement_action 或技能模拟），造成 3d8 穿刺伤害，4 格范围，冷却 3 回合。
- 风向：每回合随机改变，顺风移动 -1AP，逆风 +1AP（用全局 status effect 或地形 cost 模拟）。
- 船体破损：敌方弩炮/范围伤害命中后，随机 1 格变为 difficult terrain（用 `mud` 或 `spike` 地形模拟）。
- 阶段转换：击败 `mist_harrier`×4 后刷新 `federation_privateer_marine`；至少 1 名敌人到达南岸后刷新 `federation_privateer_commander`。

---

### 11.3 Q106 刺杀行动（联邦路线）

**剧情定位**：联邦潜入敌舰斩首私掠舰队司令。

**地图**：15×11 `default`（敌舰旗舰甲板/船舱/指挥舱，用单一甲板地图 + prop 分区表示）

**敌人**：
- 甲板巡逻：`federation_privateer_marine` ×4（实际是敌方守卫，用 marine 模板代表水手）
- 船舱守卫：`federation_meteor_rider` ×2
- 指挥舱：`federation_privateer_commander` ×1 + `wolf_alpha` ×1（司令宠物/坐骑）

**任务目标**：
1. `submit_item(sealed_dispatch, 1)` — 偷取部署图
2. `defeat_enemy(federation_privateer_commander, 1)` — 可选 Boss 战
3. `settlement_action(service_contract_board, 1)` — 撤离报告

**特殊机制**：
- 潜入：敌人初始未警觉，有固定巡逻路线（每回合移动 2-4 格，视野 3 格锥形）。玩家轻装移动力 +1，重甲潜行不利。
- 暗杀：从背后接近未警觉敌人可用 1AP 使其昏迷 3 回合（用 `mage_sleep_dust` 或特定技能模拟）。
- 警报：若被发现，3 回合内增援 `federation_privateer_marine` ×4。
- 预见 Boss：`federation_privateer_commander` 每 3 回合有 1 回合“计算过载”（用自 debuff trait 模拟），期间抗性归零。

---

### 11.4 Q103 谍影重重

**剧情定位**：双方间谍战白热化，第三方灰烬势力操纵。

**地图**：15×11 `default`（双层建筑改单层，分外围、居住区、密道区三个 prop 区）

**敌人**：
- 外围巡逻：`federation_meteor_rider` ×2
- 密道守卫：`ashen_cultist` ×2
- 反间谍：`mist_weaver` ×1（镜卫，从镜中走出）

**任务目标**：
1. `defeat_enemy(federation_meteor_rider, 2)`
2. `submit_item(bandit_insignia, 1)` — 密函
3. `defeat_enemy(mist_weaver, 1)` — 可选
4. `settlement_action(service_contract_board, 1)`

**特殊机制**：
- 搜查点：6 个可搜查点（书架、床铺、地板暗格），只有 1 个有密函，每次搜查 1AP，30% 触发毒针陷阱（中毒 -2 力量 3 回合）。
- 镜卫：HP 降至 10% 以下自动消失并夺回密函（任务失败），可通过战斗击败或逃跑。
- 隐藏发现：密函内容指向灰烬交界裂隙坐标，为第三方线铺垫。

---

### 11.5 Q105 要塞之围

**剧情定位**：帝国要塞“冰崖”遭联邦围攻，多波次防御战。

**地图**：21×13 `holdout_push`（要塞防御网格，北側城墙、中央内院、南側城门）

**敌人**（标准 8 波）：
- 波次1-2：`federation_meteor_rider` ×3/波（弓箭手，攻击城墙玩家）
- 波次3-4：`federation_privateer_marine` ×4/波（攻城型，优先攻击城门）
- 波次5-6：`federation_meteor_rider` ×2 + `federation_privateer_marine` ×3/波
- 波次7：`mist_beast` ×1 + `federation_privateer_marine` ×4
- 波次8：`wolf_alpha` ×1 + `federation_field_shaman` ×1（联邦随军萨满）

**友方**：
- 要塞守军 `empire_frost_legionnaire` ×6
- 冰霜法师 `empire_eternal_frost_mage` ×2

**任务目标**：
1. `defeat_enemy(federation_meteor_rider, N)` — N 根据波次总数变化
2. `settlement_action(service_contract_board, 1)`

**特殊机制**：
- 城门：2 格目标，30HP/AC15，被破坏后敌人可进入内院。
- 城墙：4 格障碍，50HP/AC20，友方在墙后相邻格 +4AC。
- 投石机：侧塔放置 `objective_marker`，2AP 操作，8 格范围，4d10 钝击，冷却 4 回合。
- 护城河：城墙外 1 格 `shallow_water`/`mud`，敌方进入移动力 -2，50% 滑倒（失去剩余 AP）。
- 冰霜符文：内院四角放置 4 个 `torch`/`objective_marker`，激活后释放冰霜新星（3 格范围 2d6 冷冻）。
- 士气：每阵亡 1 名友方守军，剩余守军命中率 -1（最高 -3），用全局 debuff trait 模拟。
- 波次差异：
  - 帝国优势线：6 波，波次4 增援 `empire_frost_legionnaire` ×4
  - 拉锯线：8 波
  - 联邦优势线：10 波，额外 `mist_harrier` ×3 + `federation_tower_elementalist` ×1

---

### 11.6 Q107 炼金武器

**剧情定位**：双方研发裂隙武器，玩家潜入炼金工坊。

**地图**：15×11 `default`（地下工坊单层，分生产区和核心室两个 prop 区）

**敌人**：
- 生产区：`cultist_acolyte` ×5（裂隙研究员， mage_controller）
- 核心室：`mist_weaver` ×1（首席研究员）+ `mist_beast` ×1（裂隙核心污染体）

**任务目标**：
1. `defeat_enemy(cultist_acolyte, 5)`
2. `submit_item(calamity_shard, 2)` — 稳定核心 或 `defeat_enemy(mist_beast, 1)` — 破坏核心
3. `settlement_action(service_contract_board, 1)` — 选择技术去向

**特殊机制**：
- 裂隙污染：每回合随机 1 格变为污染区（用 `spike` 地形或临时 status effect 模拟），进入受 1d8 力场伤害，金属甲额外 1d4。
- 裂隙手雷：`cultist_acolyte` 技能 `mage_ember_mine` 表示。
- 核心抉择：
  - 稳定：`submit_item(calamity_shard, 2)`，获得裂隙武器使用权
  - 破坏：击败 `mist_beast`（裂隙核心化身），全场污染消失但大爆炸 3d10 力场
  - 泄露：把技术卖给商人（接 `main_weapon_smuggling_partner`）

---

### 11.7 Q108 叛国者

**剧情定位**：突入叛国者庄园，面对第三公爵与第三方势力。

**庄园战斗**（Q108 本体）：
- 地图：15×11 `default`（贵族庄园，花园/主楼/地下室用 prop 分区）
- 敌人：`empire_frost_legionnaire` ×4（公爵私人卫队，AC+2）
- 目标：`defeat_enemy(empire_frost_legionnaire, 4)`
- NPC：`npc_empire_third_duke`（庄园书房 settlement_action）

**C 路线后续 `main_traitor_duke_truth`**：见 4.2 节详细设计（19×11 canyon 灰鸦峡谷，10 仆从 + 2 精英 + 2 野兽）。

---

### 11.8 Q109 焦土政策

**剧情定位**：帝国焦土撤退，玩家面对村民与命令的冲突。

**地图**：19×11 `canyon`（乡村网格，3 个村庄建筑分布在河岸两侧）

**敌人**：
- 帝国焦土队：`empire_frost_legionnaire` ×3（会主动焚烧建筑）
- 联邦先遣队：`federation_meteor_rider` ×4（从东侧出现，夺取粮食）

**友方/中立**：
- 村民 NPC ×5（平民，无战斗能力，移动力 2 格/回合）

**任务目标**：
1. `defeat_enemy(empire_frost_legionnaire, 3)` 或 `defeat_enemy(federation_meteor_rider, 4)`（根据玩家立场）
2. `settlement_action(service_contract_board, 1)` — 面对村庄抉择

**特殊机制**：
- 建筑焚烧：每个村庄建筑可 `settlement_action` 点燃（1AP），3 回合后烧毁，产生 2 格烟雾区（用 `forest` 或临时迷雾模拟）。
- 村民撤离：村民每回合自动向地图边缘移动 2 格，可被玩家/敌人阻挡。
- 抉择：
  - A. 严格执行焦土：焚烧所有建筑，帝国进度 +10
  - B. 拖延执行：保护村民撤离，进度不变
  - C. 违抗命令：击退双方，帝国 -10，联邦 -5，获得“守护者”称号

---

### 11.9 Q112 逃兵营

**剧情定位**：处理帝国逃兵营，面对老兵与平民。

**地图**：15×11 `default`（森林营地，木栅栏用 `spike_barricade` prop 表示）

**敌人/友方**：
- 逃兵老兵：`empire_frost_legionnaire` ×2（经验丰富但拒绝再战）
- 平民 NPC ×8（无战斗能力）
- 增援：若选择保护逃兵，帝国/联邦处理队会进攻

**任务目标**：
1. `defeat_enemy(empire_frost_legionnaire, 2)` 或 `settlement_action(negotiate)`
2. `settlement_action(service_contract_board, 1)` — 报告

**特殊机制**：
- 木栅栏：营地周围 `spike_barricade` prop，AC10/10HP。
- 玩家选择：
  - A. 执行命令：击败 2 名老兵，平民四散
  - B. 保护逃兵：击退帝国/联邦处理队
  - C. 说服回归：`settlement_action(negotiate)`，需魅力检定 DC16

---

### 11.10 Q114 武器走私

**剧情定位**：拦截/保护走私车队，揭露商人同时向双方售武。

**地图**：19×11 `canyon`（山道网格，悬崖边缘、隧道用 prop/地形表示）

**敌人**（拦截路线）：
- `federation_meteor_rider` ×4（走私护卫， ranged_archer，优先保护货车）

**友方**（保护路线）：
- 货车 ×3（移动 2 格/回合，20HP，可被攻击）
- `empire_frost_legionnaire` ×2（玩家助手）

**任务目标**：
1. `defeat_enemy(federation_meteor_rider, 4)`（拦截）或 保护货车抵达终点
2. `submit_item(bandit_insignia, 3)` — 取得走私账本
3. `settlement_action(service_contract_board, 1)` — 选择曝光/利用/加入

**特殊机制**：
- 货车：3 辆货车每回合自动前进 2 格，玩家可 1AP 推动 +1 格。货车损毁则任务失败。
- 悬崖：地图边缘格，被冲撞可能坠落（用 forced movement + 深坑/水域地形模拟）。
- 隧道：1 格宽通道，可堵口。
- 抉择：曝光/利用/加入商人，拆为三个尾任务。

---

### 11.11 Q117 海军决战

**剧情定位**：双方主力舰队在断喉海峡决战。

**地图**：23×13 `canyon`（最大峡谷，中央 6 格宽海峡水域，两岸表示双方舰队）

**敌人**：
- 波次1：`mist_harrier` ×3
- 波次2：`federation_privateer_marine` ×4（跳帮队）
- 波次3：`wolf_alpha` ×2
- 波次4（若有裂隙炮舰）：`mist_beast` ×1

**任务目标**：
1. `defeat_enemy(mist_harrier, 5)`
2. `defeat_enemy(wolf_alpha, 2)`
3. `defeat_enemy(mist_beast, 1)`（若有）
4. `settlement_action(service_contract_board, 1)`

**特殊机制**：
- 可操作武器：弩炮、投石机、裂隙炮用 `objective_marker` prop 表示，玩家相邻格可 1-2AP 操作。
- 战舰移动：每 3 回合两侧陆地板块相对移动 1 格（用刷新地形/edge feature 模拟，需扩展）。
- 深海触须：战斗超过 12 回合，地图中央刷新 `deep_sea_tentacle`（3×3 占位），每回合随机抓取 1 格单位，6d10 挤压伤害。出现 3 回合内撤离至边缘即胜利。

---

### 11.12 Q110 和谈使者

**剧情定位**：护送和谈使者穿越战区走廊，第三方势力伏击。

**地图**：19×11 `narrow_assault`（战区走廊，中央 3 格宽“中立区”用 `land` 表示，进入后自动停火）

**敌人**（伏击波次）：
- 波次1：`federation_meteor_rider` ×3（伪装成帝国逃兵）
- 波次2：`empire_frost_legionnaire` ×3（伪装成联邦叛军）
- 波次3：`ashen_balancer` ×2（第三方平衡者）

**友方**：
- `neutral_peace_envoy` ×1（临时可控队伍成员，无攻击，2AP，25HP）

**任务目标**：
1. `settlement_action(escort)` — 开始护送
2. `defeat_enemy(federation_meteor_rider, 3)`
3. `defeat_enemy(empire_frost_legionnaire, 3)`
4. `defeat_enemy(ashen_balancer, 2)`
5. `settlement_action(service_contract_board, 1)` — 面对和谈结果

**特殊机制**：
- 使者可控：每回合玩家操控使者移动，目标从地图一端走到另一端（约 12 格）。
- 中立区：走廊中央 3 格宽区域，进入后双方自动停火（用 faction 切换或目标锁定规则模拟，需扩展）。
- 平衡者：每回合攻击当前 HP 总量占优的一方，可用 `ashen_balancer` brain 实现。
- 失败条件：使者死亡则任务失败，战争线进入最残酷阶段。

---

### 11.13 Q118 战俘营

**剧情定位**：潜入战俘营，选择营救/处决对象。

**地图**：19×11 `default`（战俘营，外层警戒区+内层关押区用 prop 分区）

**敌人**：
- 外层守卫：`empire_frost_legionnaire` ×4（`frontline_bulwark`）
- 内层增援：触发警报后刷新 `empire_frost_legionnaire` ×4

**友方/中立**：
- 6 名囚犯 NPC（每个牢房 1 名）

**任务目标**：
1. `defeat_enemy(empire_frost_legionnaire, 4)`
2. `settlement_action(service_contract_board, 1)` — 选择营救/处决对象

**特殊机制**：
- 牢房：6 个 `objective_marker` prop，玩家 adjacent 后可 `settlement_action` 选择营救/处决。
- 囚犯：A 敌方将领、B 己方间谍、C 第三方人员、D-F 普通士兵。
- 选择结果：
  - 营救将领：敌方下轮数量 -20%
  - 营救间谍：解锁隐藏任务线
  - 营救第三方：开启第三方外交线
  - 处决将领：敌方士气 -10%
  - 处决间谍：己方情报网崩溃

---

### 11.14 Q116 议会政变

**剧情定位**：联邦议会大厅政变，玩家选择支持方。

**地图**：19×11 `default`（议会大厅，中央 3×3 “七席圆桌”用 `objective_marker` 表示，四周立柱用 `spike_barricade` 表示）

**敌人/友方**：
- 政变方：`federation_meteor_rider` ×4 + `federation_tower_elementalist` ×2 + `federation_coup_leader` ×1
- 保皇方：`empire_frost_legionnaire` ×3 + `wolf_shaman` ×1（家族守护萨满）

**任务目标**：
1. `defeat_enemy(federation_meteor_rider, 4)`
2. `defeat_enemy(federation_coup_leader, 1)`
3. `settlement_action(service_contract_board, 1)` — 选择支持哪一方

**特殊机制**：
- 玩家初始中立，需通过 settlement_action 选择加入政变方/保皇方/两不相帮。
- 演说机制：每回合开始时玩家选择支持哪方演说，支持方 NPC 命中率 +1，反对方 -1（用全局 aura trait 模拟）。
- 圆桌结界：站在中央 3×3 区域的法师法术范围 -1（用区域 status effect 模拟）。
- 政变领袖：使用 `mage_mirror_image`，HP 60，Boss 级。

---

### 11.15 Q124 地下抵抗

**剧情定位**：帝国占领区城市，协助/阻止抵抗组织。

**地图**：19×11 `default`（占领区城市，民居用 `tent` prop 表示，街道开阔）

**敌人**：
- 帝国占领军：`empire_frost_legionnaire` ×3（巡逻队）
- 抵抗战士：`empire_frost_legionnaire` ×2（若玩家选择镇压）

**友方/中立**：
- 平民 NPC ×6
- `npc_resistance_spring_village_acquaintance`

**任务目标**：
1. `defeat_enemy(empire_frost_legionnaire, 3)`（镇压/协助路线数量不同）
2. `submit_item(bandit_insignia, 2)` — 取得/销毁抵抗组织名单
3. `settlement_action(service_contract_board, 1)` — 选择立场

**特殊机制**：
- 玩家身份决定目标：帝国阵营镇压、联邦阵营协助、无阵营可选。
- 抵抗战术：传单（降低占领军士气）、破坏水井（HP 恢复减半）、暗杀巡逻队（1AP 秒杀，限 1 次/回合）。
- 宵禁：若抵抗活动过多，所有街道格移动力 -2（用 `mud` 地形或全局 status 模拟）。
- 道德抉择：镇压会逮捕/杀死春泉村熟人；放走则熟人存活并解锁隐藏支线。

---

### 11.16 Q115 皇帝的密令

**剧情定位**：潜入皇宫，外层禁卫 + 内廷法师。

**地图**：19×11 `default`（皇宫外层大厅） + 后续第二场 15×11 `default`（内廷）

> **注意**：当前战斗系统不支持真正的多层/连续战斗。本任务可设计为 **两场独立战斗**，第一场胜利后进入第二场。

**第一场：外层禁卫**
- 敌人：`empire_imperial_guard` ×8（盾墙阵型）
- 目标：`defeat_enemy(empire_imperial_guard, 8)`
- 特殊：相邻 ≥2 名禁卫军时每人 AC+2（用 `faction_empire_shield_wall` trait 实现）。

**第二场：内廷法师**
- 敌人：`empire_eternal_frost_mage` ×2（镜像守卫）
- 目标：`defeat_enemy(empire_eternal_frost_mage, 2)`
- 特殊：每个法师制造 2 个镜像（用 `mage_mirror_image`），镜像 30% HP 且复制法术。

**任务目标**：
1. `defeat_enemy(empire_imperial_guard, 8)`
2. `defeat_enemy(empire_eternal_frost_mage, 2)`
3. `settlement_action(service_empire_throne, 1)` — 面对皇帝

---

### 11.17 Q125 永恒者之谜

**剧情定位**：最终对话与抉择，无战斗。

**地图**：无战斗，仅在密室据点与 `npc_empire_vlakis_iii` 交互。

**任务目标**：
1. `settlement_action(service_empire_throne, 1)` — 触发对话
2. 根据玩家前九轮选择，接取不同结局任务：
   - `main_eternal_mystery_support`
   - `main_eternal_mystery_expose`
   - `main_eternal_mystery_assassinate`
   - `main_eternal_mystery_usurp`
   - `main_eternal_mystery_renew`

**特殊机制**：
- 对话通过 `QuestDef.accept_dialogue_text` 和多个互斥任务表达。
- 隐藏选项 D（成为永恒者）需要 `quest_completed(main_imperial_edict)` + `calamity_shard` ×5。
- 隐藏选项 E（新帝国线）需要完成第三方外交线相关任务。

---

## 12. 更新后的资源清单汇总

基于第 11 节战斗级设计，新增资源需求更新如下：

### 12.1 新增 `EnemyTemplateDef`

| 模板 ID | 显示名 | 复用基础 | 备注 |
|---|---|---|---|
| `empire_frost_legionnaire` | 霜铠师 | `wolf_vanguard` | 已确认 |
| `empire_eternal_frost_mage` | 永冻院法师 | `mist_weaver` | 已确认 |
| `empire_imperial_guard` | 禁卫军 | `wolf_vanguard` | 已确认 |
| `federation_meteor_rider` | 流星团轻骑兵 | `wolf_raider` | 已确认 |
| `federation_tower_elementalist` | 七塔元素法师 | `mist_weaver` | 已确认 |
| `federation_privateer_marine` | 私掠水手 | `wolf_raider` | 已确认 |
| `federation_privateer_commander` | 私掠舰队司令 | `mist_weaver` | 已确认 |
| `federation_vanguard_general` | 联邦先锋将军 | `wolf_vanguard` | 已确认 |
| `federation_coup_leader` | 政变领袖 | `mist_weaver` | 已确认 |
| `federation_field_shaman` | 联邦随军萨满 | `wolf_shaman` | 新增，Q105 使用 |
| `ashen_cultist` | 余火仆从 | `cultist_acolyte` | 已确认 |
| `ashen_balancer` | 平衡者 | `mist_weaver` | 已确认 |
| `ashen_ember_warden` | 余火守望者精英 | `mist_weaver` | 已确认 |
| `neutral_peace_envoy` | 和谈使者 | 新建 | 已确认 |

### 12.2 新增 `EnemyAiBrainDef`

| Brain ID | 用途 |
|---|---|
| `civilian_escort` | 和谈使者备用 AI |
| `ashen_balancer` | 平衡者目标选择器（攻击优势方） |

### 12.3 新增 `TraitDef`

见 5.6 节。

### 12.4 新增/确认 `ItemDef`

见 6.6 节，并新增 `federation_field_shaman` 可用武器。

---

## 13. 更新后的落地检查清单

### 13.1 第一阶段：最小可运行集（MVP）
- [ ] 创建所有核心 NPC 的 `FacilityNpcConfig`。
- [ ] 创建帝国/联邦/第三方基础兵种模板（`empire_frost_legionnaire`、`federation_meteor_rider`、`ashen_cultist`）。
- [ ] 创建首领模板（`federation_privateer_commander`、`federation_vanguard_general`、`federation_coup_leader`、`ashen_ember_warden`）。
- [ ] 创建 `neutral_peace_envoy` 模板 + `civilian_escort` brain。
- [ ] 创建阵营特性 `TraitDef`。
- [ ] 把 Q101、Q105、Q106、Q108、Q115 的 `defeat_enemy` 目标替换为新的阵营模板 ID。
- [ ] 运行 `QuestContentValidator` 与 `EnemyContentRegistry` 校验。

### 13.2 第二阶段：全部任务战斗级落地
- [ ] 为 Q102、Q103、Q107、Q109、Q110、Q112、Q114、Q116、Q117、Q118、Q124 创建/更新 quest `.tres`，替换敌人模板 ID。
- [ ] 配置 encounter roster/terrain profile（default/canyon/holdout_push/narrow_assault）。
- [ ] 放置 `objective_marker`/`spike_barricade`/`tent`/`torch` prop 表达特殊机制。
- [ ] 实现 `main_traitor_duke_truth`、`main_weapon_smuggling_*`、`main_eternal_mystery_*` 等分支任务。

### 13.3 第三阶段：系统扩展（超出本文档）
- [ ] 阵营标签/战争进度持久化。
- [ ] 真正的分支对话树或 choice modal。
- [ ] 任务内波次/阶段切换 runtime。
- [ ] 使者临时队伍成员机制。
- [ ] 相邻友方加成/盾墙等战斗规则支持。
