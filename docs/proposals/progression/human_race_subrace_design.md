# 人类种族与亚种设计方案

> 状态：`Proposal / 未实现`
> 起草日期：`2026-09-12`
> 关联现行文档：[`../../design/progression/trait_system.md`](../../design/progression/trait_system.md)、[`../../design/progression/character_module.md`](../../design/progression/character_module.md)
> 关联上下文单元：CU-11（角色成长）、CU-15（战斗运行时）

本文分两部分：第一部分是对**当前种族系统的实测分析**（逐字段核对代码，标注哪些字段真的影响规则、哪些只是数据）；第二部分是基于这些约束的**人类种族与 8 个亚种的详细设计**。

---

## 第一部分：当前种族系统实测结论

### 1.1 数据资产盘点

| 域 | 文件 | 条目数 | 说明 |
|---|---|---|---|
| races | `data/configs/json/races/content.json` | 11 | dragonborn / drow / dwarf / elf / githyanki / gnome / half_elf / half_orc / halfling / **human** / tiefling |
| subraces | `data/configs/json/subraces/content.json` | 31 | 龙裔独占 10 个；人类只有 1 个 |
| traits | `data/configs/json/traits/traits.json` | 239 | 其中非装备/套装类只有 43 个 |
| age_profiles | `data/configs/json/age_profiles/content.json` | 11 | 每族一份 |
| bloodlines / ascensions | 各自目录 | 少量 | 与种族并列的另一条身份通道 |

### 1.2 运行链

```text
races/subraces JSON
  -> ProfessionIdentityJsonImport (strict DTO)
  -> ProfessionIdentityDefinitionProjector
  -> RaceContentRegistry / SubraceContentRegistry (域内校验)
  -> ProgressionContentRegistry (跨域校验：trait / skill / age_profile / 父子关系)
  -> ProgressionIdentityCatalogData
       |-- AttributeService.CollectAllModifierEntries(...)   属性修正
       |-- RaceTraitResolver.ApplyToUnit(...)                豁免标签 / 抗性 / 视觉与熟练 / 种族技能充能
       |-- CharacterTraitService.CollectIdentity(...)        trait -> EffectiveTraitSet
       |     -> BattleTraitPassiveProjectionService          trait 被动投影
       |     -> TraitTriggerHooks                            trait 事件触发
       |-- RacialSkillGrantService.BackfillMember(...)       种族技能授予 / 回收
       |-- CharacterCreationWindow                           创角选择与预览
```

属性修正的叠加顺序固定为 `race -> subrace -> age -> bloodline -> ascension -> versatility -> profession -> skill -> trait -> equipment -> passive -> temporary`（`AttributeService.CollectAllModifierEntries`），种族与亚种都以 `rank = 1` 参与。

### 1.3 字段有效性矩阵（本次分析的核心结论）

| 字段 | 所在 | 真实效果 | 判定 |
|---|---|---|---|
| `attribute_modifiers` | race / subrace | 进入属性快照管线，`flat` 相加、`percent` 后乘 | ✅ 生效 |
| `save_advantage_tags` / `save_disadvantage_tags` / `save_immunity_tags` | race / subrace | `RaceTraitResolver` 追加到单位豁免状态，`BattleSaveResolver` 消费 | ✅ 生效 |
| `damage_resistances` | race / subrace | 合并进 `BattleUnitDamageResistanceState`，档位 `normal / half / double / immune` | ✅ 生效 |
| `racial_granted_skills` | race / subrace | 授予技能并初始化 `at_will / per_battle / per_turn` 充能；耗尽时施法被 block | ✅ 生效 |
| `trait_ids` | race / subrace | 引用的 trait 进入 effective trait 链 | ✅ 生效（但见 1.4） |
| `body_size_category` | race | 创角体型判定来源之一，决定战斗占格（medium = 1x1） | ✅ 生效（人类全 medium，无差别） |
| `proficiency_tags` | race / subrace | 写入 `BattleUnitState`，但 `HasProficiencyTag` 在生产代码里**零调用** | ⚠️ 纯数据 |
| `vision_tags` | race / subrace | 同上；战斗没有光照 / 视野规则消费它 | ⚠️ 纯数据 |
| `base_speed` / `speed_bonus` | race / subrace | 只有 `base_speed > 0` 的校验；战斗移动是固定 `DefaultMovePointsPerTurn = 2` | ❌ 未接线 |
| `dialogue_tags` | race / subrace | 全仓库零消费者 | ❌ 未接线 |
| `racial_trait_summary` | race / subrace | 创角预览与角色信息面板的说明文本 | ✅ UI 文本 |
| `age_profile_id` | race | 年龄阶段解析已实现，但 11 份 profile 的 `stage_rules.attribute_modifiers` **全为空** | ⚠️ 链路通、内容空 |

