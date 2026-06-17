# scripts 目录 C# 全量审查记录

日期：`2026-06-17`

范围：`scripts/**/*.cs`

文件总数：`469`

## 审查原则

- 逐文件检查，优先记录会导致运行时错误、GodotSharp 互操作问题、状态/序列化风险、性能热点、场景契约漂移和缺失回归的问题。
- 普通风格问题不记录，除非它掩盖了明确的正确性或维护风险。
- 发现问题后先记录到本文档，再继续后续文件。
- 兼容逻辑、旧 schema 支持、fallback 迁移只作为风险指出，不在未确认前建议直接添加。

## 覆盖进度

| 分片 | 范围 | 状态 |
| --- | --- | --- |
| enemies | `scripts/enemies/**/*.cs`（32） | 已完成，发现 3 项 |
| player | `scripts/player/**/*.cs`（89） | 已完成，发现 5 项 |
| battle-ai-rules | `scripts/systems/battle/{ai,rules,terrain,fate,_interop,presentation}/**/*.cs`（80） | 已完成，发现 5 项 |
| battle-core-runtime | `scripts/systems/battle/{core,runtime,sim}/**/*.cs`（121） | 已完成，发现 5 项 |
| runtime-systems | `scripts/systems/{content,game_runtime,inventory,persistence,progression,settlement,world,attributes,fate}/**/*.cs`（96） | 已完成，发现 2 项 |
| ui-utils | `scripts/ui/**/*.cs`, `scripts/utils/**/*.cs`, `scripts/dev_tools/**/*.cs`（51） | 已完成，发现 5 项 |

## Findings

### 已确认

`[high] scripts/enemies/actions/UseChargePathAoeAction.cs:175` - `UseChargePathAoeAction` 对 charge-path AOE 候选只调用本地 `BuildFastChargePathPreview(...)`，不像 `UseChargeAction` 在相同决策阶段调用 `context.PreviewCommand(command)` 走正式 `BattleChargeResolver` 校验。失败模式：AI 可以选出穿墙、穿单位、超出 charge 规则、或正式 resolver 会截断/拒绝的路径；最终 `IssueCommand` 或 runtime preview 拒绝，敌方回合浪费，或者 path-step 命中统计和正式执行不一致。建议补 battle AI 回归：阻挡/墙体/超距场景下 `Decide` 返回的 command 必须通过正式 `PreviewCommand`，并验证 path-step hit 统计与正式预览坐标一致。

`[medium] scripts/enemies/actions/UseGroundSkillAction.cs:279` - ground skill 候选评分只走 `_build_fast_ground_skill_preview(...)`，没有在候选进入评分前经过正式 `ValidateGroundSkillCommandResultTyped(...)` / ground runtime validation。当前 prefilter 只覆盖距离、命中和控制价值，漏掉 `allowed_base_terrains` 与特殊 ground-target 资格等 schema/runtime 约束。失败模式：带 `allowed_base_terrains` 的技能可被 AI 选到非法地形，runtime 执行阶段再拒绝，导致敌方可执行行动变为空操作。建议补非法地形和特殊 ground target 资格失败时的 AI 候选过滤回归。

`[medium] scripts/enemies/EnemyAiAction.cs:1225` - `_skill_has_tag(...)` 在 AI 威胁判断热路径读取 `sd.tags` 公共 Godot 投影，而不是 `SkillDef.TagsTyped`。这违反当前“typed owner 为正式业务态，Godot projection 只在边界使用”的约束；多单位、多候选评分时会反复构造/遍历 Godot 数组投影，增加 GC，并让 AI 行为依赖边界投影。建议改为 `TagsTyped`，并补轻量 typed-boundary 或 battle-AI trace/perf 回归覆盖 hostile/threat tag 判定。

`[medium] scripts/ui/WorldMapView.cs:141` - `_GuiInput(...)` 在判定 `_gridSystem == null` 前先调用 `_local_to_cell(...)`，而 `_local_to_cell` 会经 `_get_camera_origin_cells()` 访问 `_gridSystem.GetWorldSizeCells()`。失败模式：`WorldMapSystem._Ready()` 因缺少 `GameSession`、active save 或生成配置提前返回时，`WorldMapView` 仍可能接收鼠标输入并抛 `NullReferenceException`。建议补无配置 `WorldMapView` / `world_map.tscn` 输入回归：发送左键事件时不抛异常、不发 `cell_clicked`，正常配置仍发信号。

