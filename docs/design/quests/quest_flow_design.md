# D&D 世界观任务流程设计

> 更新日期：2026-05-31
> 适配版本：当前任务系统（QuestDef / QuestState / QuestProgressService）

## 一、架构概述

### 1.1 世界结构

| 世界层级 | 定位 | 风格 |
|---------|------|------|
| **主世界** | 程序生成大地图，玩家默认出生于此 | 经典 D&D：村庄、野外、商路、地下城 |
| **灰烬交界** | 通过界火裂隙进入的异界特区（可选起始世界） | 黑魂式：压抑、碎片化叙事、高难度 |
| **灰烬地图** | 灰烬交界内部的子地图（ deeper layer ） | 极限挑战环境 |

### 1.2 任务系统约束

当前任务系统支持以下结构：

- **目标类型（3种）**：`defeat_enemy` / `submit_item` / `settlement_action`
- **奖励类型（3种）**：`gold` / `item` / `pending_character_reward`
- **状态流转**：`inactive → active → completed → rewarded`（或 `failed`）
- **任务来源**：`provider_interaction_id` 绑定到 NPC 服务功能

> **注意**：当前系统仅有 `service_contract_board`（委托板）和 `service_bounty_registry`（悬赏登记处）两个合法 provider。若要让灰烬交界的特殊 NPC（如余火守望者、灰烬铁匠）发布任务，需先扩展 `QuestProviderContentRules.SUPPORTED_PROVIDER_IDS`。

### 1.3 设计原则

| 原则 | 说明 |
|------|------|
| **技能即核心奖励** | 主线任务优先奖励 `skill_unlock` / `skill_mastery`，金币装备为辅 |
| **模块化适配** | 任务不依赖固定坐标，通过标签和敌人模板适配程序生成世界 |
| **叙事轻量化** | 利用 `description` 提供碎片化叙事，不依赖对话树 |
| **难度分层** | 主世界任务 = 标准难度；灰烬交界任务 = 高难 + 机制考验 |
| **无 XP 设计** | 任务完成不给经验值，而是给技能领悟契机或特殊资源 |

---

## 二、主世界任务体系

### 2.1 新手教程链：《异乡人的开端》

> *你在一个名叫"春泉村"的聚落醒来。村长告诉你，最近商路不太平。*

| 任务 ID | 名称 | 目标 | 奖励 | 叙事功能 |
|---------|------|------|------|----------|
| `tutorial_first_blood` | 初阵 | 击败 `wolf_pack` ×3 | 50金 + 解锁 `charge` | 教学战斗，熟悉 d20 命中 |
| `tutorial_gather_herbs` | 采集药材 | 提交 `healing_herb` ×3 | 30金 + `bandage_roll` ×2 | 教学采集与物品系统 |
| `tutorial_wolf_alpha` | 狼群首领 | 击败 `wolf_alpha` ×1 | 150金 + 解锁 `guard` | 教学精英战，AP 管理 |

**接取条件**：`tutorial_first_blood` 无前置；后续任务需完成前一任务。

### 2.2 主世界主线链：《边境异变》

> *边境的巡逻队报告了一种"灰色的雾"——那不是自然现象。有人在裂隙边缘看到了不属于这个世界的建筑轮廓。*

| 任务 ID | 名称 | 目标 | 奖励 | 叙事功能 |
|---------|------|------|------|----------|
| `main_border_patrol` | 边境巡逻 | 击败 `wolf_raider` ×5 | 200金 + `iron_ore` ×3 | 引入区域威胁升级 |
| `main_mist_investigation` | 迷雾调查 | 提交 `bandit_insignia` ×2 + `moonfern_sample` ×1 | 300金 + 知识解锁"迷雾生态" | 调查玩法，收集证据 |
| `main_crack_seal` | 裂隙封印 | 击败 `mist_beast` ×2 + `mist_weaver` ×1 | 500金 + `calamity_shard` ×1 + 智力成长+20 | 首次面对异界敌人 |
| `main_ashen_gate` | 灰烬之门 | 在委托板报告 | 1000金 + 解锁 `warrior_backstep` 或 `mage_blink` | 开启灰烬交界入口 |

