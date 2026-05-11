# AI 模块多子代理架构审查汇总

日期：`2026-05-11`

范围：当前项目 AI 模块静态审查。本文收集 5 个 `gpt-5.5 / xhigh` 子代理的原始意见，未做代码修改，未运行测试。

## 子代理 A：Battle AI 运行时管线

Agent：`Dirac`  
ID：`019e130d-3ecf-7b53-be22-40d2b3abd627`

结论先说狠一点：这条 AI 管线已经能跑，但现在更像“资源 action 脚本直接拿运行时活体状态试算”，不是一个干净、可回放、可解释的 AI runtime。最大问题不是某个小 bug，而是 preview、score、execution 三套语义仍然没真正收束。

### 1) 关键缺失能力

- [P1] [battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:130) - AI 评分只遍历 `preview.target_unit_ids`，地形控制、空地封锁、区域预判这类“当前不命中单位但改变战场”的动作基本没有正式评分入口。`UseGroundSkillAction` 又要求 `effective_target_count >= minimum_hit_count`，所以 AI 天生偏向“砸中当前单位”，不会主动做区域控制。证据：[use_ground_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_ground_skill_action.gd:92)、[battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:136)。
- [P1] [use_multi_unit_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_multi_unit_skill_action.gd:120) - `random_chain` 被塞进 multi-unit action 评分，但 runtime preview 对 `random_chain` 直接返回空目标，execution 又随机洗牌实际目标。结果是 AI 为随机链技能生成“看似有 target group 的 command”，preview/score 却完全不按那组目标评估，执行时也不用那组目标。证据：[battle_skill_execution_orchestrator.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:818)、[battle_skill_execution_orchestrator.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:821)、[battle_skill_execution_orchestrator.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:638)。
- [P2] [enemy_ai_action_helper.gd](E:/game/magic/scripts/enemies/enemy_ai_action_helper.gd:48) - unit-target AI command builder 没有设置 `skill_variant_id`，`UseUnitSkillAction` 评分也只传 `combat_profile.effect_defs`。多个 unit cast variant 解锁时，AI 没有明确选择分支的能力。runtime 只有在 command 带 variant 或唯一 unit variant 时才解析出来。证据：[battle_skill_resolution_rules.gd](E:/game/magic/scripts/systems/battle/rules/battle_skill_resolution_rules.gd:157)。

### 2) 架构缺陷 / 耦合

- [P1] [battle_ai_context.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_context.gd:14) - `BattleAiContext` 直接暴露 live `state/unit_state/grid_service/skill_defs`，而 `BattleRuntimeModule.advance()` 把真实 `_state` 和 `active_unit` 塞进去。敌方 action 资源因此拿到的是可变运行时对象，不是只读决策快照；内容脚本和 runtime 边界太薄。证据：[battle_runtime_module.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_module.gd:439)。
- [P1] [battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:63) - `choose_command()` 不是纯决策函数，它会直接写 `unit_state.ai_brain_id/ai_state_id`，还会提交 blackboard。状态所有权被拆在 runtime turn prepare、AI service、timeline cleanup 三处。证据：[battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:339)、[battle_runtime_module.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_module.gd:2764)、[battle_timeline_driver.gd](E:/game/magic/scripts/systems/battle/runtime/battle_timeline_driver.gd:320)。
- [P2] [battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:654) - 伤害评分走静态 range preview，execution 走 `BattleDamageResolver.resolve_effects()`，中间的豁免、抗性、护盾、guard、固定减伤等大量正式结算语义没进入 AI 的 expected outcome。这里不能偷用随机 resolver，但需要一个纯 deterministic expected-result 服务，否则 AI 会持续高估“打不穿/会被豁免”的技能。证据：[battle_damage_resolver.gd](E:/game/magic/scripts/systems/battle/rules/battle_damage_resolver.gd:219)、[battle_damage_resolver.gd](E:/game/magic/scripts/systems/battle/rules/battle_damage_resolver.gd:935)。

### 3) 状态所有权和 determinism 风险

