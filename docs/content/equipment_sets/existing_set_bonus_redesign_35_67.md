# 既有传奇套装 35–67：Set Bonus Layer 重设计

> 状态：**Planned / content redesign（第 53 节凤凰重生已落地）**。
>
> 除第 53 节外，本文只重设计套装阈值层，不代表 `GearSetDef`、阈值 evaluator、derived trait/source、装备能力、UI、AI、preview、存档或 focused regression 已落地；凤凰重生的当前真相见 [装备套装系统](../../design/progression/equipment_sets.md)。
>
> 审计基线：2026-08-08 当前 checkout；原物品文档、[set_bonus_design.md](set_bonus_design.md) 与 [dragon_scale_set_full_landing.md](../../proposals/inventory/dragon_scale_set_full_landing.md)。

## 1. 冻结边界

以下内容全部冻结，不在本文改写：

- 每套 10 个原成员、`item_id`、中文/英文物品名、描述、价格、获取叙述。
- 十个槽位：`head`、`body`、`hands`、`feet`、`cloak`、`necklace`、`ring_1`、`ring_2`、`special_trinket`、`badge`。
- 原 set tag、单件 `attribute_modifiers`、单件特殊效果及单件 usage。
- 单件与套装阈值是两个独立来源；语义相似不静默删除、合并或共享次数。

本文明确覆盖原 `set_bonus_design.md` 中套装 35–67 的 2/4 件阈值。正式 authoring 时以本文的拓扑、数值与触发合同生成新的 `GearSetThresholdDef`；旧阈值不得与新阈值同时注册。

## 2. 统一运行时合同

### 2.1 Membership 与累计阈值

- 唯一成员 owner 使用套装侧 immutable `GearSetDefinition.member_item_ids[10]`；不在 `ItemDef` 新增或修改任何字段。set tag 只供搜索和文案，不参与计数。
- `GearSetEvaluationService` 按有效 `EquipmentEntry` 去重；多槽占用只贡献 1 件，损坏、卸下或换装后立即重算。
- 本文所有阈值均为累计激活；达到 10 件时，前面所有非 replacement 效果继续有效。
- 每套只有 4 或 5 档，且 10 件必有 capstone。

#### 2.1.1 可审计的 `member_item_ids[10]`

以下清单逐项来自原物品文档，不通过 tag、显示名或命名前缀在 runtime 推断：

- 35–40 护甲：`sets_31_to_40_armor.md`；饰品：`sets_31_to_40_accessories.md`。
- 41–50 护甲：`sets_41_to_50.md`；饰品：`sets_41_to_50_accessories.md`。
- 51–60 护甲：`sets_51_to_60.md`；饰品：`sets_51_to_60_accessories.md`。
- 61–67 护甲：`sets_61_to_70.md`；饰品：`sets_61_to_70_accessories.md`。

```text
35 white_raven_set = [armor_white_raven_head, armor_white_raven_body, armor_white_raven_hands, armor_white_raven_feet, acc_white_raven_cloak, acc_white_raven_necklace, acc_white_raven_ring_1, acc_white_raven_ring_2, acc_white_raven_trinket, acc_white_raven_badge]
36 red_fox_set = [armor_red_fox_head, armor_red_fox_body, armor_red_fox_hands, armor_red_fox_feet, acc_red_fox_cloak, acc_red_fox_necklace, acc_red_fox_ring_1, acc_red_fox_ring_2, acc_red_fox_trinket, acc_red_fox_badge]
37 green_viper_set = [armor_green_viper_head, armor_green_viper_body, armor_green_viper_hands, armor_green_viper_feet, acc_green_viper_cloak, acc_green_viper_necklace, acc_green_viper_ring_1, acc_green_viper_ring_2, acc_green_viper_trinket, acc_green_viper_badge]
38 golden_butterfly_set = [armor_golden_butterfly_head, armor_golden_butterfly_body, armor_golden_butterfly_hands, armor_golden_butterfly_feet, acc_golden_butterfly_cloak, acc_golden_butterfly_necklace, acc_golden_butterfly_ring_1, acc_golden_butterfly_ring_2, acc_golden_butterfly_trinket, acc_golden_butterfly_badge]
39 amethyst_psychic_set = [armor_amethyst_psychic_head, armor_amethyst_psychic_body, armor_amethyst_psychic_hands, armor_amethyst_psychic_feet, acc_amethyst_psychic_cloak, acc_amethyst_psychic_necklace, acc_amethyst_psychic_ring_1, acc_amethyst_psychic_ring_2, acc_amethyst_psychic_trinket, acc_amethyst_psychic_badge]
40 black_iron_warlock_set = [armor_black_iron_warlock_head, armor_black_iron_warlock_body, armor_black_iron_warlock_hands, armor_black_iron_warlock_feet, acc_black_iron_warlock_cloak, acc_black_iron_warlock_necklace, acc_black_iron_warlock_ring_1, acc_black_iron_warlock_ring_2, acc_black_iron_warlock_trinket, acc_black_iron_warlock_badge]
41 solar_paladin_set = [armor_solar_paladin_head, armor_solar_paladin_body, armor_solar_paladin_hands, armor_solar_paladin_feet, acc_solar_paladin_cloak, acc_solar_paladin_necklace, acc_solar_paladin_ring_1, acc_solar_paladin_ring_2, acc_solar_paladin_trinket, acc_solar_paladin_badge]
42 void_warden_set = [armor_void_warden_head, armor_void_warden_body, armor_void_warden_hands, armor_void_warden_feet, acc_void_warden_cloak, acc_void_warden_necklace, acc_void_warden_ring_1, acc_void_warden_ring_2, acc_void_warden_trinket, acc_void_warden_badge]
43 nature_warden_set = [armor_nature_warden_head, armor_nature_warden_body, armor_nature_warden_hands, armor_nature_warden_feet, acc_nature_warden_cloak, acc_nature_warden_necklace, acc_nature_warden_ring_1, acc_nature_warden_ring_2, acc_nature_warden_trinket, acc_nature_warden_badge]
44 rune_smith_set = [armor_rune_smith_head, armor_rune_smith_body, armor_rune_smith_hands, armor_rune_smith_feet, acc_rune_smith_cloak, acc_rune_smith_necklace, acc_rune_smith_ring_1, acc_rune_smith_ring_2, acc_rune_smith_trinket, acc_rune_smith_badge]
45 soul_reaper_set = [armor_soul_reaper_head, armor_soul_reaper_body, armor_soul_reaper_hands, armor_soul_reaper_feet, acc_soul_reaper_cloak, acc_soul_reaper_necklace, acc_soul_reaper_ring_1, acc_soul_reaper_ring_2, acc_soul_reaper_trinket, acc_soul_reaper_badge]
46 chrono_walker_set = [armor_chrono_walker_head, armor_chrono_walker_body, armor_chrono_walker_hands, armor_chrono_walker_feet, acc_chrono_walker_cloak, acc_chrono_walker_necklace, acc_chrono_walker_ring_1, acc_chrono_walker_ring_2, acc_chrono_walker_trinket, acc_chrono_walker_badge]
47 dream_weaver_set = [armor_dream_weaver_head, armor_dream_weaver_body, armor_dream_weaver_hands, armor_dream_weaver_feet, acc_dream_weaver_cloak, acc_dream_weaver_necklace, acc_dream_weaver_ring_1, acc_dream_weaver_ring_2, acc_dream_weaver_trinket, acc_dream_weaver_badge]
48 abyss_stalker_set = [armor_abyss_stalker_head, armor_abyss_stalker_body, armor_abyss_stalker_hands, armor_abyss_stalker_feet, acc_abyss_stalker_cloak, acc_abyss_stalker_necklace, acc_abyss_stalker_ring_1, acc_abyss_stalker_ring_2, acc_abyss_stalker_trinket, acc_abyss_stalker_badge]
49 star_gazer_set = [armor_star_gazer_head, armor_star_gazer_body, armor_star_gazer_hands, armor_star_gazer_feet, acc_star_gazer_cloak, acc_star_gazer_necklace, acc_star_gazer_ring_1, acc_star_gazer_ring_2, acc_star_gazer_trinket, acc_star_gazer_badge]
50 chaos_herald_set = [armor_chaos_herald_head, armor_chaos_herald_body, armor_chaos_herald_hands, armor_chaos_herald_feet, acc_chaos_herald_cloak, acc_chaos_herald_necklace, acc_chaos_herald_ring_1, acc_chaos_herald_ring_2, acc_chaos_herald_trinket, acc_chaos_herald_badge]
51 thunder_god_set = [armor_thunder_god_head, armor_thunder_god_body, armor_thunder_god_hands, armor_thunder_god_feet, acc_thunder_god_cloak, acc_thunder_god_necklace, acc_thunder_god_ring_1, acc_thunder_god_ring_2, acc_thunder_god_trinket, acc_thunder_god_badge]
52 medusa_gaze_set = [armor_medusa_gaze_head, armor_medusa_gaze_body, armor_medusa_gaze_hands, armor_medusa_gaze_feet, acc_medusa_gaze_cloak, acc_medusa_gaze_necklace, acc_medusa_gaze_ring_1, acc_medusa_gaze_ring_2, acc_medusa_gaze_trinket, acc_medusa_gaze_badge]
53 phoenix_rebirth_set = [armor_phoenix_rebirth_head, armor_phoenix_rebirth_body, armor_phoenix_rebirth_hands, armor_phoenix_rebirth_feet, acc_phoenix_rebirth_cloak, acc_phoenix_rebirth_necklace, acc_phoenix_rebirth_ring_1, acc_phoenix_rebirth_ring_2, acc_phoenix_rebirth_trinket, acc_phoenix_rebirth_badge]
54 siren_song_set = [armor_siren_song_head, armor_siren_song_body, armor_siren_song_hands, armor_siren_song_feet, acc_siren_song_cloak, acc_siren_song_necklace, acc_siren_song_ring_1, acc_siren_song_ring_2, acc_siren_song_trinket, acc_siren_song_badge]
55 werewolf_curse_set = [armor_werewolf_curse_head, armor_werewolf_curse_body, armor_werewolf_curse_hands, armor_werewolf_curse_feet, acc_werewolf_curse_cloak, acc_werewolf_curse_necklace, acc_werewolf_curse_ring_1, acc_werewolf_curse_ring_2, acc_werewolf_curse_trinket, acc_werewolf_curse_badge]
56 shadow_dancer_set = [armor_shadow_dancer_head, armor_shadow_dancer_body, armor_shadow_dancer_hands, armor_shadow_dancer_feet, acc_shadow_dancer_cloak, acc_shadow_dancer_necklace, acc_shadow_dancer_ring_1, acc_shadow_dancer_ring_2, acc_shadow_dancer_trinket, acc_shadow_dancer_badge]
57 crystal_seer_set = [armor_crystal_seer_head, armor_crystal_seer_body, armor_crystal_seer_hands, armor_crystal_seer_feet, acc_crystal_seer_cloak, acc_crystal_seer_necklace, acc_crystal_seer_ring_1, acc_crystal_seer_ring_2, acc_crystal_seer_trinket, acc_crystal_seer_badge]
58 frost_giant_set = [armor_frost_giant_head, armor_frost_giant_body, armor_frost_giant_hands, armor_frost_giant_feet, acc_frost_giant_cloak, acc_frost_giant_necklace, acc_frost_giant_ring_1, acc_frost_giant_ring_2, acc_frost_giant_trinket, acc_frost_giant_badge]
59 mummy_curse_set = [armor_mummy_curse_head, armor_mummy_curse_body, armor_mummy_curse_hands, armor_mummy_curse_feet, acc_mummy_curse_cloak, acc_mummy_curse_necklace, acc_mummy_curse_ring_1, acc_mummy_curse_ring_2, acc_mummy_curse_trinket, acc_mummy_curse_badge]
60 dragon_rider_set = [armor_dragon_rider_head, armor_dragon_rider_body, armor_dragon_rider_hands, armor_dragon_rider_feet, acc_dragon_rider_cloak, acc_dragon_rider_necklace, acc_dragon_rider_ring_1, acc_dragon_rider_ring_2, acc_dragon_rider_trinket, acc_dragon_rider_badge]
61 flesh_weaver_set = [armor_flesh_weaver_head, armor_flesh_weaver_body, armor_flesh_weaver_hands, armor_flesh_weaver_feet, acc_flesh_weaver_cloak, acc_flesh_weaver_necklace, acc_flesh_weaver_ring_1, acc_flesh_weaver_ring_2, acc_flesh_weaver_trinket, acc_flesh_weaver_badge]
62 bone_lord_set = [armor_bone_lord_head, armor_bone_lord_body, armor_bone_lord_hands, armor_bone_lord_feet, acc_bone_lord_cloak, acc_bone_lord_necklace, acc_bone_lord_ring_1, acc_bone_lord_ring_2, acc_bone_lord_trinket, acc_bone_lord_badge]
63 thorn_queen_set = [armor_thorn_queen_head, armor_thorn_queen_body, armor_thorn_queen_hands, armor_thorn_queen_feet, acc_thorn_queen_cloak, acc_thorn_queen_necklace, acc_thorn_queen_ring_1, acc_thorn_queen_ring_2, acc_thorn_queen_trinket, acc_thorn_queen_badge]
64 ash_walker_set = [armor_ash_walker_head, armor_ash_walker_body, armor_ash_walker_hands, armor_ash_walker_feet, acc_ash_walker_cloak, acc_ash_walker_necklace, acc_ash_walker_ring_1, acc_ash_walker_ring_2, acc_ash_walker_trinket, acc_ash_walker_badge]
65 mist_walker_set = [armor_mist_walker_head, armor_mist_walker_body, armor_mist_walker_hands, armor_mist_walker_feet, acc_mist_walker_cloak, acc_mist_walker_necklace, acc_mist_walker_ring_1, acc_mist_walker_ring_2, acc_mist_walker_trinket, acc_mist_walker_badge]
66 iron_maiden_set = [armor_iron_maiden_head, armor_iron_maiden_body, armor_iron_maiden_hands, armor_iron_maiden_feet, acc_iron_maiden_cloak, acc_iron_maiden_necklace, acc_iron_maiden_ring_1, acc_iron_maiden_ring_2, acc_iron_maiden_trinket, acc_iron_maiden_badge]
67 spider_queen_set = [armor_spider_queen_head, armor_spider_queen_body, armor_spider_queen_hands, armor_spider_queen_feet, acc_spider_queen_cloak, acc_spider_queen_necklace, acc_spider_queen_ring_1, acc_spider_queen_ring_2, acc_spider_queen_trinket, acc_spider_queen_badge]
```