`[medium] scripts/utils/DisplaySettingsService.cs:58` - `LoadSettings()` 直接把 `ConfigFile.GetValue(...)` 返回的 Variant 强转 `int` / `bool`。失败模式：`user://display_settings.cfg` 被手动改坏或写入字符串/浮点时，登录页 `_Ready()` 调用 `LoadAndApply()` 会在 UI 打开前抛异常，而不是回退默认显示设置。建议扩展 display settings regression，写入 malformed cfg 后断言 `LoadSettings()` 不抛异常且返回默认/归一化设置。

`[medium] scripts/utils/WorldMapContentValidator.cs:153` - mounted submap 递归检查把 `visitedPaths` 当全局集合使用，进入子配置后没有在返回时移除路径；`ValidateMountedSubmaps(...)` 对兄弟 submap 复用同一个非递归 `generation_config_path` 时，第二个会被误报为 recursive path。失败模式：合法复用子地图配置的世界内容被 validator 阻断。建议新增 validator 回归：父配置含两个不同 `submap_id` 但相同子配置路径时不报递归，同时真实 cycle fixture 仍失败。

`[low] scripts/ui/BattleMapPanel.cs:1779` - 多处 UI 文本把换行写成 `"/n"`，包括装备详情、空背包提示、时间轴 tooltip 和技能 tooltip。失败模式：运行时显示字面 `/n` 而不是换行，战斗 HUD 可读性退化。建议扩展 battle map panel schema/UI regression，断言相关 tooltip/detail 文本包含 `\n` 且不包含 `/n`。

`[low] scripts/ui/ShopWindow.cs:223` - `show_member_selector=false` 时只隐藏 `member_selector` 和 `member_state_label`，但 `_apply_section_titles()` 仍设置并显示 `member_title_label`。失败模式：契约板等不需要成员选择的 payload 会显示孤立的成员标题而无控件，窗口状态不一致。建议扩展 shop window schema regression：`show_member_selector=false` 时标题、下拉和状态都隐藏，true 路径仍显示。

`[medium] scripts/systems/battle/runtime/BattleRuntimeModule.cs:283` - `_ai_movement_query_service` 是 `BattleMovementQueryService : RefCounted`，在 AI 决策绑定时 `Setup(_state, _grid_service, _get_ai_move_query_cost)`，但 `DisposeManagedRuntime()` 没有 dispose 或清空它；该 service 也没有自己的 dispose/clear runtime 方法。失败模式：battle runtime dispose 后仍保留旧 battle state、grid service、move-cost delegate 和 path/cache state；长时间 simulation / AI 批跑会有 native RefCounted 生命周期泄漏与旧状态引用污染风险。建议补 lifecycle regression：绑定一次 AI helper 后 dispose runtime，断言 movement query 清空 state/grid/delegate/cache 或进入 disposed 状态。

`[medium] scripts/systems/battle/sim/BattleSimRunReport.cs:30` - formal sim run report 把 `Metrics` 存成 `Godot.Collections.Dictionary`，`FinalUnits` 存成 raw `Godot.Collections.Array`，而 `BattleSimReportBuilder` 后续继续按 `"skill_attempt_counts"`、`"skill_success_counts"`、`"factions"` 等字符串 key 回读。失败模式：metrics schema/key 漂移会静默少计 attempts/success/faction totals；raw payload 如果在 summary 前被外部修改，summary 会基于被污染的数据。建议让 run report 持有 typed metrics/final-unit snapshot，`ToDictionary()` 只作导出，并补 mutation-boundary regression。