### 1.4 trait 层的真相：现有种族特性几乎全是空壳

43 个非装备 trait 逐个核对后：

- `effect_type` 是闭合 enum（45 个值），但只有 3 个值真的有代码分支，全部在 `TraitTriggerContentRules.DISPATCH_TRIGGER_RULES`：
  - `halfling_luck` × `on_natural_one`
  - `savage_attacks` × `on_crit`
  - `relentless_endurance` × `on_fatal_damage`
- 其余 40 个（`fey_ancestry`、`darkvision`、`keen_senses`、`stonecunning`、`drow_magic`、`infernal_legacy`、`human_versatility`、`civil_militia` …）在代码里**没有任何分支**，JSON 里的通用字段也全是空数组。它们是纯标记。
- 因此现有种族的"特性"实际上 100% 由 race / subrace 的通用字段承载：精灵的抗魅惑来自 `save_advantage_tags: ["charm","sleep"]`，矮人的抗毒来自 `damage_resistances: {"poison":"half"}`，提夫林的火抗来自 `damage_resistances: {"fire":"half"}`。
- 唯一的例外是 `human_versatility`：`CharacterCreationWindow` 按 trait id 或 `EffectType` 识别它来开放"自选 +1 属性"的 UI，`AttributeService.AppendVersatilityModifierEntries` 把选中的基础属性 +1。这是人类目前唯一真实的机制。

**结论：新增种族特性时，机制必须写在通用字段里，而不是指望 `effect_type` 的名字。**

### 1.5 不改代码可用的机制通道

| 通道 | 位置 | 可写内容 |
|---|---|---|
| 属性修正 | identity 或 trait | 六基础属性、`*_modifier`、`hp_max`、`character_hp_max_percent_bonus`、`mp_max`、`stamina_max`、`stamina_recovery_percent_bonus`、`aura_max`、`action_points`、`action_threshold`、`armor_class`、`armor_ac_bonus`、`shield_ac_bonus`、`dodge_bonus`、`deflection_bonus`、`armor_max_dex_bonus` |
| 豁免优势 / 劣势 / 免疫 | identity 或 trait | 22 个裸 save tag |
| 豁免加值（按属性） | **仅 trait** `save_bonus_entries` | `strength / agility / constitution / perception / intelligence / willpower` |
| 豁免加值（按 tag） | **仅 trait** `save_tag_bonus_entries` | 任意 save tag，`stack_mode` = `add` / `highest` |
| 伤害抗性 | identity `damage_resistances` 或 trait `damage_resistance_entries` | 14 个伤害 tag × 4 档 |
| 常驻被动状态 | **仅 trait** `passive_status_effects` | 自定义 `status_id` + `power` + `stacks` + `undispellable` + 附带 `save_immunity_tags`，可当作技能条件标记 |
| 种族技能 | identity `racial_granted_skills` | 任意已存在的 `skill_id`，`at_will / per_battle / per_turn` + 充能数 |

需要改代码的（本设计**不依赖**这些）：

- 新的 `effect_type` 名字：要同步改 4 处（`data/schemas/content/traits.schema.json`、`TraitContentRules`、`TraitImportValueRules`、`TraitJsonImportContracts`）。
- 新的触发时机（例如"每场战斗第一次被击中时…"）：要加 trigger kind + dispatch + hook 实现。
- `base_speed` / `proficiency_tags` / `vision_tags` 接线。

### 1.6 数值坐标系（设计必须锚定的实测数字）

| 项 | 公式 / 数值 | 来源 |
|---|---|---|
| 创角属性 | `5d3 - 1`，下限 4 → **4..14，均值 9** | `CharacterCreationWindow.DiceCount / DiceSides / DiceOffset / DiceValueFloor` |
| 属性调整值 | `floor((score - 10) / 2)` → **-3..+2** | `AttributeSnapshot.CalculateScoreModifier` |
| 行动点 AP | `max(1, 1 + floor(agility / 10))` → **敏捷 10 是 AP 断点** | `AttributeService.BuildDefaultRules` |
| 行动阈值 | 由敏捷调整值查表，基准 40 TU，粒度 5 TU | `ActionCadenceContentRules` |
| AC | `8 + 受护甲上限限制的敏捷调整 + 持久 AC 分量` | `AttributeService.CalculateBaseArmorClass` |
| 体力上限 | `24 + 5×体质 + 力量 + 敏捷` | `BuildDefaultRules` |
| 初始 HP | `14 + 2×体质调整值` | `CharacterCreationService.CalculateInitialHpMax` |