正式 registry 必须逐 ID 解析并同时校验恰好十个唯一成员、十个目标槽各一个、无跨套重复；任何缺失、重复或错误槽位均在 snapshot seal 前失败，不能回退到 tag 计数。

### 2.2 Replacement group

同一成长轴的数值不得逐档相加。表中 `RG:<axis>` 表示稳定 replacement group：

```text
gear_set::<gear_set_id>::<axis>
```

同组只取当前已激活阈值中的最高档。例如 3 件 `mp_max +10 [RG:mana]`、6 件 `mp_max +20 [RG:mana]` 在 6 件时结果是 `+20`，不是 `+30`。不同 axis、不同来源和非成长机制仍按各自正式 stacking rule 处理。

### 2.3 伤害段、防递归与 mitigation

本文出现“主直接段”时，统一采用以下合同：

- 每个真实 `PrimaryDirect` damage segment 分别查询和触发；fixed repeat、repeat-until-fail、随机链、多目标均逐真实段处理，不按 cast、skill、target 或 event batch 去重。
- miss 不触发；save-only 的主直接伤害段可以触发。
- `extra_damage_segments`、DOT/upkeep、地形 tick、反射、自伤、装备或套装能力新生成的伤害、trigger skill 均不再次触发。
- 生成的附伤必须标记 equipment/gear-set origin，并携带 source key，防止同源或异源递归。
- 抗性只使用 `immune > half > normal > double`；重复 `half` 不继续相乘，`cold` 统一写作 `freeze`，`necrotic` 统一写作 `negative_energy`。

### 2.4 Usage、TU、preview 与 AI

- `per_battle` 在正式 battle start 初始化、battle teardown 清除；`per_world_day` 由 `EquipmentState` 顶层 set usage 持久化并随战斗 writeback 原子提交。
- 默认 usage key 为 `gear_set::<gear_set_id>::<threshold_id>`；不读取或消费任何单件 usage。
- 所有战斗持续时间只写 TU，不换算秒或分钟；duration 与 tick 均为 5 TU 的整数倍。
- 本文主动能力只使用当前 AP 成本：短位移、标记和命令通常 1 AP，常规攻击/控制/形态通常 2 AP，大型领域、召唤和终极通常 3 AP。`reaction` 只表示插入正式事件事务的触发时序，本文所有 reaction 均明确为 **不耗 AP**；被动自动触发同样不耗 AP。
- preview/AI 只读取期望结果、剩余 usage、charge 和 domain 状态，不消费次数、不掷真实 RNG、不改真实状态。
- 同时满足多个 reaction/lethal intercept 时必须由 typed reaction ordering 裁决；不得依赖数组顺序或 resource 加载顺序。

### 2.5 Typed owner 记号

| 记号 | Typed owner / 正式落点 |
|---|---|
| `ATTR` | `AttributeModifier` / `AttributeService`，只用于合法静态字段 |
| `TRAIT` | `TraitDef` 的 damage resistance、ability save、save-tag 或 immunity |
| `QUERY` | typed utility、attack、spell-hit、critical、movement、incoming/outgoing query |
| `ABILITY` | equipment-ability fact / condition / action / roll gate / outcome / state schema |
| `STATUS` | battle status、stack、charge、scheduled tick、source-bound cleanup |
| `AREA` | typed area/domain、entry/start tick、visibility、terrain 与移动规则 |
| `SUMMON` | immutable summon definition、roster ownership、command 与性能预算 |
| `WORLD` | world time、天气、地形、信号、关系或尸体等 battle 外 producer |
| `SAVE` | `EquipmentState` usage、strict codec、battle writeback 与 rollback |

## 3. 价格与静态强度校准

下表是十件原物品的 authored 总价与关键静态字段原始合计，用于同批横向校准。它不是当前 runtime 最终值：旧名、饰品 AC、抗性数值和条件字段仍须按 typed owner 迁移。

| # | 套装 | 十件总价 | 关键原始静态总量 | 拓扑 |
|---:|---|---:|---|---|
| 35 | 白鸦信使 | 99,000 | AC 6、MP 20、move 15、stealth 6 | 节奏/诡术 2/3/6/9/10 |
| 36 | 红狐盗贼 | 99,000 | AC 6、MP 5、move 10、stealth 6 | 节奏/诡术 2/3/6/9/10 |
| 37 | 青蛇刺客 | 109,000 | AC 8、poison 25、stealth 3 | 稳步循环 2/4/6/8/10 |
| 38 | 金蝶幻术师 | 99,000 | AC 5、MP 55 | 高魔 3/6/9/10 |
| 39 | 紫晶心灵师 | 109,000 | AC 5、MP 60、`spell_attack_bonus` 1 | 高魔 3/6/9/10 |
| 40 | 黑铁咒术师 | 110,000 | AC 9、MP 60、`spell_attack_bonus` 1 | 仪式/契约 2/5/8/10 |
| 41 | 太阳圣骑士 | 142,000 | AC 11、attack 2、HP 15 | 稳步循环 2/4/6/8/10 |
| 42 | 虚空守护者 | 146,000 | AC 12、attack 2、HP 15、MP 15 | 堡垒 4/6/8/10 |
| 43 | 自然守护者 | 130,000 | AC 9、HP 10、move 15 | 仪式/领域 2/5/8/10 |
| 44 | 符文铁匠 | 130,000 | AC 9、attack 2、HP/MP 各 10 | 仪式/领域 2/5/8/10 |
| 45 | 灵魂收割者 | 135,000 | AC 9、attack 2、HP/MP 各 10 | 稳步循环 2/4/6/8/10 |
| 46 | 时间行者 | 142,000 | AC 5、MP 15、move 25 | 节奏/诡术 2/3/6/9/10 |
| 47 | 梦境编织者 | 135,000 | AC 5、MP 35、psychic 15 | 高魔 3/6/9/10 |
| 48 | 深渊潜行者 | 135,000 | AC 6、attack 1、psychic/freeze 15 | 蜕变 3/5/7/10 |
| 49 | 星辰观测者 | 140,000 | AC 5、MP 35、`spell_attack_bonus` 1 | 高魔 3/6/9/10 |
| 50 | 混沌使者 | 140,000 | AC 5、MP 30、`spell_attack_bonus` 2、`critical_threat_range` 1 | 节奏/诡术 2/3/6/9/10 |
| 51 | 雷神之甲 | 146,000 | AC 11、attack 2、HP/MP 各 15 | 稳步循环 2/4/6/8/10 |
| 52 | 美杜莎之凝视 | 150,000 | AC 12、attack 2、HP 15 | 蜕变 3/5/7/10 |
| 53 | 凤凰重生 | 150,000 | AC 10、HP 30、fire 45 | 蜕变 3/5/7/10 |
| 54 | 塞壬之歌 | 135,000 | AC 9、HP/MP 各 10 | 仪式/领域 2/5/8/10 |
| 55 | 狼人诅咒 | 135,000 | AC 9、attack 2、HP/MP 各 10 | 蜕变 3/5/7/10 |
| 56 | 影舞者 | 130,000 | AC 5、stealth 9、move 10 | 节奏/诡术 2/3/6/9/10 |
| 57 | 水晶先知 | 135,000 | AC 6、MP 35、`spell_attack_bonus` 2 | 高魔 3/6/9/10 |
| 58 | 霜巨人 | 142,000 | AC 11、attack 2、HP/MP 各 15 | 堡垒 4/6/8/10 |
| 59 | 木乃伊诅咒 | 140,000 | AC 5、attack 2、negative 35 | 蜕变 3/5/7/10 |
| 60 | 龙骑士 | 151,000 | AC 12、HP 35、MP 15 | 蜕变 3/5/7/10 |
| 61 | 血肉编织者 | 135,000 | AC 9、HP/MP 各 10 | 仪式/领域 2/5/8/10 |
| 62 | 骸骨领主 | 142,000 | AC 11、attack 2、HP/MP 各 15 | 仪式/领域 2/5/8/10 |
| 63 | 荆棘女王 | 130,000 | AC 9、HP/MP 各 10 | 仪式/领域 2/5/8/10 |
| 64 | 灰烬行者 | 125,000 | AC 5、MP 15、fire 20 | 仪式/领域 2/5/8/10 |
| 65 | 迷雾行者 | 125,000 | AC 5、stealth 6、move 10 | 节奏/诡术 2/3/6/9/10 |
| 66 | 铁处女 | 146,000 | AC 12、attack 3、HP 20 | 堡垒 4/6/8/10 |
| 67 | 蜘蛛女王 | 130,000 | AC 9、attack 1、HP/MP 各 10 | 仪式/领域 2/5/8/10 |

校准原则：低价但已有高 MP/强单件能力的套装不再堆高无条件静态值；高 AC/HP 的堡垒套从 4 件才启动；领域、召唤、回溯、复活和完整形态只放 10 件或其前一档的明确准备阶段。缺少正式获取等级与同档敌人曲线，因此本文冻结机制和相对预算，不声称已经完成最终伤害/生存时长平衡。

## 4. 套装 35–67 逐套设计

## 35. 白鸦信使 / `white_raven_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 低价轻装、传信和位移是连续的小节点；鸦群领域仍只在十件形成完整战斗身份。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 鸦之眼 | `utility_check(perception,+2)`、`utility_check(investigation,+2)` `[RG:awareness]`。 | 常驻。 | `QUERY`；utility-check producer 尚缺。 |
| 3 | 轻羽步 | `move_point_capacity_delta +1 [RG:mobility]`；忽略由普通信件、卷轴和轻型道具产生的携带惩罚。 | 常驻。 | `QUERY`；物品重量标签需 typed fact。 |
| 6 | 信标网 | awareness 升为 perception/investigation 各 `+3 [RG:awareness]`；可感知当前战斗地图 12 格内 canonical signal、portal、beacon。 | 常驻，只返回方向与类别，不揭示内容。 | `WORLD` + `QUERY`；world/battle signal producer 尚缺。 |
| 9 | 鸦步 | 消耗 `1 AP`，传送到 4 格内可见空格；若终点邻接 signal/beacon，距离改为 6 格。 | `per_battle=2`，两次独立 charge；无持续时间。 | `ABILITY` + `STATUS`；合法落点与 usage。 |
| 10 | 白鸦群飞 | 消耗 `3 AP`，以自身为中心生成半径 6 格、持续 `180 TU` 的移动鸦群：敌人视距上限 3 格，穿过领域的敌方远程攻击 `attack_bonus -2`；自身 mobility 升为 `+2 [RG:mobility]`，并获得 3 层鸦步 charge，每次鸦步仍消耗 `1 AP`、可传送 5 格。 | `per_world_day=1`；鸦步 charge 随领域结束清除。 | `AREA` + `QUERY` + `SAVE`；移动领域、视距与攻击查询。 |

**单件交互与 usage：** 白鸦哨笛的传信、徽章的同伴感知、单件 camouflage 和单件移动均保持物理物品 source；信标网只读它们产生的 typed signal，不消费哨笛次数。相同 move axis 由各来源按正式规则处理，本文内部只按 RG 取最高档。

**落地缺口：** `gear_set_threshold` derived source、world signal/letter port、移动视距领域、合法传送格、AI 对遮蔽和剩余 charge 的估值。

## 36. 红狐盗贼 / `red_fox_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 套装依靠潜行、脱离和错位连续制造节奏，不用四件直接获得完整诱饵终极。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 狐步 | `utility_check(stealth,+2)`、`utility_check(sleight_of_hand,+2)` `[RG:trick]`。 | 常驻。 | `QUERY`。 |
| 3 | 无声脱离 | 若本次行动未造成伤害，在行动结束时自动进入 hidden（不耗 AP）；不绕过视线和相邻敌人限制。 | `per_battle=1`；hidden 按现有规则结束。 | `ABILITY` + hidden fact。 |
| 6 | 狡狐身法 | trick 升为 stealth/sleight 各 `+3 [RG:trick]`；`agility +1 [RG:agility]`、`armor_ac_bonus +1 [RG:defense]`。 | 常驻。 | `QUERY` + `ATTR`。 |
| 9 | 错身幻影 | 被单目标主直接攻击命中前可 reaction（不耗 AP）创建 1 HP、AC 等于穿戴者当前 AC 的幻影；攻击者进行 perception save `DC 16`，失败则该次攻击改为命中幻影。 | `per_battle=2`；每次创建一个、结算后消失。 | `ABILITY` + illusion entity + attack redirection。 |
| 10 | 九尾骗局 | 消耗 `3 AP` 创建 3 个幻影，持续 `180 TU`；每个幻影只能承受一次命中。穿戴者被选为直接攻击目标时，攻击者先作 perception save `DC 17`，失败则随机命中一个仍存活幻影；幻影对 AI 的目标权重为穿戴者的 `2.0x`，但不能主动攻击。 | `per_world_day=1`；全部幻影消失或到期结束。 | `SUMMON`/illusion、deterministic RNG、AI threat、`SAVE`。 |