- [P0] [battle_skill_execution_orchestrator.gd](E:/game/magic/scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd:689) - random-chain 执行使用 `TrueRandomSeedService.randi_range()`，这是 Crypto / randomize 的真随机，不绑定 battle seed。AI 对战、模拟报告和回放在这里直接失去 determinism。证据：[true_random_seed_service.gd](E:/game/magic/scripts/utils/true_random_seed_service.gd:15)、[true_random_seed_service.gd](E:/game/magic/scripts/utils/true_random_seed_service.gd:31)。
- [P1] [enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:246) - target 收集从 `Dictionary.keys()` 开始，排序 comparator 在距离/HP 都相等时没有 unit_id 兜底。之后候选选择在完全同分时保留“先出现”的候选，所以同布局同数值下仍可能受 Dictionary 迭代影响。证据：[enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:295)、[enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:309)、[use_unit_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_unit_skill_action.gd:102)。

### 4) 优先级建议

1. P0：把 random-chain 目标选择改成 battle-state seed/nonce 驱动，或把随机目标序列提前写进 preview/command，再由 execution 消费同一序列。
2. P1：把 AI decision 改成纯输出 `Decision + StatePatch/Trace`，由 BattleRuntimeModule 统一提交 `ai_state_id/blackboard`。
3. P1：新增只读 `BattleAiReadContext` 或 snapshot，禁止 enemy action 资源直接持有 live `BattleState`。
4. P1：做一个 deterministic expected-outcome scorer，至少纳入 hit rate、save、immunity/resistance、shield/guard、friendly fire。
5. P2：补 unit cast variant / terrain-control / equipment-change 等 AI command 能力，并加对应 headless AI 回归。

未改文件，也未运行测试；这是纯只读架构审查。

## 子代理 B：敌方内容、AI Brain、Action 数据驱动层

Agent：`Nash`  
ID：`019e130d-3f06-7073-b43f-478190b15964`

### 发现

[P1] [scripts/systems/world/encounter_roster_builder.gd](E:/game/magic/scripts/systems/world/encounter_roster_builder.gd:43) - 敌方内容缺失会被静默洗白成 fallback 敌人。`build_enemy_units()` 找不到 roster/template 后直接 `_build_fallback_enemy_units()`，后者硬编码敌人属性、空手、默认技能；测试还明确断言“缺失正式模板时仍应构建 fallback 敌方单位”：[tests/battle_runtime/runtime/run_wild_encounter_regression.gd](E:/game/magic/tests/battle_runtime/runtime/run_wild_encounter_regression.gd:631)。这会让 `enemy_roster_template_id` / `encounter_profile_id` 拼错时不炸，反而生成假内容，数据驱动层的失败信号被吞掉。

[P1] [data/configs/enemies/brains/ranged_suppressor.tres](E:/game/magic/data/configs/enemies/brains/ranged_suppressor.tres:14) - 当前正式内容已经有未注册 score bucket：`harrier_pressure`。`BattleAiScoreProfile.bucket_priorities` 没有这个 key：[scripts/systems/battle/ai/battle_ai_score_profile.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_profile.gd:40)，未知 bucket 直接回落默认优先级：[scripts/systems/battle/ai/battle_ai_score_profile.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_profile.gd:60)。而决策比较会先比 `score_bucket_priority` 再比总分：[scripts/systems/battle/ai/battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:196)。结果是字段看起来写了，实际压制优先级为 0，校验也没拦。

[P1] [scripts/systems/battle/ai/battle_ai_action_assembler.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_action_assembler.gd:23) - brain 的 `.tres` 不是行动真相源，运行时会按模板技能自动往每个 state 追加动作。`build_unit_action_plan()` 遍历 unit 的 known skills，把未声明技能生成 action 并 append；`BattleAiService` 优先吃 runtime plan：[scripts/systems/battle/ai/battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:83)。例如 `wolf_alpha` 只在模板里给了 `warrior_guard_break`：[data/configs/enemies/templates/wolf_alpha.tres](E:/game/magic/data/configs/enemies/templates/wolf_alpha.tres:21)，`melee_aggressor` brain 没有显式声明这个动作，但运行时会补。作者读 brain 资源无法知道最终行为，这不是数据驱动，是脚本偷偷扩写行为树。

[P2] [scripts/systems/battle/ai/battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:246) - brain 状态机表达力被硬编码死了。资源只暴露 `default_state_id`、几个 HP/距离阈值和 `states`：[scripts/enemies/enemy_ai_brain_def.gd](E:/game/magic/scripts/enemies/enemy_ai_brain_def.gd:7)；实际切状态只认识 `retreat/support/pressure/engage`，优先级也写死。想表达“队友被控时支援”“第 N 回合爆发”“资源低于阈值”“敌人聚团时切 AOE state”，资源层根本没有 schema，只能继续往服务脚本塞条件。