三条必须记住的陷阱：

1. **敏捷是最贵的属性。** 它同时决定 AP（10 是断点）、AC、行动阈值和体力上限。给亚种 `agility +1` 不是"+1"，对一个骰出 9 点敏捷的角色是"行动点翻倍"。
2. **`base_attack_bonus` 和 `spell_proficiency_bonus` 改不动。** 属性快照在循环结束后用 `CalculateBaseAttackBonus()` / `CalculateSpellProficiencyBonus()` 直接覆写，identity 修正会被丢弃。
3. **AC 分量修正进不了 AC。** `armor_class` 用的是**持久** AC 分量（`ResolvePersistentAcComponentTotal`），identity / trait 写的 `dodge_bonus` 只改分量自身的快照值。若要给 AC，只能直接改 `armor_class`；本设计不使用 AC 通道。

### 1.7 人类现状

```json
{ "race_id": "human", "subrace_ids": ["common_human"],
  "attribute_modifiers": [], "trait_ids": ["human_versatility", "civil_militia"],
  "damage_resistances": {}, "save_advantage_tags": [] }
```

- `common_human` 是一个**所有字段全空**的亚种。
- 人类是 11 个种族里唯一"选亚种没有任何差别"的种族，创角 UI 的亚种下拉框对人类是禁用状态（`subrace_variant_button.Disabled = ItemCount <= 1`）。
- 人类唯一的真实机制是自选 +1 属性；`civil_militia` 带来的长柄武器 / 轻甲 / 盾熟练目前不产生任何规则影响（见 1.3）。
- 年龄：成年 20 / 中年 40 / 老年 60 / 耄耋 75 / 上限 90，是文明种族里偏短命的一档，但年龄阶段现在没有任何数值后果。

---

## 第二部分：人类种族与亚种设计

### 2.1 设计定位

人类不靠血统分化，靠**出身**分化。八个亚种对应大陆上八种人类生存方式，直接挂到仓库里已有的世界观资产（`docs/content/lore/orc_empire.md`、`dwarf_narrative.md`、`docs/content/world/ashen_intersection/`、`docs/content/faith/`）：

| 亚种 | 归属 | 一句话 |
|---|---|---|
| 自由民 | 无 | 没有出身红利，也没有出身包袱 |
| 霜烬遗民 | 霜烬帝国（北方） | 在高墙和寒冬里长大，纪律换来了教条 |
| 星陨市民 | 星陨联邦（南方） | 七族混居的城邦市民，见多识广但没见过血 |
| 边墙猎民 | 西部边墙 | 与血喉帝国摩擦三百年的猎户与哨兵 |
| 灰烬余生者 | 灰烬交界 | 出生在界火裂隙旁，身体被余烬改写过 |
| 灰炉炉裔 | 灰炉城 | 与矮人混居三代的人类工匠 |
| 沉钟书徒 | 沉钟书库 | 在潮雾和禁书里泡大的抄书人 |
| 港巷子 | 沿海商路 | 码头长大的滑头（高风险项，见 2.5.8） |

### 2.2 设计原则

1. **零代码落地。** 每个亚种只用 1.5 节列出的通道。需要改代码的部分单列在 2.7 作为可选增强。
2. **保留 `common_human` 这个 id。** 现有存档的 `subrace_id` 都指向它，换 id 等于弃档。只重新定义它的内容。
3. **每个亚种都有弱点。** 没有纯增益亚种，否则玩家只会选最强的那个。
4. **不碰敏捷。** 基于 1.6 的陷阱 1，七个核心亚种没有任何一个给敏捷加值；唯一的敏捷亚种（港巷子）标为高风险待议项。
5. **属性修正不是"扣除"。** 亚种的 `-1` 走 `AttributeService` 的修正管线，不会写回 `unit_base_attributes`，与"六项基础属性不允许被技能扣减"的规则不冲突。这一点建议在实施前确认一次。

### 2.3 预算表（P 制）

每个亚种目标 **4P**，允许区间 `[3P, 5P]`。

| 机制 | 价格 |
|---|---|
| 基础属性 +1（力量 / 体质 / 感知 / 智力 / 意志） | 2P |
| 基础属性 +1（敏捷） | 3P |
| 基础属性 -1（非敏捷 / 敏捷） | -2P / -3P |
| 单 tag 豁免优势 | 2P |
| 单 tag 豁免劣势 | -2P |
| 单 tag 豁免免疫 | 4P（人类不给） |
| `save_bonus_entries` +1（某属性豁免） | 1P |
| `save_tag_bonus_entries` +1（某 tag 豁免） | 1P |
| 元素抗性 `half` | 3P |
| 元素抗性 `double`（弱点） | -3P |
| `hp_max` +2 | 1P |
| `stamina_max` +5 | 1P |
| 授予被动训练类技能（`at_will`） | 1P |
| 授予主动技能（`per_battle`，1 充能） | 3P |
| `action_points` / `action_threshold` / `armor_class` | **禁用** |