`[medium] scripts/systems/battle/core/meteor_swarm/MeteorSwarmCommitResult.cs:11` - meteor commit result 的 `report_entries` 仍是 `List<GDictionary>`，`BattleSkillOutcomeCommitter` 直接 duplicate 后塞入 common outcome/batch。失败模式：`BattleMeteorSwarmResolver` 或后续改动可注入缺失 `"text"`、`"component_breakdown"`、`"target_summaries"`、`"terrain_summary"` 的 raw report；committer 不会发现 schema 缺失，导致战报存在但 log/trace 内容不完整。建议引入 typed meteor report entry DTO，在 batch/export 边界投影字典，并扩展 meteor commit payload boundary regression。

`[medium] scripts/systems/battle/runtime/BattleRuntimeLootResolver.cs:142` - `_BuildDefeatedUnitLootEntries(...)` 遇到一个 invalid `DropEntryDef` 时直接 `return new List<BattleLootEntry>()`，丢弃之前已解析和后续有效掉落。失败模式：hand-built sim/test template 或 registry 漏过 schema validation 时，一个 stale drop entry 会让敌人的整包掉落为空且无日志，排查困难。建议明确策略：要么 battle-start/content validation 保证 invalid template 不能进入 runtime，要么 resolver 记录错误并跳过 invalid entry，保留 valid drops；对应补 runtime loot regression。

`[low] scripts/systems/battle/core/special_profiles/BattleSpecialProfileRegistry.cs:12` - registry 拥有 `BattleSpecialProfileManifestValidator : RefCounted`，但 `DisposeManagedRegistry()` 只清 collection，没有 dispose validator。失败模式：工具/测试反复创建并 dispose registry 时，小的 native RefCounted validator 会比 registry 活得更久。建议 registry dispose 时同步 dispose validator，并补内部 lifecycle regression。

`[high] scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs:480` - `PromotionPromptContainsChoice(...)` 用 `choiceSelection.Equals(selection)` 校验晋升选择，但 UI / runtime proxy / `CommandChoosePromotionTyped` 都会 duplicate selection 字典。`Godot.Collections.Dictionary` 的相等性在这里不能表达“相同选择内容”。失败模式：合法 world/battle 晋升选项可能因为 selection 不是同一实例而被判为 invalid，停留在 promotion modal 并阻断职业晋升。建议用 member/profession/choice id 或 canonical selection 内容比较，并补合法晋升选择成功回归，覆盖 `CommandChoosePromotionTyped`、`CommandSubmitPromotionChoiceTyped` 和 headless `promotion choose`。

`[high] scripts/systems/persistence/GameSession.cs:1583` - `WriteSaveIndex(...)` 在 `_write_compressed_variant_atomically(...)` 返回错误后仍先调用 `SetSaveIndexCache(...)`，然后返回 `Error.Ok`。失败模式：`CommitRuntimeState` / `CreateNewSave` 会报告成功、清掉 dirty / last error，即使 `user://saves/index.dat` 没写成功；当前进程中的 slot cache 与磁盘状态不一致，IO/权限故障也不可见。建议加 index 写失败注入回归：commit 返回底层错误、`HasPendingSave()` 保持 true、`last_error` 记录错误，并验证 slot list 不只依赖失败后的内存 cache。

`[high] scripts/player/progression/ProgressionContentRegistry.cs:2407` - registry 辅助 `TryGetValue(...)` 会在 `StringName` key 未命中时回退到 string key，但正式 `ReplaceTypedIndex(...)` 只把 `Variant.Type.StringName` key 放入 typed index。失败模式：string-key-only 的 skill/profession/achievement 等内容可能被部分验证/对象读取路径当作存在，但正式 typed catalog 丢弃该项，运行期 typed lookup 失败。按兼容策略，这里不应新增 string-key 兼容路径；建议显式拒绝/报错非 `StringName` key，并补 validation 与 typed getter 分叉回归。

`[high] scripts/player/progression/SkillContentRegistry.cs:2053` - `level_overrides` 的 int 字段校验走宽松 `DictInt(...)`，会接受 bool、float、string、StringName 并把非法字符串当 0；而 `CombatSkillDef.GetEffectiveAreaValue/GetEffectiveRangeValue/GetEffectiveMaxTargetCount/GetEffectiveAttackRollBonus` 运行时直接 `AsInt32()` 读取 override。失败模式：非法 override 类型通过内容验证后，在 effective combat profile 查询时崩溃或静默改值。建议 schema 回归覆盖 `area_value="6"`、`range_value=true`、`attack_roll_bonus="bad"`、`max_target_count="two"`，要求 validation 报错。