[P2] [scripts/enemies/enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:49) - action 校验只查 skill_id 是否存在，不查技能是否适配 action 类型。运行时 `UseUnitSkillAction` 遇到非 unit 技能才跳过：[scripts/enemies/actions/use_unit_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_unit_skill_action.gd:39)，`UseGroundSkillAction` 同理跳过非 ground 技能：[scripts/enemies/actions/use_ground_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_ground_skill_action.gd:43)。这意味着“有效但类型错误”的 skill 引用会通过静态校验，然后战斗中静默失效并落到后续 action/wait。

[P2] [scripts/enemies/enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:273) - `target_selector` 没有枚举校验，拼错会退化成默认距离排序。各 action validate 基本只检查非空，例如 [use_unit_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_unit_skill_action.gd:162)；`_sort_target_units()` 只特殊处理几个已知 selector，未知值不会报错，最后仍按距离/血量排序。一个 `lowest_hp_enmey` 这种错字不会被 validation 抓住，只会悄悄改 AI 目标选择。

### Open Questions / Assumptions

我假设当前目标是“正式敌方内容可校验、可作者化”，不是保留原型 fallback 行为。如果 fallback 仍被需要，至少应该分成 debug/test-only 路径，正式运行链 fail fast。

### Residual Risks / Test Gaps

这轮按要求未改文件，也未跑 Godot 测试。现有回归覆盖了缺失 skill/item/template，但缺少这些关键夹具：未知 `score_bucket_id`、拼错 `target_selector`、action 类型与 skill `target_mode` 不匹配、brain 内非法 state resource、以及自动生成 action 改变最终 action plan 的快照断言。

## 子代理 C：评分、目标选择、路径 / 地形 / 范围推理

Agent：`Helmholtz`  
ID：`019e130d-3f4d-71a2-a356-04717bec98a3`

### 发现

[P1] [battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:654) - 伤害评分是假的。AI 用 `BattleDamagePreviewRangeService.build_skill_damage_preview(source_unit, effect_defs)` 估均值，根本不传 `target_unit`；而正式结算会看目标抗性、免疫、固定减伤、格挡等 [battle_damage_resolver.gd](E:/game/magic/scripts/systems/battle/rules/battle_damage_resolver.gd:935)。命中也是预览平均值 [battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:982)，但正式命中看目标 AC [battle_hit_resolver.gd](E:/game/magic/scripts/systems/battle/rules/battle_hit_resolver.gd:249)。结果：AI 会把高护甲/免疫目标和脆皮目标当成同一收益，击杀评分也会膨胀。

[P1] [use_ground_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_ground_skill_action.gd:60) - 地面/地形技能只会按“命中了几个单位”活着，完全不会为了封路、铺陷阱、造高低差、控制空格施法。`minimum_hit_count` 检查的是 `score_input.effective_target_count` [use_ground_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_ground_skill_action.gd:92)，而评分服务只在遍历 `target_unit_ids` 时才累计 terrain/height 收益 [battle_ai_score_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:130)。空地格战术不存在。

[P1] [retreat_action.gd](E:/game/magic/scripts/enemies/actions/retreat_action.gd:52) - 撤退只看相邻 4 格，一格一格挪，完全浪费本回合移动力。仓库里明明已有正式路径成本接口 `resolve_unit_move_path` [battle_grid_service.gd](E:/game/magic/scripts/systems/battle/terrain/battle_grid_service.gd:626)，`MoveToRangeAction` 也会搜可达格 [move_to_range_action.gd](E:/game/magic/scripts/enemies/actions/move_to_range_action.gd:127)。所以低血远程单位只要安全格在两三步外，就可能继续站在威胁区里“理性撤退”。

[P2] [enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:326) - `nearest_role_threat_enemy` 是几何距离启发，不是路径/地形威胁模型。它用有效射程 + 曼哈顿距离窗口判定角色威胁 [enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:334)，安全距离也只是 `safe_distance - distance` [enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:494)。墙、深水、泥地绕路、边阻挡都没有进入威胁评分，只有 screening 分支局部补了一点路径成本。