**单件交互与 usage：** 单件 camouflage、开锁和陷阱感知继续独立；九尾幻影不消耗任何单件幻象或钥匙 usage。若未来单件也创建 illusion，entity key 必须包含物理实例 id，不能与 set threshold 共用 1 HP 实体。

**落地缺口：** hidden 正式入口、命中重定向事务、illusion roster、perception save、AI threat 和 preview 的期望命中率。

## 37. 青蛇刺客 / `green_viper_set`

**拓扑：稳步循环 2/4/6/8/10。** 抗毒、精准、施毒、伏击和终结天然构成五段刺杀循环；十件才允许自动暴击与短控。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 蛇蜕 | `res(poison,half)`；`utility_check(stealth,+2)`。 | 常驻；重复 half 不叠加。 | `TRAIT` + `QUERY`。 |
| 4 | 毒牙精准 | `attack_bonus +1 [RG:offense]`；正式攻击的 `crit_threshold -1 [RG:critical]`。 | 常驻，只影响正式攻击检定。 | `ATTR` + critical `QUERY`；阈值 query 尚缺。 |
| 6 | 淬毒主段 | 近战武器命中的每个主直接段附加 `1D4 poison`。 | 逐主直接段；遵守第 2.3 节递归排除。 | damage-segment `QUERY` + `ABILITY`。 |
| 8 | 潜伏毒印 | hidden 状态下第一次近战攻击尝试自动武装（不耗 AP）；命中时额外 `2D4 poison [RG:viper_ambush]`，目标作 constitution save `DC 16`，失败获得 poisoned `120 TU`。 | `per_battle=1`；miss 不消费次数，但进入 `60 TU` 冷却后才可再次武装。 | hidden fact、attempt state、save/status、cooldown。 |
| 10 | 青蛇之吻 | hidden 下的下一次近战攻击自动武装（不耗 AP）；命中自动成为 critical，并在各 qualifying 主直接段额外造成 `3D4 poison [RG:viper_ambush]`；目标 constitution save `DC 17`，失败 poisoned `180 TU`，若结果低于 DC 5 或更多，再 paralyzed `30 TU`。 | `per_battle=1`；miss 不消费，但锁定 `60 TU`；命中后消费。8/10 件共享 `gear_set::green_viper_set::hidden_strike` usage。 | critical override、主段、degree-of-failure、`STATUS`。 |

**单件交互与 usage：** 单件注毒、毒液提取和单件攻击附伤均独立逐来源结算；它们生成的 extra segment 不触发 6/8/10 件附伤。8/10 件伏击是同一成长机制，十件时只武装 `RG:viper_ambush` 最高档，不同时追加 `2D4 + 3D4`。重复 poison resistance 只取最强 tier。

**落地缺口：** hidden attempt 生命周期、critical threshold/override、主直接段 context、degree-of-failure 与防递归回归。

## 38. 金蝶幻术师 / `golden_butterfly_set`

**拓扑：高魔 3/6/9/10。** 原物品已有大量小幻象，阈值应以法力、稳定幻象、团队梦境和十件领域四次跃升，避免低件数再堆同类 cantrip。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 金粉心智 | `mp_max +10 [RG:mana]`；deception/performance utility check 各 `+2 [RG:illusion_check]`。 | 常驻。 | `ATTR` + `QUERY`。 |
| 6 | 实体幻蝶 | mana 升为 `mp_max +20 [RG:mana]`；`spell_attack` roll bonus `+1 [RG:spell]`。消耗 `1 AP` 创建一个 1 HP、AC 12、基础移动点 4 的幻蝶诱饵，不能攻击。 | `per_battle=1`；持续 `120 TU`。 | illusion `SUMMON`、spell-attack query。 |
| 9 | 蝶梦庇护 | 消耗 `2 AP` 创建半径 4 格领域；友方 `armor_ac_bonus +1 [RG:dream_defense]`，每 `60 TU` 首次在领域中结算时恢复 `1D4 HP`。 | `per_world_day=1`，定点领域持续 `180 TU`；每友方最多治疗 3 次。 | `AREA` + scheduled heal + source-bound cap。 |
| 10 | 金蝶梦境 | 消耗 `3 AP` 创建半径 5 格领域，持续 `240 TU`。敌人在进入及每 `60 TU` 开始时作 willpower save `DC 17`：失败 disoriented `60 TU`；在同一领域累计第二次失败时改为 stunned `30 TU` 并清零失败计数。友方 defense 升为 `armor_ac_bonus +2 [RG:dream_defense]`，每 `60 TU` 恢复 `1D6 HP`。 | `per_world_day=1`；9 件与 10 件共用 `gear_set::golden_butterfly_set::dream_domain` usage，激活 10 件后只出现 10 件版本。 | `AREA`、replacement domain、失败计数、heal、`SAVE`。 |

**单件交互与 usage：** 单件 minor/major illusion、储存与反射 usage 全部独立。9/10 件使用共享的套装领域 usage，是同一成长机制的 replacement，不是额外一天两次领域。

**落地缺口：** illusion entity、领域 replacement、每目标失败计数、disoriented/stunned、scheduled heal 与 AI 领域估值。

## 39. 紫晶心灵师 / `amethyst_psychic_set`

**拓扑：高魔 3/6/9/10。** 原十件已有高 MP 和心灵能力；阈值只做四次明确法术跃升，把免疫、打断和队伍风暴留到后段。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 紫晶静心 | `res(psychic,half)`；`mp_max +10 [RG:mana]`；insight utility check `+2`。 | 常驻。 | `TRAIT`、`ATTR`、`QUERY`。 |
| 6 | 心灵聚焦 | mana 升为 `mp_max +20 [RG:mana]`；`spell_attack` roll bonus `+1 [RG:spell]`；对 charmed/frightened save `+3 [RG:mind_save]`。 | 常驻。 | spell-attack query、save-tag producer。 |
| 9 | 不动心域 | 穿戴者 immune(charmed)、immune(frightened)；其 psychic spell 的每个主直接段附加 `1D4 psychic`。 | 常驻；附伤遵守第 2.3 节。 | `TRAIT` immunity、spell-source/segment query。 |
| 10 | 紫晶心灵风暴 | 消耗 `3 AP`：半径 5 格内敌人立即受到 `4D6 psychic`，willpower save `DC 17` half；失败者 stunned `30 TU` 且正在进行的 concentration 被打断。领域持续 `240 TU`：友方获得 psychic immune、charmed immune 和 `spell_attack` roll bonus `+1`。 | `per_world_day=1`；即时伤害只发生一次。 | `AREA`、concentration transaction、team trait/query projection、`SAVE`。 |

**单件交互与 usage：** 单件精神护盾、读心、预知和 `+1D6 psychic` 继续独立；单件生成的 psychic extra segment 不触发 9 件附伤。重复 immunity/half 由 mitigation owner 取最强。

**落地缺口：** spell-source query、concentration interruption、临时队伍 trait、主段附伤与 preview 期望值。

## 40. 黑铁咒术师 / `black_iron_warlock_set`

**拓扑：仪式/契约 2/5/8/10。** 契约需要签订、记账、兑现和反噬四步；低价不代表可在四件直接获得半伤、吸血和持久 AC 损伤全包。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 契约识文 | arcana utility check `+2`；willpower save `+2`。 | 常驻。 | `QUERY` + `TRAIT`。 |
| 5 | 黑铁印记 | `mp_max +20 [RG:mana]`、`spell_attack` roll bonus `+1 [RG:spell]`。消耗 `1 AP` 标记 6 格内一个敌人为 contract target。 | `per_battle=1`；标记持续 `180 TU` 或目标离场。 | `ATTR`、spell-attack query、typed contract state。 |
| 8 | 代价账本 | contract target 对穿戴者造成主直接 HP 伤害时，每段固定减少 `3`，并记录实际防止值，账本上限 `15`；穿戴者对该目标首次命中后可 reaction（不耗 AP）消费全部账本，恢复等量 HP。 | 每段触发；每次标记只可消费一次；preview 不修改账本。 | incoming damage query、damage-reduced fact、heal、state。 |
| 10 | 痛苦契约 | 消耗 `3 AP` 指定 6 格内敌人：立即 `4D6 negative_energy`，之后每 `60 TU` 受到 `1D6 negative_energy`，共 3 次；constitution save `DC 17`，成功使即时伤害 half 且不产生 tick。契约存续时该目标对穿戴者的主直接伤害取 `half`，实际防止伤害的 50% 治疗穿戴者、每段最多治疗 6。到期时目标再作 willpower save `DC 17`，失败 `armor_ac_bonus -1` 持续 `300 TU`，同源不可叠加。 | `per_world_day=1`；契约持续 `180 TU`。 | `ABILITY`、scheduled damage、mitigation query、heal cap、temporary AC status、`SAVE`。 |

**单件交互与 usage：** 单件契约卷轴、伤害储存和法术增伤不共享 5/8/10 件 usage。若单件与套装同时减伤，正式 resolver 先确定 mitigation tier，再结算 fixed reduction；只把真实防止值写入对应 source 的账本。

**落地缺口：** contract target schema、damage-reduced fact、状态与 usage 持久化、来源绑定 AC 清理、save/writeback 原子性。

## 41. 太阳圣骑士 / `solar_paladin_set`

**拓扑：稳步循环 2/4/6/8/10。** 价格和重甲骨架允许稳定成长，但日光附伤、驱暗和裁决必须分层，不能在四件一次取齐。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 日光守誓 | `res(radiant,half)`；willpower save `+2`。 | 常驻。 | `TRAIT`。 |
| 4 | 黎明武装 | `armor_ac_bonus +1 [RG:defense]`；`attack_bonus +1 [RG:offense]`。 | 常驻。 | `ATTR`。 |
| 6 | 太阳刃 | 在 daylight 中，或目标带 undead/fiend tag 时，武器与 spell attack 的每个主直接段附加 `1D4 radiant`。 | 逐主直接段；第 2.3 节排除递归。 | environment/target-tag damage query。 |
| 8 | 破暗日轮 | 消耗 `2 AP`，在半径 3 格造成 `3D6 radiant`，constitution save `DC 16` half；移除区域内一个非传奇 darkness area。被该伤害击败的 undead/fiend 使 2 格内同类敌人 frightened `60 TU`，willpower save `DC 16` negates。 | `per_battle=1`。 | `AREA`、dispel、OnKill、save/status。 |
| 10 | 太阳裁决 | 消耗 `3 AP` 创建半径 5 格 daylight `180 TU`，并立即对 8 格内一个 evil/undead/fiend 目标作一次正式武器或 spell attack：该次 `attack_bonus +3`，各命中主直接段额外 `4D6 radiant`。若击败目标，领域内同类敌人作 willpower save `DC 17`，失败 frightened `60 TU`。 | `per_world_day=1`；额外攻击仍走正式命中、暴击与伤害 pipeline。 | daylight producer、formal attack、segment、OnKill aura、`SAVE`。 |

**单件交互与 usage：** 单件日光射线、治愈、水晶、全攻击 radiant 与 evil 命中均保持独立。单件 radiant extra segment 不再触发 6/10 件附伤；多个 daylight source 只提供环境事实，不重复增强数值。

**落地缺口：** day/daylight、evil/fiend canonical tag、darkness dispel、正式额外攻击、OnKill 恐惧与 AI 估值。

## 42. 虚空守护者 / `void_warden_set`

**拓扑：堡垒 4/6/8/10。** 原十件 AC 12、HP/MP 与多项位移免疫已经偏强；四件才启动，随后由锚定、吞噬和裂隙逐级形成堡垒。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 4 | 虚空甲壳 | `res(force,half)`；`armor_ac_bonus +1 [RG:defense]`。 | 常驻。 | `TRAIT` + `ATTR`。 |
| 6 | 现实锚 | immune(forced_move)、immune(forced_teleport)；`spell_attack` roll bonus `+1 [RG:spell]`。自愿移动和传送不受影响。 | 常驻。 | movement immunity、spell-attack query。 |
| 8 | 虚空蚀刻 | 命中一个敌人后可 reaction（不耗 AP），使其随机一个 canonical ability `-2 [RG:void_ability_debuff]`，持续 `120 TU`；同一目标同源只保留最新结果。 | `per_battle=2`；每次命中后选择是否消费。RNG 由 battle seed 派生。 | post-hit reaction、deterministic RNG、ability status。 |
| 10 | 虚空吞噬 | 消耗 `3 AP`：6 格内目标受到 `4D6 force + 2D6 negative_energy`，constitution save `DC 17` half；失败再随机一个 ability `-2 [RG:void_ability_debuff]`、持续 `180 TU`。若目标在 180 TU 内被击败，其位置生成半径 2 格裂隙 `180 TU`：敌人进入或每 `60 TU` 首次开始于其中时受到 `1D6 force`。 | `per_world_day=1`；裂隙每目标每 tick 最多伤害一次。 | mixed damage、random debuff、OnKill area、scheduled tick、`SAVE`。 |

