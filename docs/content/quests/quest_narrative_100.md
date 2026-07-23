# 百任务剧情总集（详细设计版）

> 覆盖主世界与灰烬交界两大世界层级，共计 100 个任务剧情设计。本版在保留叙事风味的同时，为每个任务补充**具体任务文本**、**触发流程**与**可直接映射到当前 `QuestDef` schema 的任务规格**，供配置落地时直接参考。

---

## 当前系统约束速查

设计时严格遵循以下已确认的 `QuestDef` 字段与合法值：

| 字段 | 类型 | 说明 |
|------|------|------|
| `quest_id` | `StringName` | 本设计统一使用 `snake_case` 内部 ID。 |
| `display_name` | `string` | 游戏中显示的任务名称。 |
| `description` | `string` | 游戏中显示的任务描述（多行文本）。 |
| `provider_interaction_id` | `StringName` | 当前仅支持 `service_contract_board`（委托板）与 `service_bounty_registry`（悬赏板）。 |
| `tags` | `Array<StringName>` | 如 `tutorial`、`main_story`、`contract`、`side`、`bounty`、`ashen_intersection`、`main_world`、`cathedral`、`library`、`abyss` 等。 |
| `accept_requirements` | `Array<Dictionary>` | 配置层保留，当前运行时仅部分校验；本设计使用 `quest_completed` 作为链式前置。 |
| `objective_defs` | `Array<Dictionary>` | `objective_type` 仅支持 `defeat_enemy`、`submit_item`、`settlement_action`。 |
| `reward_entries` | `Array<Dictionary>` | `reward_type` 仅支持 `gold`、`item`、`pending_character_reward`。 |
| `is_repeatable` | `bool` | 悬赏任务为 `true`，剧情任务为 `false`。 |

**合法敌人模板 ID**：`wolf_pack`、`wolf_alpha`、`wolf_raider`、`wolf_vanguard`、`wolf_shaman`、`mist_beast`、`mist_harrier`、`mist_weaver`。

**合法物品 ID**（部分）：`healing_herb`、`iron_ore`、`bandit_insignia`、`moonfern_sample`、`calamity_shard`、`travel_ration`、`hardwood_lumber`、`forge_coal`、`dead_road_lantern`、`sealed_dispatch`、`beast_hide`、`bandage_roll`、`antidote_herb`、`bronze_sword`、`blood_debt_shawl`、`black_star_wedge`、`black_crown_core`。

**合法 pending reward entry_type**：`skill_unlock`、`skill_mastery`、`attribute_progress`、`attribute_delta`、`knowledge_unlock`。

**合法 attribute_progress target_id**：`strength`、`agility`、`constitution`、`perception`、`intelligence`、`willpower`。

> 设计备注：接取对话、成功/失败反馈、确认弹窗等字段（`accept_dialogue_text` 等）当前尚未纳入 `QuestDef` schema，因此仅在正文的「触发流程」或「设计备注」中以纯文本形式保留设计意图，不写入 JSON 规格块。

---

## 任务链依赖总览

```text
第一卷 主世界
  [Q001] → [Q003] → [Q006] → [Q007] → [Q008] → [Q009] → [Q010] → [Q011] → [Q012] → [Q013]
  [Q002] 并行
  [Q004] 并行
  [Q005] 并行（需 Q001 完成）
  [Q014]-[Q021] 支线并行
  [Q022]-[Q029] 地下城链
  [Q030]-[Q037] 种族/职业引导并行
  [Q038]-[Q045] 乡土委托并行

第二卷 灰烬交界
  [Q046] → [Q048] → [Q049] → [Q050] → [Q051]
  [Q047] 并行
  [Q052] → [Q053] → [Q054] → [Q055] → [Q056] → [Q057] → [Q058] → [Q059]
  [Q060] → [Q061] → [Q062] → [Q063] → [Q064] → [Q065] → [Q066] → [Q067]
  [Q068] → [Q069] → [Q070] → [Q071] → [Q072] → [Q073]
  [Q074] → [Q075] → [Q076] → [Q077] → [Q078] → [Q079]
  [Q080] → [Q081] → [Q082] → [Q083] → [Q084] → [Q085]

第三卷 悬赏
  [Q086]-[Q093] 主世界悬赏（可重复）
  [Q094]-[Q100] 灰烬交界悬赏（可重复）
```

---

## 第一卷：主世界 — 异乡人的开端

### 一、新手觉醒链（Q001-Q005）

#### Q001 初阵

**目标**：清理村外的荒狼群，证明你能保护自己。

> 你在春泉村的草垛上醒来，头痛欲裂。村长——一个缺了左耳的老兵——把一把生锈的短剑拍在你手里。
> "商队三天没来了。农田边缘有东西在刨土。不是野猪。"
>
> 村外的麦田被踩出一片狼藉。三只荒狼正在分食一头死骡子。它们抬起头看你，眼睛是病态的淡黄色——不像普通的野兽。

**触发流程**：
- 触发方式：新建角色后，进入春泉村据点自动在委托板可见。
- 前置条件：无。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：村长将短剑交给玩家后，委托板出现第一条契约。

