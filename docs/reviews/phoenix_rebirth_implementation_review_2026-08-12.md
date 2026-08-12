# 凤凰重生套装实现审查说明书

> 审查日期：2026-08-12
>
> 项目：Godot 4.6 / C#
>
> 审查对象：当前工作树中的 `phoenix_rebirth_set` 完整实现
>
> 文档性质：交给独立审查者的 point-in-time 审查任务书，不是实现真相本身
>
> 审查方式：findings-first、只读、以正式运行时行为和可杀回归为证据

## 1. 可直接交给 Claude 的任务描述

```text
请对当前工作树中的“凤凰重生 / phoenix_rebirth_set”完整实现做一次只读、findings-first 的端到端代码审查。

不要修改文件，不要把测试 PASS 数量当作实现正确的证据，也不要只检查 .tres 文本。你必须沿正式 Resource -> Definition -> validator -> projection -> runtime owner -> preview -> AI -> persistence/presentation 调用链核实每项声明，并逐个检查关键测试的真实 Act、正式入口、可观测 oracle、失败分支和 killability。

当前工作树高度混合，凤凰实现的大量文件是 untracked。审查对象是“当前检出内容”，不是 HEAD。请先记录 branch、HEAD、git status，再按本说明书列出的 Phoenix 文件和 owner 做 scoped review；不得因为 HEAD 中没有这些文件就判定功能未实现，也不得把无关脏改动归到 Phoenix。

输出必须先列 P0/P1/P2/P3 findings。每个 finding 必须包含：精确 path:line、当前行为、设计合同、影响、可复现方式或会杀死它的最小测试、建议的最小 owner 修复。若没有 finding，也要逐项给出已核实证据，不能只写“测试通过”。

请额外输出：
1. 合同核对矩阵：Landed / Partial / Contradicted / Unverified；
2. 测试有效性矩阵：测试方法、正式 Act、oracle、失败分支、能杀死的退化；
3. 当前 checkout 验证命令及原始结果摘要；
4. 已知边界与新增缺陷分开列示；
5. 文档与当前代码不一致清单。
```

## 2. 审查前提与证据等级

### 2.1 当前检出状态

- 审查基线分支：`codex/tactical-skills-world-quest`。
- 记录本说明书时的 `HEAD`：`967a80de`。
- 工作树包含大量无关修改、删除和未跟踪文件；Phoenix 的资源、gear-set owner 和主要回归大多尚未跟踪。
- 审查者必须以当前文件内容为准，并在报告中把 `HEAD`、当前 checkout、局部 diff、实际运行结果分开表述。
- 不得清理、reset、checkout、覆盖或顺手修复共享工作树。

### 2.2 真相优先级

按以下顺序判断“是否已经落地”：

1. 正式 runtime owner 的真实行为；
2. Resource 到 immutable Definition 的投影和 validator；
3. 通过公共 runtime/facade/session 入口执行的行为回归；
4. `docs/design/` 中经过代码核实的当前实现说明；
5. `docs/content/` 中的作者设计与数值意图；
6. `.tres` 可加载、snapshot、resource validation PASS。

仅有第 5 或第 6 项不能证明功能落地。测试只有在真实进入正式 owner，并观察到会随实现退化而变化的结果时，才构成行为证据。

### 2.3 必须先读

- `AGENTS.md`
- `docs/design/project_context_units.md`
- `docs/design/progression/equipment_sets.md`
- `docs/design/battle/equipment_ability_runtime.md`
- `docs/design/battle/skill_runtime.md`
- `docs/content/equipment_sets/existing_set_bonus_redesign_35_67.md` 的第 53 节
- `docs/content/equipment_sets/sets_51_to_60.md`
- `docs/content/equipment_sets/sets_51_to_60_accessories.md`

## 3. 需要核实的正式设计合同

### 3.1 套装成员、计数和阈值

正式套装 ID 为 `phoenix_rebirth_set`，成员固定为十件：