**单件交互与 usage：** 胸甲/披风的强制位移免疫、手套裂缝和碎片传送均独立。重复免疫只产生一个 canonical fact；8/10 件命中同一 ability 时只保留 `RG:void_ability_debuff` 的最高档/最新时长，不叠成 `-4`。单件裂缝与十件裂隙使用不同 source key 和 tick ledger。

**落地缺口：** 位移免疫、随机 ability closed domain、OnKill 裂隙、area tick ledger、preview RNG 分布。

## 43. 自然守护者 / `nature_warden_set`

**拓扑：仪式/领域 2/5/8/10。** 自然亲和先建立，再选择守护环境，最后由十件释放山崩、缠绕或兽群；召唤不应在低阈值出现。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 自然亲和 | `res(poison,half)`；animal-handling utility check `+2`。 | 常驻。 | `TRAIT` + `QUERY`。 |
| 5 | 守林韧性 | `mp_max +15 [RG:mana]`；constitution save `+2`；忽略 plant/earth difficult terrain。 | 常驻。 | `ATTR`、`TRAIT`、terrain query。 |
| 8 | 三相守护 | battle start 阶段选择 mountain/root/beast（不耗 AP），战斗中不可更改：mountain 为静止时 `armor_ac_bonus +1`；root 为 `move_point_capacity_delta +1` 且 grapple save `+2`；beast 为第一次召唤的 set-sourced 单位 `attack_bonus +1`。 | 每场选择一次；战斗 teardown 清除。 | battle-start choice、conditional query、summon modifier。 |
| 10 | 自然之怒 | 消耗 `3 AP` 三选一：①山崩，半径 3 格 `4D6 physical_blunt`，agility save `DC 17` half，失败 prone `30 TU`；②缠绕，半径 4 格领域 `240 TU`，敌人进入/每 `60 TU` 作 strength save `DC 17`，失败 restrained `30 TU`；③兽群，召唤 2 个 formal nature-wolf definition，持续 `240 TU`。 | `per_world_day=1`；只能选择一个分支。 | `AREA`、terrain、save/status、`SUMMON`、`SAVE`。 |

**单件交互与 usage：** 单件自然盟友图腾、治疗徽章、荆棘反击与单件召唤独立；十件兽群不消耗图腾次数。所有 set summon 进入同一性能预算，但保留各自 source/lifetime。

**落地缺口：** environment/terrain tag、battle-start choice、正式狼模板、summon cap、三分支 AI preview。

## 44. 符文铁匠 / `rune_smith_set`

**拓扑：仪式/领域 2/5/8/10。** 符文的核心是识别、铭刻、充能和风暴；跨战铭刻必须有正式 world-day owner，不能伪装成普通临时属性。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 识文 | arcana/investigation utility check 各 `+2 [RG:rune_check]`。 | 常驻。 | `QUERY`。 |
| 5 | 临时铭刻 | `spell_attack` roll bonus `+1 [RG:spell]`、`armor_ac_bonus +1 [RG:defense]`。在非战斗安全换装界面选择当前装备的一件武器或护甲：武器 `attack_bonus +1`，或护甲 `armor_ac_bonus +1`。 | `per_world_day=1`；不进入战斗指令且不涉及 AP；铭刻持续到下一 world-day 边界、物品卸下/损坏或 source 失效。 | equipment-target ability、spell-attack query、world duration、source cleanup、`SAVE`。 |
| 8 | 三相符能 | fire/freeze/lightning 三选一；下一次 spell 或武器命中的每个主直接段附加 `1D4` 所选类型。 | `per_battle=3` charge；每次正式攻击/施法开始前可武装（不耗 AP，不是额外主动指令），一次攻击或施法结束后消费 1 charge。 | armed intent state、主段 query、element choice。 |
| 10 | 符文风暴 | 消耗 `3 AP`：半径 4 格立即造成 `2D6 force + 2D6 lightning + 1D6 fire`；agility save `DC 17` half，constitution save `DC 17` 失败 stunned `30 TU`。领域持续 `180 TU`，其中友方 `armor_ac_bonus +1`、`attack_bonus +1`。 | `per_world_day=1`；两次 save 分别结算，不能用一次结果替代。 | mixed damage、dual-save transaction、team area、`SAVE`。 |

**单件交互与 usage：** 单件项链的铭刻与符文石风暴不共享 set usage。若同一物品同时有单件铭刻和 5 件铭刻，来源分别显示；同名 AC/attack 仍按正式 stack/replacement 规则，不按文案合并。

**落地缺口：** 装备目标选择、跨战 source cleanup、armed action、三元素主段、双 save、AI 对不同铭刻的比较。

## 45. 灵魂收割者 / `soul_reaper_set`

**拓扑：稳步循环 2/4/6/8/10。** 抗性与命中建立基础，击杀产魂、层数转伤、十件消费爆发形成完整可观察循环。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 灵魂视界 | `res(negative_energy,half)`；religion utility check `+2`。 | 常驻。 | `TRAIT` + `QUERY`。 |
| 4 | 收割之刃 | `attack_bonus +1 [RG:offense]`。 | 常驻。 | `ATTR`。 |
| 6 | 摄魂 | 由穿戴者造成最后一个真实 HP 伤害并击败 hostile living/undead 单位时，恢复 `1D6 HP` 并获得 1 soul stack，最多 5。召唤物、环境和装备生成伤害不记名。 | 每个 victim 最多触发一次；stack 战斗结束清除。 | OnKill provenance、heal、`STATUS`。 |
| 8 | 灵火 | 每个 soul stack 使穿戴者的武器或 spell attack 主直接段额外造成 `+1 negative_energy`，最多 `+5`；不消费 stack。 | 逐主直接段；遵守递归排除。 | stack query、flat extra damage。 |
| 10 | 万魂哀号 | 需要 5 stacks；消耗 `3 AP` 和全部 stacks：半径 4 格敌人受到 `5D6 negative_energy`，constitution save `DC 17` half；失败者 `hp_max -10` 直到 battle end。穿戴者恢复 `2D6 HP`。 | `per_battle=1` 且需要 5 stacks；消费与伤害在同一事务。 | stack consume、AoE/save、temporary hp-max、heal。 |

**单件交互与 usage：** 单戒 OnKill 治疗/三层灵魂、灯笼 max-HP 损伤和长袍灵魂释放独立；它们的 stack 必须使用物理实例 source key，不能计入 6/8/10 件的五层 soul stack。

**落地缺口：** 击杀归属、living/undead tag、stack provenance、hp-max 临时损伤、原子 consume 与防递归。

## 46. 时间行者 / `chrono_walker_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 高移动底盘不再直接堆大量速度；阈值按抢拍、滑步、改骰、调速、回溯逐级改变行动节奏。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 先时感 | `battle_start_progress +3 [RG:tempo]`；arcana utility check `+2`。 | battle start 一次；不在换装时重复发放 progress。 | battle-start producer、`QUERY`。 |
| 3 | 时间滑步 | `move_point_capacity_delta +1 [RG:mobility]`。 | 常驻。 | movement `QUERY`。 |
| 6 | 微小改写 | 失败的 agility save 结算前可 reaction（不耗 AP）重掷，必须使用新结果。 | `per_battle=1`；preview 只展示重掷期望。 | roll transaction、reaction usage。 |
| 9 | 快慢分流 | 消耗 `1 AP` 二选一：①加速自己，立即获得 `+30 action_progress`，下次 activation 结束前 mobility 升为 `+2 [RG:mobility]`；②减速 6 格内敌人，willpower save `DC 16`，失败 `-30 action_progress`，且下次 activation 的可用 AP 上限为 2、最多执行 1 个主动指令。 | `per_battle=1`；不能把 progress 推过合法上下界。 | timeline mutation、AP-cap status、save。 |
| 10 | 时间裂隙 | 消耗 `3 AP`，回到自己上一次 activation 开始时的 battle-local 快照：恢复当时 HP、MP、位置、action progress 和 source-bound status；不恢复已消费物品、set usage、已死亡单位、召唤物、世界状态或已提交的 objective 进度。若目标格已非法，使用最近的 deterministic safe cell；无安全格则失败且不消费。 | `per_world_day=1`；只保留一份 previous-activation snapshot。 | mutation-exact snapshot、safe placement、rollback、`SAVE`。 |

**单件交互与 usage：** 单件额外 AP、加速戒、减速戒和重掷碎片均独立。十件回溯明确不恢复它们或套装的 usage，避免复制消耗；6 件重掷与单件重掷按 reaction ordering 分别提示/估值。

**落地缺口：** previous-activation snapshot、action progress 边界、合法格回退、动作类别限制、usage 不回滚证明与 AI 反事实估值。

## 47. 梦境编织者 / `dream_weaver_set`

**拓扑：高魔 3/6/9/10。** 物品已提供睡眠、催眠和小型梦境；套装阈值集中在法力、清醒保护、团队改写和十件噩梦链。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 清醒梦 | insight/deception utility check 各 `+2 [RG:dream_check]`；`mp_max +10 [RG:mana]`。 | 常驻。 | `QUERY` + `ATTR`。 |
| 6 | 梦境免疫 | mana 升为 `mp_max +20 [RG:mana]`；immune(sleep)、immune(dream_control)。自然休息不受影响。 | 常驻。 | `TRAIT`；sleep/dream canonical tags。 |
| 9 | 共梦改写 | 4 格内友方失败 willpower save 时可 reaction（不耗 AP）令其重掷；重掷后该友方与穿戴者获得 `res(psychic,half)` `60 TU`。 | `per_battle=2`；每次只处理一个正式 save transaction。 | ally reaction、roll transaction、temporary trait。 |
| 10 | 噩梦坠落 | 消耗 `3 AP` 指定 6 格内可感知目标，willpower save `DC 17`。初次成功：受到 `2D6 psychic` 并 frightened `60 TU`；失败：stunned `30 TU`，并进入噩梦链。之后每 `60 TU` 受到 `3D6 psychic` 并重掷 save；成功结束，累计 3 次失败则 unconscious `60 TU` 并结束。unconscious 受到真实 HP 伤害时立即醒来。 | `per_world_day=1`；噩梦链最多 3 次 tick。 | scheduled save/damage、失败计数、wake condition、`SAVE`。 |

**单件交互与 usage：** 单件催眠、梦境陷阱、梦魇冲击和梦境召唤独立。多个 sleep/dream immunity 只生成一个 canonical immunity；单件造成的 psychic extra segment 不触发其他套装附伤。

**落地缺口：** dream-control tag、ally save reaction、scheduled chain、失败计数、unconscious 唤醒与 AI 对多阶段结果的估值。

## 48. 深渊潜行者 / `abyss_stalker_set`

**拓扑：蜕变 3/5/7/10。** 感知深渊、适应环境、试探性形态、完整吞噬是四次身体变化；高风险形态不拆成可叠加的零散四件能力。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 深渊感官 | `res(psychic,half)`；stealth utility check `+2`；darkvision 至 12 格。 | 常驻；已有更远 darkvision 时取最高值。 | `TRAIT` + vision/utility `QUERY`。 |
| 5 | 压力适应 | `attack_bonus +1 [RG:offense]`；immune(water_pressure)；水中 `move_point_capacity_delta +1 [RG:mobility]`。 | 常驻。 | environment immunity、movement query。 |
| 7 | 半深渊形态 | 消耗 `1 AP` 变形：`armor_ac_bonus +1 [RG:form_defense]`，武器/徒手主直接段附加 `1D4 psychic`。每 `60 TU` 作 willpower save `DC 14`，失败自身受到 `1D4 psychic`，该自伤不可减免且不递归。 | `per_battle=1`；持续 `120 TU`。 | shape status、segment query、scheduled self-save。 |
| 10 | 深渊吞噬形态 | 消耗 `2 AP` 变形：form defense 升为 `armor_ac_bonus +2 [RG:form_defense]`；每个武器/徒手主直接段附加 `1D4 psychic + 1D4 negative_energy`；immune(charmed)、immune(frightened)。每 `60 TU` 作 willpower save `DC 15`：失败自身受 `1D4 psychic`，同时 2 格内敌人受 `1D4 psychic`，敌人 willpower save `DC 17` negates。 | `per_world_day=1`；持续 `240 TU`，最多 4 次形态 tick。 | form replacement、mixed segment、self damage、pulse area、`SAVE`。 |

**单件交互与 usage：** 单件黑暗隐形、触手束缚、恐惧浪潮和深水能力独立。7/10 件是同一 `RG:form` replacement：激活十件后只能选择十件形态，不能同时开启两层。

**落地缺口：** water-pressure/light producer、形态互斥、scheduled self-save、不可减免自伤、脉冲去重与 shape preview。

## 49. 星辰观测者 / `star_gazer_set`

