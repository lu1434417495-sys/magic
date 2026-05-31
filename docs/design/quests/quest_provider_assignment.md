# 任务发布体系设计：告示板 vs NPC

> 核心原则：城镇告示板只登记常规悬赏，特殊剧情任务由地图内NPC发放，两者绝不混淆。

---

## 一、三层发布体系

```
┌─────────────────────────────────────────────────────────────────┐
│                        任务发布体系                              │
├─────────────────────────────────────────────────────────────────┤
│  层级1: 城镇告示板 (Bounty Board)                                │
│  ├─ provider_id: service_bounty_registry                        │
│  ├─ 任务特征: 可重复、无剧情、纯战斗/收集/护送                  │
│  ├─ 展示方式: 城镇据点内的物理告示板，列表式浏览                │
│  └─ 接取方式: 点击即接，无确认弹窗，可批量勾选                  │
│                                                                 │
│  层级2: 据点委托板 (Contract Board)                              │
│  ├─ provider_id: service_contract_board                         │
│  ├─ 任务特征: 不可重复、与据点直接相关、低剧情含量              │
│  ├─ 展示方式: 城镇据点内的委托登记处                            │
│  └─ 接取方式: 查看详情后接取                                    │
│                                                                 │
│  层级3: NPC剧情任务 (NPC Quest Giver)                            │
│  ├─ provider_id: npc_<npc_id>                                   │
│  ├─ 任务特征: 不可重复、强剧情、有抉择分支、有接取对话          │
│  ├─ 展示方式: 世界地图/战斗地图内的NPC，靠近交互               │
│  └─ 接取方式: 交互→对话面板→条件检查→确认→接取               │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、告示板分层规则

### 2.1 悬赏板 (`service_bounty_registry`) — 纯悬赏

**准入条件**（必须同时满足）：
1. `is_repeatable == true`
2. `tags` 中包含 `"bounty"`
3. 无 `accept_requirements` 或仅有 `gold_min`/`party_level_min`（无任务链前置）
4. `description` 长度 < 200字（无复杂剧情描述）
5. 无 `accept_confirmation_text`（无不可逆抉择）

**展示字段**：
- `display_name` + 危险度星级（1-5星，从敌人模板推算）
- `summary_text`（目标摘要）
- `cost_label`（奖励）
- 危险度标签

**接取流程**：点击 → 直接接取（无确认弹窗），支持多选批量接取。

**任务列表**（Q086-Q100 全部 + 部分灰烬高难）：

| 编号 | 任务ID | 名称 | 危险度 |
|------|--------|------|--------|
| Q086 | bounty_wolf_pack | 狼群悬赏 | ★★☆☆☆ |
| Q087 | bounty_mist_harrier | 迷雾猎手 | ★★★☆☆ |
| Q088 | bounty_wolf_alpha | 阿尔法猎杀 | ★★★★☆ |
| Q089 | bounty_mist_beast | 迷雾兽猎杀 | ★★★☆☆ |
| Q090 | bounty_mist_weaver | 编织者悬赏 | ★★★★☆ |
| Q091 | bounty_wolf_vanguard | 先锋清剿 | ★★★☆☆ |
| Q092 | bounty_wolf_shaman | 萨满猎杀 | ★★★★☆ |
| Q093 | bounty_wolf_raider | 骑兵清剿 | ★★★☆☆ |
| Q094-Q100 | （高级悬赏扩展） | 混合兽群/精英/迷雾之潮等 | ★★★★☆ |

### 2.2 委托板 (`service_contract_board`) — 据点委托

**准入条件**：
1. `is_repeatable == false`
2. `tags` 中包含 `"contract"` 或 `"delivery"` 或 `"escort"`
3. 任务内容与当前据点直接相关（如运送物资到该据点、护送该据点的商队）
4. 剧情含量低（无抉择分支、无阵营影响）

**展示字段**：完整详情面板（描述 + 目标 + 奖励）

**接取流程**：点击 → 详情面板 → 接取按钮 → 状态反馈。

**任务列表**（从主世界/灰烬交界中筛选）：

| 编号 | 任务ID | 名称 | 说明 |
|------|--------|------|------|
| Q004 | side_iron_delivery | 铁矿运送 | 从A据点运铁矿到当前据点 |
| Q005 | side_herb_moonfern | 月蕨寻觅 | 为当前据点的医师采集药材 |
| Q007 | side_dispatch_delivery | 封缄急件 | 从当前据点送信到B据点 |
| Q047 | ashen_ember_gathering | 收集余烬 | 为灰烬据点的熔炉收集燃料 |

> **注意**：委托板上的任务一旦完成后即消失，不会重新出现（除非开新存档）。

---

## 三、NPC剧情任务发布体系

### 3.1 设计理念

剧情任务不是随手可得的。它们隐藏在世界的各个角落：
- **世界地图**：在特定坐标站着一个NPC，靠近后显示交互图标
- **战斗地图**：完成某场战斗后，场地边缘出现一个受伤的幸存者/神秘的过客
- **据点内**：特定NPC站在角落里，不与告示板混在一起

**接取仪式感**：
1. 玩家发现NPC（探索驱动）
2. 交互后，NPC说出 `accept_dialogue_text`（接取对话）
3. 详情面板展示任务背景、目标、抉择后果
4. 条件检查（`accept_requirements`）
5. 若任务含不可逆抉择，弹出 `accept_confirmation_text` 确认
6. 接取后NPC可能消失、改变位置、或记住你的选择

### 3.2 Provider ID 命名规范

```
npc_<npc_id>
```

`npc_id` 与 `interaction_script_id` 共用同一命名空间。例如：
- `npc_village_chief` — 村长
- `npc_wounded_soldier` — 负伤士兵
- `npc_mysterious_stranger` — 神秘过客
- `npc_dragon_cultist` — 龙巫教徒
- `npc_fallen_angel` — 堕落天使

**注册方式**：在 `QuestProviderContentRules.SUPPORTED_PROVIDER_IDS` 中动态注册，或通过 `tags` 中的 `"npc_quest"` 标签自动识别。

### 3.3 NPC 在世界中的存在方式

| 存在类型 | 触发条件 | 示例 |
|---------|---------|------|
| 常驻NPC | 据点/地图中固定位置 | 村长、铁匠、酒馆老板 |
| 条件出现NPC | 完成前置任务后出现 | 帝国信使（完成Q101后出现） |
| 战斗后NPC | 特定战斗胜利后出现 | 濒死的龙裔（击败Q141区域敌人后出现） |
| 隐藏NPC | 需要特定物品/条件才能看到 | 只有持有 `dead_road_lantern` 才能看到的幽灵 |
| 随机遭遇NPC | 世界地图移动时概率出现 | 迷路的商人、逃兵 |

### 3.4 NPC 任务板的 UI 差异

与城镇告示板不同，NPC任务交互是**一对一**的：

```
┌─────────────────────────────────────┐
│  [NPC头像] 老猎人格雷                │
├─────────────────────────────────────┤
│                                     │
│  "你杀了那些普通的，很好。"         │  ← accept_dialogue_text
│  "但那只白的……它不一样。"           │
│  "眼睛是红的，像裂隙里的光。"       │
│                                     │
│  ── 任务详情 ──                     │
│  【狼王挑战】                        │
│  描述：村口的狼群出了个首领...      │
│  目标：击败 wolf_alpha ×1           │
│  奖励：150金 + warrior_guard        │
│                                     │
│  [接受任务]  [离开]                 │
│                                     │
└─────────────────────────────────────┘
```

---

## 四、200个任务的发布者重新分配

### 4.1 主世界任务（Q001-Q045）

| 编号 | 任务ID | 名称 | 发布者 | 发布者类型 | NPC说明 |
|------|--------|------|--------|-----------|---------|
| Q001 | tutorial_first_blood | 初阵 | npc_village_chief | 常驻 | 村长，新手村中央 |
| Q002 | tutorial_gather_herbs | 采集药材 | npc_village_healer | 常驻 | 村医，草药棚旁 |
| Q003 | tutorial_wolf_alpha | 狼群首领 | npc_old_hunter | 条件出现 | 完成Q001后出现，村东口 |
| Q004 | side_iron_delivery | 铁矿运送 | service_contract_board | 委托板 | — |
| Q005 | side_herb_moonfern | 月蕨寻觅 | service_contract_board | 委托板 | — |
| Q006 | main_mist_investigation | 迷雾调查 | npc_militia_captain | 常驻 | 民兵队长，哨塔 |
| Q007 | side_dispatch_delivery | 封缄急件 | service_contract_board | 委托板 | — |
| Q008 | main_border_patrol | 边境巡逻 | npc_militia_captain | 常驻 | 同上，可重复对话接不同任务 |
| Q009 | side_bandit_cleansing | 匪徒清剿 | npc_village_chief | 常驻 | 村长，第二阶段任务 |
| Q010 | main_crack_seal | 裂隙封印 | npc_mysterious_stranger | 条件出现 | 完成Q006后出现，裂隙边缘 |
| Q011 | main_ashen_gate | 灰烬之门 | npc_ash_seer | 常驻 | 灰烬先知，村边小屋 |
| Q012-Q045 | （主世界进阶任务） | 多样化 | 多样化NPC | 常驻/条件 | 详见下文分配表 |

### 4.2 灰烬交界任务（Q046-Q085）

| 编号 | 任务ID | 名称 | 发布者 | 发布者类型 | NPC说明 |
|------|--------|------|--------|-----------|---------|
| Q046 | ashen_awakening | 灰烬觉醒 | npc_ash_guide | 常驻 | 灰烬向导，交界入口 |
| Q047 | ashen_ember_gathering | 收集余烬 | service_contract_board | 委托板 | — |
| Q048 | ashen_mist_weaver | 编织者之影 | npc_ash_guide | 常驻 | 同上，进阶任务 |
| Q049 | ashen_cathedral_heresy | 异端之骸 | npc_broken_priest | 条件出现 | 大教堂门外跪着的破碎祭司 |
| Q050 | ashen_burned_seal | 烧融圣徽 | npc_broken_priest | 条件出现 | 同上 |
| Q051 | ashen_bell_ringer | 钟楼余响 | npc_broken_priest | 条件出现 | 同上 |
| Q052 | ashen_drowned_index | 溺水的索引 | npc_librarian_ghost | 常驻 | 书库门口的无名幽灵 |
| Q053 | ashen_forbidden_pages | 禁书残页 | npc_librarian_ghost | 常驻 | 同上 |
| Q054 | ashen_abyss_survival | 裂界生存 | service_bounty_registry | 悬赏板 | 高难生存挑战 |
| Q055 | ashen_dread_alpha | 恐惧阿尔法 | service_bounty_registry | 悬赏板 | 首领猎杀 |
| Q056 | ashen_rift_warden | 裂隙守望者 | service_bounty_registry | 悬赏板 | 精英防御 |

### 4.3 悬赏任务（Q086-Q100）

全部归入 `service_bounty_registry`，无NPC。

### 4.4 帝国烽烟（Q101-Q125）

| 编号 | 任务ID | 名称 | 发布者 | NPC说明 |
|------|--------|------|--------|---------|
| Q101 | empire_conscription | 征兵令 | npc_empire_recruiter | 帝国征兵官，据点前广场 |
| Q102 | empire_border_skirmish | 边境冲突 | npc_empire_captain | 帝国上尉，军营帐篷 |
| Q103 | empire_spy_network | 谍影重重 | npc_federation_informant | 联邦间谍（伪装成商人） |
| Q104 | empire_naval_battle | 海战烽火 | npc_empire_admiral | 帝国海军上将，港口 |
| Q105 | empire_airship_assault | 空艇突袭 | npc_empire_sky_captain | 空艇舰长，飞艇甲板 |
| Q106 | empire_scorched_earth | 焦土政策 | npc_empire_colonel | 帝国上校，指挥部 |
| Q107 | empire_peace_talks | 和平谈判 | npc_federation_diplomat | 联邦外交官，中立区 |
| Q108 | empire_shadow_war | 暗战 | npc_federation_spymaster | 联邦间谍首脑，地下酒窖 |
| Q109 | empire_final_battle | 决战 | npc_empire_field_marshal | 帝国元帅，决战前线 |
| Q110 | empire_endgame | 终局 | npc_federation_chancellor | 联邦议长（或帝国皇帝） |
| Q111-Q125 | 帝国支线 | 多样化 | 多样化NPC | 士兵、逃兵、平民、密探 |

### 4.5 种族之殇（Q126-Q150）

| 编号 | 任务ID | 名称 | 发布者 | NPC说明 |
|------|--------|------|--------|---------|
| Q126 | race_elven_alliance | 远古盟约 | npc_elven_exile | 精灵流亡者，森林废墟 |
| Q127 | race_elven_tide | 退潮之歌 | npc_elven_druid | 精灵德鲁伊，圣地边缘 |
| Q128 | race_drow_infiltration | 卓尔渗透 | npc_drow_defector | 卓尔叛逃者，城市下水道 |
| Q129 | race_elven_banished | 精灵放逐 | npc_banished_elf | 被放逐的精灵，森林边缘 |
| Q130 | race_last_forest | 最后的森林 | npc_elven_elder | 精灵长老，古树下 |
| Q131 | race_deep_road | 深路之殇 | npc_dwarf_miner | 矮人矿工，矿井入口 |
| Q132 | race_mithril_blood | 秘银之血 | npc_dwarf_foreman | 矮人工头，矿脉核心 |
| Q133 | race_soul_forging | 锻魂仪式 | npc_dwarf_blacksmith | 矮人铁匠，锻造场 |
| Q134 | race_stone_exile | 矮人放逐者 | npc_stone_exile | 石肤者，洞穴深处 |
| Q135 | race_underground_war | 地底战争 | npc_dwarf_commander | 矮人指挥官，地下堡垒 |
| Q136 | race_orc_migration | 大迁徙 | npc_orc_chieftain | 兽人酋长，草原营地 |
| Q137 | race_honor_duel | 荣耀决斗 | npc_orc_berserker | 兽人狂战士，竞技场 |
| Q138 | race_orc_shaman | 兽人萨满 | npc_orc_shaman | 兽人萨满，图腾柱旁 |
| Q139 | race_mixed_blood | 混血之子 | npc_half_orc_child | 半兽人孩子（由保护者NPC转交） |
| Q140 | race_old_god_blood | 旧神之血 | npc_orc_shaman | 兽人萨满（进阶） |
| Q141 | race_dragonborn_awakening | 血脉觉醒 | npc_dragonborn_hermit | 龙裔隐士，火山口 |
| Q142 | race_dragonborn_discrimination | 龙裔歧视 | npc_dragonborn_youth | 龙裔少年，城市贫民区 |
| Q143 | race_dragon_pact | 龙族盟约 | npc_gold_dragon_avatar | 金龙化身，云端神殿 |
| Q144 | race_dragon_soul_weapon | 龙魂武器 | npc_dragonborn_gravekeeper | 龙裔守墓人，地下墓室 |
| Q145 | race_dragon_trial | 龙之审判 | npc_dragon_judge | 龙族法官，审判庭 |
| Q146 | race_gnome_feud | 侏儒世仇 | npc_gnome_inventor | 侏儒发明家，工坊 |
| Q147 | race_halfling_neutrality | 半身人中立 | npc_halfling_merchant | 半身人商人，商路驿站 |
| Q148 | race_tiefling_blood | 提夫林血统 | npc_tiefling_refugee | 提夫林难民，避难所 |
| Q149 | race_half_elf_dilemma | 半精灵困境 | npc_half_elf_guard | 半精灵守卫，边境哨站 |
| Q150 | race_githyanki_raid | 吉斯洋基掠夺 | npc_githyanki_captain | 吉斯洋基队长，裂隙边缘 |

### 4.6 巨龙纪元（Q151-Q175）

| 编号 | 任务ID | 名称 | 发布者 | NPC说明 |
|------|--------|------|--------|---------|
| Q151 | dragon_red_karazan | 红龙焚心者 | npc_dragon_hunter | 屠龙者，火山脚下 |
| Q152 | dragon_blue_volaxis | 蓝龙雷霆暴君 | npc_storm_witness | 雷暴幸存者，城市废墟 |
| Q153 | dragon_green_lisera | 绿龙毒藤夫人 | npc_poisoned_noble | 中毒贵族，庄园地牢 |
| Q154 | dragon_black_morgath | 黑龙腐沼之王 | npc_swamp_hermit | 沼泽隐士，泥潭中央 |
| Q155 | dragon_white_iselyn | 白龙霜牙 | npc_frozen_climber | 冻僵的登山者，雪山营地 |
| Q156 | dragon_egg_guardian | 龙蛋守护者 | npc_dragon_egg_cultist | 龙蛋教徒，洞穴深处 |
| Q157 | dragon_blood_potion | 龙血药剂 | npc_alchemist_outlaw | 炼金术 outlaw，地下工坊 |
| Q158 | dragon_half_dragon | 半龙生物 | npc_dragon_researcher | 龙类学者，森林小屋 |
| Q159 | dragon_cult | 龙巫教 | npc_resistance_leader | 反抗军领袖，邪教总部外 |
| Q160 | dragon_slayer_tomb | 屠龙者之墓 | npc_tomb_robber | 盗墓者，墓地入口 |
| Q161 | dragon_gold_paladin | 金龙圣骑士 | npc_gold_dragon_avatar | 金龙化身（同Q143） |
| Q162 | dragon_silver_bard | 银龙吟游诗人 | npc_silver_dragon_bard | 银龙吟游诗人，酒馆 |
| Q163 | dragon_bronze_guardian | 青铜龙守卫 | npc_coast_warden | 海岸守卫，灯塔 |
| Q164 | dragon_copper_judge | 赤铜龙法官 | npc_court_clerk | 法庭书记员，审判庭 |
| Q165 | dragon_brass_library | 黄铜龙图书馆 | npc_librarian_apprentice | 图书管理员学徒，图书馆 |
| Q166 | dragon_knight_oath | 龙骑士誓言 | npc_cursed_dragon_knight | 被诅咒的龙骑士，竞技场 |
| Q167 | dragon_backlash | 龙之反噬 | npc_undead_dragon_rider | 亡灵龙骑士，墓地 |
| Q168 | dragon_lost_nest | 失落的龙巢 | npc_dragon_conservationist | 龙类保护主义者，森林 |
| Q169 | dragon_council | 龙族议会 | npc_dragon_council_herald | 龙族议会传令官，议会厅 |
| Q170 | dragon_final_war | 最后的龙战 | npc_dragon_mediator | 龙族调解者，终极战场边缘 |
| Q171 | dragon_sleeper_dream | 龙眠者之梦 | npc_dream_walker | 梦境行者，龙眠者身旁 |
| Q172 | dragon_sigh | 龙之叹息 | npc_last_dragon_witness | 最后龙的见证者，天顶 |
| Q173 | dragonborn_ultimate | 龙裔终极抉择 | npc_dragonborn_elder | 龙裔长老，广场中央 |
| Q174 | dragon_twilight | 龙之黄昏 | npc_faded_dragon_whisperer | 褪色龙低语者，黄昏之地 |
| Q175 | dragon_eternal_legend | 永龙传说 | npc_dragon_chronicler | 龙族编年史官，传说之殿 |

### 4.7 信仰深渊（Q176-Q200）

| 编号 | 任务ID | 名称 | 发布者 | NPC说明 |
|------|--------|------|--------|---------|
| Q176 | faith_hell_gate | 地狱之门 | npc_demon_hunter | 恶魔猎手，裂隙边缘 |
| Q177 | faith_abyss_call | 深渊呼唤 | npc_mad_prophet | 疯狂先知，腐蚀森林 |
| Q178 | faith_devil_contract | 魔鬼契约 | npc_devil_merchant | 魔鬼商人，十字路口 |
| Q179 | faith_abyss_gaze | 深渊凝视 | npc_survivor_of_gaze | 凝视幸存者，深渊边缘 |
| Q180 | faith_hell_war | 地狱战争 | npc_hell_deserter | 地狱逃兵，战场废墟 |
| Q181 | faith_abyss_child | 深渊之子 | npc_cult_defector | 邪教叛逃者，祭坛外 |
| Q182 | faith_soul_trade | 灵魂交易 | npc_soul_broker | 灵魂经纪人，地狱市场 |
| Q183 | faith_abyss_whisper | 深渊低语 | npc_whisper_listener | 低语聆听者，裂隙旁 |
| Q184 | faith_hell_order | 地狱秩序 | npc_hell_judge | 地狱法官，法庭 |
| Q185 | faith_abyss_bottom | 深渊之底 | npc_mirror_wanderer | 镜中徘徊者，深渊之底 |
| Q186 | faith_fallen_heaven | 天堂陨落 | npc_fallen_angel | 堕落天使，天界废墟 |
| Q187 | faith_old_god_awaken | 古神觉醒 | npc_old_god_cultist | 古神信徒，祭坛 |
| Q188 | faith_faith_conflict | 信仰之争 | npc_neutral_priest | 中立祭司，圣城 |
| Q189 | faith_god_proxy | 神之代理 | npc_chosen_one | 被选中者，神殿 |
| Q190 | faith_old_god_gift | 古神礼物 | npc_old_god_whisperer | 古神低语者，遗迹 |
| Q191 | faith_heaven_war | 天堂战争 | npc_angel_deserter | 天使逃兵，云端 |
| Q192 | faith_faith_cost | 信仰代价 | npc_sacrifice_priest | 牺牲祭司，神殿 |
| Q193 | faith_god_death | 神之死 | npc_godslayer | 弑神者，神域入口 |
| Q194 | faith_new_god | 新神诞生 | npc_ascension_candidate | 成神候选者，神殿中央 |
| Q195 | faith_last_temple | 最后神殿 | npc_temple_guardian | 神殿守护者，废弃神殿 |
| Q196 | faith_vampire_lord | 吸血鬼领主 | npc_vampire_hunter | 吸血鬼猎人，城堡外 |
| Q197 | faith_werewolf_moon | 狼人之月 | npc_werewolf_hunter | 狼人猎人，月圆森林 |
| Q198 | faith_lich_phylactery | 巫妖之匣 | npc_lich_hunter | 巫妖猎人，巫妖塔外 |
| Q199 | faith_undead_legion | 不死军团 | npc_undead_hunter | 不死军团猎人，不死战场 |
| Q200 | faith_god_death_finale | 神之死·终章 | npc_final_witness | 终焉见证者，终焉之地 |

---

## 五、NPC 在世界中的分布规则

### 5.1 常驻NPC

- 出现在据点/地图的固定位置
- 不随时间/任务状态改变位置
- 可重复交互（但任务不可重复）
- 示例：村长、民兵队长、酒馆老板

### 5.2 条件出现NPC

- 满足特定条件后才出现
- 条件类型：
  - `quest_completed`：完成某任务后出现
  - `quest_active`：接取某任务后出现
  - `world_step_min`：世界步数达到后出现
  - `settlement_rank_min`：据点等级达到后出现
- 示例：帝国信使（完成Q101后出现）、破碎祭司（接近大教堂后出现）

### 5.3 战斗后NPC

- 特定战斗胜利后出现在战场边缘
- 通常带有"紧急"或"隐藏"性质
- 示例：濒死的龙裔（击败区域敌人后出现）、幸存商人（保护商队后出现）

### 5.4 隐藏NPC

- 需要特定物品或条件才能看到/交互
- 增加探索深度
- 示例：只有持有 `dead_road_lantern` 才能看到的幽灵卓尔

### 5.5 随机遭遇NPC

- 世界地图移动时概率出现
- 不绑定固定位置
- 示例：迷路商人、逃兵、神秘过客

---

## 六、任务板的过滤与展示规则

### 6.1 悬赏板过滤逻辑

```csharp
bool ShouldShowOnBountyBoard(QuestDef quest)
{
    return quest.is_repeatable
        && quest.tags.Contains("bounty")
        && quest.accept_requirements.Count == 0
        && quest.description.Length < 200
        && quest.accept_confirmation_text == "";
}
```

### 6.2 委托板过滤逻辑

```csharp
bool ShouldShowOnContractBoard(QuestDef quest)
{
    return !quest.is_repeatable
        && (quest.tags.Contains("contract") || quest.tags.Contains("delivery") || quest.tags.Contains("escort"))
        && !quest.tags.Contains("main_story")
        && !quest.tags.Contains("npc_quest")
        && quest.accept_confirmation_text == "";
}
```

### 6.3 NPC任务过滤逻辑

```csharp
bool IsNpcQuest(QuestDef quest)
{
    return !quest.is_repeatable
        && (quest.provider_interaction_id.StartsWith("npc_")
            || quest.tags.Contains("npc_quest")
            || quest.tags.Contains("main_story")
            || quest.accept_confirmation_text != "");
}
```

---

## 七、交互流程对比

| 环节 | 悬赏板 | 委托板 | NPC任务 |
|------|--------|--------|---------|
| **发现方式** | 进入据点→点击告示板 | 进入据点→点击委托处 | 地图探索→靠近NPC |
| **展示内容** | 列表（名称+星级+奖励） | 列表+详情面板 | NPC头像+对话+详情 |
| **接取对话** | 无 | 无（或极简） | 有（accept_dialogue_text） |
| **确认弹窗** | 无 | 无 | 可选（accept_confirmation_text） |
| **条件检查** | 等级/金币 | 无 | 完整accept_requirements |
| **接取反馈** | 系统默认 | 系统默认 | 专属反馈（accept_feedback_success） |
| **失败反馈** | 无（不可点） | 无（不可点） | 专属反馈（accept_feedback_failure） |
| **接取后NPC** | — | — | 可能消失/改变/记住选择 |

---

## 八、Schema 建议（文档层，暂不修改代码）

### 8.1 新增字段建议

在 `QuestDef` 中新增：

```csharp
// 接取对话：NPC对玩家说的话
[Export(PropertyHint.MultilineText)]
public string accept_dialogue_text { get; set; } = "";