**接取条件**：链式前置，每步需完成上一步。

### 2.3 支线委托（非链式）

| 任务 ID | 名称 | 目标 | 奖励 | 标签 |
|---------|------|------|------|------|
| `side_iron_delivery` | 铁矿运送 | 提交 `iron_ore` ×5 | 100金 + `bronze_sword` ×1 | `contract` |
| `side_herb_moonfern` | 月蕨寻觅 | 提交 `moonfern_sample` ×2 | 80金 + `antidote_herb` ×3 | `contract` |
| `side_dispatch_delivery` | 封缄急件 | 提交 `sealed_dispatch` ×1 | 60金 + 感知成长+10 | `contract` |
| `side_bandit_cleansing` | 匪徒清剿 | 击败 `wolf_raider` ×8 | 250金 + `bandit_insignia` ×2 | `contract` |

---

## 三、灰烬交界任务体系

### 3.1 灰烬觉醒链（灰烬交界新手 / 直接出生玩家）

> *余烬祭所的篝火永不熄灭——或者说，不能熄灭。你在这里醒来，身边是其他和你一样"掉进来"的人。*

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `ashen_awakening` | 灰烬觉醒 | 击败 `mist_beast` ×1 | 100金 + 解锁 `warrior_backstep` |
| `ashen_embers` | 收集余烬 | 提交 `forge_coal` ×5 | 150金 + `dead_road_lantern` ×1 |
| `ashen_mist_weaver` | 编织者之影 | 击败 `mist_weaver` ×2 | 400金 + 解锁 `mage_black_flame` 或 `priest_aid` |
| `ashen_lantern_road` | 提灯之路 | 提交 `dead_road_lantern` ×2 | 200金 + 意志成长+15 |

### 3.2 大教堂区域：《焚毁的信仰》

> *炭化大教堂的钟楼倒在主殿中央。倒悬的焦尸手里握着一枚被烧融的圣徽。*

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `ashen_cathedral_heresy` | 异端之骸 | 击败 `mist_beast` ×3 + `mist_harrier` ×2 | 600金 + `blood_debt_shawl` ×1 |
| `ashen_burned_seal` | 烧融圣徽 | 提交 `bandit_insignia` ×3（叙事包装为"找回圣徽残片"） | 400金 + 知识解锁"灰烬信仰" |
| `ashen_bell_ringer` | 钟楼余响 | 击败 `mist_weaver` ×3 + `settlement_action` 报告 | 800金 + 解锁 `warrior_dragon_shake_heaven` |

### 3.3 书库区域：《溺水的索引》

> *书库的中庭被水淹没了一半。倾斜的塔楼里，某种巨大的东西正在"翻阅"禁书。*

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `ashen_drowned_index` | 溺水的索引 | 提交 `hardwood_lumber` ×5（搭建通道）+ 击败 `mist_harrier` ×3 | 500金 + 智力成长+25 |
| `ashen_forbidden_pages` | 禁书残页 | 提交 `moonfern_sample` ×3（叙事包装为"干燥书页"） | 350金 + 解锁 `mage_dispel_magic` |

### 3.4 深渊裂界区域（高难）

> *地面像被烧穿后又被黑雾缝合。站在裂界边缘，你能看到"下面"——是另一片天空。*

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `ashen_abyss_survival` | 裂界生存 | 击败 `mist_beast` ×5 | 1000金 + `black_star_wedge` ×1 |
| `ashen_dread_alpha` | 恐惧阿尔法 | 击败 `wolf_alpha` ×3 | 1500金 + `black_crown_core` ×1 |
| `ashen_rift_warden` | 裂隙守望者 | 击败 `mist_weaver` ×5 + `mist_beast` ×3 | 2000金 + `calamity_shard` ×3 + 全属性成长+10 |

---

## 四、悬赏任务（可重复）