**拓扑：高魔 3/6/9/10。** 高价法系已有星图、预知和小型流星；套装从观测、施法、预报到十件星落，避免再复制单件望远镜的同名使用次数。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 星图感知 | arcana/perception utility check 各 `+2 [RG:star_check]`；`mp_max +10 [RG:mana]`。 | 常驻；夜间 star_check 额外 `+1`，仍在同组中取最高 `+3`。 | `QUERY` + night fact + `ATTR`。 |
| 6 | 星辰聚焦 | mana 升为 `mp_max +15 [RG:mana]`；`spell_attack` roll bonus `+1 [RG:spell]`；夜间或露天导航检定自动获得 advantage。 | 常驻。 | spell-attack/utility query、night/sky producer。 |
| 9 | 星象预报 | battle start 冻结一个 forecast：显示首个 hostile 单位的 intended action category；穿戴者可在该行动结算前 reaction（不耗 AP）令其本次 `attack_bonus -2`，或令其本次 save DC `-2`。 | `per_battle=1`；不揭示随机数或隐藏内容。 | intended-action surface、reaction query。 |
| 10 | 星辰坠落 | 消耗 `3 AP` 选择 10 格内具备 sky access 的中心，显示半径 4 格 telegraph；`60 TU` 后坠落，造成 `5D6 radiant + 2D6 force`，agility save `DC 17` half；失败 prone `60 TU` 且 blinded `60 TU`。中心半径 2 格成为 crater difficult terrain `180 TU`。 | `per_world_day=1`；施放后即消费，目标离开不会退款。 | sky validation、scheduled area、mixed damage、terrain、`SAVE`。 |

**单件交互与 usage：** 单件微型流星和望远镜星辰坠落独立；十件使用自己的 telegraph/source。相同位置的多个 crater 只保留最强移动惩罚，不增加多份困难地形。

**落地缺口：** night/sky access、intended action、scheduled telegraph、可破坏/派生地形、AI 路径与躲避估值。

## 50. 混沌使者 / `chaos_herald_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 单件已经高随机；套装先稳住心智和施法，再逐步开放可复现的随机改色、单次 surge 与最终全场风暴。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 混沌定锚 | willpower save `+2`；arcana utility check `+2`。 | 常驻。 | `TRAIT` + `QUERY`。 |
| 3 | 无序施法 | `spell_attack` roll bonus `+1 [RG:spell]`。 | 常驻。 | spell-attack query。 |
| 6 | 混沌改色 | spell 造成的每个主直接 elemental segment 独立从 fire/freeze/lightning/acid/thunder/force 六类中均匀选择新 damage tag；只改 tag，不改 power。 | 每个主直接段独立 deterministic roll；preview 返回分布、不掷真实结果。 | per-segment RNG、damage-tag rewrite。 |
| 9 | 混沌涌动 | 消耗 `2 AP` 掷 `D6`：1 自身 `attack_bonus +2` 120 TU；2 自身 `armor_ac_bonus +2` 120 TU；3 `move_point_capacity_delta +2` 120 TU；4 恢复 `3D6 HP`；5 6 格内随机敌人受到 `3D6 force`；6 自身与 6 格内随机单位原子换位，若无合法单位则重掷一次，再失败则恢复 3D6 HP。 | `per_battle=1`；由 battle seed 决定。 | outcome router、status、random target、atomic swap。 |
| 10 | 混沌风暴 | 消耗 `3 AP` 创建半径 5 格领域 `240 TU`，每 `60 TU` 对领域内每个单位独立掷 D6：1 `2D6 fire`；2 `2D6 freeze` 且 `move_point_capacity_delta -1` 30 TU；3 `2D6 lightning`；4 `2D6 force` 并推离 2 格；5 恢复 `2D6 HP`；6 `action_progress +15` 或 `-15` 等概率。 | `per_world_day=1`；敌友均可成为目标；每单位每 tick 一次。 | deterministic per-target/tick RNG、area ledger、push/heal/progress、`SAVE`。 |

**单件交互与 usage：** 单件混沌骰、随机 AC/移动、属性变化和重掷各自独立；所有 RNG stream 按 source key 分流，套装 outcome 不改变单件下一次结果。preview 只返回期望分布。

**落地缺口：** RNG stream、tag rewrite、通用 outcome router、随机换位、per-target tick ledger、AI 风险偏好。

## 51. 雷神之甲 / `thunder_god_set`

**拓扑：稳步循环 2/4/6/8/10。** 高价重甲允许稳定攻防成长，但蓄电、放电、延迟雷击和雷神之锤必须依次建立。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 雷霆耐受 | `res(lightning,half)`；agility save `+2`。 | 常驻。 | `TRAIT`。 |
| 4 | 雷铸武装 | `attack_bonus +1 [RG:offense]`；`armor_ac_bonus +1 [RG:defense]`。 | 常驻。 | `ATTR`。 |
| 6 | 蓄雷 | 自身实际受到 lightning 主直接 HP 伤害后获得 1 thunder charge，最多 3；同一伤害事件只获得 1 层。 | battle-local；战斗结束清除。half 等 mitigation 结算后才判定。 | post-damage fact、charge state。 |
| 8 | 连锁放电 | 武器或 spell attack 命中后可 reaction（不耗 AP）消费 1 charge：目标额外受 `1D6 lightning`，2 格内另一随机敌人受 `1D4 lightning`，两段均为 gear-set origin 且不递归。 | 每次命中最多消费 1 层；无第二目标时只结算主目标。 | post-hit reaction/consume、secondary target、extra origin。 |
| 10 | 雷神之怒 | 消耗 `3 AP` 指定 8 格内目标，立即 `5D6 lightning + 3D6 thunder`，agility save `DC 17` half，失败 stunned `30 TU`；`60 TU` 后目标位置再落雷 `2D6 lightning`，半径 2 格 agility save `DC 16` half。 | `per_world_day=1`；延迟落雷绑定位置，不追踪单位。 | mixed damage、dual phase schedule、area save、`SAVE`。 |

**单件交互与 usage：** 单件雷电储存、静电爆发和雷神核心之锤不共享 charge/usage。单件装备过载产生的 lightning extra segment 不能为 6 件蓄雷，也不能触发 8 件再次放电。

**落地缺口：** post-mitigation damage fact、charge、随机次目标、scheduled strike、位置 telegraph 与防递归。

## 52. 美杜莎之凝视 / `medusa_gaze_set`

**拓扑：蜕变 3/5/7/10。** 毒鳞、石肤、阶段凝视和群体石化表现为身体与目光逐级异化；十件才允许完整 petrified。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 蛇血 | `res(poison,half)`；intimidation utility check `+2`。 | 常驻。 | `TRAIT` + `QUERY`。 |
| 5 | 石鳞 | `armor_ac_bonus +1 [RG:defense]`；近战攻击者命中后自动反刺（不耗 AP），攻击者受到 `1D4 poison`，每攻击者每 `30 TU` 最多一次，gear-set origin 不递归。 | 常驻 reaction（不耗 AP）。 | incoming-hit reaction、attacker ledger。 |
| 7 | 渐石凝视 | 消耗 `2 AP`：6 格内、与穿戴者互相具有 line-of-sight 的目标作 constitution save `DC 16`；失败获得 petrify stage 1 `120 TU`（`move_point_capacity_delta -1`、不能 reaction）；在 stage 1 再次失败升级 stage 2（移动点上限为 0、下次 activation 可用 AP 上限为 2），成功移除一层。 | `per_battle=2`；每次只选一个目标。 | gaze/facing fact、stage state、save。 |
| 10 | 女王石化 | 消耗 `3 AP`：6 格锥形内敌人 constitution save `DC 17`，失败获得 stage 1。之后每 `60 TU` 自动重掷，失败升级一层，成功降低一层；stage 3 为 petrified `180 TU` 并停止重掷。`restoration` canonical cleanse 可移除全部 stage。 | `per_world_day=1`；链持续最多 `180 TU`，每目标最多 3 次重掷。 | cone gaze、scheduled save、stage machine、cleanse、`SAVE`。 |

**单件交互与 usage：** 头冠/项链单体石化、石肤戒指和蛇发攻击独立。所有 petrify effect 共用 canonical stage taxonomy，但按 source 维护 duration/usage；同目标不同来源只采用最高 stage，不把层数相加超过 3。

**落地缺口：** mutual gaze/facing、petrify stage owner、scheduled save、restoration cleanse、反伤 ledger。

## 53. 凤凰重生（护甲文档称“凤凰涅槃”） / `phoenix_rebirth_set`

**命名与冻结：** 本节不裁决新的 canonical 展示名；中央旧效果表与饰品文档沿用“凤凰重生”，护甲文档沿用“凤凰涅槃”。`phoenix_rebirth_set`、十个 `item_id`、全部原物品名、槽位、价格和既有基础属性原样冻结，不创建 alias。单件特殊效果的正式格数、TU、AP、次数与仲裁语义已经同步到原物品文档和当前实现文档。

**冻结来源：** [护甲单件](sets_51_to_60.md)、[饰品单件](sets_51_to_60_accessories.md)、[旧阈值方案](set_bonus_design.md)。本节只覆盖旧阈值层，不把覆盖案反向解释为单件配置变更。

**拓扑：蜕变 3/5/7/10。** 三件补足凤凰最明确的 freeze 短板，五件扩大所有治疗与百分比恢复共同依赖的生命池，七件提供每战低血量回稳，十件则是玩家主动掌控的完整涅槃。原单件已经有四个个人致死恢复来源，徽章祝福还可临时提供第五个，因此十件不再注册另一份 lethal intercept；它以主动终极能力形成新玩法，而不是继续堆“第六条命”。十件原物品总价为 150,000。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 凤凰血脉 | `res(freeze,half)`；constitution save `+2`。 | 常驻；重复 freeze half 不叠加。头冠单件保留 `fire half`，本阈值不再添加 fire 抗性。 | `TRAIT`。 |
| 5 | 不灭心火 | `hp_max +20 [RG:vitality]`。 | 常驻；阈值失效时按正式 max-HP 规则钳制当前 HP，不保留失去来源的额外生命。 | `ATTR`。 |
| 7 | 余烬展翼 | `res(fire,immune)`；一次正式伤害事务及其致死仲裁全部结束后，若穿戴者仍存活且 HP 首次处于 max HP 的 25% 或以下，自动恢复 `2D8 HP`，并进入 `RG:phoenix_form` 余烬档 `120 TU`：`move_point_capacity_delta +1`，每个 qualifying 武器、徒手或 spell attack 主直接段附加 `1D4 fire`。 | 火焰免疫随7件阈值常驻，降至6件时回落到装备来源的 `fire half`；低血触发为自动、`0 AP`、`per_battle=1`。从高血量直接死亡且没有任何致死恢复成功时不触发、不消费；任一成功致死恢复来源最终落在该区间后都可触发。 | threshold trait mitigation、post-lethal finalized-HP fact、canonical heal、form status、逐段 damage query。 |
| 10 | 太阳涅槃 | 穿戴者存活且当前 HP 不高于 50% 时可消耗 `3 AP`：先按 canonical heal 恢复相当于“到 max HP 的 50%”的基础治疗量；随后半径 3 格敌人受到 `4D10 fire`，agility save `DC 17` half，半径 3 格内 HP 百分比最低的至多 4 名其他友方各恢复 `2D8 HP`；最后进入 `RG:phoenix_form` 金焰档 `240 TU`：`move_point_capacity_delta +2`，每个 qualifying 武器、徒手或 spell attack 主直接段附加 `1D10 fire`。 | 主动、`3 AP`、`per_world_day=1`；基础自疗为 `max(ceil(hp_max*50%)-current_hp,0)`，过量治疗丢弃。合法提交能力后消费 usage；没有敌人或其他友方不退款。 | set command、HP gate、canonical heal/damage/save、deterministic ally selection、form status、persistent usage、`SAVE`。 |

### 53.1 十槽机会成本与混搭基线

凤凰满套固定占用 `head/body/hands/feet/cloak/necklace/ring_1/ring_2/special_trinket/badge` 全部十个非武器槽；武器槽不参与套装计数。满套强度不得只比较十件阈值本身，而要把冻结单件底盘、3/5/7前置阈值、太阳涅槃和仅满套解锁的凤凰蛋视为同一个十槽总包，并与相同槽位上的十件最优混搭比较。

阈值拓扑会改变混搭收益：两套 `3/5/7/10` 各穿5件时，双方都同时激活3件与5件阈值，共取得四个阈值效果；两套 `4/6/8/10` 各穿5件时，双方只能各激活4件阈值，共取得两个阈值效果。因此后续套装验收必须同时列出“完整十件总包”和“5+5混搭”基线，不能用单一 capstone 数值跨拓扑比较。凤凰的十件预算中，太阳涅槃与凤凰蛋属于同一满套回报，不得分别当作两个互不计价的奖励。

| 配装基线 | 十槽分配 | 激活的套装阈值 | 凤凰验收用途 |
|---|---|---|---|
| 凤凰完整十件 | `10 + 0` | 凤凰 `3/5/7/10`，并解锁凤凰蛋 | 计算冻结单件、四档阈值、太阳涅槃和凤凰蛋的完整总包。 |
| 同拓扑双套混搭 | `5 + 5` | 两套 `3/5`，共四个阈值效果 | 检查前两档叠加是否超过完整十件的机会成本。 |
| 后置拓扑双套混搭 | `5 + 5` | 两套 `4`，共两个阈值效果 | 不得直接套用 `3/5/7/10` 的阈值预算。 |
| 十件最优单件混搭 | 十个槽位各取当前最优单件 | 无套装阈值 | 作为完整十件的最低机会成本对照；若内容池变化，应重新评估。 |