`[medium] scripts/player/progression/PartyMemberState.cs:410` - `FromDictionary(...)` 只要求 `body_size_category` 能解析为非空 `StringName`，未用 `BodySizeContentRules.IsValidBodySizeCategory(...)` / `BodySizeMatchesCategory(...)` 校验类别合法性和 `body_size` 一致性。失败模式：存档可载入 `body_size=2, body_size_category="boss"` 或未知类别；后续投影到 `BattleUnitState.SetBodySizeCategory(...)` 或 UI/runtime 读取时会抛异常或带着不一致体型继续运行。若要做存档修复路径需先确认兼容策略；当前建议先补 `PartyMemberState/PartyState.FromDictionary` schema 回归覆盖 unknown category 与 mismatch。

`[medium] scripts/player/warehouse/ItemContentRegistry.cs:580` - weapon `damage_tag` 只检查非空，合法 physical damage tag 校验只在 item tags 包含 `melee` 时执行。失败模式：ranged weapon/template 可以用 `damage_tag="poison"` 之类非 `physical_slash/physical_pierce/physical_blunt` 的值通过 registry；`ItemDef.GetWeaponPhysicalDamageTag()` 随后投影为空，依赖 `use_weapon_physical_damage_tag` 的战斗伤害会在运行时进入 invalid damage tag。建议 item registry fixture 覆盖 ranged weapon invalid `damage_tag` 应被 `ValidateTyped()` 拒绝。

`[low] scripts/player/warehouse/WeaponProfileDef.cs:136` - `NormalizePropertiesMode(...)` 把非法 `properties_mode` 静默归一成 `INHERIT`，`ItemContentRegistry` 不报告配置错误。失败模式：内容作者写错 add/remove/replace mode 后，weapon properties 继承行为悄悄改变，影响 versatile/two_handed 等武器投影。建议加 item registry schema 回归：非法 `properties_mode=99` 必须报错。

`[high] scripts/systems/battle/ai/BattleAiDecisionEngine.cs:174` - `BattleAiSafetyGate` 只作为 helper/test 存在，没有接入 scored decision 选择。失败模式：带 `post_action_is_lethal_survival_risk` 或 escape 后不降险的高分候选仍可凭 bucket/score 赢过低分安全候选，AI 会选择显式不安全动作。建议补 `ChooseCommand` 级 safety gate 回归：高分候选满足 `BattleAiSafetyGate.GetRejectionReason(scoreInput) != ""` 时，应选择低分安全候选或 fallback。

`[high] scripts/systems/battle/ai/BattleAiScoreService.Effects.cs:870` - AI 评分把 `BattleEffectKind.Execute` 当普通 `burst_damage` 累加，没有复用 `BattleExecutionRules` / damage resolver 的低血处决、豁免、staged finisher 致死分支。失败模式：执行器实际能 fatal execute 或 staged finisher 击杀低血目标，但 AI 的 lethal count、damage estimate 和 target priority 不增加，导致处决技能被低估。建议新增 execute AI scoring regression，对比 score input 与 `BattleDamageResolver` 预览/执行结果。

`[medium] scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs:318` - `BattleEffectKind.Execute` 不算 damage/offensive affordance；`BattleTypedEnums.IsAiOffensiveEffect(...)` 与 `BattleAiTypedActionHelper.EffectListHasHostileThreat(...)` 同样未把 execute-only 效果作为 AI 威胁。失败模式：无额外 tags 的 execute-only 主动技能不会正确进入伤害角色、威胁索引和目标优先级，AI 只能依赖人工 tag 补偿。建议在 affordance / role-threat 回归中加入无额外 tags 的 execute-only unit skill。

