# CU-14 Progression Rules Subagent Review - 2026-05-11

## Scope

本轮按 `algorithm-design` 流程，对 CU-14 progression 规则执行、属性快照、职业晋升、技能描述与测试可观测性做并行对抗性分析。四个子任务分别覆盖：

- `ProgressionService` 主线：学习、合并、动态上限、晋升和状态卫生。
- 职业规则链路：unlock/rank-up、核心技能、练习成长、promotion prompt。
- 属性/身份/装备来源：`AttributeService`、建卡、体型、battle-local equipment view。
- formatter / headless / 测试覆盖：等级描述与真实效果一致性、文本快照可观测性。

未改运行时代码，未跑 Godot 测试；本文只沉淀审查意见和建议落地顺序。

## 合并判断

未发现 P0。CU-14 的主要风险不是数据结构大面积失控，而是规则合同和执行口径有几处关键漂移：

- 属性合同写的是普通 AC `10 + DEX modifier`，实现目前按 `8 + DEX modifier` 计算。
- 正式职业 rank 1 -> rank 2 可能因为“需要 2 个已分配核心，但 rank 1 容量又不允许先分配第 2 个核心”而自锁。
- promotion 提交失败时，world/battle UI 状态可能被当作成功清掉。
- 技能描述系统不能稳定从 typed effect fields 派生文本，导致正式内容描述和运行时结算容易漂移。

这些问题都适合先补窄测试锁定，再做小步修复。

## P1 Findings

### 普通 AC 基准与 CU-14 合同不一致

`scripts/systems/attributes/attribute_service.gd` 中 `BASE_ARMOR_CLASS := 8`，`_calculate_base_armor_class()` 用它计算普通 AC。CU-14 context map 明确写的是普通 AC `10 + 敏捷调整值`。

失败模式：DEX 10、无装备角色得到 AC 8 而不是 10；`BattleHitResolver` 消费 `armor_class` 后，战斗命中率和 CMM 暴露的 attribute snapshot 都偏低。

建议：补 AttributeService/CMM 聚焦回归，覆盖无甲 AC、护甲 `max_dex_bonus` 截断、`-1` 不截断、equipment view 下 AC component/source 生效。修复前先确认是否存在旧模拟路径依赖 AC 8；若只是 bug，常量应回到 10。

### 正式职业 rank 1 -> rank 2 可能被规则链自锁

`ProfessionRuleService.can_rank_up_profession()` 判定 rank-up 时只看当前 `profession_progress.core_skill_ids`。`ProfessionAssignmentService.can_promote_non_core_to_core()` 又在 `core_skill_ids.size() >= rank` 时拒绝新增核心。`ProgressionService.promote_profession()` 只在 unlock 分支分配 consumed core，rank-up 分支不会把本次触发技能挂入职业核心位。

失败模式：正式 `warrior.tres` rank 2 要求 2 个 `assigned_core/core_max`，但正常 rank 1 职业通常只有 unlock 时的 1 个核心。第二个已学满级核心既无法预先分配，又不会在 rank-up 中先挂入，因此 pending choice 可能永远出不来。

建议：补官方 warrior/mage 从 rank 1 到 rank 2 的真实服务回归：rank 1 + 第二个已学核心满级技能 + active trigger，断言能出现 pending choice 并成功晋升。实现上需要明确 rank-up 是否允许按 target rank 容量先纳入触发核心，再提升 rank。

### promotion 提交失败会清 prompt / 解冻战斗

`GameRuntimeRewardFlowHandler.on_promotion_choice_submitted()` 在 world 分支先清 pending prompt 和 modal，再调用 `_promote_profession()`。`BattleRuntimeModule.submit_promotion_choice()` 不检查 CMM delta 是否真的晋升成功，就 append delta、刷新单位、记录“完成职业晋升”，并清 `modal_state` / 解冻 timeline。

失败模式：外部命令或陈旧 UI 提交错误 `profession_id/selection` 时，CMM 可能返回空 delta，但 prompt 已被吞掉；battle runtime 还会解冻，强制晋升选择可被一次失败确认跳过。

建议：world/battle 两边补非法 profession 或非法 selection 回归，要求 prompt/modal 保留，timeline 仍冻结，状态文案不报完成晋升。再让提交入口只在 delta 表示实际晋升或后续 prompt 已重建时清 UI 状态。

### 技能描述与运行时效果漂移

正式 `mage_chain_lightning.tres` 描述只写“敏捷豁免减半并附加 shocked”，但状态 effect 还有 `constitution` 豁免。运行时会按每个 effect 的 `save_ability` 单独结算。