| 槽位 | item_id |
|---|---|
| head | `armor_phoenix_rebirth_head` |
| body | `armor_phoenix_rebirth_body` |
| hands | `armor_phoenix_rebirth_hands` |
| feet | `armor_phoenix_rebirth_feet` |
| cloak | `acc_phoenix_rebirth_cloak` |
| necklace | `acc_phoenix_rebirth_necklace` |
| ring_1 | `acc_phoenix_rebirth_ring_1` |
| ring_2 | `acc_phoenix_rebirth_ring_2` |
| trinket | `acc_phoenix_rebirth_trinket` |
| badge | `acc_phoenix_rebirth_badge` |

必须核实：

- 只统计已装备、未损坏、属于成员清单的不同 `item_id`；重复副本不增加件数。
- 候选必须满足 `ItemDefinition.GetEquipmentSlotIdsTyped()` 的 entry slot，并且实际 occupied slots 必须与 `GetFinalOccupiedSlotIdsTyped(entrySlotId)` 完全一致。
- 套装定义不维护第二份 per-member 槽位表；槽位真相由 `ItemDefinition` / `EquipmentRules` 保证。
- 3、5、7、10 件阈值累计激活；降件后失效，战斗内重投影也必须更新。
- 10 件正好独占十个非武器槽。这是刻意保留的构筑机会成本，不应被审查者误改为少占槽；但应检查文档是否如实披露其与“十件最优混搭”的平衡基线。

### 3.2 阈值属性单一 owner 与 provenance

必须重点复查此前指出的 `attribute_modifiers` 空转/双通道问题：

- 3 件：寒冷（canonical tag 为 `freeze`）伤害减半、体质豁免 `+2`，通过阈值 trait 表达。该阈值用于补足凤凰套装的 freeze 短板，不重复单件已有的 fire half。
- 5 件：`hp_max +20`，现在应直接 authored 在 `GearSetThresholdDef.attribute_modifiers`；不再用一个只承载静态 HP 的冗余 trait。
- 7 件：火焰免疫及余烬形态，通过阈值 trait/装备能力表达。
- 10 件：太阳涅槃及凤凰蛋能力门禁，通过阈值 trait/装备能力表达。

核实以下保障不是只写在文档中：

- `GearSetDefinition.FromResource` 必须强制覆盖直接阈值属性的来源为 `source_type=gear_set`、`source_id=gear_set::<set_id>::<threshold_id>`，不能信任作者手写 provenance。
- 同一阈值的直接 `attribute_modifiers` 不能重复声明相同 `attribute_id`。
- 同一阈值的直接属性不能与其授予 trait 重复声明同一 `attribute_id`。
- Phoenix 5 件阈值的 `granted_trait_ids` 应为空；旧的 `gear_set_phoenix_rebirth_5_undying_heartfire` 资源不应仍作为正式内容注册。

### 3.3 四档套装效果

| 件数 | 正式合同 |
|---|---|
| 3 | `freeze` 伤害减半；体质豁免 `+2`。 |
| 5 | `hp_max +20`。 |
| 7 | 常驻火焰免疫。每场战斗第一次在完整伤害/致死仲裁后仍存活且 HP 首次降至最大生命 25% 或以下时，恢复 `2D8 HP`，获得 `120 TU` 余烬形态：有效移动点容量 `+1`，qualifying 武器/徒手/spell attack 的主直接段附加 `1D4 fire`。 |
| 10 | HP 不高于 50% 时可用，`3 AP`、每世界日一次：自身恢复至至少 50% 最大生命；半径 3 格敌人受 `4D10 fire`，敏捷 DC17 成功减半；半径 3 格内按 HP% 最低选最多 4 名其他友军恢复 `2D8`；获得 `240 TU` 金焰形态，移动点容量 `+2`，qualifying 主直接段附加 `1D10 fire`。 |

必须检查 7 件的 `fire immune` 是否真正覆盖头冠、披风、灰烬之戒等单件提供的 `fire half`，而不是叠出错误倍率；降回 6 件时应恢复仍在装备来源中的 half。3 件阈值是独立的 `freeze half`，不参与这条 fire 升级链。