**任务规格**：
```json
{
  "quest_id": "tutorial_first_blood",
  "display_name": "初阵",
  "description": "村长告诉你，最近商路不太平。去村外清理几只在农田边缘徘徊的荒狼，证明你能保护自己。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["tutorial", "main_story", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_wolves",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 50
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "player_sword_01",
      "entries": [
        {
          "entry_type": "skill_mastery",
          "target_id": "warrior_heavy_strike",
          "amount": 80
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q002 采集药材

**目标**：为村里的医师采集药草。

> 医师的小屋里弥漫着干草药的苦味。她把三株干枯的 healing_herb 样本放在桌上："这是你能认得的。去找活的回来。注意叶片背面——有银丝的才是真的，别采成毒芹。"
>
> 野外的草药生长在溪流边。但你不是唯一在采集的东西。某种更大的东西最近也在溪边徘徊，留下带爪印的泥坑。

**触发流程**：
- 触发方式：进入春泉村据点后与医师所在区域交互，委托板刷新。
- 前置条件：无（与 Q001 并行的新手任务）。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：医师需要活草药样本，玩家可在探索溪流时顺便采集。

**任务规格**：
```json
{
  "quest_id": "tutorial_gather_herbs",
  "display_name": "采集药材",
  "description": "村里的药草不够了。去野外采集一些 healing_herb，医师会教你如何处理伤口。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["tutorial", "main_story", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "gather_herbs",
      "objective_type": "submit_item",
      "target_id": "healing_herb",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 30
    },
    {
      "reward_type": "item",
      "item_id": "bandage_roll",
      "quantity": 2
    }
  ],
  "is_repeatable": false
}
```

---

#### Q003 狼群首领

**目标**：击败组织袭击的狼群首领。

> 普通荒狼只是散兵游勇。这只不一样——村民说它"在指挥"。有人看到它站在山丘上嚎叫，然后三面的狼同时发起攻击。它比普通荒狼大一圈，毛皮上有一道旧伤疤，从右眼一直裂到嘴角。
>
> 村长补充："它不住在森林里。它从北边来——裂隙的方向。"

**触发流程**：
- 触发方式：完成 Q001 后自动在委托板解锁。
- 前置条件：`quest_completed: tutorial_first_blood`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：老猎人格雷出现在村东口，委托板上出现关于白色狼首的契约。

**任务规格**：
```json
{
  "quest_id": "tutorial_wolf_alpha",
  "display_name": "狼群首领",
  "description": "普通荒狼只是前锋。一只体型巨大的狼群首领正在组织更有威胁的袭击。击败它，让商路恢复安全。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["tutorial", "main_story", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_first_blood"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 150
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "skill_unlock",
          "target_id": "warrior_guard",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q004 铁匠的试炼

**目标**：为铁匠收集铁矿并锻造第一件武器。

> 铁匠铺里，一个独眼矮人正在敲打一块发红的铁锭。他没有停手："你会用剑吗？不会？那你会需要一把好点的。村东的老矿坑废弃了，但浅层还有些铁矿。自己挖，自己搬，我教你锻造。"
>
> 矿坑里的空气有股硫磺味。墙壁上有些刻痕——不是矿工的记号，是某种爪印，深深地嵌在石头里。

**触发流程**：
- 触发方式：进入春泉村后访问铁匠铺，委托板刷新。
- 前置条件：无（与 Q001/Q002 并行的装备引导任务）。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：铁匠愿意教玩家锻造，但前提是先证明能弄到原料。

**任务规格**：
```json
{
  "quest_id": "tutorial_blacksmith_trial",
  "display_name": "铁匠的试炼",
  "description": "村里的铁匠急需原料。帮他收集一些铁矿，他会给你一把像样的武器。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["tutorial", "main_story", "main_world", "contract"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "deliver_iron",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 100
    },
    {
      "reward_type": "item",
      "item_id": "bronze_sword",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q005 第一张委托

**目标**：在委托板完成登记，正式成为冒险者。

> 委托板其实是一块钉在谷仓墙上的松木板，上面钉着各种纸条——有的字迹工整，有的沾着血迹。告示书记员是个半精灵，她用羽毛笔在你的名字下面画了一条线："好了。你现在是'注册冒险者'了。意思是：死了没人赔，活着没赏金上限。"
>
> 她递给你一枚铜质徽章，背面刻着一行小字："春泉村出品，真伪自负。"

**触发流程**：
- 触发方式：完成 Q001 后，与告示书记员对话后委托板刷新。
- 前置条件：`quest_completed: tutorial_first_blood`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：玩家完成第一次实战后，被邀请正式注册为冒险者。

**任务规格**：
```json
{
  "quest_id": "tutorial_adventurer_registration",
  "display_name": "第一张委托",
  "description": "完成一次登记，正式成为注册冒险者。这样你才能接取更高级别的契约。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["tutorial", "main_story", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_first_blood"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "register_contract",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 20
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

### 二、主线：边境异变（Q006-Q013）

#### Q006 边境巡逻

**目标**：清剿商道上的狼骑兵。

> 边境哨站的士兵少了三分之一——不是阵亡，是"消失"。巡逻队长给你看了一张草图：狼骑兵的袭击路线正在向北偏移，像是在躲避什么。"它们怕的不是我们。"
>
> 你在商道的第三个转弯处发现了证据：一具狼骑兵的尸体，不是被武器杀死的——它的胸腔从内部炸开，肋骨向外翻折，像一朵盛开的花。

**触发流程**：
- 触发方式：完成 Q003 后，边境哨站委托板解锁。
- 前置条件：`quest_completed: tutorial_wolf_alpha`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：巡逻队长需要人手清剿商道上的狼骑兵，并调查它们异常北移的原因。

**任务规格**：
```json
{
  "quest_id": "main_border_patrol",
  "display_name": "边境巡逻",
  "description": "边境巡逻队报告称，狼骑兵的活动频率异常升高。它们似乎在躲避什么东西——或者，被什么东西驱赶。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_wolf_alpha"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q007 迷雾调查

**目标**：收集裂隙边缘的异常样本。

> 巡逻队在裂隙边缘发现了一种灰色的雾。那不是晨雾——它在中午最浓，而且不随风移动。雾里有建筑轮廓，尖顶和拱门，不属于任何已知的建筑风格。
>
> 你在雾的边缘找到了两枚 bandit_insignia，但上面的纹章被烧熔了一半，只剩一个模糊的圆环。还有一株月蕨，但它的叶片是黑色的，摸起来像纸灰。

**触发流程**：
- 触发方式：完成 Q006 后，民兵队长在边境哨站发布新契约。
- 前置条件：`quest_completed: main_border_patrol`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：巡逻队需要玩家前往裂隙边缘采集证据，确认灰色迷雾的来源。

**任务规格**：
```json
{
  "quest_id": "main_mist_investigation",
  "display_name": "迷雾调查",
  "description": "巡逻队在裂隙边缘发现了一种灰色的雾。那不是自然现象——雾里有建筑轮廓，不属于这个世界。收集现场证据。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_border_patrol"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 2
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "mist_lore",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q008 裂隙封印

**目标**：击退从裂隙涌出的异界生物。

> 裂隙正在扩大。从雾里走出来的东西不再是普通的野兽——它们披着灰烬，眼里没有瞳孔，眼眶里是两团暗红色的余火。一只迷雾兽走在前面，后面跟着一个迷雾编织者，它的手指在空中划动，像是在"编辑"空气本身。
>
> 你在战斗结束后发现了一块 calamity_shard，它在你的掌心里微微发热，像是活着的。

**触发流程**：
- 触发方式：完成 Q007 后，裂隙边缘出现神秘过客，委托板新增高优先级契约。
- 前置条件：`quest_completed: main_mist_investigation`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：裂隙扩大，有异界生物涌出，需要有人前往击退并暂时稳定裂隙。

**任务规格**：
```json
{
  "quest_id": "main_crack_seal",
  "display_name": "裂隙封印",
  "description": "裂隙正在扩大。从雾里走出来的东西不再是普通的野兽——它们披着灰烬，眼里没有瞳孔。阻止它们。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_mist_investigation"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_mist_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    },
    {
      "objective_id": "defeat_mist_weaver",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q009 灰烬侦察

**目标**：穿越裂隙，在灰烬交界建立初步情报。

> "穿过去。看看另一边是什么。然后回来告诉我们。"——巡逻队长的命令简单直接，但他的手在发抖。
>
> 穿过裂隙的感觉像是从冷水跳进热水。你出现在一片灰色的荒原上，天空是铅色的，没有太阳，但光线不知从何处照来。远处有一座倒塌的钟楼，钟还在响，虽然没有人敲它。

**触发流程**：
- 触发方式：完成 Q008 后，裂隙变为可交互穿越，返回委托板报告即可完成任务。
- 前置条件：`quest_completed: main_crack_seal`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：巡逻队长要求玩家穿越裂隙侦察另一侧，这是首次进入灰烬交界的主线节点。

**任务规格**：
```json
{
  "quest_id": "main_ashen_scout",
  "display_name": "灰烬侦察",
  "description": "裂隙无法被封印——因为它不是意外。穿越它，你会闻到三百年前的硝烟。另一侧有人在等你。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_crack_seal"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 1000
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "skill_unlock",
          "target_id": "warrior_backstep",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q010 村庄保卫战

**目标**：抵御大规模狼群袭击。

> 你回到主世界时，春泉村正在燃烧。不是普通的火——火焰是苍白的，不发热，但烧得更快。十只狼骑兵正在围攻村口的栅栏，它们的眼睛里也有那种暗红色的余火。
>
> 村长在屋顶上射箭，他的箭袋里只剩三支箭了。

**触发流程**：
- 触发方式：完成 Q009 后从灰烬交界返回主世界时自动触发；委托板出现紧急契约。
- 前置条件：`quest_completed: main_ashen_scout`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：玩家返回时发现村庄遭袭，村长请求协助防守栅栏。

**任务规格**：
```json
{
  "quest_id": "main_village_defense",
  "display_name": "村庄保卫战",
  "description": "你回到主世界时，春泉村正在遭受大规模狼群袭击。保护村口栅栏，击退入侵者。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_scout"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 10
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q011 古代遗迹

**目标**：探索裂隙附近新出现的遗迹。

> 裂隙扩大后，主世界这边也出现了一截石墙——之前埋在地下，现在被"推"了上来。墙上的铭文你一种都不认识，但你能读懂其中的情绪：警告，绝望，和某种保护性的愤怒。
>
> 遗迹深处有两只迷雾编织者在进行某种仪式，它们的法杖插在地上，形成一个发光的双圆。圆圈里有一具尸体——穿着主世界某个王国的军服。

**触发流程**：
- 触发方式：完成 Q010 后，裂隙附近地图出现古代遗迹探索点，委托板刷新。
- 前置条件：`quest_completed: main_village_defense`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：裂隙扩大导致地下遗迹上浮，需要有人进入调查并终止编织者的仪式。

**任务规格**：
```json
{
  "quest_id": "main_ancient_ruins",
  "display_name": "古代遗迹",
  "description": "裂隙扩大后，主世界这边出现了一截被'推'出地面的石墙。遗迹深处有迷雾编织者在进行某种仪式。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_village_defense"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q012 失踪的巡逻队

**目标**：寻找一支消失在迷雾中的巡逻队。

> 第三巡逻队七天没有回报。他们的最后一条消息是通过传讯魔法发出的，只有三个词："它们在学。"
>
> 你在裂隙边缘找到了他们的营地——帐篷完好，武器整齐地堆在一起，食物还在锅里煮着。人不见了。地上没有血迹，只有 footprints，走向裂隙，步伐整齐，像是行军。

**触发流程**：
- 触发方式：完成 Q011 后，巡逻队长发布寻人契约。
- 前置条件：`quest_completed: main_ancient_ruins`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：第三巡逻队失踪，需要调查营地并带回线索。

**任务规格**：
```json
{
  "quest_id": "main_missing_patrol",
  "display_name": "失踪的巡逻队",
  "description": "第三巡逻队七天没有回报。他们的最后一条消息只有三个词：'它们在学。'前往裂隙边缘的营地调查真相。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ancient_ruins"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 2
    },
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 2
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "healing_herb",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q013 灰烬之门

**目标**：完成最终报告，开启通往灰烬交界的稳定通道。

> 裂隙无法被封印——因为它不是意外。某种力量在刻意维持它开放。你在遗迹的最深处找到了一块石碑，上面刻着："界火不灭，王朝不亡。"
>
> 当你把报告交给委托板时，告示书记员没有像往常一样记录。她只是看着你，然后说："你知道'灰烬王朝'是什么吗？不知道？很好。保持这样。"

**触发流程**：
- 触发方式：完成 Q012 后，委托板出现最终报告契约；完成后灰烬交界稳定通道开启。
- 前置条件：`quest_completed: main_missing_patrol`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：所有调查线索指向灰烬王朝，玩家需要提交最终报告并开启稳定通道。

**任务规格**：
```json
{
  "quest_id": "main_ashen_gate",
  "display_name": "灰烬之门",
  "description": "裂隙无法被封印——因为它不是意外。某种力量在刻意维持它开放。提交最终报告，开启通往灰烬交界的稳定通道。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_missing_patrol"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 1000
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "ashen_dynasty",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 三、商路风云（Q014-Q021）

#### Q014 商队护卫

**目标**：护送商队通过危险路段。

> 商人公会的代表是个胖胖的半身人，他的算盘从不离手。"三辆车，十二箱货，目的地是银溪镇。报酬按箱算——货到了，你拿钱。货丢了，你赔。"
>
> 第一辆车里装的是香料，第二辆是铁器，第三辆用黑布盖着，商人拒绝告诉你里面是什么。你听到里面有抓挠声。

**触发流程**：
- 触发方式：完成 Q005 后，春泉村委托板刷新商队相关契约。
- 前置条件：`quest_completed: tutorial_adventurer_registration`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：商人公会招募护卫护送车队前往银溪镇。

**任务规格**：
```json
{
  "quest_id": "side_caravan_escort",
  "display_name": "商队护卫",
  "description": "商人公会有一批货物要送往银溪镇。护送商队通过危险路段，确保货物安全到达。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "escort"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_adventurer_registration"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 6
    },
    {
      "objective_id": "escort_arrive",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 250
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q015 匪徒伏击

**目标**：清剿商道上的匪徒据点。

> 商队第三次被劫后，商人公会提高了赏金。匪徒不是普通的强盗——他们有组织，有暗号，而且只劫特定的货物。被劫的商人说，匪徒的领袖穿着一件被烧熔的铠甲，上面刻着你不认识的纹章。
>
> 据点藏在峡谷里，入口被伪装成岩壁。里面有六个人，但你只找到了五个。第六个消失在一条通往地下深处的隧道里。

**触发流程**：
- 触发方式：完成 Q014 后，银溪镇委托板出现清剿匪徒据点的契约。
- 前置条件：`quest_completed: side_caravan_escort`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：商队被劫三次，商人公会悬赏清剿匪徒据点。

**任务规格**：
```json
{
  "quest_id": "side_bandit_ambush",
  "display_name": "匪徒伏击",
  "description": "商队第三次被劫后，商人公会提高了赏金。匪徒有组织、有暗号，而且只劫特定货物。清剿他们的据点。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_caravan_escort"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 6
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q016 失落的货物

**目标**：找回商队遗失的木材。

> 一场暴雨冲垮了山道，三车 hardwood_lumber 滚进了峡谷。商人不在乎木材，他在乎的是其中一根——据说是从"某棵特别的树"上砍下来的。"找到那根，其他的你随便烧。"
>
> 峡谷底部有动物的痕迹，但不是普通的野兽。爪印太大，太深，而且有三个趾——不是狼，不是熊，是你没见过的东西。

**触发流程**：
- 触发方式：完成 Q014 后，在银溪镇委托板刷新。
- 前置条件：`quest_completed: side_caravan_escort`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：暴雨导致货物遗失，商人悬赏找回特定木材。

**任务规格**：
```json
{
  "quest_id": "side_lost_cargo",
  "display_name": "失落的货物",
  "description": "一场暴雨冲垮了山道，三车 hardwood_lumber 滚进了峡谷。商人需要找回其中一根特别的木材。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "delivery"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_caravan_escort"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "recover_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q017 商人之誓

**目标**：收集匪徒的纹章作为商会信誉担保。

> 商人公会的会长是个盲眼的老妇人，她的眼睛是被魔法烧坏的。"我需要证据。不是匪徒的尸体——是它们的标志。五个 bandit_insignia，我要知道它们来自同一个源头。"
>
> 你把 insignia 放在她的桌上。她用手指摸了摸，脸色变了。"这个纹章……我见过。三百年前的某本书里有这个图案。"

**触发流程**：
- 触发方式：完成 Q015 后，商人公会在银溪镇发布新契约。
- 前置条件：`quest_completed: side_bandit_ambush`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：商会会长需要匪徒纹章作为证据，追溯其来源。

**任务规格**：
```json
{
  "quest_id": "side_merchant_oath",
  "display_name": "商人之誓",
  "description": "商人公会会长需要匪徒的纹章作为证据。收集五个 bandit_insignia，证明它们来自同一个源头。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_bandit_ambush"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "bandit_symbol_origin",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q018 丝绸之路

**目标**：送达两份封缄急件到远方城镇。

> 两份 sealed_dispatch，同一个发件人，同一个封印——一只被烧断翅膀的鸟。第一份送往银溪镇，第二份送往风息城。发件人要求它们同时送达，虽然两个城镇相距两百英里。
>
> 你偷看了第一份（封印很容易撬开），里面只有一句话："仪式已经开始，停止已经太晚。"

**触发流程**：
- 触发方式：完成 Q014 后，在春泉村委托板刷新。
- 前置条件：`quest_completed: side_caravan_escort`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：神秘发件人需要两份急件同时送达不同城镇。

**任务规格**：
```json
{
  "quest_id": "side_sealed_dispatch",
  "display_name": "丝绸之路",
  "description": "两份 sealed_dispatch 需要分别送往银溪镇和风息城。路途危险，但报酬丰厚。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "delivery"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_caravan_escort"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "deliver_dispatch",
      "objective_type": "submit_item",
      "target_id": "sealed_dispatch",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 250
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q019 走私者巢穴

**目标**：捣毁走私异界物品的团伙。

> 有人在走私"裂隙货"——从灰烬交界带回来的东西。它们不危险，但非法，而且价格离谱。最近一批货里有一盏灯，点燃后会投射出你不认识的星座。
>
> 走私者的头目是个谈吐优雅的精灵，他的书架上有十七种语言的字典。"我不是罪犯，"他说，"我是考古学家。区别在于，考古学家不埋尸体。"

**触发流程**：
- 触发方式：完成 Q013（灰烬之门）后，银溪镇委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：玩家见过灰烬交界后，才能识别并追查走私的异界物品。

**任务规格**：
```json
{
  "quest_id": "side_smuggler_den",
  "display_name": "走私者巢穴",
  "description": "有人在走私从灰烬交界带回来的'裂隙货'。捣毁这个团伙，阻止异界物品流入主世界。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_smugglers",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 5
    },
    {
      "objective_id": "collect_lanterns",
      "objective_type": "submit_item",
      "target_id": "dead_road_lantern",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q020 公会特许状

**目标**：在城镇公会完成正式注册。

> 要成为"正式冒险者"，你需要在至少三个城镇的委托板注册。每个城镇的要求不同：银溪镇要你证明你能战斗，风息城要你证明你能阅读，铁炉堡要你证明你能喝酒。
>
> 铁炉堡的公会代表是个矮人，他的酒杯比你的头还大。"不是喝光，"他说，"是喝到还能站着签名的程度。这是我们的标准。"

**触发流程**：
- 触发方式：完成 Q005 后，任意城镇委托板刷新。
- 前置条件：`quest_completed: tutorial_adventurer_registration`。
- 发布者：任意城镇委托板 `service_contract_board`。
- 接取情境：玩家需要在多个城镇公会注册，提升冒险者等级。

**任务规格**：
```json
{
  "quest_id": "side_guild_charter",
  "display_name": "公会特许状",
  "description": "要成为正式冒险者，你需要在至少三个城镇的委托板完成注册。每个城镇的要求都不同。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_adventurer_registration"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "register_towns",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q021 紧急运送

**目标**：在时限内将补给送达前线哨站。

> 前线哨站的最后一只信鸽三天前死了。补给车队在路上被迷雾兽袭击，只剩一辆车的 travel_ration。你被告知："尽快。如果 ration 没到，哨站的人会在四天后开始吃靴子。"
>
> 路上你遇到了一个独行的旅人，他的行李里有一本书，书的封面和你在灰烬交界看到的一样。他说他是"历史学者"。他的手指上有 burns，和你见过的迷雾编织者一样的 burns。

**触发流程**：
- 触发方式：完成 Q013 后，边境哨站委托板刷新紧急补给契约。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：前线哨站补给中断，需要玩家限时送达 travel_ration。

**任务规格**：
```json
{
  "quest_id": "side_urgent_delivery",
  "display_name": "紧急运送",
  "description": "前线哨站的补给车队被迷雾兽袭击，只剩一辆车的 travel_ration。尽快将补给送达哨站。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "delivery", "escort"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "deliver_rations",
      "objective_type": "submit_item",
      "target_id": "travel_ration",
      "target_value": 5
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

### 四、地下城探索（Q022-Q029）

#### Q022 鼠患之巢

**目标**：清剿矿坑深处的巨型鼠群。

> 老矿工说矿坑深处有"比狗大的东西"。你以为是醉话，直到你看到爪印——每只爪有五个趾，趾尖像刀。矿坑深处的空气有股甜味，像是腐败的蜂蜜。
>
> 你在最深处发现了一个巢，里面不是老鼠，是某种混合体：狼的身体，人的手指，眼睛是迷雾兽的那种暗红色。它们在吃矿石，不是吃肉。

**触发流程**：
- 触发方式：完成 Q004（铁匠的试炼）后，春泉村委托板刷新矿坑清剿契约。
- 前置条件：`quest_completed: tutorial_blacksmith_trial`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：老矿坑出现变异鼠群，需要有人进入深处清剿。

**任务规格**：
```json
{
  "quest_id": "dungeon_rat_nest",
  "display_name": "鼠患之巢",
  "description": "老矿坑深处出现了比狗还大的变异兽群。清剿它们，并带回特殊矿石样本。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_blacksmith_trial"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 8
    },
    {
      "objective_id": "collect_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 5
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q023 古代墓穴

**目标**：探索最近坍塌暴露的地下墓穴。

> 地震掀开了一座山丘，露出一扇石门。门上刻着警告："沉睡者不应被打扰。"但显然有人已经打扰过了——门锁被暴力破坏，痕迹是新的。
>
> 墓穴里有七具石棺，六具是空的。第七具的盖子被推开了一半，里面有一具干尸，穿着主世界某个王国的将军制服，胸口的徽章刻着："灰烬王朝，永垂不朽。"

**触发流程**：
- 触发方式：完成 Q011（古代遗迹）后，世界地图出现墓穴探索点，委托板刷新。
- 前置条件：`quest_completed: main_ancient_ruins`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：地震暴露古代墓穴，需要调查其中与灰烬王朝有关的线索。

**任务规格**：
```json
{
  "quest_id": "dungeon_ancient_tomb",
  "display_name": "古代墓穴",
  "description": "地震掀开了一座山丘，露出一扇石门。墓穴深处似乎与灰烬王朝有关。探索并带回线索。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ancient_ruins"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "ashen_dynasty_tomb",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q024 邪教徒藏身处

**目标**：捣毁崇拜裂隙的邪教徒据点。

> 有人在崇拜裂隙。他们不是疯子——他们太理性了，理性到可怕。他们的教义很简单："灰烬王朝比我们更古老，因此更正确。让我们加入他们。"
>
> 据点在一座废弃教堂的地下室。墙上有壁画，画的是一个巨大的裂隙，裂隙里伸出无数只手，每只手都握着一个小人。小人在笑。

**触发流程**：
- 触发方式：完成 Q013（灰烬之门）后，春泉村委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：玩家从灰烬交界返回后，才能识别并追查崇拜裂隙的邪教徒。

**任务规格**：
```json
{
  "quest_id": "dungeon_cultist_hideout",
  "display_name": "邪教徒藏身处",
  "description": "有人在崇拜裂隙。他们太理性了，理性到可怕。捣毁这座废弃教堂地下室里的邪教徒据点。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 4
    },
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q025 下水道恐怖

**目标**：消灭城市下水道中的异界渗透体。

> 银溪镇的下水道最近发出怪味——不是粪便，是烧焦的骨头。渔民报告说河里有"灰色的鱼"，它们有牙齿，而且不咬钩——它们咬网。
>
> 你在下水道的最深处找到了一只迷雾兽的幼体，它被困在栅栏里，正在融化铁条。它的身体在发光，光芒投射在墙壁上，形成了文字——是你不认识的文字，但你知道那是文字。

**触发流程**：
- 触发方式：完成 Q015 后，银溪镇委托板刷新。
- 前置条件：`quest_completed: side_bandit_ambush`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：银溪镇下水道出现异界渗透体，需要清除。

**任务规格**：
```json
{
  "quest_id": "dungeon_sewer_horror",
  "display_name": "下水道恐怖",
  "description": "银溪镇的下水道出现了烧焦骨头的怪味。深入下水道，消灭其中的异界渗透体。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_bandit_ambush"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 3
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "antidote_herb",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q026 禁书图书馆

**目标**：从被封锁的图书馆中取回危险文献。

> 风息城的学者公会有一个地下保险库，里面锁着"不可读之书"。最近有人闯入了，偷走了三本。会长说："不是普通的贼。贼不会把书放回原位——但这些书被放回去了，只是内容变了。"
>
> 你翻开了其中一本，里面的文字在蠕动。当你移开视线再回看时，同一页的内容已经不同了。

**触发流程**：
- 触发方式：完成 Q013 后，风息城委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：风息城委托板 `service_contract_board`。
- 接取情境：学者公会需要有人取回被异界污染的危险文献。

**任务规格**：
```json
{
  "quest_id": "dungeon_forbidden_library",
  "display_name": "禁书图书馆",
  "description": "风息城学者公会的地下保险库中被盗走了三本'不可读之书'。取回这些危险的文献。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 3
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q027 龙之巢穴

**目标**：调查 rumors 中的"小龙"目击事件。

> 农民说他看到了龙。不是成年龙——是"小狗那么大"的。它在吃羊，但不吃肉，吃骨头。它的呼吸不是火焰，是灰色的雾，和裂隙的雾一样。
>
> 你在山谷深处找到了巢穴，里面有两只"龙崽"。它们不是龙——是迷雾兽的变种，有翅膀，会飞，而且比普通的迷雾兽更聪明。它们在模仿鸟叫。

**触发流程**：
- 触发方式：完成 Q013 后，世界地图刷新龙之巢穴探索点。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：农民报告看到"小龙"，玩家需要调查真相。

**任务规格**：
```json
{
  "quest_id": "dungeon_dragon_lair",
  "display_name": "龙之巢穴",
  "description": "农民报告在山谷深处看到了'小龙'。调查这个巢穴，找出真相。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 3
    },
    {
      "objective_id": "collect_beast_hide",
      "objective_type": "submit_item",
      "target_id": "beast_hide",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "beast_hide",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q028 深矿迷途

**目标**：深入废弃矿坑寻找失踪矿工。

> 十一个矿工下去了，三个上来了。上来的那个疯了，只会说一句话："它们在挖。它们比我们挖得更深。"
>
> 矿坑的最深处有一扇新出现的门——不是矿工挖的，是从内部推开的。门后面是一条隧道，隧道的墙壁上有爪印，但不是往外走，是往里面走。有什么东西在地下深处挖了一条路。

**触发流程**：
- 触发方式：完成 Q022 后，春泉村委托板刷新。
- 前置条件：`quest_completed: dungeon_rat_nest`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：矿工在深矿失踪，需要深入调查新出现的隧道。

**任务规格**：
```json
{
  "quest_id": "dungeon_lost_miners",
  "display_name": "深矿迷途",
  "description": "十一个矿工下去了，三个上来了。深入废弃矿坑，寻找失踪者并调查新出现的隧道。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "dungeon_rat_nest"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 4
    },
    {
      "objective_id": "collect_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q029 深渊之门

**目标**：封印主世界出现的次级裂隙。

> 主世界出现了第二个裂隙——比第一个小，但更近，就在银溪镇东边的森林里。从里面走出来的不只是迷雾兽，还有"东西"——人形的，穿着衣服，但脸是平的，没有五官。
>
> 你封印了裂隙，但在最后一刻，其中一个"东西"对你说了话。它没有嘴，但你在脑子里听到了声音："谢谢。我们正好需要休息。"

**触发流程**：
- 触发方式：完成 Q025 后，银溪镇委托板刷新。
- 前置条件：`quest_completed: dungeon_sewer_horror`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：银溪镇森林出现次级裂隙，需要用月蕨稳定封印。

**任务规格**：
```json
{
  "quest_id": "dungeon_abyss_gate",
  "display_name": "深渊之门",
  "description": "主世界出现了第二个裂隙，就在银溪镇东边的森林里。击败守护者，用月蕨稳定封印。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["main_story", "contract", "main_world", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "dungeon_sewer_horror"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    },
    {
      "objective_id": "seal_with_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 2
    }
  ],
  "is_repeatable": false
}
```

---

### 五、种族与职业之道（Q030-Q037）

#### Q030 精灵的远古誓言

**目标**：在精灵遗迹中完成古老的净化仪式。

> 精灵长老给你看了一枚叶形徽章："我们的祖先曾守护界门。我们失败了。现在轮到你——或者任何人——完成我们没有完成的事。"
>
> 遗迹在森林最深处，被藤蔓覆盖。里面的雕像不是神，是战士——穿着和你在灰烬交界看到的盔甲一样的战士。底座上刻着："第一批守门人。"

**触发流程**：
- 触发方式：完成 Q013 后，世界地图刷新精灵遗迹探索点，委托板出现契约。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：精灵长老请求玩家前往遗迹完成净化仪式。

**任务规格**：
```json
{
  "quest_id": "race_elven_ancient_vow",
  "display_name": "精灵的远古誓言",
  "description": "精灵长老请求你前往森林深处的遗迹，完成祖先未能完成的净化仪式。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "race", "elf"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 2
    },
    {
      "objective_id": "collect_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "ancient_alliance",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q031 矮人的深路

**目标**：为矮人氏族找回失落矿脉的坐标。

> 矮人铁炉说他的祖父的祖父的日记里提到一条"深路"——通往地底深处的隧道，里面有比秘银更珍贵的矿石。"但坐标在日记的最后一页，那一页被撕掉了。"
>
> 你在灰烬交界的一本书里找到了同样的坐标——不是主世界的坐标，是灰烬交界的坐标。两个世界的矿脉是同一根。

**触发流程**：
- 触发方式：完成 Q013 后，铁炉堡委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：铁炉堡委托板 `service_contract_board`。
- 接取情境：矮人铁炉需要找回失落矿脉坐标，线索指向灰烬交界。

**任务规格**：
```json
{
  "quest_id": "race_dwarf_deep_road",
  "display_name": "矮人的深路",
  "description": "矮人铁炉需要你找回失落矿脉的坐标。线索可能藏在灰烬交界的书籍中。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "race", "dwarf", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 4
    },
    {
      "objective_id": "collect_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 5
    }
  ],
  "is_repeatable": false
}
```

---

#### Q032 兽人的血之仪式

**目标**：在战斗中证明自己的荣耀。

> 兽人萨满说你的"血不够浓"——意思是你的战斗经验不足。"去杀一只值得尊敬的敌人。不是弱小的，不是生病的。一只会选择战斗而不是逃跑的敌人。"
>
> 你找到了一只狼群首领，它看着你，没有逃跑，反而低伏身体，准备冲锋。萨满看了伤口后点头："它会记住你。你也应该记住它。"

**触发流程**：
- 触发方式：完成 Q003 后，世界地图出现兽人营地，委托板刷新。
- 前置条件：`quest_completed: tutorial_wolf_alpha`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：兽人萨满要求玩家击败一只值得尊敬的敌人以证明荣耀。

**任务规格**：
```json
{
  "quest_id": "race_orc_blood_ritual",
  "display_name": "兽人的血之仪式",
  "description": "兽人萨满要求你击败一只值得尊敬的敌人，证明你的血足够浓。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "race", "orc"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_wolf_alpha"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q033 战士的试炼场

**目标**：在竞技场中击败五名对手。

> 铁炉堡的地下竞技场不合法，但很有名。规则很简单：站着的人赢，倒下的人输。没有武器限制，没有魔法限制，没有道德限制。
>
> 你的第三个对手是个蒙面人，他的战斗风格和你在灰烬交界见过的某个"东西"一模一样——相同的步伐，相同的习惯性动作。你击败他后，摘下面具，面具下面没有脸。

**触发流程**：
- 触发方式：完成 Q003 后，进入铁炉堡据点触发。
- 前置条件：`quest_completed: tutorial_wolf_alpha`。
- 发布者：铁炉堡委托板 `service_contract_board`。
- 接取情境：铁炉堡地下竞技场招募挑战者，连续击败五名对手。

**任务规格**：
```json
{
  "quest_id": "class_warrior_trial",
  "display_name": "战士的试炼场",
  "description": "铁炉堡的地下竞技场不合法，但很有名。击败五名对手，证明你站得住。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "class", "warrior"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_wolf_alpha"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_opponents",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "skill_unlock",
          "target_id": "warrior_guard",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q034 法师的奥术研习

**目标**：收集三种罕见的魔法材料。

> 法师公会的入学考试不是笔试，是实操：收集三种材料，然后活着回来。第一种是月蕨——只在迷雾边缘生长。第二种是 Elemental Essence——从元素生物身上提取。第三种是"灰烬之泪"——只在灰烬交界降雨时形成。
>
> 导师最后说："别读你找到的任何书。尤其是会自己翻页的那种。"

**触发流程**：
- 触发方式：完成 Q007 后，风息城委托板刷新。
- 前置条件：`quest_completed: main_mist_investigation`。
- 发布者：风息城委托板 `service_contract_board`。
- 接取情境：法师公会入学考试需要收集三种罕见材料。

**任务规格**：
```json
{
  "quest_id": "class_mage_study",
  "display_name": "法师的奥术研习",
  "description": "法师公会的入学考试需要收集三种罕见材料：月蕨、元素精华与灰烬之泪。活着回来。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "class", "mage"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_mist_investigation"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 3
    },
    {
      "objective_id": "collect_shards",
      "objective_type": "submit_item",
      "target_id": "calamity_shard",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q035 牧师的神圣使命

**目标**：净化被亵渎的神殿。

> 一座偏远神殿的牧师失踪了。当你到达时，神殿还在运作——蜡烛亮着，祭坛上有新鲜的花。但神殿里没有人。只有 footprints，走向祭坛，然后消失。
>
> 祭坛下面有一条通道，通向一个密室。密室里有六具尸体，穿着牧师的袍子，围成一圈，手拉着手。他们的表情是微笑的。墙上写着："我们找到了真正的神。"

**触发流程**：
- 触发方式：完成 Q013 后，世界地图刷新被亵渎神殿探索点。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：偏远神殿发生异常，需要净化并调查失踪牧师。

**任务规格**：
```json
{
  "quest_id": "class_priest_mission",
  "display_name": "牧师的神圣使命",
  "description": "一座偏远神殿的牧师失踪了，神殿被异界力量亵渎。净化神殿并调查真相。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "class", "priest"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 3
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q036 盗贼的暗影契约

**目标**：在不触发警报的情况下取得目标物品。

> 盗贼公会的入门任务不是偷窃，是"证明你能不被人看见"。目标是一个 bandit_insignia，它被锁在商会金库里，周围有十二名守卫，三扇魔法门，和一只据说能闻出谎言的猎犬。
>
> 你拿到了 insignia，但发现金库里还有别的东西——一个笼子，里面关着一个小孩。小孩的眼睛是暗红色的。商会的人不是在保护金库——他们是在困住那个小孩。

**触发流程**：
- 触发方式：完成 Q015 后，银溪镇委托板刷新。
- 前置条件：`quest_completed: side_bandit_ambush`。
- 发布者：银溪镇委托板 `service_contract_board`。
- 接取情境：盗贼公会需要有人从商会金库中取得一枚纹章。

**任务规格**：
```json
{
  "quest_id": "class_rogue_shadow_contract",
  "display_name": "盗贼的暗影契约",
  "description": "盗贼公会要求你从商会金库中取得一枚 bandit_insignia，证明你能不被人看见。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "class", "rogue"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "side_bandit_ambush"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 10
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "scout_charm",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q037 弓箭手的猎场

**目标**：在限定箭数内猎杀指定目标。

> 猎手公会的测试很直接：给你十支箭，一只目标，一片森林。目标是一只 mist_harrier，它飞得快，视力好，而且能感知魔法。"用你学的一切。如果十支箭不够，你还需要学更多。"
>
> 你追踪了它三天。最后一天，你发现它不是在逃跑——它在引导你。它想让你看某个东西。那个东西是一座被藤蔓覆盖的石塔，塔顶有一个裂隙，比你见过的任何一个都小，但它在呼吸。

**触发流程**：
- 触发方式：完成 Q006 后，世界地图出现猎场探索点。
- 前置条件：`quest_completed: main_border_patrol`。
- 发布者：边境哨站委托板 `service_contract_board`。
- 接取情境：猎手公会要求玩家用有限箭数猎杀一只 mist_harrier。

**任务规格**：
```json
{
  "quest_id": "class_archer_hunt",
  "display_name": "弓箭手的猎场",
  "description": "猎手公会要求你猎杀一只 mist_harrier。它飞得快、视力好，而且能感知魔法。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "class", "archer"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_border_patrol"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 250
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 六、乡土委托（Q038-Q045）

#### Q038 农夫的恳求

**目标**：清理农田里的害兽。

> 老农夫的玉米地被糟蹋了一半。他说是狼，但玉米秆不是被咬断的——是被烧断的，断口处有灰色的灰烬。"去年还没有这种火。"
>
> 你在田地中央找到了一个 footprint，不是狼的，不是人的，是某种东西的混合体。它有三个前趾和一个后趾，趾尖有 burns。

**触发流程**：
- 触发方式：完成 Q001 后，春泉村委托板刷新。
- 前置条件：`quest_completed: tutorial_first_blood`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：老农夫的农田被异界污染生物糟蹋，请求清理。

**任务规格**：
```json
{
  "quest_id": "folk_farmers_plea",
  "display_name": "农夫的恳求",
  "description": "老农夫的玉米地被能烧断作物的异界害兽糟蹋了一半。清理农田里的威胁。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_first_blood"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_wolves",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 100
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q039 磨坊的麻烦

**目标**：修复被洪水冲毁的磨坊水渠。

> 磨坊主说水渠里有"东西"堵塞了水流。他以为是木头或石头，但派下去的人上来后吐了——不是恶心，是恐惧。"下面有手，"他说，"很多手，从泥里伸出来。"
>
> 你在水渠底部找到了它们：不是手，是树根。但树根的形状太像手了，而且它们握着东西——一枚硬币，一面刻着你不认识的纹章，另一面空白。

**触发流程**：
- 触发方式：完成 Q001 后，春泉村委托板刷新。
- 前置条件：`quest_completed: tutorial_first_blood`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：磨坊水渠被洪水冲毁并堵塞，需要清理修复。

**任务规格**：
```json
{
  "quest_id": "folk_mill_trouble",
  "display_name": "磨坊的麻烦",
  "description": "磨坊水渠被洪水冲毁，里面堵塞了形似手的诡异树根。清理水渠并修复磨坊。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_first_blood"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 5
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 150
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q040 失踪的孩子

**目标**：寻找在森林中失踪的孩童。

> 小女孩五天前在森林里采蘑菇，没有回来。搜索队找到了她的篮子，里面的蘑菇排列成一个圆环，圆环中央有一小块烧焦的地面。
>
> 你跟着 footprints 走到森林深处。她在那里，坐在一棵倒下的树上，正在和一个"东西"说话——那个东西穿着她的斗篷，有着她的脸，但当你喊她的名字时，两个头同时转了过来。

**触发流程**：
- 触发方式：完成 Q003 后，春泉村委托板刷新。
- 前置条件：`quest_completed: tutorial_wolf_alpha`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：村民请求寻找在森林中失踪的孩子。

**任务规格**：
```json
{
  "quest_id": "folk_missing_child",
  "display_name": "失踪的孩子",
  "description": "小女孩五天前在森林里采蘑菇后失踪。搜索队找到了她的篮子，但人不见踪影。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_wolf_alpha"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 3
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "healing_herb",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q041 草药师的请求

**目标**：采集稀有草药。

> 草药师要的不是普通药草，是"那种"——只在裂隙边缘生长的黑色月蕨。"它们不是毒药，也不是解药。它们是'第三种东西'。我需要它们来研究裂隙的影响。"
>
> 你在采集时割伤了手指，血滴在月蕨上。月蕨的叶片吸收了血，然后变红了——不是被染红，是长出了红色的叶脉，像血管。

**触发流程**：
- 触发方式：完成 Q002 后，春泉村委托板刷新。
- 前置条件：`quest_completed: tutorial_gather_herbs`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：草药师需要裂隙边缘的黑色月蕨样本进行研究。

**任务规格**：
```json
{
  "quest_id": "folk_herbalist_request",
  "display_name": "草药师的请求",
  "description": "草药师需要只在裂隙边缘生长的黑色月蕨来研究裂隙的影响。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_gather_herbs"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 150
    },
    {
      "reward_type": "item",
      "item_id": "antidote_herb",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q042 铁匠的人情

**目标**：为铁匠寻找特定矿石。

> 铁匠要的不是普通的 iron_ore，是"有纹的"——矿石里有自然形成的纹路，像文字。"我祖父用这种矿石打过一把剑，那把剑能切开迷雾。我不知道为什么。"
>
> 你在矿坑深处找到了一块，纹路在月光下会发光。当你把矿石带回来，铁匠看了一会儿，然后说："这不是文字。是地图。"

**触发流程**：
- 触发方式：完成 Q004 后，春泉村委托板刷新。
- 前置条件：`quest_completed: tutorial_blacksmith_trial`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：铁匠需要带有天然纹路的特殊铁矿。

**任务规格**：
```json
{
  "quest_id": "folk_blacksmith_favor",
  "display_name": "铁匠的人情",
  "description": "铁匠需要一种带有天然纹路、像文字一样的特殊铁矿。他说这种矿石能打造出切开迷雾的剑。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "tutorial_blacksmith_trial"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 8
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 3
    }
  ],
  "is_repeatable": false
}
```

---

#### Q043 旅店的传闻

**目标**：调查旅店客人报告的夜间异响。

> 旅店的老板说最近有客人投诉——半夜听到墙壁里有抓挠声。"不是老鼠，"投诉的客人说，"老鼠不会按照节奏抓。这是某种代码。"
>
> 你在墙壁的夹层里找到了一个空洞，里面有一张纸条，上面的字迹和你在灰烬交界看到的一样。纸条上写着："第七个守门人已经醒了。"

**触发流程**：
- 触发方式：完成 Q013 后，任意城镇旅店委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：旅店墙壁夜间传出异响，需要调查来源。

**任务规格**：
```json
{
  "quest_id": "folk_inn_rumor",
  "display_name": "旅店的传闻",
  "description": "旅店客人报告半夜听到墙壁里有按节奏抓挠的声音。调查这个异响的来源。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "investigate_inn",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    },
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 250
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "seventh_gatekeeper",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q044 盗墓贼

**目标**：阻止盗墓团伙亵渎本地墓地。

> 本地的墓地最近被盗了，但不是普通的盗墓——尸体没有被带走，是被"修改"了。它们的姿势变了，手被摆成某种 gesture，嘴里塞进了灰烬。
>
> 你抓住了盗墓贼中的一个。他不是贼，是学者。"我在研究，"他说，"这些尸体在死前就被标记了。它们属于某个名单。"

**触发流程**：
- 触发方式：完成 Q011 后，春泉村委托板刷新。
- 前置条件：`quest_completed: main_ancient_ruins`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：本地墓地发生异常盗墓事件，需要阻止盗墓团伙。

**任务规格**：
```json
{
  "quest_id": "folk_tomb_robbers",
  "display_name": "盗墓贼",
  "description": "本地墓地的尸体被'修改'了，姿势改变，嘴里塞进灰烬。阻止这群盗墓贼。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk", "dungeon"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ancient_ruins"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 5
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q045 女巫猎杀

**目标**：调查被指控为女巫的异端。

> 村民说森林边缘住着一个女巫，她能控制迷雾。当你找到她的茅屋时，里面住着一个老太婆——她不是女巫，是前界的守门人，已经活了三百岁。"我不是控制迷雾，"她说，"我是在阻止它扩散。你们的人帮我，或者帮它。选一个。"

**触发流程**：
- 触发方式：完成 Q013 后，春泉村委托板刷新。
- 前置条件：`quest_completed: main_ashen_gate`。
- 发布者：春泉村委托板 `service_contract_board`。
- 接取情境：村民指控森林边缘的老太婆是女巫，需要调查真相。

**任务规格**：
```json
{
  "quest_id": "folk_witch_hunt",
  "display_name": "女巫猎杀",
  "description": "村民指控森林边缘住着能控制迷雾的女巫。调查真相，决定帮助她或帮助村民。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["contract", "side", "main_world", "folk", "ashen_intersection"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "main_ashen_gate"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "investigate_witch",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    },
    {
      "objective_id": "collect_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "gatekeeper_legacy",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

## 第二卷：灰烬交界 — 余烬与遗忘

### 八、炭化大教堂（Q052-Q059）

#### Q052 异端之骸

**目标**：清剿炭化大教堂外围的异端造物。

> 你第一次站在大教堂的拱门下。这里的天空是铅灰色的，石头被烧成了蜂窝状。祭坛上有一具穿着祭服的骷髅，它的头骨转向你，下颌一张一合，像是在笑。
>
> 它不是唯一在动的。两侧的告解室里爬出了三只迷雾兽，它们的外皮上嵌满了焦黑的祈祷书页。钟楼上传来钟声——没有钟舌的钟自己在响。

**触发流程**：
- 触发方式：进入灰烬交界后，余烬祭所委托板刷新。
- 前置条件：无（第二卷第八章起始任务）。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂废墟出现异动，委托板发布清剿契约。

**任务规格**：
```json
{
  "quest_id": "ashen_cathedral_heresy",
  "display_name": "异端之骸",
  "description": "炭化大教堂里已经没有活人了——但有些东西还在走。它们穿着烧焦的祭服，嘴里念叨着三百年前的祷词。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 3
    },
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "blood_debt_shawl",
      "quantity": 1
    }
  ],
  "is_repeatable": false
}
```

---

#### Q053 烧融圣徽

**目标**：收集大教堂内被烧融的圣徽碎片。

> 圣徽原本挂在祭坛后方，足有一人高。现在它碎成了几十片，每片都被高温烧得扭曲变形。你捡起其中一片，发现背面的纹章不是灰烬王朝的徽记——而是主世界某个王国的印章。
>
> "它们不是被入侵者毁掉的，"同行的守夜人说，"是被自己的信徒融掉的。他们不想让我们知道，这座教堂曾经属于谁。"

**触发流程**：
- 触发方式：完成 Q052 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_cathedral_heresy`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：灰烬祭司的追随者融掉了大教堂圣徽，需要收集碎片以还原真相。

**任务规格**：
```json
{
  "quest_id": "ashen_burned_seal",
  "display_name": "烧融圣徽",
  "description": "在大教堂的废墟中，有人看到被烧融的圣徽碎片。收集它们——也许能拼凑出灰烬王朝信仰的真相。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral", "faith"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_cathedral_heresy"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "collect_seal_fragments",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "ashen_faith",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q054 钟楼余响

**目标**：调查钟楼并终结其中的异响源。

> 钟楼已经倾斜，钟却悬在半空，没有绳索。你走近时，钟声停止了。然后你看见钟的内部——里面不是空的，有一团灰雾在旋转，雾中伸出几只手，握着钟舌。
>
> 当你爬上钟楼，三只迷雾编织者站在横梁上，它们的法杖指向大钟。它们不是在敲钟，是在用钟声传递某种信号——也许是在召唤什么。

**触发流程**：
- 触发方式：完成 Q053 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_burned_seal`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：钟楼无风自鸣，委托板悬赏查明并阻止编织者的信号传递。

**任务规格**：
```json
{
  "quest_id": "ashen_bell_ringer",
  "display_name": "钟楼余响",
  "description": "大教堂的钟楼虽然倒塌，但偶尔还能听到钟声。不是风——是某种东西在钟里。去查清楚。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_burned_seal"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 3
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "skill_unlock",
          "target_id": "warrior_dragon_shake_heaven",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q055 灰烬祭司

**目标**：击败在大教堂布道的灰烬祭司及其随从。

> 主祭厅的穹顶塌了一半，铅灰色的光从裂缝里漏下来，照在一个身穿灰袍的人影上。它没有脸，只有一张不断变换表情的面具，每张脸都在念诵不同的祷词。
>
> 它周围跪着十几个狼萨满，它们的毛皮上画着同样的灰烬符记。这不是战斗，是一场仪式——而你是祭品名单上的最后一个名字。

**触发流程**：
- 触发方式：完成 Q054 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_bell_ringer`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂主祭厅出现灰烬祭司，委托板需要有人中断其仪式。

**任务规格**：
```json
{
  "quest_id": "ashen_ash_priest",
  "display_name": "灰烬祭司",
  "description": "大教堂主祭厅中出现了一位无面的灰烬祭司，它正在指挥狼萨满完成某种仪式。击败它们，阻止献祭。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral", "faith"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_bell_ringer"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 3
    },
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "dead_road_lantern",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q056 圣遗物的下落

**目标**：寻回大教堂失落的圣遗物。

> 灰烬祭司死后，它的面具碎裂，露出下面的一张羊皮纸——一张清单，列着七件"不能被带出灰烬交界"的圣遗物。其中一件就藏在大教堂地下墓室。
>
> 你在墓室最深处的石棺里找到了它：一盏 `dead_road_lantern`，灯油已经干涸，但灯芯还在微微发光。它不属于主世界，也不属于这里——它是门的钥匙。

**触发流程**：
- 触发方式：完成 Q055 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_ash_priest`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂圣遗物清单现世，委托板悬赏取回第一件遗物。

**任务规格**：
```json
{
  "quest_id": "ashen_holy_relic",
  "display_name": "圣遗物的下落",
  "description": "灰烬祭司随身携带的清单上列着七件圣遗物，其中一件藏在大教堂地下墓室。取回它，也许能打开更深的道路。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral", "faith"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_ash_priest"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "recover_lantern",
      "objective_type": "submit_item",
      "target_id": "dead_road_lantern",
      "target_value": 1
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q057 十字军的终结

**目标**：击败驻守大教堂侧厅的灰烬十字军残部。

> 侧厅的彩色玻璃全部碎了，只剩一个持剑骑士的轮廓还完整。地上的尸体穿着锈蚀的板甲，胸甲上刻着"界火不灭"。当你走近，它们一个接一个地站了起来。
>
> 它们的动作整齐划一，像是被同一根线操纵。领头的骑士没有头，但它的剑知道你在哪里。你意识到，这些不是亡灵——是某种纪律的残余，一场早已失败的远征最后的队列。

**触发流程**：
- 触发方式：完成 Q056 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_holy_relic`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂侧厅的灰烬十字军残部开始活化，委托板悬赏清理。

**任务规格**：
```json
{
  "quest_id": "ashen_crusaders_end",
  "display_name": "十字军的终结",
  "description": "大教堂侧厅中沉睡着一支灰烬十字军的残部。它们的铠甲上刻着“界火不灭”，如今被某种力量重新唤醒。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_holy_relic"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 5
    },
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 700
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 5
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q058 大教堂深处

**目标**：深入大教堂地下圣所，阻止仪式完成。

> 地下圣所的阶梯长得不像这座建筑能容纳。墙壁上开始出现主世界的铭文——不是刻上去的，是长出来的，像苔藓。越往下，两个世界的边界越模糊。
>
> 圣所中央有一个祭坛，祭坛上放着一本书，书页自己在翻。四只迷雾编织者守在四角，它们的吟唱和书页的翻动同步。你必须在仪式完成前打断它们。

**触发流程**：
- 触发方式：完成 Q057 后，余烬祭所委托板刷新。
- 前置条件：`quest_completed: ashen_crusaders_end`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂地下圣所传出自动翻书的声音，委托板需要有人阻止仪式。

**任务规格**：
```json
{
  "quest_id": "ashen_cathedral_depths",
  "display_name": "大教堂深处",
  "description": "大教堂地下圣所的祭坛上有一本自动翻动的书，四只迷雾编织者守在四角吟唱。在仪式完成前打断它们。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral", "faith"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_crusaders_end"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 4
    },
    {
      "objective_id": "report_back",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q059 信仰的崩塌

**目标**：向委托板提交大教堂的最终调查报告。

> 你撕下了那本自动翻动的书最后一页。纸上的文字开始燃烧，但不是烧掉——是"离开"，像一群受惊的鸟飞回了书页之间。整座大教堂震动了一下，然后安静下来。
>
> 钟楼的钟终于落了地，发出一声沉闷的响。你知道这不是结束。灰烬王朝的信仰体系还有六个支点，这只是第一个。但你已经知道它们怕什么了：被看见，被记录，被命名。

**触发流程**：
- 触发方式：完成 Q058 后，余烬祭所委托板刷新最终报告契约。
- 前置条件：`quest_completed: ashen_cathedral_depths`。
- 发布者：余烬祭所委托板 `service_contract_board`。
- 接取情境：大教堂事件告一段落，需要将调查结论归档。

**任务规格**：
```json
{
  "quest_id": "ashen_faith_collapse",
  "display_name": "信仰的崩塌",
  "description": "大教堂深处的仪式已被阻止。将最终调查报告提交给委托板，揭开灰烬王朝信仰体系的第一个支点。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "cathedral", "faith"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_cathedral_depths"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "ashen_cathedral_truth",
          "amount": 1
        },
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 九、沉钟书库（Q060-Q067）

#### Q060 溺水的索引

**目标**：为书库搭建通道并清理水中的威胁。

> 沉钟书库的中庭被水淹没，水面平静得像一面黑色的镜子。你扔了一块石头进去，石头没有沉，而是浮在水面下，慢慢溶解。
>
> 水里有东西在游动。不是鱼——是半透明的影子，形状像人，但四肢太长。它们不攻击你，只要你还在岸上。要进入书库，你需要先搭一条临时的通道。

**触发流程**：
- 触发方式：进入灰烬交界后，沉钟书库入口委托板刷新。
- 前置条件：无（第九章起始任务）。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：书库中庭被水淹没，需要木材搭建通道并清理水中暗影。

**任务规格**：
```json
{
  "quest_id": "ashen_drowned_index",
  "display_name": "溺水的索引",
  "description": "沉钟书库的中庭被水淹没。需要木材搭建通道——但水里的东西不喜欢被打扰。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "gather_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 5
    },
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q061 禁书残页

**目标**：用月蕨吸收水分，抢救禁书残页。

> 禁书区的水位更高，书架上的书都被泡烂了。但你发现有些文字还在——它们浮在水面上，像油膜一样，即使书页已经烂掉，文字仍然保持完整。
>
> 草药师教过你，月蕨的根系能吸收异常液体。你把 `moonfern_sample` 放进水里，文字开始重新排列，组合成你能读懂的句子。第一句话是："王朝的火不是被扑灭，是被藏起来了。"

**触发流程**：
- 触发方式：完成 Q060 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_drowned_index`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：禁书区文字浮于水面，需要月蕨样本吸收异常液体并抢救知识。

**任务规格**：
```json
{
  "quest_id": "ashen_forbidden_pages",
  "display_name": "禁书残页",
  "description": "书库里的禁书被水泡烂了，但有些文字还在。用月蕨吸收水分，也许能读出被禁止的知识。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_drowned_index"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "skill_unlock",
          "target_id": "mage_dispel_magic",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q062 知识之虫

**目标**：清除蚕食禁书的知识之虫。

> 书库最深处的书架在动。不是风，是书脊之间有东西在穿梭——一种半透明的蠕虫，身体由文字组成。它们啃食书页，被啃过的地方不是空白，而是变成了另一种语言。
>
> 你点燃火把，它们没有逃，反而向你聚拢。它们想吃掉你身上的"已知"——你的记忆、名字、技能。幸好，它们的实体部分仍然会被利器切断。

**触发流程**：
- 触发方式：完成 Q061 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_forbidden_pages`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：禁书区出现以文字为食的知识之虫，需要清剿以保护剩余典籍。

**任务规格**：
```json
{
  "quest_id": "ashen_knowledge_worm",
  "display_name": "知识之虫",
  "description": "沉钟书库深处出现了一种由文字组成的蠕虫，它们啃食禁书并改变内容。清除这些知识之虫。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_forbidden_pages"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_worms",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 450
    },
    {
      "reward_type": "item",
      "item_id": "antidote_herb",
      "quantity": 3
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q063 图书馆员的幽灵

**目标**：帮助图书馆员的残影取回藏书印章。

> 一个穿着湿透长袍的老人坐在索引台前，他的脸被水浸泡得模糊不清。他不是活人，但也没有敌意——至少一开始没有。"目录需要更新，"他说，"帮我找到三本没有被虫吃过的书，我就给你印章。"
>
> 你找来了书。他接过去，手指穿过书页，像是想起什么。"我以前也活着，"他说，"然后我发现了一个不该被读出的词。现在这个词在我身体里，我也在书里。"

**触发流程**：
- 触发方式：完成 Q062 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_knowledge_worm`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：图书馆员的幽灵守在索引台前，需要帮他找回未被虫蚀的书籍以换取印章。

**任务规格**：
```json
{
  "quest_id": "ashen_librarian_ghost",
  "display_name": "图书馆员的幽灵",
  "description": "沉钟书库的索引台前守着一位图书馆员的幽灵。帮他找回三本未被知识之虫侵蚀的书，换取藏书印章。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_knowledge_worm"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "submit_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 2
    },
    {
      "objective_id": "report_to_ghost",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "drowned_library_index",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q064 淹没的密室

**目标**：打通被水淹没的密室并取回核心文献。

> 书库底层有一扇门，门缝不断渗出黑水。门上的锁不是金属的，是一团缠绕的头发。你拽开门，水涌出来，带着纸浆和某种更像墨水的液体。
>
> 密室里只剩下一个浮在水面上的铁箱。箱子里是一叠手稿，每一页都记载着同一个日期——灰烬王朝灭亡的那天。日期下方只有一句话："我们熄灭了火，但火学会了等待。"

**触发流程**：
- 触发方式：完成 Q063 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_librarian_ghost`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：书库底层密室被黑水淹没，需要取回其中的核心文献。

**任务规格**：
```json
{
  "quest_id": "ashen_flooded_vault",
  "display_name": "淹没的密室",
  "description": "书库底层有一扇被黑水封锁的密室。打通密室，取回记载灰烬王朝灭亡日期的核心文献。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_librarian_ghost"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 8
    },
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q065 符文石

**目标**：在书库遗址中激活并解读符文石。

> 密室的墙后面还有一间更小的石室，中央立着一块符文石。石头上的文字不属于任何已知语言，但当你的影子落在上面时，文字开始移动，排列成你能读懂的句子。
>
> "第七个守门人不是一个人，是一个位置。"你读完这句话，符文石裂成两半，里面流出一滴黑色的液体——不是血，是浓缩的墨水。它落在你手上，留下一个暂时洗不掉的印记。

**触发流程**：
- 触发方式：完成 Q064 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_flooded_vault`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：密室深处的符文石被激活，需要解读其预言并记录内容。

**任务规格**：
```json
{
  "quest_id": "ashen_rune_stones",
  "display_name": "符文石",
  "description": "书库密室深处有一块符文石，当影子落在上面时文字会移动。用月蕨提取墨迹，解读其中的预言。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_flooded_vault"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_moonfern",
      "objective_type": "submit_item",
      "target_id": "moonfern_sample",
      "target_value": 4
    },
    {
      "objective_id": "report_runes",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 550
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "rune_stone_prophecy",
          "amount": 1
        },
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q066 焚书

**目标**：阻止焚书者销毁剩余典籍。

> 你回到书库中层时，空气里已经有烟味。一群狼萨满正在焚烧书架，它们的火焰是冷的，书不是被烧掉，而是被"删除"——纸灰还没落地就消失了。
>
> "知识是污染，"领头的萨满说，"王朝需要干净的终结。"你打断它时，它手中的火把掉在一本敞开的书上。书页没有燃烧，而是把你和它的脸同时映了出来——你们都在书里。

**触发流程**：
- 触发方式：完成 Q065 后，沉钟书库委托板刷新。
- 前置条件：`quest_completed: ashen_rune_stones`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：狼萨满试图冷焚剩余典籍，委托板需要有人阻止焚书。

**任务规格**：
```json
{
  "quest_id": "ashen_book_burning",
  "display_name": "焚书",
  "description": "一群狼萨满正在用冷火焚烧沉钟书库的剩余典籍。阻止它们，别让最后的历史也被删除。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_rune_stones"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 3
    },
    {
      "objective_id": "defeat_wolves",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 650
    },
    {
      "reward_type": "item",
      "item_id": "forge_coal",
      "quantity": 3
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q067 学者的命运

**目标**：将书库核心文献带回委托板，完成学者委托。

> 在书库坍塌前，你找到了最后一份完整的记录：一份名单，列着所有被派往主世界的"学者"真名。你看到其中一个名字和你的姓氏一样——也许是巧合，也许是拼写错误。
>
> 你把文献交给委托板时，书记员没有记录。她只是把名单折好，放进一个标着"不要归档"的抽屉。"有些人的命运，"她说，"不是被找到的，是被认出来的。"

**触发流程**：
- 触发方式：完成 Q066 后，沉钟书库委托板刷新最终归档契约。
- 前置条件：`quest_completed: ashen_book_burning`。
- 发布者：沉钟书库委托板 `service_contract_board`。
- 接取情境：书库事件收尾，需要将核心文献归档并领取学者委托的报酬。

**任务规格**：
```json
{
  "quest_id": "ashen_scholars_fate",
  "display_name": "学者的命运",
  "description": "沉钟书库的核心文献揭示了被派往主世界的学者名单。将文献归档，学者委托至此结束。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "library"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_book_burning"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "scholars_fate",
          "amount": 1
        },
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 十、坠墓镇（Q068-Q073）

#### Q068 亡者的债务

**目标**：调查坠墓镇死者被追债的异象。

> 坠墓镇没有活人，但镇口的账本还在更新。一个戴着账房帽子的骷髅坐在柜台后，用一只烧焦的笔在账本上写写画画。"你欠我一条命，"它对你——不对，是对你身上的某个东西——说。
>
> 你注意到镇上的墓碑都刻着数字，像是欠款。有些墓碑被挖开，里面空空如也。亡者不是复活了，是被收走了。

**触发流程**：
- 触发方式：进入灰烬交界坠墓镇区域后，委托板刷新。
- 前置条件：无（第十章起始任务）。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：坠墓镇出现亡者被追债的异象，需要调查账本与墓碑数字的来源。

**任务规格**：
```json
{
  "quest_id": "ashen_debt_of_dead",
  "display_name": "亡者的债务",
  "description": "坠墓镇的墓碑上刻着欠款数字，亡者似乎被某种契约追债。调查这一异象的真相。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 3
    },
    {
      "objective_id": "collect_markers",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "blood_debt_shawl",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 10
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q069 亡者复生

**目标**：阻止亡者从墓园大规模爬出。

> 墓园的土开始翻滚，像是有什么东西在地下排队。第一只手伸出来时，你看见手指上缠着账本纸。然后更多，更多——它们不是僵尸，是债务的执行者，被某种契约强制唤醒。
>
> 你必须在它们离开墓园前把它们按回去。方法很简单：撕掉它们额头上的契约纸。困难的是，它们不愿意让你靠近。

**触发流程**：
- 触发方式：完成 Q068 后，坠墓镇委托板刷新。
- 前置条件：`quest_completed: ashen_debt_of_dead`。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：墓园亡者大规模苏醒，需要阻止它们离开墓园。

**任务规格**：
```json
{
  "quest_id": "ashen_dead_reborn",
  "display_name": "亡者复生",
  "description": "坠墓园的亡者被契约强制唤醒，正成群爬出地面。在它们离开墓园前将它们按回去。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_debt_of_dead"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_wolves",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 8
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 4
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "constitution",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q070 尸车轨道

**目标**：修复尸车轨道并清除阻碍。

> 坠墓镇外围有一条轨道，用来把尸体运到焚化炉。轨道现在断了，几辆装满尸体的车卡在半路上。尸体开始自己动起来，互相拖拽，像是要把车队拉向某个方向。
>
> 你需要木材和铆钉修复轨道，同时处理那些已经爬出车厢的东西。铁匠说铆钉可以用 `iron_ore` 临时替代——反正这里没人挑剔工艺。

**触发流程**：
- 触发方式：完成 Q069 后，坠墓镇委托板刷新。
- 前置条件：`quest_completed: ashen_dead_reborn`。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：尸车轨道断裂，需要木材与矿石修复并清理尸变体。

**任务规格**：
```json
{
  "quest_id": "ashen_corpse_cart",
  "display_name": "尸车轨道",
  "description": "坠墓镇外围的尸车轨道断裂，装满尸体的车辆卡在半路。修复轨道并清理爬出车厢的亡者。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_dead_reborn"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 6
    },
    {
      "objective_id": "gather_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 4
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 550
    },
    {
      "reward_type": "item",
      "item_id": "forge_coal",
      "quantity": 3
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q071 盗墓贼

**目标**：清剿盗掘坠墓镇的匪徒。

> 一群狼骑兵正在有计划地挖掘墓碑。它们不是普通的盗墓贼——它们在找特定的人。你看到它们从一座墓里拖出一具穿着旧军服的尸体，用布包好，装上马车。
>
> "它们在收集债权人，"你的向导低声说，"债务不会随着死亡消失。它们要把债主也抓回来。"

**触发流程**：
- 触发方式：完成 Q070 后，坠墓镇委托板刷新。
- 前置条件：`quest_completed: ashen_corpse_cart`。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：狼骑兵盗掘坠墓镇特定尸体，需要阻止并夺回尸体。

**任务规格**：
```json
{
  "quest_id": "ashen_tomb_robbers",
  "display_name": "盗墓贼",
  "description": "一群狼骑兵正在坠墓镇有计划地盗掘特定尸体。清剿它们，阻止债权人名单被补齐。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_corpse_cart"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 5
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "agility",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q072 第一位债权人

**目标**：击败觉醒的第一位债权人。

> 镇中心的广场上，一座最大的墓碑裂开了。从里面爬出来的不是骷髅，而是一个戴着王冠残片的高大身影。它没有皮肤，肌肉和灰烬直接暴露在空气中。
>
> "你是来还债的，还是来讨债的？"它问。你不确定它是在问你，还是在问它自己。它的武器是一把锈迹斑斑的账房尺，每一击都像是在计算什么。

**触发流程**：
- 触发方式：完成 Q071 后，坠墓镇委托板刷新。
- 前置条件：`quest_completed: ashen_tomb_robbers`。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：镇中心最大墓碑裂开，第一位债权人觉醒，委托板悬赏击败它。

**任务规格**：
```json
{
  "quest_id": "ashen_first_creditor",
  "display_name": "第一位债权人",
  "description": "坠墓镇中心最大的墓碑裂开，第一位债权人从中觉醒。击败它，打破亡者债务契约的核心。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_tomb_robbers"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    },
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 700
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q073 债务已清

**目标**：向委托板提交坠墓镇事件终结报告。

> 第一位债权人倒下后，镇口的账房骷髅停下了笔。它看了你很久——如果眼眶里那两团灰雾算看的话——然后在账本上写下最后一行："此账户已结清。"
>
> 所有墓碑上的数字开始褪色，像被水洗掉的墨迹。你离开时，听见背后传来一声轻轻的叹息，不是悲伤，是释然。

**触发流程**：
- 触发方式：完成 Q072 后，坠墓镇委托板刷新最终报告契约。
- 前置条件：`quest_completed: ashen_first_creditor`。
- 发布者：坠墓镇委托板 `service_contract_board`。
- 接取情境：第一位债权人被击败，需要将坠墓镇事件的终结报告归档。

**任务规格**：
```json
{
  "quest_id": "ashen_debt_cleared",
  "display_name": "债务已清",
  "description": "第一位债权人已被击败，坠墓镇的债务契约终止。提交最终报告，让亡者真正安息。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "tomb"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_first_creditor"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "item",
      "item_id": "blood_debt_shawl",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "knowledge_unlock",
          "target_id": "debt_of_dead",
          "amount": 1
        },
        {
          "entry_type": "attribute_progress",
          "target_id": "constitution",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 十一、断桥营寨（Q074-Q079）

#### Q074 断桥残梦

**目标**：侦察断桥营寨并击退先遣的迷雾猎手。

> 断桥原本横跨一条干涸的河床，现在只剩半截桥墩。营寨就建在桥墩上，旗帜全被扯掉了，只剩旗杆。你在桥下发现了几具尸体——不是战死的，是吊死的，脖子上挂着同样的木牌："逃兵"。
>
> 两只迷雾猎手从断桥的阴影里扑出来。它们不是守卫，是清理者——专门处理那些试图离开的人。

**触发流程**：
- 触发方式：进入灰烬交界断桥营寨区域后，委托板刷新。
- 前置条件：无（第十一章起始任务）。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：断桥营寨失去联系，需要侦察并清理先遣的迷雾猎手。

**任务规格**：
```json
{
  "quest_id": "ashen_broken_bridge_dream",
  "display_name": "断桥残梦",
  "description": "断桥营寨的旗帜全部被扯掉，桥下吊着逃兵。侦察营寨并击退先遣的迷雾猎手。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 3
    },
    {
      "objective_id": "scout_report",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 450
    },
    {
      "reward_type": "item",
      "item_id": "bandage_roll",
      "quantity": 3
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "perception",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q075 无旗之军

**目标**：削弱营寨外围的无旗部队。

> 营寨里的士兵没有旗帜，没有番号，甚至没有面孔——他们的头盔下只有灰烬。可他们的队列依然整齐，口令依然清晰。这是一支失去了归属但仍在执行命令的军队。
>
> 你俘虏了一个，扯下它的头盔。里面是一团写有文字的灰，文字在不断变化，最后定格成一个日期：王朝覆灭的那一天。

**触发流程**：
- 触发方式：完成 Q074 后，断桥营寨委托板刷新。
- 前置条件：`quest_completed: ashen_broken_bridge_dream`。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：无旗之军仍在执行旧命令，需要先削弱其外围部队。

**任务规格**：
```json
{
  "quest_id": "ashen_flagless_army",
  "display_name": "无旗之军",
  "description": "断桥营寨里驻扎着一支无旗之军，它们没有面孔却仍在执行旧命令。削弱它们的外围防线。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_broken_bridge_dream"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 550
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 4
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q076 siege 准备

**目标**：为反攻断桥营寨收集攻城材料。

> 营寨的城墙虽然破旧，但还立着。要攻进去，需要木材加固冲车，需要铁矿打造抓钩。附近的难民愿意帮忙，但他们需要你先证明不是在送死。
>
> 你在收集材料时注意到，营寨的箭垛后面没有人——只有一排排站着的空铠甲，像是一群等待指令的幽灵。

**触发流程**：
- 触发方式：完成 Q075 后，断桥营寨委托板刷新。
- 前置条件：`quest_completed: ashen_flagless_army`。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：反攻营寨需要木材与铁矿，委托板悬赏收集攻城材料。

**任务规格**：
```json
{
  "quest_id": "ashen_siege_preparation",
  "display_name": "siege 准备",
  "description": "反攻断桥营寨需要大量木材和铁矿来制造冲车与抓钩。收集攻城材料，为总攻做准备。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_flagless_army"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "gather_lumber",
      "objective_type": "submit_item",
      "target_id": "hardwood_lumber",
      "target_value": 8
    },
    {
      "objective_id": "gather_ore",
      "objective_type": "submit_item",
      "target_id": "iron_ore",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "forge_coal",
      "quantity": 4
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "constitution",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q077 逃兵猎杀

**目标**：猎杀从营寨叛逃并劫掠难民的逃兵。

> 不是所有无旗士兵都愿意继续战斗。有一批逃兵离开了营寨，却没有回家——他们在附近的废墟里劫掠难民，比军队更残忍。难民说他们的眼睛是红的，像在发烧。
>
> 你在他们的临时营地里找到了一些被劫的商货，还有一面被烧掉一半的旗帜。旗帜上的纹章和你在主世界见过的某个王国一模一样。

**触发流程**：
- 触发方式：完成 Q076 后，断桥营寨委托板刷新。
- 前置条件：`quest_completed: ashen_siege_preparation`。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：营寨逃兵劫掠难民，委托板悬赏猎杀逃兵并夺回物资。

**任务规格**：
```json
{
  "quest_id": "ashen_deserter_hunt",
  "display_name": "逃兵猎杀",
  "description": "一批从断桥营寨叛逃的士兵正在劫掠附近难民。猎杀他们，并夺回被劫物资的标识。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_siege_preparation"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 6
    },
    {
      "objective_id": "collect_insignia",
      "objective_type": "submit_item",
      "target_id": "bandit_insignia",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 600
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 4
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "agility",
          "amount": 15
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q078 指挥官的王座

**目标**：击败断桥营寨的指挥官及其护卫。

> 营寨中央的指挥帐里，一个穿着旧铠甲的身影坐在由武器熔成的王座上。它没有呼吸，但手指在扶手上敲击着某种节拍——像是战鼓，又像是心跳。
>
> "我接到了命令，"它说，"守住这座桥，直到王朝归来。"你问它王朝还会不会归来，它停顿了很久。"我不知道，"它最后说，"但命令没有截止日期。"

**触发流程**：
- 触发方式：完成 Q077 后，断桥营寨委托板刷新。
- 前置条件：`quest_completed: ashen_deserter_hunt`。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：营寨指挥官死守王座，需要击败它以瓦解无旗之军的指挥。

**任务规格**：
```json
{
  "quest_id": "ashen_commanders_throne",
  "display_name": "指挥官的王座",
  "description": "断桥营寨的指挥官坐在由旧武器熔成的王座上，死守王朝归来的命令。击败它，瓦解指挥核心。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_deserter_hunt"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alphas",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 2
    },
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 750
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q079 最后的防线

**目标**：守住断桥，阻止营寨残部反扑。

> 指挥官倒下后，营寨的士兵并没有溃散。它们开始有序地向断桥集结，像是要执行最后一个命令：不惜一切守住通道。你意识到，它们不是在防御你，是在防御桥另一边的某种东西。
>
> 战斗持续了一整夜。天亮时，桥上的灰烬被风吹散，露出桥面原本的刻字："此桥通向一切开始的地方。"

**触发流程**：
- 触发方式：完成 Q078 后，断桥营寨委托板刷新。
- 前置条件：`quest_completed: ashen_commanders_throne`。
- 发布者：断桥营寨委托板 `service_contract_board`。
- 接取情境：营寨残部向断桥集结，需要守住桥梁并击退反扑。

**任务规格**：
```json
{
  "quest_id": "ashen_last_defense",
  "display_name": "最后的防线",
  "description": "指挥官倒下后，无旗之军残部向断桥集结，执行最后的守桥命令。守住断桥，击退反扑。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "bridge"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_commanders_throne"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 6
    },
    {
      "objective_id": "hold_bridge",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "constitution",
          "amount": 25
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

### 十二、深渊裂界（Q080-Q085）

#### Q080 裂界生存

**目标**：在深渊裂界外围生存并清理迷雾兽。

> 深渊裂界的地面是活的——不是比喻，它会呼吸。你每走一步，脚下的岩石就轻微下陷，然后慢慢回弹。远处的迷雾里传来低沉的共鸣，像是大地在做梦。
>
> 迷雾兽在这里比在别处更大，更安静。它们不是猎手，是免疫系统——而你是外来物。

**触发流程**：
- 触发方式：进入灰烬交界深渊裂界区域后，委托板刷新。
- 前置条件：无（第十二章起始任务）。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：深渊裂界环境恶劣，需要先清理外围迷雾兽以建立立足点。

**任务规格**：
```json
{
  "quest_id": "ashen_abyss_survival",
  "display_name": "裂界生存",
  "description": "深渊裂界是灰烬交界最危险的区域。地面不稳定，迷雾最浓——但最大的危险是那些适应了这里的生物。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_mist_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 800
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "constitution",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q081 恐惧阿尔法

**目标**：猎杀深渊裂界中变异的首领荒狼。

> 这里的狼群首领有三米高，背脊上长满了结晶化的迷雾。它出现时，周围的狼会低下头，不是服从，是恐惧。你闻到它身上的气味——不是野兽，是烧焦的皮革和铁水。
>
> 它没有立刻攻击你。它看着你，像是在确认什么。然后它开口了，不是嚎叫，是一个人的名字——你的名字。

**触发流程**：
- 触发方式：完成 Q080 后，深渊裂界委托板刷新。
- 前置条件：`quest_completed: ashen_abyss_survival`。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：裂界深处出现能呼名的变异阿尔法，委托板悬赏猎杀。

**任务规格**：
```json
{
  "quest_id": "ashen_dread_alpha",
  "display_name": "恐惧阿尔法",
  "description": "裂界深处的狼群首领和其他地方的不同。它们被迷雾改变了——更大，更聪明，而且不再怕火。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_abyss_survival"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alphas",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 900
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "strength",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q082 裂隙守望者

**目标**：消灭守护裂隙的守望者。

> 裂隙在这里不是一条缝，而是一片湖——一片由暗红色光组成的湖。湖边站着几个身影，它们的身体一半在光里，一半在现实中。它们是守望者，确保门不会关闭。
>
> 你数了数：五只迷雾编织者，三只迷雾兽。它们没有看你，但你知道它们已经知道你来。光湖表面开始出现你的手印，虽然你还没有靠近。

**触发流程**：
- 触发方式：完成 Q081 后，深渊裂界委托板刷新。
- 前置条件：`quest_completed: ashen_dread_alpha`。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：裂隙光湖由守望者守护，需要消灭它们以继续深入。

**任务规格**：
```json
{
  "quest_id": "ashen_rift_warden",
  "display_name": "裂隙守望者",
  "description": "有人在维持裂隙的开放。消灭那些守护裂隙的编织者和迷雾兽——也许能延缓两个世界的融合。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_dread_alpha"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 5
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 1000
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 3
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q083 虚空行者

**目标**：猎杀在裂界边缘穿梭的虚空行者。

> 虚空行者是迷雾猎手的变体——更快，更轻，而且似乎能穿过实体。你看到一只从岩石里走出来，又走回岩石，像穿过水面。
>
> 它们不攻击你。它们攻击你的影子。如果你的影子被它们撕碎，你会忘记自己为什么来这里。你必须在阳光——或者任何稳定光源——下与它们战斗。

**触发流程**：
- 触发方式：完成 Q082 后，深渊裂界委托板刷新。
- 前置条件：`quest_completed: ashen_rift_warden`。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：虚空行者在裂界边缘猎杀探索者的影子，需要清理它们。

**任务规格**：
```json
{
  "quest_id": "ashen_void_walker",
  "display_name": "虚空行者",
  "description": "深渊裂界边缘出现了一种能穿越实体的虚空行者。它们攻击影子，而非肉体。在稳定光源下猎杀它们。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_rift_warden"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 5
    },
    {
      "objective_id": "gather_lanterns",
      "objective_type": "submit_item",
      "target_id": "dead_road_lantern",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 850
    },
    {
      "reward_type": "item",
      "item_id": "dead_road_lantern",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "agility",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q084 现实撕裂

**目标**：稳定裂界边缘被撕裂的现实节点。

> 现实在这里是可选的。你看见一座塔倒着生长，一条河在空中流动，一具尸体在和你说话——而那具尸体是你自己。每一个"异常"都是裂隙扩张的证据。
>
> 你找到六个正在渗出的节点。节点周围有狼先锋在巡逻，它们似乎能感知哪些现实是"正确"的。你必须在它们修复现实之前摧毁节点。

**触发流程**：
- 触发方式：完成 Q083 后，深渊裂界委托板刷新。
- 前置条件：`quest_completed: ashen_void_walker`。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：裂界边缘出现多个现实撕裂节点，需要清除守卫并稳定区域。

**任务规格**：
```json
{
  "quest_id": "ashen_reality_tear",
  "display_name": "现实撕裂",
  "description": "深渊裂界边缘的现实正在被撕裂，出现倒长的塔、空中的河流等异常。清除守卫并稳定节点。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_void_walker"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 6
    },
    {
      "objective_id": "defeat_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 950
    },
    {
      "reward_type": "item",
      "item_id": "black_star_wedge",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "intelligence",
          "amount": 20
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

#### Q085 终焉之门

**目标**：击败终焉之门的看守者，关闭大门。

> 裂界尽头是一扇没有材质的门。它不是石头，不是金属，也不是光——它是"被留下打开"这个概念本身。门缝里不断涌出灰烬，每一粒灰都是一句没说完的誓言。
>
> 守门的是一只巨大的阿尔法，和两只编织者。阿尔法的胸口嵌着 `black_crown_core`，那是维持门开启的心脏。你击败它时，门发出一声类似哭泣的响声，然后慢慢闭合。
>
> 你站在闭合的门前，手里握着核心。你知道这不是胜利——只是暂停。门还会再开，因为有人从另一边需要它。

**触发流程**：
- 触发方式：完成 Q084 后，深渊裂界委托板刷新最终决战契约。
- 前置条件：`quest_completed: ashen_reality_tear`。
- 发布者：深渊裂界委托板 `service_contract_board`。
- 接取情境：终焉之门是深渊裂界的核心，必须击败看守者并关闭大门。

**任务规格**：
```json
{
  "quest_id": "ashen_final_gate",
  "display_name": "终焉之门",
  "description": "深渊裂界尽头有一扇概念层面的终焉之门。击败守门者，夺取黑冠核心，暂时关闭两界通道。",
  "provider_interaction_id": "service_contract_board",
  "tags": ["ashen_intersection", "main_story", "abyss", "milestone"],
  "accept_requirements": [
    {
      "requirement_type": "quest_completed",
      "quest_id": "ashen_reality_tear"
    }
  ],
  "objective_defs": [
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    },
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    },
    {
      "objective_id": "report_to_board",
      "objective_type": "settlement_action",
      "target_id": "service_contract_board",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 1500
    },
    {
      "reward_type": "item",
      "item_id": "black_crown_core",
      "quantity": 1
    },
    {
      "reward_type": "pending_character_reward",
      "member_id": "hero",
      "entries": [
        {
          "entry_type": "attribute_progress",
          "target_id": "willpower",
          "amount": 25
        },
        {
          "entry_type": "knowledge_unlock",
          "target_id": "final_gate",
          "amount": 1
        }
      ]
    }
  ],
  "is_repeatable": false
}
```

---

## 第三卷：悬赏与日常

### 十三、主世界悬赏（Q086-Q093）

#### Q086 狼群悬赏

**目标**：清理商道附近的荒狼群。

> 商人公会发布的悬赏总是很简单：死了几只狼，付多少钱。但你注意到这次清单的背面有一行小字："不要留下完整的尸体。"
>
> 你问书记员为什么。她头也不抬："最近有人在收集狼骨。不是我们做皮毛生意的那种。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：商道附近荒狼群聚集，悬赏猎人清理。

**任务规格**：
```json
{
  "quest_id": "bounty_wolf_pack",
  "display_name": "狼群悬赏",
  "description": "商道附近的荒狼群又聚集起来了。清理它们，赏金归你。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_wolves",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_pack",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 100
    },
    {
      "reward_type": "item",
      "item_id": "beast_hide",
      "quantity": 2
    }
  ],
  "is_repeatable": true
}
```

---

#### Q087 迷雾猎手

**目标**：猎杀边境出没的迷雾突袭者。

> 迷雾猎手不会直接杀死猎物。它们会绕着目标跑，越来越快，直到猎物自己崩溃。边境的哨兵说，被它们杀死的士兵脸上没有恐惧，只有困惑。
>
> "它们让你忘记自己在哪，"一个老兵说，"然后你就可以在任何地方死去。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：迷雾猎手在边境神出鬼没，悬赏猎杀以保障巡逻安全。

**任务规格**：
```json
{
  "quest_id": "bounty_mist_harrier",
  "display_name": "迷雾猎手",
  "description": "迷雾突袭者在边境神出鬼没。它们的速度很快，但赏金更高。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 250
    }
  ],
  "is_repeatable": true
}
```

---

#### Q088 阿尔法猎杀

**目标**：猎杀统合荒狼的首领。

> 这只阿尔法不是普通的狼王。巡逻队注意到它在有目的地吞并其他狼群，而且只选择那些靠近裂隙的群体。它的皮毛上有一道旧疤，形状像是一道门。
>
> "它在组织军队，"哨兵队长说，"不是猎队。军队。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：一只狼群首领正在统合周边荒狼，悬赏在它壮大前除掉它。

**任务规格**：
```json
{
  "quest_id": "bounty_wolf_alpha",
  "display_name": "阿尔法猎杀",
  "description": "一只狼群首领正在统合周边的荒狼。在它壮大之前除掉它。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_alpha",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 1
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "item",
      "item_id": "beast_hide",
      "quantity": 5
    }
  ],
  "is_repeatable": true
}
```

---

#### Q089 迷雾兽猎杀

**目标**：猎杀裂隙附近游荡的迷雾兽。

> 迷雾兽是裂隙渗出后最常见的威胁。它们笨重，但皮糙肉厚。一个商人曾用三辆货车的速度撞向一只迷雾兽，结果马车碎了，兽只退了一步。
>
> "打它们的腿，"猎人们说，"它们的腿是雾凝成的，最脆弱。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：迷雾兽在裂隙附近游荡，威胁商道，悬赏猎杀。

**任务规格**：
```json
{
  "quest_id": "bounty_mist_beast",
  "display_name": "迷雾兽猎杀",
  "description": "迷雾兽是裂隙渗出后最常见的威胁。它们笨重，但皮糙肉厚。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_mist_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    }
  ],
  "is_repeatable": true
}
```

---

#### Q090 编织者悬赏

**目标**：猎杀扭曲现实的迷雾编织者。

> 迷雾编织者的悬赏一直是最高的。不是因为它们难找——它们难杀。它们能把自己藏进现实的褶皱里，你明明看见它站在那里，箭却穿过去打中空气。
>
> "别瞄准它们，"一个活着回来的猎人说，"瞄准它们身边的影子。影子是实的。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：迷雾编织者的法术能扭曲现实，悬赏很高，需要高手接取。

**任务规格**：
```json
{
  "quest_id": "bounty_mist_weaver",
  "display_name": "编织者悬赏",
  "description": "迷雾编织者的法术能扭曲小范围的现实。悬赏很高，因为很少有人能活着回来。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": true
}
```

---

#### Q091 先锋清剿

**目标**：清剿狼群中的先锋个体。

> 狼先锋总是冲在最前面。它们不聪明，但快，而且不怕疼。商队最怕的不是阿尔法——阿尔法会评估风险。先锋不会。
>
> "它们是天生的破阵者，"一个退役士兵说，"如果你能挡住第一只，后面的就会犹豫。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：狼先锋是商队最大威胁之一，悬赏清剿冲在最前面的先锋个体。

**任务规格**：
```json
{
  "quest_id": "bounty_wolf_vanguard",
  "display_name": "先锋清剿",
  "description": "狼先锋是荒狼群中最具攻击性的个体。它们总是冲在最前面。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 3
    }
  ],
  "is_repeatable": true
}
```

---

#### Q092 萨满猎杀

**目标**：猎杀用仪式强化狼群的萨满。

> 狼萨满本身不危险——危险的是它们的能力。它们会画圈、敲骨、念咒，然后普通荒狼的眼睛就会变红，速度和力量都提升一倍。
>
> "先杀萨满，"每个有经验的猎人都这么说，"但萨满总是躲在最后面。"

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：狼萨满用仪式强化其他荒狼，悬赏优先猎杀萨满。

**任务规格**：
```json
{
  "quest_id": "bounty_wolf_shaman",
  "display_name": "萨满猎杀",
  "description": "狼萨满会用奇怪的仪式强化其他荒狼。优先解决它们，战斗会轻松很多。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 450
    }
  ],
  "is_repeatable": true
}
```

---

#### Q093 骑兵清剿

**目标**：清剿商道上的狼骑兵。

> 狼骑兵的机动性让它们成为商队最大的噩梦。它们不正面冲锋，而是绕着车队跑，寻找最弱的一辆车。一旦找到，就会像剪刀一样切断队伍的尾部。
>
> 商人公会的悬赏不限量——因为损失也不限量。

**触发流程**：
- 触发方式：在主世界任意悬赏板可见。
- 前置条件：无。
- 发布者：主世界悬赏板 `service_bounty_registry`。
- 接取情境：狼骑兵机动性强，商队损失惨重，悬赏清剿不限量。

**任务规格**：
```json
{
  "quest_id": "bounty_wolf_raider",
  "display_name": "骑兵清剿",
  "description": "狼骑兵的机动性让它们成为商队最大的噩梦。悬赏不限量。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "main_world"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 6
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 300
    },
    {
      "reward_type": "item",
      "item_id": "bandit_insignia",
      "quantity": 2
    }
  ],
  "is_repeatable": true
}
```

---

### 十四、灰烬交界悬赏（Q094-Q100）

#### Q094 灰烬兽群

**目标**：清剿灰烬交界游荡的迷雾兽群。

> 灰烬交界里的迷雾兽比主世界的更安静。它们不嚎叫，不示威，只是站在雾里等你靠近。等你看见它们时，通常已经来不及了。
>
> 悬赏板上的纸条很简短："三只。不要完整皮。"你猜是某位研究者需要样本，而不是猎人需要毛皮。

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：灰烬兽群在交界区域游荡，悬赏清剿以保障据点安全。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_beasts",
  "display_name": "灰烬兽群",
  "description": "灰烬交界中的迷雾兽群比主世界更安静也更危险。清剿它们，带回悬赏。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_mist_beasts",
      "objective_type": "defeat_enemy",
      "target_id": "mist_beast",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 200
    },
    {
      "reward_type": "item",
      "item_id": "beast_hide",
      "quantity": 3
    }
  ],
  "is_repeatable": true
}
```

---

#### Q095 编织者猎杀

**目标**：猎杀灰烬交界中活跃的迷雾编织者。

> 灰烬交界的编织者似乎在进行某种大型同步仪式。你杀死一只时，另外几只会在同一瞬间尖叫——不是用嘴，是用整个身体震动空气。
>
> 悬赏要求三只。书记员提醒你："最好在同一天完成。它们会记住仇恨。"

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：灰烬交界编织者活跃，悬赏猎杀以削弱其仪式网络。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_weavers",
  "display_name": "编织者猎杀",
  "description": "灰烬交界中的迷雾编织者正在同步进行某种仪式。猎杀它们，打断其网络。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_weavers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_weaver",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 450
    },
    {
      "reward_type": "item",
      "item_id": "calamity_shard",
      "quantity": 1
    }
  ],
  "is_repeatable": true
}
```

---

#### Q096 迷雾突袭

**目标**：猎杀灰烬交界边缘的迷雾猎手。

> 迷雾猎手在灰烬交界不像猎手，更像信使。它们从一个据点跑到另一个据点，传递着某种肉眼看不见的讯息。拦截它们，就是拦截讯息。
>
> 但你总觉得它们也在传递关于你的讯息——每次你出现在裂界，它们的数量就会增加。

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：迷雾猎手在交界边缘频繁突袭，悬赏猎杀以截断讯息传递。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_harriers",
  "display_name": "迷雾突袭",
  "description": "灰烬交界边缘的迷雾猎手像信使一样穿梭。猎杀它们，截断其讯息网络。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_harriers",
      "objective_type": "defeat_enemy",
      "target_id": "mist_harrier",
      "target_value": 4
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 350
    },
    {
      "reward_type": "item",
      "item_id": "travel_ration",
      "quantity": 2
    }
  ],
  "is_repeatable": true
}
```

---

#### Q097 阿尔法狩猎

**目标**：猎杀灰烬交界中的变异阿尔法。

> 这里的阿尔法不只是首领，它们是地标。每只变异阿尔法都占据着一片废墟，其他生物会避开它的领地。杀死一只，地图就安全一点。
>
> 但你杀死的第二只阿尔法临死前看了你一眼，那眼神里没有兽性，只有某种你熟悉的东西——失望。

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：灰烬交界出现多只变异阿尔法，悬赏猎杀以收复失地。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_alpha",
  "display_name": "阿尔法狩猎",
  "description": "灰烬交界中的变异阿尔法是区域地标。猎杀它们，为据点收复失地。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_alphas",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_alpha",
      "target_value": 2
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "beast_hide",
      "quantity": 5
    }
  ],
  "is_repeatable": true
}
```

---

#### Q098 灰烬萨满

**目标**：清剿灰烬交界中的狼萨满。

> 灰烬交界的萨满不再只是强化普通荒狼。它们会把自己的血滴进灰烬里，然后从灰中召唤出更多的迷雾兽。每个萨满都是一座小型兵营。
>
> 悬赏要求三只。你带了比平时多一倍的箭——不是因为它们难杀，是因为它们周围总是有更多东西。

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：灰烬萨满能召唤迷雾兽，威胁据点，悬赏清剿。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_shaman",
  "display_name": "灰烬萨满",
  "description": "灰烬交界中的狼萨满会用灰烬召唤迷雾兽。优先清剿它们，减少交界威胁。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_shamans",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_shaman",
      "target_value": 3
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 400
    },
    {
      "reward_type": "item",
      "item_id": "bandage_roll",
      "quantity": 3
    }
  ],
  "is_repeatable": true
}
```

---

#### Q099 先锋歼灭

**目标**：歼灭灰烬交界中的狼先锋集群。

> 灰烬交界的先锋集群不像主世界那样盲目冲锋。它们会埋伏在雾中，等目标进入裂隙光湖的边缘才发起攻击。那意味着被伏击者往往无处可退。
>
> 一位幸存者告诉你："它们不再只是野兽。它们在执行某种战术。"

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：狼先锋在交界区域采用战术伏击，悬赏歼灭集群。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_vanguard",
  "display_name": "先锋歼灭",
  "description": "灰烬交界中的狼先锋集群懂得埋伏与协同。歼灭它们，削弱交界中的战术威胁。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_vanguards",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_vanguard",
      "target_value": 5
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 450
    },
    {
      "reward_type": "item",
      "item_id": "iron_ore",
      "quantity": 4
    }
  ],
  "is_repeatable": true
}
```

---

#### Q100 骑兵终结

**目标**：终结灰烬交界中狼骑兵的劫掠。

> 灰烬交界的狼骑兵不再只是巡逻。它们有组织地劫掠各据点之间的补给线，把俘虏运往裂界深处。没有人见过那些被带走的人回来。
>
> 悬赏要求七只。你看着这个数字，想起主世界悬赏上同样 unlimited 的骑兵清剿。也许两边的狼骑兵从来就不是两拨。

**触发流程**：
- 触发方式：在灰烬交界任意悬赏板可见。
- 前置条件：无。
- 发布者：灰烬交界悬赏板 `service_bounty_registry`。
- 接取情境：狼骑兵劫掠补给线并押送俘虏，悬赏终结其劫掠行动。

**任务规格**：
```json
{
  "quest_id": "bounty_ashen_raider",
  "display_name": "骑兵终结",
  "description": "灰烬交界中的狼骑兵有组织地劫掠补给线并押送俘虏。终结它们的劫掠行动。",
  "provider_interaction_id": "service_bounty_registry",
  "tags": ["bounty", "repeatable", "ashen_intersection"],
  "accept_requirements": [],
  "objective_defs": [
    {
      "objective_id": "defeat_raiders",
      "objective_type": "defeat_enemy",
      "target_id": "wolf_raider",
      "target_value": 7
    }
  ],
  "reward_entries": [
    {
      "reward_type": "gold",
      "amount": 500
    },
    {
      "reward_type": "item",
      "item_id": "bandit_insignia",
      "quantity": 3
    }
  ],
  "is_repeatable": true
}
```

---