[P2] [use_multi_unit_skill_action.gd](E:/game/magic/scripts/enemies/actions/use_multi_unit_skill_action.gd:151) - 多目标组合生成太粗暴，只取排序池里的连续窗口，不枚举组合。6 个候选选 2 个时，它会看 `[0,1] [1,2] [2,3]...`，但不会看 `[0,3]` 这种“低血目标 + 高威胁治疗者”的真实最优组。后面的评分再聪明也没用，因为候选已经被剪没了。

[P2] [enemy_ai_action_helper.gd](E:/game/magic/scripts/enemies/enemy_ai_action_helper.gd:132) - trace 的 “top_candidates” 只按 `total_score` 排，但真实选择器先比友伤、致命威胁、逃生、bucket priority 等 [battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:177)。这会让调试报告误导人：看起来最高分的候选不是实际选择依据。更糟的是比较器在 `BattleAiService` 和 `EnemyAiAction` 复制了两份 [enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:171)，以后必然漂移。

### 假设

我按当前主干做静态架构审查，没有改文件，没有跑测试。重点只读了 CU-20/CU-16 相关 AI、grid、range、terrain 接口。

### 测试缺口

现有 AI 回归覆盖了 move_to_range、screening、威胁目标、友伤和部分 ground skill，但缺少：目标抗性/AC 对 AI 评分的影响、空地形控制施法、撤退多步可达安全格、非连续多目标最优组合、以及 trace 排序与真实选择器一致性的断言。

## 子代理 D：测试、Headless / Simulation、可观测性

Agent：`Cicero`  
ID：`019e130d-3f81-7c01-a72a-b724a84473b3`

未改文件，未跑测试；以下是静态架构审查。结论很直接：常规 headless 覆盖已经不薄，但 simulation/report/trace 这层有几处“看起来有观测性，实际会误导分析”的硬伤。

### Findings

- [P1] [tests/run_regression_suite.py](E:/game/magic/tests/run_regression_suite.py:63) / [run_battle_balance_simulation.gd](E:/game/magic/tests/battle_runtime/simulation/run_battle_balance_simulation.gd:15) - `--include-simulation` 入口会收集所有 `run_*.gd`，但 `run_battle_balance_simulation.gd` 无参数直接 `quit(1)`。这不是“别把模拟当常规测试”的问题，而是显式 simulation 扩展入口本身会踩 CLI-only runner。最小回归：`--include-simulation --list` 应排除 CLI-only runner，或给它一个无参数 smoke 示例场景。
- [P1] [battle_sim_execution_loop.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_execution_loop.gd:46) / [battle_sim_runner.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_runner.gd:155) / [battle_sim_report_builder.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_report_builder.gd:27) - idle guard 的 `stalled` 被 execution loop 返回，但 runner 丢掉；report builder 又把所有 runs 直接进平均和胜率分母。未结束/停滞场次会把 balance 结论污染成“低胜率/高迭代”，而不是“样本无效”。最小回归：一 ended、一 stalled 的 synthetic runs，断言 summary 暴露 `completed_run_count / unfinished_run_count / stalled_count / completed_only_win_rate`。
- [P2] [battle_metrics_collector.gd](E:/game/magic/scripts/systems/battle/runtime/battle_metrics_collector.gd:81) / [battle_sim_report_builder.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_report_builder.gd:145) - metrics 明明按 faction 记录 `action_counts / skill_attempt_counts / skill_success_counts`，report summary 合并 faction 时却只保留伤害、治疗、击杀、死亡等少数整数。结果是 summary 层看不出“哪一边”在尝试/成功某技能。最小回归：构造 player/hostile 同 skill 不同计数，断言 `faction_metric_totals.<faction>.skill_*` 和 `action_counts` 不丢。
- [P2] [battle_ai_score_input.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_input.gd:83) / [battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:182) / [battle_sim_trace_summary_builder.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_trace_summary_builder.gd:275) - trace summary 丢了很多真正决定 AI 排序的字段：friendly fire、friendly lethal、lethal threat、healing/status/terrain/cooldown、desired distance 等。可是 AI 比较优先级先看这些，再看 bucket/score。低 token summary 会解释不出“为什么这个动作赢”。最小回归：trace summary fixture 塞入这些 score 字段，断言精简输出至少保留比较优先级字段。
- [P2] [battle_sim_runner.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_runner.gd:249) / [run_battle_simulation_regression.gd](E:/game/magic/tests/battle_runtime/simulation/run_battle_simulation_regression.gd:24) - runner 只返回非空 output path，`FileAccess.open()` 失败不会让测试失败；现有 regression 也只断言字符串非空。`trace_summary_json` 有 trace 时也没被主 simulation regression 守住。最小回归：断言 report/jsonl/trace_summary 文件实际存在、可 JSON parse、trace_count > 0。
- [P2] [run_battle_balance_simulation.gd](E:/game/magic/tests/battle_runtime/simulation/run_battle_balance_simulation.gd:34) / [battle_sim_runner.gd](E:/game/magic/scripts/systems/battle/sim/battle_sim_runner.gd:294) - balance CLI 默认把 progress log 写到 `res://battle_sim_progress.log`，这是仓库路径，不是 `user://`。这会制造本地瞬态脏文件。最小回归：CLI 默认 progress log 必须落 `user://simulation_reports/...` 或显式 env 指定。