### 3.4 十件单件效果

| 单件 | 必须核实的行为 |
|---|---|
| 凤凰涅槃头冠 | 基础 `AC +2`、火焰伤害减半。普通致死时每场一次尝试，D100 `<=25` 成功：恢复到最大生命 25%，再对半径 3 格敌人造成 `3D10 fire`；范围内没有敌人仍应复活并消费尝试。不得拦截律令死亡。头冠的 fire half 与 7 件 fire immune 是升级关系，不把特殊效果再做一份免疫。 |
| 凤凰涅槃板甲 | 基础 `AC +6`、`hp_max +20`。外部、非自身、非装备生成且 raw fire > 0 的伤害使其获得/刷新 `60 TU` 燃烧层，最多 3 层，每层 `AC +1`。每日一次 `2 AP` 火盾消耗全部层数，获得 `60 TU AC +3`；期间敌方成功近战命中时反击 `2D6 fire`，每次命中只结算一次。 |
| 凤凰涅槃护手 | 基础 `AC +1`、攻击检定 `+1`。主手为空时 `1 AP` 火焰打击，正式走徒手攻击：`1D8 blunt + 1D10 fire`，暴击时两段骰分别翻倍。另有每日 3 次、`1 AP`、只允许相邻其他存活友方的 `2D10` 治疗。这里的“主手为空”明确表示不能装备主手武器；副手不应被误当主手。 |
| 凤凰涅槃胫甲 | 基础 `AC +1`。普通移动每个离开格留下 `60 TU`、任意阵营进入时受 `1D6 fire` 的足迹；同一单位每次移动命令只受同一片轨迹一次。每日一次 `2 AP` 正交直线冲锋，最大距离为有效移动点容量的 3 倍，离开格足迹改为 `2D6`，不与 `1D6` 重复。危险格必须有正式 overlay source，即使暂无专用贴图也要 generated fallback 可见。 |
| 凤凰涅槃披风 | 火焰减半、体质豁免 `+2`。每场第一次外部/非自身/非装备伤害使 HP 从 >50% 跨到 <=50% 时，对半径 1 格敌人 `2D6 fire`；空范围也消费。普通致死每场一次 D100 `<=25`，成功恢复 `1D12 HP`；致死恢复后的阈值跨越仍可触发披风爆发。不得拦截律令死亡。 |
| 凤凰涅槃项链 | 每日一次、`1 AP`，自身或相邻存活友方恢复 `3D8`，并移除最多 1 个 canonical 可驱散有害魔法。当前系统没有 disease/curse/legendary 的 typed taxonomy，不能把“非传奇疾病/诅咒”宣称为精确落地。 |
| 重生之戒 | `hp_max +10`。HP <=50% 时 `2 AP`，每场一次，恢复“缺失生命的 60%”；不能复活，受治疗削减影响，使用后戒指实例、耐久和 traits 不得被销毁或重铸。 |
| 灰烬之戒 | 火焰减半、体质豁免 `+1`。每日一次共享入口，`1 AP`：自身/相邻存活友军恢复 `2D10`，或相邻敌人承受 `3D10 fire`；两个分支共享同一个每日账本。 |
| 凤凰蛋 | 基础 `mp_max +15`。只有完整 10 件套 trait 存在时才投影。每世界月一次，世界月固定 `450 world steps`；致死时恢复最大生命 30%，获得 `120 TU` 火焰化身（fire immune、qualifying 主直接段附加 `1D10 fire`），再对半径 2 格敌人造成 `3D10 fire`。它是当前唯一允许拦截律令死亡的 Phoenix 来源。月度账本归凤凰蛋实例，不归阈值 trait。 |
| 凤凰徽章 | 体质豁免 `+1`。持有者与半径 2 格存活友军获得动态 fire half 光环；来源死亡、卸下、出圈或敌对阵营时即时失效。每日一次 `2 AP` 给半径 2 格存活友军 `120 TU` 凤凰祝福；每个受益者每场一次普通致死恢复 `1D10`，并获得 qualifying 主直接段 `+1D6 fire`。 |