### 53.2 为什么十件不再是另一份复活

独立的十件致死恢复会与头冠、披风和凤凰蛋竞争同一死亡事务；若再加上徽章祝福，一名满套角色可同时拥有五个候选。重生之戒已改为低血主动大治疗，不再加入候选池。把十件改成低血量主动终极后：

- 不删除、替换或借用其他单件 usage；头冠、披风、凤凰蛋和有效的凤凰祝福仍负责各自的致死保护。
- 七件在低血量提供一次战斗内缓冲，为玩家争取到下一次 activation 主动使用十件终极的机会。
- 十件爆发 `4D10` 高于凤凰蛋的 `3D10`，金焰档使用同级 `1D10` 附伤，但额外拥有正式 `240 TU`、移动与最多四名友方治疗；不再用重复 fire immune、第二次复活或周期自疗虚增 capstone。
- 十件是有 AP 决策的攻防转换，不会因为一次普通致死事件自动浪费世界日 usage。

太阳涅槃在 command commit 时冻结友方候选快照：排除自身、已死亡单位与范围外单位，按 `hp_percent_bp ASC, unit_id ordinal ASC` 排序后取前四名。后续位移或 HP 变化不重选目标；如此 preview、AI 与正式执行使用同一确定性结果。

### 53.3 冻结单件的致死恢复仲裁

下表只明确单件既有效果在同一致死事务中的顺序，不修改其数值或次数。徽章只有在其主动效果确实把 `凤凰祝福` status 施加到濒死单位时才进入候选。

| 选择顺位 | 冻结来源 | 成功结果 | usage / 尝试语义 |
|---:|---|---|---|
| 1 | 凤凰祝福 status | 自动恢复 `1D10 HP`。 | 徽章主动为 `per_world_day=1`；每名受益者的祝福致死恢复为 `per_battle=1`，status 必须仍有效。 |
| 2 | 凤凰重生烈焰披风 | 25% 成功，恢复 `1D12 HP`。 | `per_battle=1 attempt`；正式掷骰即消费，失败也消费。 |
| 3 | 凤凰涅槃头冠 | 25% 成功，恢复至 max HP 的 25%，再对半径 3 格所有敌人结算 `3D10 fire`；没有敌人时仍完成恢复。 | `per_battle=1 attempt`；正式掷骰即消费，失败也消费。 |
| 4 | 凤凰重生凤凰蛋 | 仅在10件套完整激活时投影；自动恢复至 max HP 的 30%，获得 `120 TU` 火焰化身后，再对半径 2 格所有敌人结算 `3D10 fire`。 | `protection_priority=900`，可拦截律令死亡；`per_world_month=1`，一个世界月为 `450 world steps`。 |

凤凰祝福、披风和头冠的 `protection_priority` 固定为 `100`，只能拦截普通致死；凤凰蛋为 `900`，但它的 binding 还要求10件套阈值 trait，因此 Power Word Kill 类处决只有在完整10件套的凤凰蛋仍可用时才能被拦截。重生之戒不再具有 `protection_priority`。

仲裁采用同步 `fatal-intercept` 事务。所有 `BeforeDamageResolved` hook 完成、伤害未取消且最终投影重算后，在任何 shield / HP 写入前冻结 `was_alive_before_event` 与 `hp_before`；取消伤害时不创建 fatal transaction。只有该快照仍存活且本事件形成 alive-to-fatal 转换，才能打开候选池。该事务位于现有 fatal trait、death ward / Last Stand 尝试之后和唯一一次对外死亡 finalization 之前：

1. Last Stand 必须作为 provisional attempt：成功则结束致死事务；失败必须显式继续凤凰仲裁，并沿用事件开始时的存活快照，不能因当前分支已经临时写入 `IsAlive=false` 而跳过。只有 Last Stand 与凤凰候选都失败后，才执行一次最终 `MarkDead` 和对外死亡事件。
2. 披风、头冠与凤凰蛋先复核对应装备实例仍装备且有效、source-instance usage 可用；凤凰祝福只复核受益者身上的 status、provenance 与有效期，不要求施术徽章仍装备。每个候选再由 canonical death-priority 规则确认能拦截本次死亡；不可用或被压制时不掷骰、不消费，继续下一项。
3. 凤凰祝福致死次数使用独立 battle ledger，键至少为 `(beneficiary_unit_id, phoenix_blessing_fatal_ability_id)`；刷新、过期后重施或来自另一枚徽章的同名 status 都不重置次数。消费只关闭该受益者本战的致死恢复组件，不提前删除 status 剩余的附伤效果。
4. 25% 候选通过预检后只掷一次 canonical RNG。失败消耗本战尝试并继续下一项；若失败不消费，就会在每次濒死时无限重掷，实际不再是“每战一次”。
5. 第一个成功来源原子提交自身 usage 与恢复，结束本伤害事件的仲裁；后续候选不消费，也不结算第二份恢复、爆发或形态。全部失败才正式死亡。
6. 成功拦截不会先发送 wearer death、attacker kill 或掉落事件。恢复从 0 HP 基线计算，骰值和百分比至少产生 1 HP，并钳制到当前 `hp_max`。
7. 同一技能的后续真实伤害段是新的 damage event；仅当前一段成功拦截、下一段开始时目标仍然存活时，下一段才可使用尚未消费的来源。目标一旦正式死亡，后续伤害段不得对尸体重开凤凰候选池；不得按 cast 或外层 batch 去重。
8. `BypassDeathPrevention`、死亡来源优先级更高、来源损坏/卸下、usage 已耗尽或显式 `blocks_fatal_intercept` 都会跳过候选且不消费。stunned、silenced、0 AP 或普通 reaction opportunity 已用不压制这些自动被动来源。
9. preview / AI 只读取候选、成功率与恢复结果，不推进 RNG 或 usage。V1 使用上表固定顺位，不在同步伤害调用栈打开选择弹窗，也不依赖 Resource 数组、装备槽位或加载顺序。

十件“太阳涅槃”不是 fatal-intercept 候选，不读取、不消费也不重置上述任何单件 usage。

### 53.4 凤凰形态与伤害段

- 七件余烬档与十件金焰档属于同一 `RG:phoenix_form`：金焰档启动时立即替换余烬档并从 `240 TU` 重新计时；金焰结束后余烬档不恢复，七件次数不返还。金焰存在时触发七件仍可完成其 `2D8` 恢复并消费次数，但不能以低阶形态覆盖高阶形态。
- 另设仅约束“凤凰形态攻击附伤”的 `RG:phoenix_attack_append`，强度顺序固定为：余烬展翼 `1D4` < 凤凰祝福 `1D6` < 凤凰蛋火焰化身 `1D10` < 太阳涅槃金焰 `1D10`（同骰时金焰优先）。同一 qualifying 主直接段最多生成一个该组火焰附伤段，不叠成 `1D10 + 1D10`；高阶来源结束后，仍有效的下一阶来源可继续生效。
- 上述 damage-only replacement 不合并 status、不删除来源、不修改任何单件 usage；七件套常驻 fire immune、凤凰蛋形态 fire immune、各来源移动/持续时间以及凤凰祝福的致死恢复分别按自身合同继续存在。相同 fire immune 只按 `immune` 生效一次。
- 本节所有“主直接段”继续遵守第 2.3 节合同：逐个真实 `PrimaryDirect` segment 查询；miss 不触发；`extra_damage_segments`、DOT/upkeep、地形、反射、自伤、装备/套装生成伤害和 trigger skill 不再触发；新增火焰段携带 gear-set origin 与 source key。

### 53.5 单件单位与正式落地

单件描述中的尺、回合、分钟和“三倍速度”不直接进入 formal definition。本套对外展示只使用格与 TU：头冠爆发半径 3 格；披风低血量爆发半径 1 格；项链与灰烬之戒射程 1 格；凤凰蛋爆发半径 2 格；徽章光环与祝福半径 2 格；板甲燃烧、火盾和移动足迹均为 `60 TU`；火焰冲锋最大距离为当前有效移动点容量的 3 倍。这里没有建立通用的“尺到格”换算器。

本节的套装 membership、3/5/7/10 阈值、fatal-intercept、finalized-HP、主动技能、形态替换、附伤仲裁、世界日/月 usage、AI/preview、唯一实例获取与 headless 回归均已落地。当前实现真相见 [装备套装系统当前实现](../../design/progression/equipment_sets.md)；本文件保留为内容设计来源，不再作为运行时缺口清单。

## 54. 塞壬之歌 / `siren_song_set`

**拓扑：仪式/领域 2/5/8/10。** 表演、水域发声、诱导歌域和完整挽歌是四阶段仪式；十件把敌方控制与友方治疗合成 capstone。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 海潮歌者 | performance/persuasion utility check 各 `+2 [RG:song_check]`；水中 `move_point_capacity_delta +1 [RG:swim]`。 | 常驻。 | `QUERY` + environment movement。 |
| 5 | 无界歌喉 | `mp_max +15 [RG:mana]`；audible ability range `+4 cells [RG:voice]`；underwater 不阻止 verbal/source-audible ability。 | 常驻。 | audible-source query、water producer。 |
| 8 | 牵潮旋律 | 消耗 `2 AP` 创建半径 4 格移动歌域 `180 TU`：敌人进入或每 `60 TU` 作 willpower save `DC 16`，失败必须向歌者移动至多 1 格且不能对歌者作 opportunity attack；友方 charm/fear save `+2`，每 `60 TU` 恢复 `1D4 HP`，最多 3 次。 | `per_battle=1`。 | moving aura、forced approach、save-tag、heal ledger。 |
| 10 | 塞壬挽歌 | 消耗 `3 AP` 创建半径 6 格移动歌域 `240 TU`：敌人进入及每 `60 TU` 作 willpower save `DC 17`；失败 charmed `120 TU`，只能接近歌者且不能攻击，期间每 `60 TU` 受 `2D6 psychic` 并重掷 save，成功结束。友方 charm/fear immune，每 `60 TU` 恢复 `1D6 HP`。 | `per_world_day=1`；8/10 件共用 `RG:song_domain` usage，十件只出现升级版。 | audibility、moving area、charm state、scheduled damage/heal、`SAVE`。 |

**单件交互与 usage：** 单件塞壬项链、号角、潮汐戒和水下隐形独立。只有能听见 source 的单位进入领域；deafened 或 silence 可阻断新施加，但不自动清除已经生效的非持续聆听状态，除非状态定义明确要求。

**落地缺口：** audibility/silence/deafened、water/ship producer、forced approach path、移动 aura、每目标 tick 与 AI 站位。

## 55. 狼人诅咒 / `werewolf_curse_set`

**拓扑：蜕变 3/5/7/10。** 感官、半兽能力、可控半形态和失控全形态依次展开；高价单件已经有月光变身，套装 usage 必须独立且十件承担代价。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 狼之感官 | `attack_bonus +1 [RG:offense]`；perception utility check `+2`；获得 scent tracking 8 格。 | 常驻。 | `ATTR`、utility/scent query。 |
| 5 | 月骨 | `strength +1 [RG:strength]`、`agility +1 [RG:agility]`；night 时 scent range 升至 12 格。 | 常驻。 | `ATTR` + night producer。 |
| 7 | 可控半兽 | 消耗 `1 AP` 变形：10 temporary HP、`move_point_capacity_delta +1 [RG:form_move]`，近战武器/徒手主直接段附加 `1D4 physical_slash`。 | `per_battle=1`；持续 `180 TU`；允许正常施法。 | shape status、temp HP、segment query。 |
| 10 | 狼人狂化 | 消耗 `2 AP` 变形：20 temporary HP；`strength +2 [RG:strength]`、`agility +1 [RG:agility]`、`armor_ac_bonus +1`、`move_point_capacity_delta +2 [RG:form_move]`；近战/徒手主直接段附加 `1D6 physical_slash`。形态中不能施法；每次 activation 若无 hostile 邻接，必须向最近 hostile 移动并以其为优先目标，willpower save `DC 16` 可忽略本次强制。带 silver tag 的 incoming physical damage tier 为 `double`。 | `per_world_day=1`；持续 `240 TU`。 | shape replacement、target compulsion、silver mitigation、`SAVE`。 |

**单件交互与 usage：** 单件月光水晶变身和套装两种形态独立 usage，但同一时刻只允许一个 `shapechange` family；启动更高 priority 形态会结束较低形态并清理其 source-bound buff，不返还次数。

**落地缺口：** scent、night/moon、shape exclusivity、nearest-hostile forced target、silver tag、禁法与 AI 形态决策。