### 现有测试能抓到

- 常规 suite 默认跳过 simulation/benchmarks，符合“不把模拟 runner 当常规测试”的边界：[tests/run_regression_suite.py](E:/game/magic/tests/run_regression_suite.py:38)。
- AI 单步决策覆盖很密：自动 whirlwind、致命目标优先、资源阻断压制射击、move-to-range、taunt 强制目标都有回归：[run_battle_runtime_ai_regression.gd](E:/game/magic/tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd:80)。
- simulation 已覆盖 ready queue 不额外推进 TU、profile patch 改变技能使用、AI vs AI 两阵营 trace、formal terrain/fixture、report attempt/failure totals、trace summary compaction。
- text runtime 已覆盖非法 scalar 不漂移状态、validation 不靠日志抓取、日志默认不写文件、战斗命令日志带 post-state。

### 抓不到的核心盲区

- 抓不到 simulation 输出文件真实写盘失败。
- 抓不到未完成/停滞 run 被混进胜率和均值。
- 抓不到 per-faction 技能尝试/成功丢失。
- 抓不到精简 trace 缺少真正影响 AI 排序的字段。
- 抓不到 analysis packet 工具链本身回归；`tools/build_battle_sim_analysis_packet.py` 是关键交接面，但目前没有对应测试证据。

## 子代理 E：Godot 架构边界

Agent：`McClintock`  
ID：`019e130d-3fc1-7320-8f8d-5fac82b55046`

### 架构审查结论

这套 AI 边界现在最危险的问题不是“AI 不够聪明”，而是内容、运行时、调试、测试入口互相越界。很多地方看起来有 registry / service / context，但实际是软边界，坏内容可以被 fallback 吃掉，Resource 可以直接碰 BattleState，热路径还在无条件构造调试 trace。

按严重程度：

`[高]` [scripts/systems/world/encounter_roster_builder.gd](E:/game/magic/scripts/systems/world/encounter_roster_builder.gd:43) - 内容 registry 不是硬边界，坏敌方内容会被 runtime 悄悄降级。`build_enemy_units()` 找不到 roster/template 时直接走 fallback；[同文件](E:/game/magic/scripts/systems/world/encounter_roster_builder.gd:543) 还会合成默认敌人。与此同时 [scripts/systems/battle/ai/battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:49) 缺 brain 只是待机。registry 校验在 [scripts/systems/persistence/game_session.gd](E:/game/magic/scripts/systems/persistence/game_session.gd:1660) 做快照、[同文件](E:/game/magic/scripts/systems/persistence/game_session.gd:1702) 只报日志，不阻止启动。这会让缺 template、缺 brain、坏引用变成“能跑但行为假”的战斗，最容易污染回归结论。

`[高]` [scripts/enemies/enemy_ai_action.gd](E:/game/magic/scripts/enemies/enemy_ai_action.gd:1) - AI action 是 `Resource`，但实际在做运行时规划服务的活。[scripts/systems/battle/ai/battle_ai_context.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_context.gd:14) 把 `state/unit_state/grid_service/skill_defs/callbacks` 全裸塞给 action；[scripts/enemies/actions/move_to_range_action.gd](E:/game/magic/scripts/enemies/actions/move_to_range_action.gd:445) 甚至在评分时临时改 grid occupant，再恢复。这不是“资源定义策略”，这是资源对象直接操作战斗状态。缺一个真正的 `AiPlanningService / CandidateEvaluationService` 来拥有寻路、占位模拟和 preview。