### 3.5 致死仲裁和律令死亡

普通致死候选正式顺序：

1. fatal trait；
2. Death Ward / Last Stand；
3. 凤凰祝福 `order=100`；
4. 烈焰披风 `order=300`；
5. 凤凰头冠 `order=400`；
6. 凤凰蛋 `order=500`；
7. `MarkDead`。

保护优先级：祝福/披风/头冠为 `100`，凤凰蛋为 `900`。`phantasmal_kill_execute` 的 death-source priority 为 `300`，因此会在消耗 usage 前跳过保护优先级 `100` 的 Phoenix 候选，但仍可被凤凰蛋拦截。律令死亡的 death-source priority 为 `900`，所以它必须在消耗 usage 前跳过普通 fatal trait、Death Ward / Last Stand、祝福、披风和头冠；只有完整 10 件套时存在、且月度次数可用的凤凰蛋能够拦截。

请验证：概率候选、consume-on-attempt / consume-on-success、连续多段致死、saving throw 成败分支、致死成功后的 finalized reaction、预览不消费 RNG/usage，以及正式 Issue 的结果相互一致。

### 3.6 凤凰附伤互斥

以下来源共享 `phoenix_attack_append` replacement group，只取当前最高优先级，不相加：

| 来源 | 优先级 | 附伤 |
|---|---:|---:|
| 7 件余烬形态 | 100 | `1D4 fire` |
| 凤凰祝福 | 200 | `1D6 fire` |
| 凤凰蛋火焰化身 | 300 | `1D10 fire` |
| 10 件金焰形态 | 400 | `1D10 fire` |

只对 qualifying 武器、徒手或 spell attack 的主直接段生效。爆发、反伤、足迹和装备能力生成伤害不得递归触发，也不得重复投骰。应在掷骰前仲裁 winner，并验证最高来源失效后会回落到次高来源。

### 3.7 Preview 与 AI

必须核实：

- 装备技能的正式 preview 通过 canonical preview owner，不污染 canonical `BattleState`、world step、单位状态、格子、usage 或 RNG 队列。
- 致死预览使用 detached branch frontier，连续 damage effects 会延续分支内 usage/status/ability-state，而不是每段重新获得一次拦截。
- 概率致死成功后的 finalized Phoenix 动作按条件概率暴露，公开结果为 conditional / applied=false；detached 成功分支内的确定性 state action 会推进，供后续伤害段消费。
- saving throw 分支分别计算 fatal surface；不能用期望伤害替代分支致死。
- AI 从装备 binding/item catalog 取得 entry，由 `BattleAiSkillAffordanceClassifier` 按每个 effect 的 canonical target filter 聚合 mixed hostile/support affordance。
- 灰烬之戒必须能生成正确的友方治疗和敌方伤害候选；太阳涅槃必须能生成 ground hostile action；候选必须过 canonical preview 和 mutation guard。
- AI 的 lethal probability 必须进入最终 score 和候选排序，而不是只出现在 trace/breakdown 中。

### 3.8 新存档唯一获取链

必须核实完整事务，而不是仅检查资源 tag：

- 仅 `GameSession.CreateNewSave(...)` 的新存档初始化一次隐藏 `WorldUniqueEquipmentPoolState`。
- 从正式 gear set 的十个成员各 mint 一个传奇、稳定 `instance_id` 的原始实例，并真实进行 trait roll。
- 旧存档缺少该字段时不回填；玩家手改存档制造多个副本时允许载入，不扫描、不去重、不销毁。
- 商店刷新和 ordinary `random_equipment` 各以当前配置的 5% 机会从 reserve 转移原实例；不能重新 mint。
- 显式请求 Phoenix 成员而 reserve 中无该实例时 fail closed，且普通装备 generator 调用数必须为 0。
- 商店未售刷新退回 reserve；购买、出售、再次买回均保留同一 instance ID、耐久和完整 trait rolls。
- 仓库容量、窗口构建、settlement 写回、payload 持久化或 loot commit 失败时，pool/warehouse/gold/shop location 必须一起回滚。
- 十件带 `world_unique_equipment` tag；raw item-id mint、空 instance ID、force-new 路径 fail closed。
- 当前内部 typed transfer 仅以非空 `instance_id` 作为可信前置，没有池签发 token。这是已披露的受信 seam，不要误报为“用户输入可直接伪造”，除非能证明正式不受信入口可送入任意非空 ID。