### 2.4 种族层（`human`）调整

种族层保持"零修正基线"，差异全部下放到亚种：

- `attribute_modifiers` 保持空数组。
- `trait_ids` 保持 `["human_versatility", "civil_militia"]`——自选 +1 是人类的签名，必须留在种族层，让所有亚种共享。
- `subrace_ids` 扩到 8 个，`default_subrace_id` 保持 `common_human`。
- `racial_trait_summary` 改写为"人类的差别来自出身"。

### 2.5 八个亚种详细设计

| 亚种 | 属性 | 抗性 | 豁免 | 授予技能 | 预算 |
|---|---|---|---|---|---|
| `common_human` 自由民 | — | — | 体质 / 意志豁免各 +1 | — | 1+1+1+1 = 4P |
| `frostash_human` 霜烬遗民 | 体质 +1 | 寒冷减半 | 恐惧豁免 +1；**魅惑劣势** | — | 2+3+1-2 = 4P |
| `starfall_human` 星陨市民 | 智力 +1 | — | 幻术优势；智力豁免 +1；**恐惧劣势** | — | 2+2+1-2+1 = 4P |
| `borderwall_human` 边墙猎民 | 感知 +1 | — | 恐惧优势；毒素豁免 +1；**魔法劣势** | `bow_training` | 2+2+1-2+1 = 4P |
| `ashenborn_human` 灰烬余生者 | 意志 +1 | 火焰减半 / **寒冷双倍** | 魔法优势 | — | 2+3-3+2 = 4P |
| `grayforge_human` 灰炉炉裔 | 力量 +1 | — | 石化优势；体质豁免 +1；**幻术劣势** | — | 2+2+1-2+1 = 4P |
| `belltower_human` 沉钟书徒 | 智力 +1 / **体质 -1** | — | 意志优势；魔法豁免 +1 | `basic_meditation` | 2-2+2+1+1 = 4P |
| `dockside_human` 港巷子 | 敏捷 +1 / **力量 -1** | — | 敏捷豁免 +1 | `unarmed_training` | 3-2+1+1 = 3P ⚠️ |

#### 2.5.1 自由民 `common_human`（默认）

**定位**：农庄、小镇、行会学徒——大陆上最多的那种人。卖点是**没有任何劣势**，配合种族层的自选 +1，任何职业都能开局。

**机制**：`hp_max +2`、`stamina_max +5`、体质豁免 +1、意志豁免 +1（后三项通过新 trait `human_freeman_grit`）。

**为什么这样配**：四项都是"薄但全面"的补丁，刻意不给任何尖峰。它是新玩家和还没想好玩法的玩家的安全选项，也是老存档角色平滑落地的位置——现有角色升级到新内容后会白得这一包，属于轻微加强而不是重做。

#### 2.5.2 霜烬遗民 `frostash_human`

**定位**：北方高墙下长大的平民与退役军团兵。霜烬帝国的"净化"最初是防御性的（见 `orc_empire.md` 1.3），在这里长大的人从小背军团条令。

**机制**：体质 +1；寒冷伤害减半；恐惧豁免 +1；**魅惑豁免劣势**。

**为什么这样配**：体质 +1 同时吃 `hp_max`（调整值跨档时 +2）和体力上限（+5）两份收益，是坦克向最扎实的一点。寒冷减半在北境和灰烬交界的冰系敌人面前是硬通货。魅惑劣势是这个亚种的设计核心：他们不是意志薄弱，而是被训练成"服从正确的声音"——谁扮演了那个声音，他们就跟谁走。

#### 2.5.3 星陨市民 `starfall_human`

**定位**：南方城邦的市民、书记官、商会子弟。七族广场上长大，见过精灵的把戏也见过矮人的账本。

**机制**：智力 +1；幻术豁免优势；智力豁免 +1；**恐惧豁免劣势**。

**为什么这样配**：幻术优势（豁免两次取好）是很强的定向防御，对应"骗子见得多了"。恐惧劣势对应城邦市民没上过战场——能识破幻象，但真被兽人冲锋吓到时腿是软的。与霜烬遗民构成镜像：一个抗恐惧怕魅惑，一个抗幻术怕恐惧。

#### 2.5.4 边墙猎民 `borderwall_human`

**定位**：西部边墙的猎户、哨塔守夜人、走私贩。血喉帝国每三年一次的劫掠就是他们的童年记忆。