## 56. 影舞者 / `shadow_dancer_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 高 stealth 底盘需要通过短传送、精准、击杀刷新和终曲形成连击，而不是再堆无条件隐匿。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 影步基础 | stealth/acrobatics utility check 各 `+2 [RG:shadow_check]`。 | 常驻。 | `QUERY`。 |
| 3 | 短影跃 | 消耗 `1 AP`，传送到 3 格内具备 shadow cell fact 的可见空格。 | `per_battle=2` charge。 | shadow-cell、teleport、usage。 |
| 6 | 暗杀节拍 | `attack_bonus +1 [RG:offense]`；正式攻击的 `crit_threshold -1 [RG:critical]`。 | 常驻。 | `ATTR` + critical query；阈值 query 尚缺。 |
| 9 | 击杀续舞 | 穿戴者造成最后一个真实 HP 伤害并击败 hostile 后，恢复 1 层短影跃 charge；下一次武器攻击命中的主直接段额外 `1D4 negative_energy`。 | 每场最多触发 3 次；每 victim 一次；buff 在 `120 TU` 后失效。 | OnKill provenance、charge、armed segment。 |
| 10 | 暗影终曲 | 消耗 `3 AP`，选择至多 3 个彼此不同、位于 shadow cell 或其相邻格且在 8 格内的敌人，按稳定顺序依次传送并各执行一次正式武器攻击；每次命中主直接段附加 `1D4 negative_energy`。不能重复选择目标，不触发 opportunity attack；中途无合法落点时停止剩余攻击。 | `per_battle=1`；使用正式攻击、暴击、反应和击杀 pipeline。 | multi-step formal attack、safe teleport、stable ordering、OnKill。 |

**单件交互与 usage：** 单件阴影披风传送、暗影形态、分身和沉默领域独立。终曲攻击可触发单件 OnHit，但任何装备生成 extra segment 不再触发 9/10 件附伤；终曲自身击杀可以恢复 9 件 charge，但不能增加终曲攻击次数。

**落地缺口：** shadow cell producer、multi-step attack transaction、合法落点、OnKill provenance、AI 目标组合与 preview mutation safety。

## 57. 水晶先知 / `crystal_seer_set`

**拓扑：高魔 3/6/9/10。** 原十件已有高 MP、法术命中、预见与单件水晶风暴；阈值只保留四次高魔跃升，并让十件承担复合领域而非复制单件次数。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 晶体洞察 | arcana/insight utility check 各 `+2 [RG:crystal_check]`；`mp_max +10 [RG:mana]`。 | 常驻。 | `QUERY` + `ATTR`。 |
| 6 | 折光聚焦 | mana 升为 `mp_max +20 [RG:mana]`；`spell_attack` roll bonus `+1 [RG:spell]`。battle start 阶段可选择一个可见敌人（不耗 AP），读取其首个 intended action category。 | 每场只读取一次，不揭示 roll 或隐藏 payload。 | spell-attack query、intended-action surface。 |
| 9 | 水晶折射 | 自身或 3 格内友方成为直接攻击目标时可 reaction（不耗 AP），使该次 `attack_bonus -2`；若因此 miss，受保护者获得 1 枚 crystal reroll token，可重掷一次 attack/save/check 并使用新结果。 | `per_battle=2`；token 持续 `120 TU`，每单位最多 1 枚。 | incoming attack query、causal miss fact、roll token。 |
| 10 | 水晶风暴 | 消耗 `3 AP`：半径 5 格敌人受到 `4D6 force + 2D6 radiant`，agility save `DC 17` half；失败 blinded `60 TU`。区域内友方恢复 `2D6 HP` 并获得 1 枚 crystal reroll token。带 brittle/glass/crystal tag 的 battle object 受到双倍 power，但不绕过 objective immunity。 | `per_world_day=1`；即时结算，无持续领域。 | mixed area damage、heal、token、object/material query、`SAVE`。 |

**单件交互与 usage：** 单件预知项链、自动 miss 戒、水晶球风暴和 counterspell 戒独立。9/10 件产生的 reroll token 共用 `RG:crystal_token`，同单位只保留一枚，不与单件通用重掷合并 usage。

**落地缺口：** intended action、causal “因 -2 而 miss”事实、通用 roll token、材质/物体伤害、objective immunity 与 AI token 估值。

## 58. 霜巨人 / `frost_giant_set`

**拓扑：堡垒 4/6/8/10。** 高 AC、HP、freeze 静态总量已经突出；四件后才追加抗性和力量，再用冰地形、主段附伤、十件坍塌形成慢速堡垒。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 4 | 永冻甲壳 | `res(freeze,half)`；`strength +1 [RG:strength]`；`armor_ac_bonus +1 [RG:defense]`。 | 常驻；重复 freeze half 不叠加。 | `TRAIT` + `ATTR`。 |
| 6 | 冰原巨步 | `attack_bonus +1 [RG:offense]`；immune(cold_environment)；忽略 ice/snow difficult terrain。 | 常驻。 | environment immunity、terrain query。 |
| 8 | 冰川重击 | melee weapon、徒手或 physical_blunt skill 的每个主直接段附加 `1D4 freeze`；命中后目标 `move_point_capacity_delta -1` `30 TU`，同源只刷新、不叠加。 | 逐主直接段；移动减益每次攻击最多应用一次。 | source/segment query、movement status。 |
| 10 | 冰川坍塌 | 消耗 `3 AP`：6 格锥形造成 `4D6 freeze + 3D6 physical_blunt`，constitution save `DC 17` half；失败 restrained `60 TU`。锥形覆盖格成为 ice terrain `240 TU`，敌人 `move_point_capacity_delta -1`，穿戴者和具备 ice traversal 者不受影响。 | `per_world_day=1`。 | cone area、mixed damage、terrain projection、restrained、`SAVE`。 |

**单件交互与 usage：** 单件 freeze immunity、冰霜拳、绝对零度与冰面通行独立。免疫优先于 half；单件产生的 freeze extra segment 不触发 8 件附伤。多个 ice terrain 只取最强 movement penalty。

**落地缺口：** cold environment、terrain traversal、physical source filter、锥形持久地形、restrained 与 AI 路径更新。

## 59. 木乃伊诅咒 / `mummy_curse_set`

**拓扑：蜕变 3/5/7/10。** 抗腐、诅咒肉身、沙形态和最终衰败逐层失去“活人”特征；复活限制只能由十件命中的明确目标状态产生。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 防腐之躯 | `res(negative_energy,half)`；constitution save `+2`。 | 常驻。 | `TRAIT`。 |
| 5 | 诅咒甲胄 | `attack_bonus +1 [RG:offense]`；对 curse/disease save `+3 [RG:curse_save]`。 | 常驻；不是通用 willpower/constitution 加值。 | save-tag query。 |
| 7 | 沙化 | reaction（不耗 AP）化为沙：该次 incoming physical 主直接伤害取 `half`，并可移动到 3 格内合法空格；直到下一 activation 开始前不能再次 reaction，且不能被 grapple。 | `per_battle=2`；每次只处理一个 incoming damage transaction。 | mitigation reaction、safe movement、temporary immunity。 |
| 10 | 木乃伊衰败 | 消耗 `2 AP` 进入诅咒形态 `240 TU`：武器/徒手主直接段附加 `1D4 negative_energy`；每个目标第一次被命中时获得 decay，之后每 `60 TU` 受 `1D6 negative_energy`，共 3 次，且 `hp_max -5` 直到 battle end。带 decay 被击败的单位在本场 battle 内获得 revive_locked；battle teardown 后解除。 | `per_world_day=1`；每目标只创建一个 decay chain。 | form status、segment、scheduled DOT、hp-max damage、revive lock、`SAVE`。 |

**单件交互与 usage：** 单件疾病/诅咒免疫、绷带束缚、腐朽之触和木乃伊心脏复苏独立。套装 revive_locked 只作用于被穿戴者十件形态命中的目标，不阻止穿戴者自己的单件复苏。

**落地缺口：** curse/disease taxonomy、sand mitigation reaction、safe cell、decay ledger、hp-max 临时损伤、battle-only revive lock。

## 60. 龙骑士 / `dragon_rider_set`

**拓扑：蜕变 3/5/7/10。** 全批最高总价和强防御底盘适合四段龙化：元素血统、骑士甲胄、短时龙翼、十件完整降临。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 3 | 龙血选择 | 装备达到阈值时从 fire/freeze/lightning/acid/poison 选择一个 dragon element；获得对应 `res(element,half)` 与 intimidation utility check `+2`。 | 选择写入 set state；只能在非战斗安全换装界面重选。 | typed element enum、Trait、`SAVE`。 |
| 5 | 骑士甲胄 | `armor_ac_bonus +1 [RG:defense]`、`attack_bonus +1 [RG:offense]`；对具有 dragon tag 的 world relation check `+1` step，但不能把 hostile 直接变为 ally。 | 常驻。 | `ATTR` + world relation query。 |
| 7 | 龙翼共鸣 | 消耗 `1 AP` 进入 `RG:dragon_form` 龙翼档：获得 flight 与 `move_point_capacity_delta +2 [RG:flight_move]` `120 TU`；形态中可消耗 `2 AP` 一次释放 4 格锥形 `3D6` 所选 element，agility save `DC 16` half。 | `per_battle=1`；每次形态只可吐息一次。 | flight/elevation、cone skill、choice state。 |
| 10 | 龙骑降临 | 消耗 `2 AP` 进入 `RG:dragon_form` 降临档：获得 flight、`move_point_capacity_delta +3 [RG:flight_move]` 与 `armor_ac_bonus +2 [RG:defense]`，持续 `300 TU`；可消耗 `2 AP` 吐息 6 格锥形 `4D6` 所选 element，cooldown `60 TU`。形态中一次从至少 4 elevation 格高度向地面目标俯冲的 melee attack，可把该攻击的 base weapon/skill damage dice 掷两次并取总和，再在各主直接段附加 `2D4` 所选 element；装备/套装 extra segment 不参与翻倍。 | `per_world_day=1`；俯冲每次形态 1 次。 | flight/elevation、cooldown、base-dice rewrite、segment filter、`SAVE`。 |

**单件交互与 usage：** 单件披风元素选择、项链龙息、戒指飞行和龙蛋爆发独立。首次形成 3 件时若单件已有选择，套装 UI 可默认同元素但仍保存独立 choice；不同元素并存时分别按各 source 结算，不自动改写单件。7/10 件属于同一 `RG:dragon_form`，启动降临档先清理龙翼档及其吐息状态，不叠加 flight、移动或形态次数，也不返还 7 件 usage。

**落地缺口：** 持久元素选择、安全重选、dragon relation、flight/elevation、base dice 与 extra segment 分离、吐息 cooldown、AI 俯冲规划。

## 61. 血肉编织者 / `flesh_weaver_set`

**拓扑：仪式/领域 2/5/8/10。** 医疗与缝合先出现，尸体仪式在八件试作，十件才形成可转移伤害并爆裂的正式血肉傀儡。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 血肉知识 | medicine utility check `+2`；`res(negative_energy,half)`。 | 常驻。 | `QUERY` + `TRAIT`。 |
| 5 | 战地缝合 | `hp_max +15 [RG:vitality]`；消耗 `2 AP` 治疗 3 格内一个 living ally `2D6 HP`，并移除一个非传奇 bleed 或 poison。 | `per_battle=2` charge。 | heal、status cleanse、usage。 |
| 8 | 试作傀儡 | 消耗 `2 AP`，把 3 格内 eligible hostile corpse 缝合为 flesh puppet：HP 为原单位 max HP 的 30%（上限 30），AC 12，基础移动点 3，只拥有 formal basic attack；不继承技能、装备、AI brain 或掉落。 | `per_battle=1`；持续 `120 TU`；同时最多 1 个 set puppet。 | corpse fact、derived `SUMMON`、sanitized template。 |
| 10 | 血肉傀儡 | 消耗 `3 AP`，把 4 格内 eligible corpse 转为 puppet：HP 为原 max HP 的 50%（上限 60），AC 14，基础移动点 4，只继承 formal basic attack。穿戴者受主直接伤害时可 reaction（不耗 AP）把实际 HP 伤害的 50% 转给 puppet，每次形态最多 2 次；puppet 被击败时半径 2 格敌人受 `3D6 negative_energy`，constitution save `DC 17` half。 | `per_world_day=1`；持续 `240 TU`；启动时替换并清理 8 件 puppet。 | corpse summon、damage transfer、death explosion、replacement summon、`SAVE`。 |

**单件交互与 usage：** 单件快速缝合、治疗光环和替身傀儡致死拦截独立。单件替身不算 set puppet，不能承接十件 damage transfer；傀儡爆炸为 gear-set origin，不触发击杀或附伤递归。

**落地缺口：** corpse eligibility/provenance、sanitized summon builder、damage transfer transaction、source recursion guard、summon replacement 与性能预算。

## 62. 骸骨领主 / `bone_lord_set`

**拓扑：仪式/领域 2/5/8/10。** 防御和亡灵权威先建立，八件召集小队，十件才展开军团；召唤数量必须显式受统一性能上限约束。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 骨甲 | `armor_ac_bonus +1 [RG:defense]`；`res(physical_pierce,half)`。 | 常驻。 | `ATTR` + `TRAIT`。 |
| 5 | 骨王号令 | willpower save `+2`；低级 mindless undead 不主动攻击穿戴者，除非其阵营 objective 强制；消耗 `1 AP` 可命令 6 格内一个 allied undead 执行 formal basic move 或 basic attack。 | 命令每 `30 TU` 最多一次。 | cognition/relation、restricted command port。 |
| 8 | 骸骨集结 | 消耗 `2 AP` 召唤 2 个 formal bone-warrior definition，持续 `180 TU`；使用统一 set summon group，穿戴者可用 5 件命令控制其中一个。 | `per_battle=1`；同时最多 2 个 set-sourced bone summon。 | `SUMMON`、group/lifetime、command。 |
| 10 | 骸骨军团 | 消耗 `3 AP` 清理 8 件 set summon，召唤 4 个 bone-warrior 与 1 个 bone-mage，持续 `240 TU`；消耗 `1 AP` 可对整个 group 下达一次 move 或 focus-target 命令，cooldown `30 TU`。 | `per_world_day=1`；全组硬上限 5；没有可用生成格则按 warrior→mage 优先顺序缩减，至少 1 个，否则不消费。 | group summon、safe spawn、batch command、performance budget、`SAVE`。 |