### 3.9 展示与文档

核实至少以下可视/文档链：

- 角色/装备界面能显示套装归属、当前件数、已激活和下一阈值。
- 足迹的 `render_overlay_id` 从 terrain state 投影到 board snapshot，并被默认 render profile 解析为可见 overlay source。
- `docs/design/progression/equipment_sets.md` 只陈述已核实实现；仍未精确支持或仍有边界的内容不能写成完整落地。
- 旧 redesign 文档若仍含历史 2/4 件、24 小时、10 尺等内容，必须明确标成历史/已取代，不得与当前 3/5/7/10、格、TU、世界月合同混淆。

## 4. 关键实现文件地图

### 4.1 内容和投影

- `data/configs/gear_sets/phoenix_rebirth_set.tres`
- `data/configs/equipment_abilities/phoenix_rebirth_set_pack.tres`
- `data/configs/equipment_abilities/phoenix_rebirth_single_item_pack.tres`
- `data/configs/items/*phoenix_rebirth*.tres`
- `data/configs/skills/equipment_phoenix_rebirth_*.tres`
- `data/configs/skills/gear_set_phoenix_rebirth_solar_rebirth.tres`
- `data/configs/traits/*phoenix_rebirth*.tres`
- `scripts/player/progression/gear_sets/GearSetDefinition.cs`
- `scripts/player/progression/gear_sets/GearSetContentRegistry.cs`
- `scripts/player/progression/gear_sets/GearSetDef.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityBindingValidator.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityDefinitionProjection.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityPayloadValidators.cs`

### 4.2 套装计算和角色投影

- `scripts/systems/inventory/GearSetEvaluationService.cs`
- `scripts/systems/progression/CharacterTraitService.cs`
- `scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs`
- `scripts/systems/content/ContentSnapshotBuilder.cs`

重点检查 `GearSetEvaluationService.BuildValidEquippedItemIndex`、canonical placement 校验、阈值累计、anchor、稳定 source key，以及装备变更后的重新计算。

### 4.3 战斗能力、伤害、致死、预览和 AI

- `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
- `scripts/systems/battle/runtime/BattleEquipmentAbilityRuntimeService.cs`
- `scripts/systems/battle/runtime/EquipmentAbilityUsageRuntime.cs`
- `scripts/systems/battle/runtime/BattleDetachedPreviewState.cs`
- `scripts/systems/battle/runtime/BattleCommandPreviewService.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.Preview.cs`
- `scripts/systems/battle/rules/BattleFatalInterceptContracts.cs`
- `scripts/systems/battle/runtime/BattleEquipmentAttackModifierResolver.cs`
- `scripts/systems/battle/terrain/BattleTerrainEffectSystem.cs`
- `scripts/systems/battle/runtime/BattleChargeResolver.cs`
- `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
- `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`

若实际 owner 已移动，请在报告中记录新 owner，不要仅因本清单路径过期就停止审查。

### 4.4 唯一装备、商店、掉落和存档

- `scripts/systems/world/WorldUniqueEquipmentPoolState.cs`
- `scripts/systems/world/WorldRuntimeData.cs`
- `scripts/systems/world/WorldRuntimeSaveSchema.cs`
- `scripts/systems/persistence/GameSession.UniqueEquipment.cs`
- `scripts/systems/persistence/GameSession.CharacterCreation.cs`
- `scripts/systems/persistence/SaveSerializer.cs`
- `scripts/player/warehouse/WorldUniqueEquipmentContentRules.cs`
- `scripts/systems/inventory/PartyWarehouseService.cs`
- `scripts/systems/settlement/SettlementShopService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.SettlementCommandPort.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.BattleLootPort.cs`
- `scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs`