**机制**：感知 +1；恐惧豁免优势；毒素豁免 +1；**魔法豁免劣势**；授予 `bow_training`（`at_will`，1 级）。

**为什么这样配**：感知 +1 服务弓手 / 斥候路线。恐惧优势对应"见惯了兽人冲锋"。毒素 +1 对应边境毒箭。魔法劣势是代价：边墙没有法术教育，面对法系敌人豁免天然吃亏。`bow_training` 是已存在的被动熟练技能（`learn_source: innate`），种族授予它完全符合现有语义。

#### 2.5.5 灰烬余生者 `ashenborn_human`

**定位**：出生在灰烬交界界火裂隙附近的人。余烬改写了他们的体质——火焰对他们很温柔，寒冷对他们很残忍。

**机制**：意志 +1；火焰伤害减半；**寒冷伤害双倍**；魔法豁免优势。

**为什么这样配**：八个亚种里波动最大的一个，也是唯一带 `double` 弱点的。火抗 + 魔法优势让它在灰烬交界地图和法系遭遇里极强；寒冷双倍让它在北境基本不能用。这是"给玩家一个真正需要看地图再选的亚种"，与霜烬遗民（抗寒）直接对位。

**风险提示**：`double` 是最重的惩罚档，上线前应在含冰系敌人的遭遇里实测一轮，确认是"有代价"而不是"不可玩"。

#### 2.5.6 灰炉炉裔 `grayforge_human`

**定位**：灰炉城的人类工匠家系，三代与矮人混居，学了摩拉丹的规矩但没有矮人的血。

**机制**：力量 +1；石化豁免优势；体质豁免 +1；**幻术豁免劣势**。

**为什么这样配**：力量 +1 服务近战与负重。石化优势是刻意的 niche——仓库里已经有石化内容（`weapon.polearm.rock_halberd`、`weapon.crossbow.gorgon`），石工世家抗石化在设定上顺理成章，遇到那类敌人时能救命。幻术劣势对应实证主义者的盲区：他们相信手能摸到的东西，所以最容易被骗。

#### 2.5.7 沉钟书徒 `belltower_human`

**定位**：东北沉钟书库的抄书人与学徒。潮雾、禁书、钟声，身体泡坏了，脑子和意志磨出来了。

**机制**：智力 +1；**体质 -1**；意志豁免优势；魔法豁免 +1；授予 `basic_meditation`（`at_will`，1 级）。

**为什么这样配**：唯一带属性惩罚的核心亚种，换来法系最想要的两样东西——智力和意志豁免优势。体质 -1 是真疼：HP 和体力上限都掉。`basic_meditation` 的授予是身份标签（书库出身天生会冥想），而且它是可成长的被动，实际收益取决于玩家后续投入，比直给属性更健康。

**注意**：`basic_meditation` 当前是 `learn_source: "player"`。种族授予会绕过正常学习路径，实施前需确认这是否与冥想系统的设计意图冲突；若冲突，改为不授予技能，把这 1P 换成 `mp_max +3`。

#### 2.5.8 港巷子 `dockside_human`（⚠️ 高风险待议项）

**定位**：商路港口的码头孩子、扒手、船帮跑腿。

**机制**：敏捷 +1；**力量 -1**；敏捷豁免 +1；授予 `unarmed_training`（`at_will`，1 级）。

**为什么标高风险**：见 1.6 陷阱 1。敏捷 +1 在 9→10 这个点上等于 AP 从 1 变 2，是行动经济翻倍，远超 3P 的定价。三个处理方式：

1. **不上线**，盗贼向玩家用种族层的自选 +1 去点敏捷（效果相同，但代价是放弃补短板）。
2. **上线但改 AP 公式**，把断点从 `agility / 10` 换成更平滑的曲线——这属于战斗节奏层面的改动，不是种族设计。
3. **上线并接受**，把力量 -1 加重到 -2，或再加一条豁免劣势。

推荐按方案 1 处理，把 `dockside_human` 留在本文档里作为待议项，不进第一批内容。

### 2.6 需要新建的 trait

`save_bonus_entries` 和 `save_tag_bonus_entries` 只能写在 trait 上，所以每个亚种需要一个承载 trait。全部用 `trigger_type: "passive"`（唯三的非 passive dispatch 已被半身人 / 半兽人占满），`effect_type` 复用现有 enum 值以避免改代码：