| 任务 ID | 名称 | 目标 | 奖励 | 重复 |
|---------|------|------|------|------|
| `bounty_wolf_pack` | 狼群悬赏 | 击败 `wolf_pack` ×5 | 100金 + `beast_hide` ×2 | 是 |
| `bounty_mist_harrier` | 迷雾猎手 | 击败 `mist_harrier` ×3 | 250金 | 是 |
| `bounty_wolf_alpha` | 阿尔法猎杀 | 击败 `wolf_alpha` ×1 | 300金 + `beast_hide` ×5 | 是 |
| `bounty_mist_beast` | 迷雾兽猎杀 | 击败 `mist_beast` ×2 | 400金 | 是 |
| `bounty_mist_weaver` | 编织者悬赏 | 击败 `mist_weaver` ×2 | 500金 + `calamity_shard` ×1 | 是 |
| `bounty_wolf_vanguard` | 先锋清剿 | 击败 `wolf_vanguard` ×4 | 350金 + `iron_ore` ×3 | 是 |
| `bounty_wolf_shaman` | 萨满猎杀 | 击败 `wolf_shaman` ×2 | 450金 | 是 |

---

## 五、信仰路线任务

> **前提**：需扩展 `QuestProviderContentRules` 添加 `service_fortuna_shrine` 和 `service_misfortune_altar`。

### 5.1 Fortuna（命运女神）路线

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `faith_fortuna_prayer` | 命运女神的注视 | `settlement_action`（祈祷）×1 | 意志+1 (`attribute_delta`) |
| `faith_fortuna_lucky_hunt` | 幸运狩猎 | 击败 `wolf_alpha` ×1 + `mist_beast` ×1 | 500金 + 解锁 `reverse_fate_amulet` 相关技能 |
| `faith_fortuna_miracle` | 神迹之证 | 提交 `calamity_shard` ×2 | 解锁 `scout_charm` + 信仰进度 |

### 5.2 Misfortune（黑冕诅咒）路线

| 任务 ID | 名称 | 目标 | 奖励 |
|---------|------|------|------|
| `faith_misfortune_bargain` | 黑冕交易 | 提交 `black_crown_core` ×1 | 解锁 `black_crown_seal` |
| `faith_misfortune_doom_hunt` | 厄运狩猎 | 击败 `mist_weaver` ×3 | 600金 + 解锁 `doom_sentence` |
| `faith_misfortune_debt` | 血债契约 | 提交 `blood_debt_shawl` ×1 | 解锁 `black_contract_push` + 力量+1 |

---

## 六、奖励设计原则

### 6.1 金币奖励基准

| 任务类型 | 金币范围 | 说明 |
|---------|---------|------|
| 新手教程 | 30-150 | 低额，引导性质 |
| 主世界主线 | 200-1000 | 阶梯上升，封印任务给最多 |
| 灰烬交界主线 | 100-800 | 高风险高回报 |
| 支线委托 | 60-350 | 中等 |
| 悬赏（可重复） | 100-500 | 按难度分档 |
| 信仰任务 | 300-600 | 中等，侧重特殊奖励 |

### 6.2 技能奖励分配

| 解锁技能 | 来源任务 | 适用职业 |
|---------|---------|---------|
| `charge` | `tutorial_first_blood` | 战士/通用 |
| `guard` | `tutorial_wolf_alpha` | 战士/通用 |
| `warrior_backstep` / `mage_blink` | `main_ashen_gate` | 战士/法师分支 |
| `mage_black_flame` / `priest_aid` | `ashen_mist_weaver` | 法师/牧师分支 |
| `mage_dispel_magic` | `ashen_forbidden_pages` | 法师 |
| `warrior_dragon_shake_heaven` | `ashen_bell_ringer` | 战士/高阶 |
| `black_crown_seal` | `faith_misfortune_bargain` | Misfortune 路线 |
| `doom_sentence` | `faith_misfortune_doom_hunt` | Misfortune 路线 |

### 6.3 特殊物品奖励

| 物品 | 来源 | 用途 |
|------|------|------|
| `calamity_shard` | 裂隙相关任务 | 高级锻造材料 / 信仰祭品 |
| `black_crown_core` | 深渊高难任务 | Misfortune 路线核心材料 |
| `black_star_wedge` | 深渊高难任务 | 未知用途（叙事钩子） |
| `dead_road_lantern` | 灰烬交界任务 | 照亮迷雾区域 / 叙事道具 |
| `blood_debt_shawl` | 大教堂任务 | 装备或信仰材料 |