`[高]` [scripts/enemies/enemy_ai_action_helper.gd](E:/game/magic/scripts/enemies/enemy_ai_action_helper.gd:92) - AI trace 开关是假的性能开关。即使 [battle_runtime_module.gd](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_module.gd:448) 传了 `trace_enabled`，action 仍然无条件创建 trace 字典、复制 metadata；[helper](E:/game/magic/scripts/enemies/enemy_ai_action_helper.gd:124) 每个候选还 duplicate + sort top candidates。真正的门禁只在 [BattleAiContext.record_action_trace](E:/game/magic/scripts/systems/battle/ai/battle_ai_context.gd:101) 最后丢弃。配合 [UseGroundSkillAction](E:/game/magic/scripts/enemies/actions/use_ground_skill_action.gd:53) 遍历全图格子的候选枚举，这是热路径分配炸弹。

`[中高]` [scripts/enemies/enemy_content_registry.gd](E:/game/magic/scripts/enemies/enemy_content_registry.gd:84) - registry 直接返回可变字典；[GameSession](E:/game/magic/scripts/systems/persistence/game_session.gd:1654) 原样缓存 enemy templates/brains/rosters，[GameSession getter](E:/game/magic/scripts/systems/persistence/game_session.gd:499) 又原样暴露，[GameRuntimeFacade](E:/game/magic/scripts/systems/game_runtime/game_runtime_facade.gd:240) 再原样注入 battle runtime。这里没有只读 bundle，没有 duplicate/freeze，任何 runtime/test 都能污染全局内容资源。

`[中]` [scripts/systems/battle/core/battle_unit_state.gd](E:/game/magic/scripts/systems/battle/core/battle_unit_state.gd:142) - `ai_blackboard` 注释说是“单场战斗临时上下文”，但 [to_dict](E:/game/magic/scripts/systems/battle/core/battle_unit_state.gd:552) 会序列化 `ai_brain_id/ai_state_id/ai_blackboard`，[from_dict](E:/game/magic/scripts/systems/battle/core/battle_unit_state.gd:718) 要求它是 schema 字段并在 [811](E:/game/magic/scripts/systems/battle/core/battle_unit_state.gd:811) 复制回来。临时黑板已经进入 payload 契约，save/load 或 headless fixture 一旦复用 BattleUnitState 字典，就会把临时 AI 决策垃圾变成正式格式。

`[中]` [scripts/systems/battle/ai/battle_ai_score_profile.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_profile.gd:33) - 热路径 ID 体系混乱。profile 的 `action_base_scores/bucket_priorities` 用 String key，但每次评分在 [53](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_profile.gd:53) 和 [60](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_profile.gd:60) 把 `StringName` 转 `String` 查表。评分输入在 [BattleAiScoreService](E:/game/magic/scripts/systems/battle/ai/battle_ai_score_service.gd:40) 每个候选都会构造。AI 多目标和全图技能越多，这种 String/StringName 来回转换越恶心。

`[中]` [scripts/systems/battle/ai/battle_ai_service.gd](E:/game/magic/scripts/systems/battle/ai/battle_ai_service.gd:177) - 评分比较规则被复制散落。service 有一份 `_is_better_score_input`；[EnemyAiAction](E:/game/magic/scripts/enemies/enemy_ai_action.gd:171) 有几乎同构的一份；[MoveToAdvantagePositionAction](E:/game/magic/scripts/enemies/actions/move_to_advantage_position_action.gd:265) 和 [MoveToMultiUnitSkillPositionAction](E:/game/magic/scripts/enemies/actions/move_to_multi_unit_skill_position_action.gd:194) 又各自补一份局部排序。以后改“友伤优先级/致命威胁/桶优先级”，大概率只改中一处，然后 AI 行为裂开。

### Node / RefCounted

没看到明显 Node 滥用；问题反而是 RefCounted 被当成动态接口袋子。比如 [BattleRuntimeModule.setup](E:/game/magic/scripts/systems/battle/runtime/battle_runtime_module.gd:235) 接 `Object/Variant/Dictionary`，再靠 `has_method` 和裸字典传递边界。headless 更狠，[headless_game_test_session.gd](E:/game/magic/scripts/systems/game_runtime/headless/headless_game_test_session.gd:481) 直接摸 `battle_runtime._active_loot_entries` 私有字段。这个不是辅助链，是穿墙。

未改文件，未跑测试。这里只做只读架构取证。