失败模式：玩家看到的规则少了一次关键抗性判断；这类问题不是单个技能独有，而是 formatter 与 content validation 缺少“描述覆盖 save-enabled effect”的一致性检查。

建议：新增官方技能渲染描述与 save-enabled effects 的一致性校验，至少覆盖“伤害豁免 + 状态豁免”双效果技能。

### formatter 只合并 effect params，不合并 typed effect fields

`SkillLevelDescriptionFormatter` 自动合并 `effect_def.params`，但运行时和校验已大量使用 typed fields，例如 `power`、`duration_tu`、`save_ability`、`forced_move_distance`。描述里的 `{dmg}`、`{duration}`、豁免文本等因此需要手写复制在 `level_description_configs`。

失败模式：改正式 typed field 时，描述可以静默过期；现有 mage alignment runner 主要检查配置存在和效果存在，不比对渲染文本与 typed runtime values。

建议：为 formatter 定义最小派生字段集合，绑定到 `CombatSkillDef` getter 与 `CombatEffectDef` typed fields；新增官方技能描述一致性测试，渲染每级描述并检查关键字段来源。

## P2 Findings

### 失败晋升可能留下 rank 0 职业进度

`ProgressionService.promote_profession()` 在确认 selection 有效前创建并写入 `UnitProfessionProgress`。如果 `_resolve_promotion_selection()` 返回空，或后续 core assignment 失败，函数返回 `false` 但 `progress.professions[profession_id]` 已留下 rank 0 条目。

建议：补“ready trigger 存在但显式 selection 无效”的回归，断言 `professions`、`hp_max`、`promotion_history` 完全不变。

### active_level_trigger_core_skill_id 状态卫生不足

`UnitProgress.from_dict()` 接受任意非空 `active_level_trigger_core_skill_id`，`ProgressionService.refresh_runtime_state()` 不清理与实际技能状态不一致的 active trigger。

失败模式：坏档或陈旧状态可保留“指向不存在 / 未学习 / 非核心 / 已锁定技能”的 active trigger；晋升一直失败，但坏状态继续被序列化。

建议：明确策略是拒绝 payload 还是 refresh 时清空；补 schema/core 测试覆盖缺失技能、非核心技能、已锁定技能。

### active level trigger 晋升成长路径比普通领取路径宽松

CMM 晋升触发的属性成长路径会把 `attribute_growth_progress` key 通过 `to_string_name()` 宽松接收，并在循环后无条件设置 `core_max_growth_claimed`。普通核心满级奖励路径只接受 `String` key。

失败模式：旧 `StringName` key 或全无效成长项在晋升路径可能被入账；或者没有任何进度实际应用却消耗一次性领取标记。

建议：补“active level trigger 晋升路径 + StringName/非法 attribute_growth_progress key”的断言，并与普通领取路径共享校验。

### 修炼技能替换绕过普通学习前置

`PracticeGrowthService.apply_replacement()` 直接创建 `UnitSkillProgress` 并设置 `is_learned = true`，没有走 `ProgressionService.learn_skill()` 的 learn_source、knowledge、skill level、attribute、achievement、blocked relearn 校验。

失败模式：未来 meditation/cultivation 技能带高阶前置或非 book 来源时，替换确认可越权学习。

建议：构造同轨旧功法 + 新功法带未满足 attribute/achievement/knowledge 前置的回归，确认替换失败且不扣技能书。

### profession gate 可达性仍未静态校验

`ProfessionContentRegistry` 只校验 profession gate 引用存在、`min_rank > 0` 和 `check_mode` 合法，不校验 `min_rank <= referenced.max_rank`、unlock 自引用、target rank 自锁等可达性。

失败模式：内容表面合法，运行时永远返回 false，职业永久不可解锁或不可升阶。

建议：补非法 profession fixture：`min_rank > referenced.max_rank`、unlock 自引用、rank requirement 自引用当前 target，要求 registry 报错。

### battle-local 换装只同步 HP，不同步 MP/Aura

`BattleChangeEquipmentResolver` 刷新 battle-local equipment projection 后只处理 `hp_max/current_hp`。如果未来装备或临时装备视图影响 `mp_max/aura_max`，卸下后 `current_mp/current_aura` 可短暂高于新上限，直到战斗资源提交回 CMM 时才 clamp。

建议：用测试装备提供 `mp_max/aura_max` modifier，验证 battle-local equipment view 刷新后 current/max 同步语义。顺手明确 stamina/action_threshold 是否需要同步。