**单件交互与 usage：** 单件亡灵召唤书和低级亡灵指挥独立，但所有召唤共同受 battle 全局性能预算；套装 capstone 只清理相同 gear-set source 的 8 件单位，不清理物品或技能召唤。

**落地缺口：** undead relation/cognition、restricted command、formal summon definitions、safe spawn、全局预算和 AI group command。

## 63. 荆棘女王 / `thorn_queen_set`

**拓扑：仪式/领域 2/5/8/10。** 防御、反刺、移动花园和十件王座逐级扩大控制范围；反伤和领域 tick 必须分别防递归。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 荆棘甲 | `armor_ac_bonus +1 [RG:defense]`；`res(physical_pierce,half)`。 | 常驻。 | `ATTR` + `TRAIT`。 |
| 5 | 反刺 | `mp_max +15 [RG:mana]`；被 melee 主直接攻击命中后自动反刺（不耗 AP），攻击者受 `1D4 physical_pierce`，每攻击者每 `30 TU` 最多一次。 | 常驻 reaction（不耗 AP）；反伤为 gear-set origin，不递归。 | incoming-hit reaction、attacker ledger。 |
| 8 | 移动花园 | 消耗 `2 AP`，以自身为中心创建半径 3 格移动领域 `180 TU`：敌人 `move_point_capacity_delta -1`，每 `60 TU` 首次开始于其中受 `1D4 physical_pierce`；穿戴者忽略 plant difficult terrain。 | `per_battle=1`；每敌人每 tick 一次。 | moving `AREA`、scheduled damage、terrain query。 |
| 10 | 荆棘王座 | 消耗 `3 AP` 创建半径 5 格移动领域 `240 TU`：敌人 `move_point_capacity_delta -2`，每 `60 TU` 受 `1D6 physical_pierce` 并作 strength save `DC 17`，失败 restrained `30 TU`。领域内敌人被击败时半径 `+1`，最多 7；友方 `armor_ac_bonus +1`，且其每 `30 TU` 第一次受到 melee 命中时对攻击者自动反伤（不耗 AP）`1D4 physical_pierce`。 | `per_world_day=1`；8/10 件共用 `RG:thorn_domain` usage，十件只出现升级版。 | moving/expanding area、OnKill、ally reaction、tick ledger、`SAVE`。 |

**单件交互与 usage：** 单件披风荆棘反击、花园王冠和站立治疗独立。每个反伤 source 有独立 cooldown ledger，但反伤生成伤害一律不能触发任何 OnDamage/反伤链。

**落地缺口：** moving/expanding area、反伤递归 guard、OnKill provenance、plant world fact、ally reaction projection、AI 路径重算。

## 64. 灰烬行者 / `ash_walker_set`

**拓扑：仪式/领域 2/5/8/10。** 低静态防御用环境适应起步，随后形成烟幕、伤害领域和十件灰烬风暴；单件灰烬重生不并入套装 capstone。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 余烬体 | `res(fire,half)`；constitution save `+2`。 | 常驻。 | `TRAIT`。 |
| 5 | 焦土行者 | `attack_bonus +1 [RG:offense]`；忽略 ash、burned-ground 和非熔岩 fire terrain 的 difficult terrain；不能因此免疫 lava damage。 | 常驻。 | `ATTR` + terrain query。 |
| 8 | 灰幕 | 消耗 `2 AP` 创建半径 3 格定点领域 `180 TU`：除穿戴者外单位视距上限 2 格，敌方 `attack_bonus -2`；每 `60 TU` 敌人受到 `1D4 fire`，constitution save `DC 16` negates 本次 tick。 | `per_battle=1`。 | visibility area、attack query、scheduled save/damage。 |
| 10 | 灰烬风暴 | 消耗 `3 AP`：半径 5 格立即造成 `4D6 fire + 2D6 negative_energy`，constitution save `DC 17` half；失败者下次 activation 的可用 AP 上限为 2、最多执行 1 个主动指令。领域持续 `240 TU`，视距上限 1 格，敌人每 `60 TU` 受到 `2D6 fire`，constitution save `DC 17` half。 | `per_world_day=1`；即时与 tick 分别结算。 | mixed damage、AP-cap status、visibility/tick area、`SAVE`。 |

**单件交互与 usage：** 单件灰烬云、烟尘领域和灰烬重生独立。多个烟幕使用最严格视距、最高 attack penalty，不相加；每个伤害领域仍按 source 维护 tick ledger。

**落地缺口：** fire/ash terrain、visibility query、动作类别限制、scheduled area、lethal source ordering 与 AI 视距。

## 65. 迷雾行者 / `mist_walker_set`

**拓扑：节奏/诡术 2/3/6/9/10。** 低价轻装通过潜行、雾跃、精准、连续伏击和最终领域形成节奏；驻留计数仅在十件出现。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 雾感 | stealth utility check `+2`；agility save `+2`。 | 常驻。 | `QUERY` + `TRAIT`。 |
| 3 | 雾跃 | 消耗 `1 AP`，传送到 3 格内具备 fog/mist/steam fact 的合法空格。 | `per_battle=2` charge。 | environment cell、teleport、usage。 |
| 6 | 雾刃 | `attack_bonus +1 [RG:offense]`；正式攻击的 `crit_threshold -1 [RG:critical]`。 | 常驻。 | `ATTR` + critical query；阈值 query 尚缺。 |
| 9 | 雾中伏击 | 从 hidden 或雾跃后自动武装（不耗 AP）：下一次直接攻击获得 advantage，并在各命中主直接段附加 `1D4 negative_energy [RG:ambush]`。 | 每 activation 最多武装一次；`per_battle=3` 次，武装 `60 TU` 后失效。 | hidden/teleport fact、armed attack、segment。 |
| 10 | 雾中死神 | 消耗 `3 AP` 创建半径 6 格定点浓雾领域 `240 TU`：敌人视距上限 1 格；穿戴者在其中 invisible，可在每次 activation 消耗 `1 AP` 传送 4 格；伏击附伤升为 `2D4 negative_energy [RG:ambush]` 且不消耗 9 件三次额度。敌人连续 3 个 `60 TU` tick 位于领域内时受到 `2D6 freeze`，随后计数清零。 | `per_world_day=1`；每 activation 最多一次 capstone ambush。 | fog visibility/invisibility、area teleport、consecutive tick counter、`SAVE`。 |

**单件交互与 usage：** 单件雾中隐形/传送、迷雾召唤、分身和幻象独立。9/10 件 ambush 是 replacement group，不同档不叠加；单件传送可武装 9 件，但不消耗 3 件雾跃 charge。

**落地缺口：** fog cell producer、invisibility/visibility、armed attack provenance、consecutive presence、area teleport 与 AI 雾中目标选择。

## 66. 铁处女 / `iron_maiden_set`

**拓扑：堡垒 4/6/8/10。** 全批最高一档 AC/attack/HP 底盘之一，不应在 2 件再获防御；四件立壳、六件强化、八件反制、十件释放痛苦。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 4 | 铁壳 | `res(physical_blunt,half)`；`armor_ac_bonus +1 [RG:defense]`。若从上次 activation 结束后未移动，额外 `armor_ac_bonus +1 [RG:stationary]`、`move_point_capacity_delta -1`。 | 常驻；移动后 stationary 立即失效。 | `TRAIT`、`ATTR`、stationary query。 |
| 6 | 闭锁堡垒 | defense 升为 `armor_ac_bonus +2 [RG:defense]`；stationary 升为额外 `armor_ac_bonus +2 [RG:stationary]`，`move_point_capacity_delta` 仍只 `-1`。 | 常驻。 | replacement modifiers、movement fact。 |
| 8 | 铁刺刑 | melee 攻击命中后可 reaction（不耗 AP），使攻击者受 `2D6 physical_pierce`；攻击者 strength save `DC 16`，失败武器 locked，直到其花费 `1 AP` 拔出；徒手/自然武器失败时改为该 limb 下次 attack `-2`，不创建可拔武器。 | `per_battle=3` reaction（不耗 AP）。 | incoming-hit reaction、weapon/limb fact、AP unlock。 |
| 10 | 痛苦绽放 | 穿戴者实际失去 HP 时获得 1 pain stack，每个 damage event 最多 1，最多 4。消耗 `3 AP` 选择相邻敌人并消费全部 stacks：造成 `3D6 physical_pierce + 每层 1D4 psychic`，constitution save `DC 17` half；失败 frightened `60 TU`。少于 1 stack 不能使用。 | `per_world_day=1`；pain stack battle-local，消费与伤害原子结算。 | post-damage stack、mixed attack、consume、save/status、`SAVE`。 |

**单件交互与 usage：** 单件披风反伤、项链伤后攻击、痛苦锁链和面具爆发独立。单件自伤可产生 pain stack，但同一 event 只 1 层；所有反伤和痛苦伤害均为 equipment origin，不能形成递归。

**落地缺口：** stationary 生命周期、weapon/limb lock、AP unlock、post-damage stack、防递归与 AI 是否消耗 capstone。

## 67. 蜘蛛女王 / `spider_queen_set`

**拓扑：仪式/领域 2/5/8/10。** 蛛感和攀行先建立，八件试铺蛛网，十件才把移动、束缚、传送和毒注入合成完整巢穴。

| 件数 | 阈值 | 准确效果 | 触发、次数与 TU | Typed owner / gap |
|---:|---|---|---|---|
| 2 | 蛛感 | stealth utility check `+2`；`res(poison,half)`；可攀爬 wall/ceiling，忽略 web difficult terrain。 | 常驻。 | `QUERY`、`TRAIT`、surface traversal。 |
| 5 | 毒网猎手 | `attack_bonus +1 [RG:offense]`；对 restrained 目标的第一个命中主直接段附加 `1D4 poison`，每目标每 `30 TU` 最多一次。 | 常驻；遵守递归排除。 | target-status/segment query、target ledger。 |
| 8 | 试作蛛网 | 消耗 `2 AP` 创建半径 3 格定点 web field `180 TU`：敌人 `move_point_capacity_delta -1`；进入及每 `60 TU` 作 agility save `DC 16`，失败 restrained `30 TU`。穿戴者在 field 内 `move_point_capacity_delta +1`，每 activation 可消耗 `1 AP` 传送到 3 格内 web cell。 | `per_battle=1`。 | web `AREA`、save/status、area teleport。 |
| 10 | 蛛网领域 | 消耗 `3 AP` 创建半径 5 格定点 web field `240 TU`：敌人 `move_point_capacity_delta -2`；进入及每 `60 TU` 作 agility save `DC 17`，失败 restrained `60 TU`。穿戴者 `move_point_capacity_delta +2`，每 activation 可消耗 `1 AP` 传送到任意 5 格内 web cell。每个在领域中首次被 restrained 的敌人可被消耗 `1 AP` 注毒一次：`2D6 poison`，之后每 `60 TU` `1D4 poison`、共 3 次。 | `per_world_day=1`；8/10 件共用 `RG:web_domain` usage，十件只出现升级版。 | web area/cell、restrained、teleport、per-target injection/tick、`SAVE`。 |

**单件交互与 usage：** 单件蛛网喷射、毒牙、蜘蛛群卵囊和 web/grapple immunity 独立。单件 restrained 可触发 5 件附伤，但不能被十件“领域中首次 restrained”注毒，除非目标的 restrained source 明确来自十件领域。

**落地缺口：** wall/ceiling traversal、web cell、area teleport、restrained source provenance、per-target poison chain、领域与蜘蛛群性能预算。

## 5. 正式落地与验收门槛

本文完成的是 set-bonus 设计，不是可玩实现。正式落地至少需要：

1. 套装侧 `GearSetDef/GearSetDefinition.member_item_ids[10]`、threshold Resource、registry 交叉校验与 immutable snapshot；不得修改原 `ItemDef`，不得按 tag 猜成员。
2. `GearSetEvaluationService` 按有效 equipment entry 累计评估，支持损坏、换装、多槽占用去重和 stable threshold summary。
3. `GearSetThreshold` typed trait/source、replacement group、source-bound status/area/summon cleanup 与 battle 内换装原子刷新。
4. 本文列出的 environment、target/source、damage origin、reaction ordering、usage、RNG、snapshot、summon 和 area typed owner。
5. preview/AI mutation-exact；UI 展示拓扑、当前件数、下一阈值、replacement 后实际值、remaining usage/charge。
6. `EquipmentState` strict save/writeback；battle-local derived source、status、area、charge 和 summon 不泄漏到 `PartyState`，持久 usage 原子提交。
7. 每套至少覆盖 membership、逐阈值、跨阈值换装、replacement、单件并存、usage、preview/AI 不消费、capstone 到期/清理和一个机制特有失败分支的 focused regression。
8. 在获取等级、同档敌人和完整 peer curve 落地后，用正式 BattleSim/coverage 重新冻结伤害、DC、TU 与次数；普通 runner PASS 不等于数值有效或生产覆盖。