| trait_id | 归属亚种 | 复用的 effect_type | 承载内容 |
|---|---|---|---|
| `human_freeman_grit` 自由民韧劲 | common_human | `attribute_modifier` | `hp_max +2`、`stamina_max +5`、体质 / 意志豁免 +1 |
| `frostash_discipline` 霜烬军纪 | frostash_human | `save_advantage` | 恐惧 tag 豁免 +1 |
| `starfall_cosmopolitan` 七族通识 | starfall_human | `save_advantage` | 智力豁免 +1 |
| `borderwall_vigil` 边墙哨戒 | borderwall_human | `save_advantage` | 毒素 tag 豁免 +1 |
| `grayforge_stonehand` 炉边石手 | grayforge_human | `save_advantage` | 体质豁免 +1 |
| `belltower_scholarship` 钟塔学识 | belltower_human | `save_advantage` | 魔法 tag 豁免 +1 |
| `dockside_footwork` 码头步法 | dockside_human | `save_advantage` | 敏捷豁免 +1 |

灰烬余生者不需要新 trait（机制全在 identity 字段上）。

> **首次内容化提醒**：`save_tag_bonus_entries` 在全仓库 239 个 trait 里**一次都没被用过**（实现与回归都在，内容是空的）。第一批内容落地后必须跑 `tests/progression/schema/run_trait_save_tag_bonus_schema_regression.cs` 和 `tests/battle_runtime/runtime/run_trait_save_tag_bonus_regression.cs`。

### 2.7 可选增强（需要改代码，不在第一批）

按性价比排序：

1. **人类年龄阶段数值化**（改内容，可能改 UI）。人类是偏短命的文明种族，但年龄现在没有任何后果。给 `human_age_profile` 的 `stage_rules` 补 `middle_age` / `old_age` 两级，中年"智力 / 意志 +1、力量 / 敏捷 -1"，老年再推一档。11 份 age_profile 的 stage_rules 现在全是空的，人类可以当第一个样板。
2. **`civil_militia` / `proficiency_tags` 接线**（改代码）。让"不熟练的武器有命中惩罚"真正成立，`civil_militia` 和边墙猎民的 `bow_training` 才有意义。当前 `HasProficiencyTag` 在生产代码零调用。
3. **新增 trigger 时机**（改代码）。例如 `on_first_hit_taken`，可以做"霜烬遗民每场战斗第一次被击中时获得 1 层减伤"这类真正有质感的种族特性。现在三个 trigger 位置已被占满。
4. **`base_speed` 接线**（改代码）。让移动点受种族影响，`base_speed` / `speed_bonus` 才不是死字段。

### 2.8 落地清单

1. `data/configs/json/traits/traits.json` 追加 7 个 trait（见 2.6）。
2. `data/configs/json/subraces/content.json` 追加 7 个亚种 + 重写 `common_human`。
3. `data/configs/json/races/content.json` 的 `human` 条目扩 `subrace_ids`、改 `racial_trait_summary`。
4. 跑内容校验与相关回归：
   - `tests/progression/identity/run_trait_content_registry_regression.cs`
   - `tests/progression/schema/run_trait_json_content_regression.cs`
   - `tests/progression/schema/run_trait_save_tag_bonus_schema_regression.cs`
   - `tests/battle_runtime/runtime/run_trait_save_tag_bonus_regression.cs`
   - 创角链路相关 runner
5. 存档影响：只新增内容 id，不改 `SaveVersion`；老角色的 `subrace_id = common_human` 继续有效，只是内容变强了一点。若"老角色白得一包"不可接受，需要单独讨论。

---

## 附录 A：可直接粘贴的 JSON

### A.1 新增 trait（追加到 `traits.json` 的 `entries`）

```json
{
  "trait_id": "human_freeman_grit",
  "display_name": "自由民韧劲",
  "description": "在农庄和作坊里长出来的底子：耐得住活，扛得住事。",
  "categories": ["human_subrace"],
  "allowed_source_kinds": ["identity"],
  "effect_type": "attribute_modifier",
  "trigger_type": "passive",
  "stack_policy": "unique_by_trait",
  "charge_scope": "none",
  "charge_reset_timing": "none",
  "highest_roll_compare_key": "",
  "vision_range": 0,
  "proficiency_choice_count": 0,
  "attribute_modifiers": [
    { "attribute_id": "hp_max", "mode": "flat", "value": 2, "value_per_rank": 0, "source_type": "identity", "source_id": "human_freeman_grit" },
    { "attribute_id": "stamina_max", "mode": "flat", "value": 5, "value_per_rank": 0, "source_type": "identity", "source_id": "human_freeman_grit" }
  ],
  "save_advantage_tags": [],
  "save_disadvantage_tags": [],
  "save_immunity_tags": [],
  "damage_resistance_entries": [],
  "save_bonus_entries": [
    { "save_ability": "constitution", "bonus": 1 },
    { "save_ability": "willpower", "bonus": 1 }
  ],
  "save_tag_bonus_entries": [],
  "passive_status_effects": [],
  "roll_value_schema": []
}
```