### 4.5 展示

- `scripts/ui/BattleBoardRenderProfile.cs`
- board snapshot / equipment window / character info 对 gear-set summary 的正式投影 owner
- `tests/battle_runtime/rendering/run_equipment_movement_trail_overlay_regression.cs`

## 5. 必须逐方法审查的回归

不要只运行文件。必须打开每个测试方法，记录它是否使用正式入口及其 oracle 能杀死什么退化。

| runner | 审查重点 |
|---|---|
| `tests/equipment/run_gear_set_evaluation_regression.cs` | 损坏/重复成员；错 entry slot 和错 footprint 不计数；3/5/7/10 累计；direct attribute provenance；直接属性与 trait 重复 owner 的真实 invalid fixture。 |
| `tests/progression/schema/run_phoenix_rebirth_single_item_content_regression.cs` | 十件逐项 item signature、slot/base price/基础属性/固定 traits、`world_unique_equipment`、能力包投影；不能只扫源文本。 |
| `tests/progression/schema/run_phoenix_rebirth_body_cloak_content_regression.cs` | 板甲/披风条件组、持续时间、层数、roll gate、fatal order/priority。 |
| `tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs` | 真实 full-set projection、7 件免疫、7/10件行为、律令死亡仅凤凰蛋、月度账本、附伤 winner 与回落、戒指实例保留。 |
| `tests/battle_runtime/runtime/run_phoenix_rebirth_single_item_behavior_regression.cs` | 头冠 25/26 边界及空范围；护手攻击 +1、1 AP、主手为空、分段暴击；治疗目标门禁和 3/4 次及次日；胫甲 6 格/7 格、日账本；项链最多移除一个；灰烬戒共享账本；重生戒缺失生命 60%。 |
| `tests/battle_runtime/runtime/run_phoenix_rebirth_body_cloak_behavior_regression.cs` | raw fire 即使 final=0 仍叠层；排除 self/equipment/nonfire；60 TU/3层/AC；火盾正式 grant、日账本和近战门禁；披风 preview 纯度、空范围消费、致死恢复跨阈值、conditional finalized。 |
| `tests/battle_runtime/runtime/run_equipment_ability_preview_integrity_regression.cs` | 多段致死 branch usage；consume-on-success 的 dead lane 重试；save 分支；detached state；RNG/usage/canonical mutation guard；最终 AI lethal score。 |
| `tests/battle_runtime/runtime/run_equipment_fatal_intercept_runtime_regression.cs` | 通用 fatal 顺序、priority、usage 语义以及正式 DamageResolver 接线。 |
| `tests/battle_runtime/runtime/run_equipment_bonus_damage_replacement_regression.cs` | 掷骰前 replacement 仲裁、稳定 tie、回落、非 qualifying/递归伤害排除。 |
| `tests/battle_runtime/rules/run_extra_damage_segment_critical_regression.cs` | extra segment 只在显式配置时随暴击加一组骰，dice bonus/power 不重复；默认 false 不改变其他技能。 |
| `tests/battle_runtime/runtime/run_equipment_mitigation_aura_regression.cs` | 动态 ally/self 范围、死亡/卸下/出圈、重复 half 不叠乘、tag/bypass。 |
| `tests/battle_runtime/runtime/run_equipment_movement_trail_regression.cs` | 正式 Move/charge 生成、离开格、any-team contact、每命令一次、2D6 替换 1D6、装备来源标记。 |
| `tests/battle_runtime/rendering/run_equipment_movement_trail_overlay_regression.cs` | snapshot -> source resolution -> 对应高度 Overlay TileMapLayer -> atlas/tile 的真实可见链。 |
| `tests/battle_runtime/ai/run_battle_ai_equipment_granted_skill_regression.cs` | 真实 Phoenix binding/实例日账本、mixed affordance、合法 command、canonical preview、mutation guard。 |
| `tests/equipment/run_phoenix_rebirth_unique_acquisition_regression.cs` | `CreateNewSave` 真池；正式 shop open/refresh/buy/sell/reload；失败回滚；真实 battle loot request -> facade commit；显式缺货 fail closed；raw warehouse guards。 |
| `tests/world_map/ui/run_party_management_window_regression.cs` | 使用正式 process snapshot 和真实五件 Phoenix 装备驱动 `PartyManagementWindow.ShowParty(...)`，检查战外装备页显示 `5/10`、五件阈值已激活、下一档七件阈值未激活；不得只调用 formatter。 |