// 接取成功反馈
[Export]
public string accept_feedback_success { get; set; } = "";

// 接取失败反馈
[Export]
public string accept_feedback_failure { get; set; } = "";

// 确认弹窗文本（空则不弹窗）
[Export]
public string accept_confirmation_text { get; set; } = "";
```

### 8.2 Provider 扩展建议

`QuestProviderContentRules` 支持动态 provider_id（`npc_*` 前缀自动识别），无需硬编码注册每个NPC。

### 8.3 新增 Tag 建议

- `"bounty"` — 悬赏任务（上告示板）
- `"contract"` — 据点委托（上委托板）
- `"npc_quest"` — NPC剧情任务（不上任何板）
- `"main_story"` — 主线剧情（强制NPC发布）
- `"hidden"` — 隐藏任务（需条件触发）
- `"repeatable"` — 可重复（与 is_repeatable 字段一致）

---

## 九、接取对话文本汇总（31个已验证任务）

### 主世界

**tutorial_first_blood**（村长发布）
```
"你看起来还没沾过血。
北边林子里有三只狼在啃商队的货。
把它们清理掉，回来找我。"
```

**tutorial_wolf_alpha**（老猎人发布）
```
"你杀了那些普通的，很好。
但那只白的……它不一样。
眼睛是红的，像裂隙里的光。别逞强。"
```

**main_mist_investigation**（民兵队长发布）
```
"迷雾不是天气。
有人在雾里看到了……东西。
带两份样本回来，我要知道那是什么。"
```

**main_crack_seal**（神秘过客发布）
```
"裂隙在扩大。不是比喻，是真的在扩大。
两只迷雾兽和一只织者守在那里。
杀了它们，封印能撑多久算多久。"
```

**main_ashen_gate**（灰烬先知发布）
```
"灰烬交界不是一个地方。是一个选择。
你可以选择永远不去。
但如果你想去，门已经开了。"
```

### 灰烬交界

**ashen_awakening**（灰烬向导发布）
```
"你醒了。或者你以为你醒了。
这里的规则不一样。
先杀两只迷雾兽，证明你能呼吸这里的空气。"
```

**ashen_mist_weaver**（灰烬向导发布）
```
"织者不编织布。它们编织现实。
杀两只。别听它们说话。
听了你就不是你了。"
```

**ashen_cathedral_heresy**（破碎祭司发布）
```
"那座教堂不应该存在。
但它在那里，而且门是开的。
进去。或者转身。选择一个。"
```

**ashen_rift_warden**（悬赏板，无NPC）
```
"裂界深处有人在笑。
或者那是你自己的声音。
只有下去才知道。"
```

### 悬赏（无NPC，告示板直接发布）

**bounty_wolf_pack**
```
"五只狼。标准活。别太骄傲。"
```

**bounty_mist_weaver**
```
"一只织者。别听它说话。
听了就忘了你来干嘛的。"
```

**bounty_wolf_alpha**
```
"首领级。单只。
但单只够杀一队人。你确定要接？"
```

---

## 十、关键设计决策

1. **悬赏板 = 纯工具，NPC = 故事**：悬赏板不提供任何剧情体验，它是纯 gameplay 系统；NPC 任务承载全部叙事重量。

2. **剧情任务不上板**：一旦任务有 `accept_dialogue_text` 或 `accept_confirmation_text`，它就不应该出现在任何告示板上。

3. **NPC 位置即叙事**：NPC 站在哪里、穿什么、周围有什么，都是任务叙事的一部分。委托板的文字描述无法替代这种空间叙事。

4. **委托板作为缓冲区**：不是所有常规任务都适合悬赏板，也不是所有任务都值得 NPC 发布。委托板承接"据点相关的低剧情任务"，形成三层缓冲。

5. **动态出现 = 世界感**：条件出现 NPC 让任务不是静态列表，而是"世界在变化"。玩家会记住"上次来这里还没有这个人"。