### CharacterCreationService 无内容源时保留 payload body size

`CharacterCreationService.create_member_from_character_creation_payload()` 默认允许 `progression_content_source = null`。这种路径会先接收 payload 的 `body_size/body_size_category`，再因无法解析身份内容而不覆盖。

失败模式：直接调用服务可创建 `body_size = 99, body_size_category = large` 这类不一致成员；世界侧装备需求可能先消费 `member_state.body_size`，在进入战斗严格校验前误判可装备性。

建议：补建卡对抗性回归，验证无内容源时拒绝或规范化 payload body size；或者明确该入口必须传内容源。

### formatter optional 条件块契约容易失真

`SkillDef` 注释倾向于“未写字段会隐藏条件块”，但 formatter 会先从 profile overrides 注入 `attack_roll_bonus`、`aura_cost` 等字段；`0` 又会被视为非空而显示。当前个别技能靠手动写空字符串压掉 0。

失败模式：后续内容容易出现“攻击检定0”之类描述。

建议：补一个合成技能 formatter 回归：模板含 `{{?attack_roll_bonus}}`，profile override 为 0，config 省略字段，明确断言期望行为。

### headless 文本快照漏 progression 关键状态

结构化 snapshot 已有 `current_aura`、`unlocked_combat_resource_ids`、`learned_skill_ids` 等状态，但 `GameTextSnapshotRenderer` 文本层主要输出 hp/mp/ac/equip。学习技能后 MP/Aura 解锁、技能等级、核心/锁定触发规则出错时，文本输出可能完全不变。

建议：给学习 MP/Aura 技能、新建随机技能、职业奖励锁定/核心状态各加一条文本快照断言，让 agent/headless 排障能看到 progression 规则状态。

### cast variant 描述合并未过滤 variant min_skill_level

formatter 合并 cast variant effects 时没有检查 `cast_variant.min_skill_level`；运行时与 mage alignment helper 会过滤。当前尚未形成明确正式内容故障，但未来模板引用 variant effect params 时，低等级描述可能提前泄露高等级规则。

建议：formatter 的 effect 合并口径应与 runtime 可用 variant 口径一致。

## Confirmed Healthy Areas

- `SkillMergeService` 与 `SkillEffectiveMaxLevelRules` 主线未发现可直接定级的 P0/P1；动态上限的 aura/custom stat 与 `profession_rank:mage` 路径已有核心测试覆盖。
- `AttributeSourceContext` 与 `AttributeService` 的来源顺序整体贴近 CU-14：race -> subrace -> effective age -> bloodline -> ascension/stage -> versatility -> profession -> skill -> equipment -> passive -> temporary。
- 未发现 CMM 的 `equipment_state_override` 在属性快照中被忽略；`BattleUnitFactory` 会通过 CMM 的 `get_member_attribute_snapshot_for_equipment_view()` 使用 battle-local equipment view。
- `BodySizeRules` 与 battle payload 校验基本符合 CU-14：category 与派生 int/footprint 不匹配会拒绝。

## Suggested Landing Order

1. 先补锁定 bug 的窄测试：AC 基准、rank 1 -> rank 2 官方晋升、promotion 提交失败保留 prompt。
2. 修 AC 常量和职业 rank-up 容量/触发核心纳入策略。
3. 修 promotion submit 成功判定，world/battle 两边都以 delta 或明确结果决定是否清 modal。
4. 建 formatter 与 typed effect fields 的最小派生规则，再修 chain lightning 描述。
5. 收拾 P2 状态卫生：失败晋升原子性、active trigger 清理、修炼替换前置、MP/Aura 换装同步、headless 文本可观测性。

## Test Gaps To Add

- `tests/progression/core`：AttributeService 无甲 AC、max dex、equipment view。
- `tests/progression/core`：official warrior/mage rank 1 -> rank 2 晋升。
- `tests/battle_runtime/runtime` 与 `tests/text_runtime`：promotion 提交失败时 prompt/modal/timeline 保留。
- `tests/progression/core`：失败晋升不污染 `professions/hp_max/promotion_history`。
- `tests/progression/schema` 或 core：active trigger 指向坏技能时拒绝或清理。
- `tests/progression/core`：晋升触发属性成长与普通核心满级领取路径一致。
- `tests/progression/core`：修炼替换不能绕过学习前置。
- `tests/runtime/validation` 或 `tests/battle_runtime/skills`：描述与 save-enabled effect / typed fields 一致性。
- `tests/text_runtime`：文本快照输出 progression 关键状态。