## 6. 建议的聚焦验证命令

在项目根目录顺序运行，避免共享 `user://` 状态导致并发假失败：

```powershell
dotnet build magic.csproj --nologo

godot --headless -s res://tests/equipment/run_gear_set_evaluation_regression.cs
godot --headless -s res://tests/progression/schema/run_phoenix_rebirth_single_item_content_regression.cs
godot --headless -s res://tests/progression/schema/run_phoenix_rebirth_body_cloak_content_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_phoenix_rebirth_single_item_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_phoenix_rebirth_body_cloak_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_equipment_ability_preview_integrity_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_equipment_fatal_intercept_runtime_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_equipment_bonus_damage_replacement_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_equipment_mitigation_aura_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_equipment_movement_trail_regression.cs
godot --headless -s res://tests/battle_runtime/rendering/run_equipment_movement_trail_overlay_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_equipment_granted_skill_regression.cs
godot --headless -s res://tests/equipment/run_phoenix_rebirth_unique_acquisition_regression.cs
godot --headless -s res://tests/world_map/ui/run_party_management_window_regression.cs
godot --headless -s res://tests/runtime/validation/run_process_content_host_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

这些命令是聚焦回归，不包含 routine full suite，也不包含数值 battle simulation。审查者若需要运行更大范围测试，应单独报告范围、耗时和与当前脏工作树的关系。

## 7. 本次实现方最近一次验证快照

以下是生成本说明书前实施方完成的局部验证，不替代独立复跑和方法级审查：

- `dotnet build magic.csproj --nologo`：0 warnings / 0 errors。
- gear-set evaluation：PASS。
- Phoenix set runtime：PASS。
- Phoenix single-item behavior：PASS。
- Phoenix body/cloak behavior：PASS。
- Phoenix single-item content：PASS。
- Phoenix body/cloak content：PASS。
- equipment preview integrity：PASS。
- fatal intercept、replacement、aura、movement trail、trail rendering、equipment AI：聚焦 PASS。
- unique acquisition E2E：PASS。
- party management equipment-tab gear-set presentation：PASS，lifecycle failures=0。
- process content host：PASS。
- resource validation：official content errors=0，runner PASS。
- 未运行 routine full regression suite。
- 未运行 numeric battle simulation，因此没有“10 件 capstone 已通过十件最优混搭数值平衡”的证据。

## 8. 已知边界：不要隐藏，也不要无证据升级

以下是当前已披露边界。审查者应核实边界描述是否准确；若能证明其影响 Phoenix 正式路径，可重新定级为缺陷。

1. 项链目前只精确支持“移除最多一个 canonical 可驱散有害魔法”，没有 disease/curse/legendary typed taxonomy。
2. detached preview 只复制当前正式需要的 BattleState 子集，不保证 objective、barrier、target marks、temporary edge features 等所有未来状态。
3. finalized reaction 的本地概率 stateful action 目前只提供条件摘要，不继续分裂状态；当前 Phoenix 会影响后续状态的 finalized 动作为本地 100%，所以 Phoenix 路径可精确续接。
4. fatal preview branch frontier 当前没有硬上限；这属于复杂内容扩展时的性能/拒绝服务风险，本套现有候选规模尚未证明触发实际问题。
5. granted-skill preview 中的 `source_preview_after` 表示装备 reaction projection，不是“技能自身效果 + reaction”的完整合并 post-state。
6. world-unique typed instance transfer 信任内部调用传入的非空 instance ID，没有独立 pool-issued provenance token。
7. full set 占满十个非武器槽；已在内容设计中加入混搭机会成本基线，但尚未做数值模拟来证明其强度一定超过十件最优混搭。

“已知边界”不是自动免除项。若审查者能给出当前正式内容可达的错误路径、可杀用例或未受信入口，应按实际严重度报告。

## 9. 特别容易出现的误判

- 把 3 件阈值误读成 fire half：3 件正式效果是 `freeze half`；fire half 来自头冠、披风和灰烬之戒等单件，才会在 7 件时升级为 fire immune，并在降回 6 件时回落。
- 把头冠、披风、祝福、凤凰蛋视为重复复活：它们有不同来源、恢复量、次数周期、优先级和律令死亡权限；应检查仲裁与机会成本，不应简单合并。
- 把重生之戒当成复活：当前正式设计是低血主动恢复缺失生命 60%，不能复活。
- 把凤凰蛋当成单件常驻能力：它必须依赖完整 10 件阈值 trait，只有月度 usage 归蛋实例。
- 把护手“主手为空”理解成“任意没有攻击动作”：要求是没有主手武器，正式攻击仍走徒手 attack pipeline。
- 把护手攻击 `+1` 当成伤害 `+1`：它应进入 attack check，不增加 `1D8` 或 `1D10` 伤害。
- 看到 `.tres` 字段正确就宣称 runtime 已落地：必须找到正式 consumer 和真实行为 oracle。
- 看到 runner PASS 就宣称测试有效：必须说明删掉/绕过哪个生产 owner 会使它失败。
- 以 HEAD 为基线判定所有 untracked Phoenix 文件不存在。
- 把工作树中无关 AI、技能、文档改动算入 Phoenix finding。
- 把 `set_bonus_design.md` 开头的 Set 4 `flame_mage_set`（主题和技能名称也使用“凤凰涅槃”）误认为 Set 53 `phoenix_rebirth_set` 的旧配置；二者 item/set ID 不同。Set 53 的历史 2/4 件段已经在同文档第 53 节明确标为“已被正式方案取代”。

## 10. Claude 最终报告模板

```markdown
# 凤凰重生实现独立审查