---

## 七、与现有系统的集成

### 7.1 配置文件位置

```
data/configs/quests/
  ├── main_world_quests.json      # 主世界任务
  ├── ashen_intersection_quests.json  # 灰烬交界任务
  └── bounty_quests.json          # 悬赏任务（可重复）
```

### 7.2 加载方式

QuestDef 支持 `from_dict()` 方法，可将 JSON 中的字典数组批量转换为 QuestDef 资源。建议在 `ProgressionContentRegistry` 或 `GameSession` 初始化时加载：

```csharp
// 伪代码示例
var questJson = LoadJson("res://data/configs/quests/main_world_quests.json");
foreach (var questDict in questJson["quests"].AsGodotArray())
{
    var questDef = QuestDef.from_dict(questDict.AsGodotDictionary());
    if (questDef != null)
        questDefs[questDef.quest_id] = questDef;
}
```

> **重要**：Godot 的 `JSON.parse()` 会把所有数字解析为 `float`（`Variant.Type.Float`），但 `QuestDef.validate_schema()` 对 `target_value`、`amount` 等字段要求严格的 `int`（`Variant.Type.Int`）。加载时必须递归地将 float 转换为 int，否则 `from_dict()` 会返回 `null`。
>
> 参考实现见 `tests/runtime/validation/run_quest_config_validation.cs` 中的 `ConvertFloatsToInts()` 函数。

### 7.3 若要扩展灰烬交界 NPC 发布任务

修改 `scripts/player/progression/QuestProviderContentRules.cs`：

```csharp
public static readonly StringName PROVIDER_ASHEN_CONTRACT = "service_ashen_contract";
public static readonly StringName PROVIDER_FORTUNA_SHRINE = "service_fortuna_shrine";
public static readonly StringName PROVIDER_MISFORTUNE_ALTAR = "service_misfortune_altar";

public static readonly Godot.Collections.Dictionary SUPPORTED_PROVIDER_IDS = new()
{
    { PROVIDER_CONTRACT_BOARD, true },
    { PROVIDER_BOUNTY_REGISTRY, true },
    { PROVIDER_ASHEN_CONTRACT, true },
    { PROVIDER_FORTUNA_SHRINE, true },
    { PROVIDER_MISFORTUNE_ALTAR, true },
};
```

同时在灰烬交界据点的 NPC 配置中绑定对应的 `interaction_script_id`。

### 7.4 任务完成前置条件

当前 `accept_requirements` 的 schema 未做严格验证。建议的实现方式：

```json
// 完成任务前置
{"requirement_type": "quest_completed", "quest_id": "tutorial_first_blood"}

//  settlements 声望前置
{"requirement_type": "settlement_reputation", "settlement_id": "spring_village_01", "minimum_value": 10}

// 物品持有前置（检查背包）
{"requirement_type": "has_item", "item_id": "calamity_shard", "minimum_quantity": 1}

// 等级前置
{"requirement_type": "party_level", "minimum_value": 5}
```

> 上述 `requirement_type` 需要额外的运行时检查实现；当前 JSON 配置中会保留这些字段作为设计意图，实际生效需配合接取检查逻辑。

---

## 八、叙事碎片化设计

由于系统无对话树，叙事通过以下方式传递：

1. **任务描述（description）**：提供背景、氛围、只言片语的线索
2. **物品描述**：奖励物品自带 lore
3. **任务名称**：暗示故事走向（如"血债契约""钟楼余响"）
4. **任务链顺序**：通过前后任务描述拼凑真相

**示例叙事拼图**：
- `main_mist_investigation` 描述提到"裂隙边缘有建筑轮廓"
- `main_crack_seal` 描述提到"那不是雾——是灰烬王朝的呼吸"
- `main_ashen_gate` 描述提到"穿过裂隙后，你闻到的是三百年前的硝烟"
- 完成链后，玩家拼凑出：灰烬交界是一块被拖入的古代废土，且有人在刻意维持裂隙开放。