`[medium] scripts/systems/battle/rules/BattleAttackCheckPolicyService.cs:969` - attack-roll modifier 的 distance filter 使用单位 anchor Manhattan distance，而不是 `BattleGridService.GetDistanceBetweenUnits(...)` 这类 footprint distance。失败模式：2x2 等大型单位与 1x1 单位贴边时，`distance_max_inclusive` / `distance_min_exclusive` 地形命中修正会错误套用或跳过。建议补多格 footprint 的 attack modifier distance 回归，断言 modifier bundle 使用 footprint 语义。

`[medium] scripts/systems/battle/terrain/BattleTerrainTopologyService.cs:275` - flow direction 传播读取邻居旧的 `base_terrain`，同一批 typed topology changes 内看不到邻居 pending flow。失败模式：新生成多格水道只有贴 outlet 的格子变 `flowing_water`，上游格子需要第二次 reconcile 才可能变化，移动/显示/拓扑结果不是一次幂等。建议补 1x3 或 L 形水道一端 outlet 回归：第一次 reconcile 后整段流向连续，第二次 reconcile 无新增变化。

### 子代理覆盖记录

- enemies：已审查 32/32 个文件；未覆盖文件：无。
- player：已审查 89/89 个文件；未覆盖文件：无。
- ui-utils：已审查 51/51 个文件；未覆盖文件：无。
- battle-core-runtime：已审查 121/121 个文件；未覆盖文件：无。
- battle-ai-rules：已审查 80/80 个文件；未覆盖文件：无。
- runtime-systems：已审查 96/96 个文件；未覆盖文件：无。

## 完成状态

- 6 个 GPT-5.5 xhigh 子代理分片均已完成。
- 所有子代理 findings 已按文件/行号做主线程复核，并写入上方 `Findings`。
- `scripts/**/*.cs` 覆盖总数为 469/469，无遗漏文件。

## 验证记录

- `dotnet build magic.csproj`：通过，0 warnings / 0 errors。
- 分片覆盖核对：无遗漏文件、无重复分配。

## 修复记录

日期：`2026-06-17`

本轮已修复上方 25 项已确认 findings，范围包括：

- AI 候选正式预览校验、typed tag 读取、safety gate 接入、execute-only 技能威胁/评分/affordance 处理。
- battle runtime lifecycle 清理、loot invalid entry 跳过、meteor report entry schema 边界、sim report 字典/数组复制边界。
- save index 写失败返回错误、promotion selection 结构化比较、progression registry key 精确匹配、skill level override 严格 int 校验、party body size schema 校验。
- weapon profile damage tag / properties mode 内容校验、attack modifier footprint distance、terrain topology 批量 flow 传播。
- WorldMapView 空 grid 输入保护、display settings malformed cfg 回退、mounted submap sibling path 复用、battle/shop UI 文本和显示状态。

新增/扩展回归：

- `tests/runtime/persistence/run_display_settings_service_regression.cs`
- `tests/runtime/validation/run_world_map_content_validator_typed_regression.cs`
- `tests/progression/schema/run_battle_save_skill_schema_regression.cs`

本轮验证：

- `dotnet build magic.csproj`
- `godot --headless -s res://tests/runtime/persistence/run_display_settings_service_regression.cs`
- `godot --headless -s res://tests/runtime/validation/run_world_map_content_validator_typed_regression.cs`
- `godot --headless -s res://tests/battle_runtime/runtime/run_save_index_resilience_regression.cs`
- `godot --headless -s res://tests/runtime/facade/run_battle_session_promotion_prompt_regression.cs`
- `godot --headless -s res://tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs`
- `godot --headless -s res://tests/world_map/ui/run_settlement_shop_window_schema_regression.cs`
- `godot --headless -s res://tests/runtime/validation/run_item_recipe_registry_typed_regression.cs`
- `godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_intent_safety_gate_regression.cs`
- `godot --headless -s res://tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs`
- `godot --headless -s res://tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.cs`
- `godot --headless -s res://tests/battle_runtime/terrain/run_battle_terrain_topology_service_regression.cs`
- `godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs`
- `godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs`
- `godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`
- `godot --headless -s res://tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs`
- `godot --headless -s res://tests/progression/schema/run_battle_save_skill_schema_regression.cs`

全部通过。