## 结论
- 审查 checkout：<branch / HEAD / dirty summary>
- 总体状态：Landed / Partial / Contradicted / Unverified
- P0/P1/P2/P3 数量：
- 未验证范围：

## Findings
### [P1] 标题
- 证据：path:line
- 当前行为：
- 设计合同：
- 影响：
- 复现或 killer test：
- 最小 owner 修复：

## 合同核对矩阵
| 合同 ID | 结论 | Resource/Definition | Runtime owner | 正式测试 | 备注 |

## 测试有效性矩阵
| runner::method | 正式 Act | oracle | 失败分支 | 可杀退化 | 结论 |

## 已知边界复核
| 边界 | 描述是否准确 | 当前 Phoenix 是否可达 | 严重度 |

## 文档一致性
| 文档位置 | 当前陈述 | 代码证据 | 结论 |

## 实际运行记录
| 命令 | exit code | 结果 | lifecycle/resource errors |
```

## 11. 审查完成标准

只有同时满足以下条件，才可给出“整体已落地”的结论：

- 十件内容、3/5/7/10 阈值、slot/footprint/破损/重复计数和属性 provenance 均经正式 owner 核实；
- 十件单件行为均找到正式 runtime consumer，并至少有一条可杀行为回归；
- 单件 fire half -> 7 件 fire immune、独立的 3 件 freeze half、fatal priority、律令死亡、附伤 replacement、预览分支和 AI 候选不存在资源/执行分歧；
- 新存档唯一池、商店、掉落、出售、保存重载和失败回滚走真实事务入口；
- 足迹等玩家可见机制存在 presentation consumer；
- `docs/design/` 没有把已知边界或未完成分类写成完整落地；
- 所有新 finding 均有精确证据，所有未验证项均明确披露；
- 没有用 source-text assertion、PASS 数量、resource loadability 或 HEAD 缺文件代替端到端证明。