```json
{
  "trait_id": "frostash_discipline",
  "display_name": "霜烬军纪",
  "description": "从识字起就背诵的军团条令：队列不散，声音不抖。",
  "categories": ["human_subrace"],
  "allowed_source_kinds": ["identity"],
  "effect_type": "save_advantage",
  "trigger_type": "passive",
  "stack_policy": "unique_by_trait",
  "charge_scope": "none",
  "charge_reset_timing": "none",
  "highest_roll_compare_key": "",
  "vision_range": 0,
  "proficiency_choice_count": 0,
  "attribute_modifiers": [],
  "save_advantage_tags": [],
  "save_disadvantage_tags": [],
  "save_immunity_tags": [],
  "damage_resistance_entries": [],
  "save_bonus_entries": [],
  "save_tag_bonus_entries": [
    { "save_tag": "frightened", "bonus": 1, "stack_mode": "add" }
  ],
  "passive_status_effects": [],
  "roll_value_schema": []
}
```

其余五个 trait 同构，只换 `trait_id` / `display_name` / `description` 和最后的加值条目：

| trait_id | 加值条目 |
|---|---|
| `starfall_cosmopolitan` | `save_bonus_entries: [{ "save_ability": "intelligence", "bonus": 1 }]` |
| `borderwall_vigil` | `save_tag_bonus_entries: [{ "save_tag": "poison", "bonus": 1, "stack_mode": "add" }]` |
| `grayforge_stonehand` | `save_bonus_entries: [{ "save_ability": "constitution", "bonus": 1 }]` |
| `belltower_scholarship` | `save_tag_bonus_entries: [{ "save_tag": "magic", "bonus": 1, "stack_mode": "add" }]` |
| `dockside_footwork` | `save_bonus_entries: [{ "save_ability": "agility", "bonus": 1 }]` |

### A.2 亚种（追加 / 覆盖到 `subraces/content.json` 的 `entries`）

```json
{
  "subrace_id": "common_human",
  "parent_race_id": "human",
  "display_name": "自由民",
  "description": "农庄、小镇和行会学徒出身的普通人。没有出身带来的红利，也没有出身带来的包袱。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [],
  "trait_ids": ["human_freeman_grit"],
  "racial_granted_skills": [],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": [],
  "save_disadvantage_tags": [],
  "save_immunity_tags": [],
  "damage_resistances": {},
  "dialogue_tags": ["human", "common_human"],
  "racial_trait_summary": [
    "生命上限 +2，体力上限 +5。",
    "体质与意志豁免各 +1。",
    "没有任何出身劣势。"
  ]
}
```

```json
{
  "subrace_id": "frostash_human",
  "parent_race_id": "human",
  "display_name": "霜烬遗民",
  "description": "北方高墙下长大的平民与退役军团兵。寒冬和条令一起塑造了他们——也一起限制了他们。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "constitution", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "frostash_human" }
  ],
  "trait_ids": ["frostash_discipline"],
  "racial_granted_skills": [],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": [],
  "save_disadvantage_tags": ["charm"],
  "save_immunity_tags": [],
  "damage_resistances": { "freeze": "half" },
  "dialogue_tags": ["human", "frostash_human", "frostash_empire"],
  "racial_trait_summary": [
    "体质 +1。",
    "寒冷伤害减半。",
    "对恐惧的豁免 +1。",
    "对魅惑的豁免为劣势。"
  ]
}
```

```json
{
  "subrace_id": "starfall_human",
  "parent_race_id": "human",
  "display_name": "星陨市民",
  "description": "南方城邦的市民、书记官与商会子弟。在七族广场上长大，识得破把戏，见不得血。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "intelligence", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "starfall_human" }
  ],
  "trait_ids": ["starfall_cosmopolitan"],
  "racial_granted_skills": [],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": ["illusion"],
  "save_disadvantage_tags": ["frightened"],
  "save_immunity_tags": [],
  "damage_resistances": {},
  "dialogue_tags": ["human", "starfall_human", "starfall_federation"],
  "racial_trait_summary": [
    "智力 +1。",
    "对幻术的豁免为优势。",
    "智力豁免 +1。",
    "对恐惧的豁免为劣势。"
  ]
}
```

```json
{
  "subrace_id": "borderwall_human",
  "parent_race_id": "human",
  "display_name": "边墙猎民",
  "description": "西部边墙的猎户、哨塔守夜人与走私贩。三百年的劫掠教会了他们两件事：先看见，先射出去。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "perception", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "borderwall_human" }
  ],
  "trait_ids": ["borderwall_vigil"],
  "racial_granted_skills": [
    { "skill_id": "bow_training", "minimum_skill_level": 1, "charge_kind": "at_will", "charges": 1 }
  ],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": ["frightened"],
  "save_disadvantage_tags": ["magic"],
  "save_immunity_tags": [],
  "damage_resistances": {},
  "dialogue_tags": ["human", "borderwall_human", "borderwall"],
  "racial_trait_summary": [
    "感知 +1。",
    "对恐惧的豁免为优势。",
    "对毒素的豁免 +1。",
    "对魔法的豁免为劣势。",
    "天生掌握弓术训练。"
  ]
}
```

```json
{
  "subrace_id": "ashenborn_human",
  "parent_race_id": "human",
  "display_name": "灰烬余生者",
  "description": "出生在界火裂隙旁的人。余烬改写了他们的身体——火焰对他们温柔，寒冷对他们残忍。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "willpower", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "ashenborn_human" }
  ],
  "trait_ids": [],
  "racial_granted_skills": [],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": ["magic"],
  "save_disadvantage_tags": [],
  "save_immunity_tags": [],
  "damage_resistances": { "fire": "half", "freeze": "double" },
  "dialogue_tags": ["human", "ashenborn_human", "ashen_intersection"],
  "racial_trait_summary": [
    "意志 +1。",
    "火焰伤害减半。",
    "寒冷伤害翻倍。",
    "对魔法的豁免为优势。"
  ]
}
```

```json
{
  "subrace_id": "grayforge_human",
  "parent_race_id": "human",
  "display_name": "灰炉炉裔",
  "description": "灰炉城的人类工匠家系，三代与矮人混居。学了摩拉丹的规矩，却没有矮人的血。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "strength", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "grayforge_human" }
  ],
  "trait_ids": ["grayforge_stonehand"],
  "racial_granted_skills": [],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": ["petrification"],
  "save_disadvantage_tags": ["illusion"],
  "save_immunity_tags": [],
  "damage_resistances": {},
  "dialogue_tags": ["human", "grayforge_human", "grayforge"],
  "racial_trait_summary": [
    "力量 +1。",
    "对石化的豁免为优势。",
    "体质豁免 +1。",
    "对幻术的豁免为劣势。"
  ]
}
```

```json
{
  "subrace_id": "belltower_human",
  "parent_race_id": "human",
  "display_name": "沉钟书徒",
  "description": "沉钟书库的抄书人与学徒。潮雾泡坏了身体，禁书和钟声磨出了意志。",
  "body_size_category_override": "",
  "speed_bonus": 0,
  "attribute_modifiers": [
    { "attribute_id": "intelligence", "mode": "flat", "value": 1, "value_per_rank": 0, "source_type": "subrace", "source_id": "belltower_human" },
    { "attribute_id": "constitution", "mode": "flat", "value": -1, "value_per_rank": 0, "source_type": "subrace", "source_id": "belltower_human" }
  ],
  "trait_ids": ["belltower_scholarship"],
  "racial_granted_skills": [
    { "skill_id": "basic_meditation", "minimum_skill_level": 1, "charge_kind": "at_will", "charges": 1 }
  ],
  "proficiency_tags": [],
  "vision_tags": [],
  "save_advantage_tags": ["willpower"],
  "save_disadvantage_tags": [],
  "save_immunity_tags": [],
  "damage_resistances": {},
  "dialogue_tags": ["human", "belltower_human", "belltower_library"],
  "racial_trait_summary": [
    "智力 +1，体质 -1。",
    "意志豁免为优势。",
    "对魔法的豁免 +1。",
    "天生掌握基础冥想法。"
  ]
}
```

`dockside_human` 的 JSON 在决定是否上线前不写入内容目录，结构与上面同构：`agility +1` / `strength -1` + `dockside_footwork` + `unarmed_training`。

### A.3 `human` 种族条目改动

```json
{
  "race_id": "human",
  "default_subrace_id": "common_human",
  "subrace_ids": [
    "common_human",
    "frostash_human",
    "starfall_human",
    "borderwall_human",
    "ashenborn_human",
    "grayforge_human",
    "belltower_human"
  ],
  "racial_trait_summary": [
    "人类多才：创角时自选一项基础属性 +1。",
    "公民民兵训练。",
    "人类的差别来自出身，不来自血统——具体特性见亚种。"
  ]
}
```

（其余字段保持现状：`attribute_modifiers` 空、`trait_ids` 为 `["human_versatility", "civil_militia"]`、`base_speed` 6、`body_size_category` medium。）
