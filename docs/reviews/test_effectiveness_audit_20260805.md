# 测试有效性逐文件审计（2026-08-05）

> 时点审计报告。覆盖 tests/ 下全部测试入口（461 个文件：444+ 个 `run_*.cs` headless 回归 + tests/tooling 的 Python 测试），每个文件逐字精读、逐个 Test* 方法判定。
> 判定口径：
> - **有效**：断言针对被测系统的行为/状态/边界/错误路径，具体且可能失败；
> - **偏弱**：存在弱断言模式（仅非空/Count>0、Set 后立即 Get 回读、回读 .tres 数据常量、源码字符串 Contains 断言、断言消息与内容不符、文件内个别空方法），但整体仍有真实验证；
> - **无效**：恒真断言、断言不执行、与即时自生成输出对比、整文件零断言的工具脚本、catch 吞失败。

## 总体统计

| 判定 | 数量 | 占比 |
|---|---|---|
| 有效 | 432 | 93.7% |
| 偏弱 | 25 | 5.4% |
| 无效 | 4 | 0.9% |
| 合计 | 461 | 100% |

分批次统计：

| 批次 | 范围 | 有效 | 偏弱 | 无效 |
|---|---|---|---|---|
| 01 | battle_runtime/runtime（前 54） | 50 | 4 | 0 |
| 02 | battle_runtime/runtime（后 53） | 52 | 1 | 0 |
| 03 | battle_runtime/ai + status + ui + presentation | 46 | 4 | 0 |
| 04 | battle_runtime/rules + skills | 53 | 2 | 0 |
| 05 | battle_runtime/fate + objectives + state_schema + rendering + terrain + benchmarks | 38 | 3 | 2 |
| 06 | battle_runtime/simulation + world_map/runtime + world_map/schema | 40 | 2 | 0 |
| 07 | world_map/ui + runtime/validation + runtime/persistence + runtime/facade | 45 | 2 | 0 |
| 08 | runtime/lifecycle + contracts + text_runtime + shared + static_analysis + tooling(py) | 26 | 2 | 2 |
| 09 | progression/core + progression/schema | 49 | 1 | 0 |
| 10 | progression/fate + identity + warehouse + equipment + e2e | 33 | 4 | 0 |

## 无效文件（4 个，均非"假断言"而是零断言工具/空转）

1. `tests/battle_runtime/benchmarks/run_longsword_3v3_mastery_analysis.cs` — 0 断言，纯统计聚合输出，仅战斗完成率 exit 门槛。
2. `tests/battle_runtime/benchmarks/run_mixed_2s1a_mirror_analysis.cs` — 同上，0 断言分析脚本。
3. `tests/static_analysis/run_contingency_autocast_no_known_spoof_regression.cs` — 空转：`ExtractMethodBody` 找不到方法时返回空串使后续负向 Contains 断言恒真（:48-56）；且该文件当前已从工作树删除，完全不运行。
4. `tests/text_runtime/tools/run_text_command_repl.cs` — 0 断言交互式 REPL 开发工具，被套件排除。

注：4 个"无效"全部是工具脚本混入测试命名或已删除文件残留，不存在"看起来在验证实际恒通过"的活跃假测试。

## 偏弱文件汇总（25 个，文件:行号证据见下文各批次明细）

- battle_runtime/runtime：`run_barrier_architecture_contract_regression.cs`（源码字符串 Contains + 文件存在性断言）、`run_battle_ground_effect_typed_sets_regression.cs`（4 个空 Test 方法）、`run_battle_shield_service_typed_context_regression.cs`（1 个空 Test 方法）、`run_battle_unit_factory_weapon_projection_regression.cs`（1 个空 Test 方法）
- battle_runtime/runtime：`run_meteor_swarm_preview_surface_contract_regression.cs:114`（自比恒真断言，同行已有真实断言）
- battle_runtime/ai：`run_ai_trace_recorder_regression.cs`（源码 Contains）、`run_battle_ai_unit_skill_candidate_evaluator_regression.cs`（源码 !Contains）、`run_mist_harrier_level_regression.cs`（.tres 常量回读为主）、`run_wolf_alpha_content_regression.cs`（.tres 常量回读为主）
- battle_runtime/rules + skills：`run_attack_roll_modifier_bundle_regression.cs`（空调用空断言方法）、`run_passive_status_orchestrator_regression.cs`（同上）
- battle_runtime/rendering + terrain + benchmarks：`run_battle_board_render_profile_schema_regression.cs`（仅 2 条 Set→Get 回读）、`run_battle_cell_state_owner_api_regression.cs`（7/9 条为回读）、`run_battle_panel_full_refresh_benchmark.cs`（无性能阈值断言）
- battle_runtime/simulation + world_map：`run_battle_balance_simulation.cs`（零断言 balance 运行器）、`run_world_map_view_color_config_regression.cs`（.tscn 默认值回读为主）
- runtime/validation：`run_enemy_content_registry_typed_regression.cs`（1 个方法为 .tres 回读）、`run_resource_validation_regression.cs`（1 个方法约 60 条 .tres 回读）
- text_runtime：`run_text_command_quest_progress_regression.cs`（对纯数据 fact 类约 20 条恒真断言）、`run_text_command_script.cs`（仅冒烟门槛）
- progression：`run_identity_sub_registry_schema_regression.cs`（1 个方法仅 Count>0）、`run_bloodline_ascension_regression.cs`（空 helper 致 1 个方法零断言）、`run_identity_payload_validator_regression.cs`（12 个用例仅断言 errors.Count>0）
- warehouse + equipment：`run_skill_book_item_helpers_regression.cs:97`（仅 errors.Count>=4）、`run_equipment_drop_service_regression.cs:114-116`（空 Test 方法）

## 体系性发现（超出单文件层面）

1. **失败捕获链路完整可靠**：TestHarness.Finish → ExitCode=1 → ShutdownReport 强制 exit=1 → runner 收集非零退出码；CI 以 `--fail-on-output-error --lifecycle-correctness` 运行。全审计未发现一处吞失败的 catch 或恒真快照基线。
2. **CI 覆盖缺口**：tests/e2e（7 个，质量高）与 tests/tooling（Python，质量高）均不在 CI 门禁中；其中 tooling 的 `test_run_regression_suite.py` 在 git HEAD 上有一个已腐烂失败的 `--jobs 16` 断言（工作树已改未提交）。
3. **static_analysis 防护整体停用**：两个 guard 测试在工作树均已删除（仅剩孤儿 .uid），目录的静态防护当前不运行。
4. **空 Test 方法共 10 处**（见各批次明细），多为"AssertPlainType 类 helper 被清空后调用点残留"，虚报用例数。
5. **runner 命名约定风险**：套件只收集 `run_*.cs`，命名不符的测试会被静默漏跑，无告警。

---

以下为逐文件明细（10 个批次）。


## 批次 01 明细

# 测试有效性审计 — batch01

范围：`tests/battle_runtime/runtime/` 下按文件名排序的前 54 个 `run_*.cs`
（从 `run_barrier_architecture_contract_regression.cs` 到 `run_frostbite_weapon_ability_regression.cs`，含）。
每个文件均已打开精读全文（含多 Test* 方法逐个检查）。

---

### tests\battle_runtime\runtime\run_barrier_architecture_contract_regression.cs — 偏弱
- 断言数: 约16
- 验证内容: 屏障相关运行时代码文件存在性 + 源码中是否使用 typed changed-coords API。
- 证据/问题: `run_barrier_architecture_contract_regression.cs:26-53` 7 处断言均为 `FileAccess.FileExists`（文件存在即过）；`:70-79` `AssertSourceUsesTypedChangedCoords` 直接对 .cs 源码字符串做 `source.Contains("runtime._append_changed_coords_typed(batch, coords);")` 断言，属源码文本匹配而非行为验证；`:90-96` `RequireNull` 为从未调用的死方法。整体是脆弱的源码文本/文件存在性契约，无任何运行时行为断言。

### tests\battle_runtime\runtime\run_barrier_geometry_contract_regression.cs — 有效
- 断言数: 约13
- 验证内容: 屏障几何服务的 footprint 越界分类、投影线穿越、坐标内外判定。
- 证据/问题: 精确断言大体积单位 footprint 越界（:31-42）、inside-to-inside 不阻挡（:58-63）、线穿越正反例（:69-92）、CoordInsideBarrier 正反例（:98-105），全部针对真实服务输出且可能失败。

### tests\battle_runtime\runtime\run_battle_barrier_move_cost_regression.cs — 有效
- 断言数: 约5
- 验证内容: 屏障放逐中断移动时 executed path 与移动力扣费只按已抵达锚点计算。
- 证据/问题: 精确断言 `result.Executed=false`、`StoppedByBarrier=true`、executed path 仅含起点（:57-59）及中断时只扣 1 点移动力且未到达目标（:94-95），另有不变量异常兜底（:60-63、:96-99）。

### tests\battle_runtime\runtime\run_battle_barrier_store_typed_regression.cs — 有效
- 断言数: 约6
- 验证内容: 非法 barrier payload 不应清空/改写 live typed barrier store，且 store 读出为副本。
- 证据/问题: 精确断言 malformed payload 注入后 RemainingTu 保持 42、层数保持 1（:25-30），以及写副本不影响 live state（:32-37）。

### tests\battle_runtime\runtime\run_battle_change_equipment_requirement_regression.cs — 有效
- 断言数: 约45
- 验证内容: 战斗换装的需求门禁（预览/执行拒绝、错误码、AP/背包不变）、临时属性与置换装备不纳入需求、同物品多实例的装备/卸装实例保真、非当前行动单位拒绝。
- 证据/问题: 精确断言 preview.allowed、error_code=item_not_equippable/equipment_instance_required/target_not_self、AP 扣费、背包实例签名（:81-132、:249-325、:360-366）。

### tests\battle_runtime\runtime\run_battle_contribution_event_builder_regression.cs — 有效
- 断言数: 约13
- 验证内容: 战斗贡献事件从 Dictionary 解析（含负伤害 clamp 到 0、relation 阵营回退）与 typed 事件回投影。
- 证据/问题: 精确断言 clamp（:42）、relation 回退（:59-63）、投影字段（:86-102）。

### tests\battle_runtime\runtime\run_battle_execute_effect_regression.cs — 有效
- 断言数: 约17
- 验证内容: execute（律令死亡）效果：低 HP 致死与死亡来源投影、高 HP 无效、无视 boss 标记、穿透护盾不改护盾字段、豁免成功附加 soul_fracture 倍率、min_hp 不回血。
- 证据/问题: 精确断言死亡来源与优先级（:39-48）、护盾 snapshot 相等（:114-120）、治疗/护盾倍率 50/40（:144-153）、0 伤害不回血（:169-170）。

### tests\battle_runtime\runtime\run_battle_execute_ground_protocol_regression.cs — 有效
- 断言数: 约9
- 验证内容: ground 目标模式携带 execute 时 preview/issue 直接拒绝且不扣 AP/MP/冷却、不改目标 HP/状态。
- 证据/问题: 精确断言 preview.allowed=false（:26）、AP/MP/冷却不变（:47-49）、目标 HP/状态不变（:65-67）。

### tests\battle_runtime\runtime\run_battle_execute_lethal_regression.cs — 有效
- 断言数: 约10
- 验证内容: PWK 与死亡保护优先级交互：跳过低优先级 death_ward、允许高优先级触发并附加 soul_fracture/last_stand、穿透护盾不改字段。
- 证据/问题: 精确断言 ward 消耗与 last_stand_active（:52-59）、护盾 snapshot 相等（:85-90）。

### tests\battle_runtime\runtime\run_battle_execute_target_gate_regression.cs — 有效
- 断言数: 约14
- 验证内容: execute 目标 HP 阈值门禁：高 HP preview/affordance/issue 拒绝（含中文原因文本）、阈值点允许、boss 标记不绕过。
- 证据/问题: 精确断言阈值 21/20 HP 边界（:29-31、:91-94）、affordance 原因含“生命高于律令死亡阈值”（:50-53）、boss 同门禁（:112-113）。

### tests\battle_runtime\runtime\run_battle_ground_effect_typed_sets_regression.cs — 偏弱
- 断言数: 约25
- 验证内容: 风推连锁位移与 typed affected set、ground 效果结果投影、坐标展开排序、effect 去重。
- 证据/问题: 主体断言有效（风推坐标 :40-50、投影字段 :122-156、square2 展开 :179-181、去重 :205-209）。但存在 4 个空方法：`TestGroundEffectServiceUsesPlainTypedHelperBoundary`（:21-23）、`TestSpecialSkillResolverUsesPlainTypedHelperBoundary`（:54-56）、`TestGroundApplicationResultPublicApiStaysTyped`（:104-106）、`TestEdgeClearUsesTypedPrivateBoundary`（:217-219），且 `AssertResultTypePublicApiStaysTyped`（:108-114）从未被调用——这些是遗留空壳（好在均未被 _Initialize 调用，但属死代码占位）。

### tests\battle_runtime\runtime\run_battle_loot_commit_service_regression.cs — 有效
- 断言数: 约40
- 验证内容: 战利品提交服务：装备实例入仓、旧 alias/StringName 字段拒绝、非法奖励丢弃但结算完成、开战确认门控 TU 推进、满包 overflow 反馈进 snapshot 与日志。
- 证据/问题: 精确断言提交数量与仓库实例（:69-90）、KeyNotFoundException 拒绝路径（:103-123）、TU 确认前不动确认后 +5（:273-317）、overflow entries 与 save lock 释放（:385-416）。

### tests\battle_runtime\runtime\run_battle_loot_drop_luck_regression.cs — 有效
- 断言数: 约25
- 验证内容: per-kill 掉落使用击杀者幸运值（spy service 捕获 DropLuck=-6/0）、固定材料入仓、满包装备丢失计入 overflow、attack_equipment 不隐式掉落。
- 证据/问题: spy EquipmentDropService 精确捕获调用次数与 luck 值（:92-151、:215-236）、overflow entry item/数量（:294-330）。

### tests\battle_runtime\runtime\run_battle_map_panel_schema_regression.cs — 有效
- 断言数: 约35
- 验证内容: BattleMapPanel 应用 HUD snapshot（按钮可用性、标签文本、状态徽章）、pending payload 生命周期清理、TopBar 布局与缩放指示。
- 证据/问题: 精确断言具体文本/数值（:207-218、:275-297）、反射读取私有字段验证清理（:63-104）。

### tests\battle_runtime\runtime\run_battle_metrics_collector_regression.cs — 有效
- 断言数: 约15
- 验证内容: 战斗 metrics typed 聚合（回合/动作/技能/伤害/击杀）与 Dictionary 投影隔离（改投影不污染 typed state）。
- 证据/问题: 精确数值断言（:53-63、:67-88）与污染隔离（:90-105）。

### tests\battle_runtime\runtime\run_battle_move_path_result_projection_regression.cs — 有效
- 断言数: 约17
- 验证内容: 移动路径结果/路径树/执行结果的投影，以及 movement service typed 路径实际执行移动。
- 证据/问题: 精确断言投影字段与坐标（:35-38、:53-59、:82-95）、reachable 集合与移动后坐标（:117-133）。

### tests\battle_runtime\runtime\run_battle_movement_query_service_lifecycle_regression.cs — 有效
- 断言数: 约25
- 验证内容: 移动查询服务生命周期：unbind 保留缓存、epoch 轮换、geometry revision 触发重建、旧 state 单位不再泄露、dispose 后拒绝重绑。
- 证据/问题: 精确断言 RejectReason=missing_unit（:41-45、:77-81）、SnapshotRebuildCount 递增（:91-106）、epoch 变化（:155-174）、ObjectDisposedException（:186-195）。

### tests\battle_runtime\runtime\run_battle_projection_lease_regression.cs — 有效
- 断言数: 约80
- 验证内容: Godot 投影 lease 的容器归属、键序 schema、JSON golden 哈希、重复投影指纹稳定、lease 关闭后拒绝访问、输入深拷贝隔离。
- 证据/问题: golden 为固定常量哈希（:106-110、:173-177、:285-289），属标准快照测试；深拷贝隔离断言具体（:309-431）；Throws<ObjectDisposedException>（:135-138）。

### tests\battle_runtime\runtime\run_battle_rating_projection_regression.cs — 有效
- 断言数: 约6
- 验证内容: rating stats 投影保留字段且修改投影不污染 typed state；stats map 按 member id 投影。
- 证据/问题: 精确断言（:23-31、:45）。

### tests\battle_runtime\runtime\run_battle_runtime_attack_check_smoke.cs — 有效
- 断言数: 约12
- 验证内容: 命中检定的天然 1/20 边界语义（required roll 夹取、显示 2+/20+、95%/5%）与 armor_break 降 AC 提升命中率但不提供承伤易伤。
- 证据/问题: 精确数值断言（:29-38、:52-62、:92-101、:132-136）。

### tests\battle_runtime\runtime\run_battle_runtime_borrower_teardown_regression.cs — 有效
- 断言数: 约60
- 验证内容: BattleRuntimeModule borrower 拓扑拆卸：内容重绑清 AI borrower、state 重绑清 plan/context、装备服务 dispose 后拒绝工作且需显式重绑、正常/异常 teardown 清空全部 borrower 与 audit 基线、双 dispose 幂等。
- 证据/问题: 精确计数与引用断言（:84-97、:186-263、:298-367），异常路径用抛异常的 terrain generator 验证（:346-367）。

### tests\battle_runtime\runtime\run_battle_runtime_terrain_generator_ownership_regression.cs — 有效
- 断言数: 约4
- 验证内容: terrain generator 所有权：默认实例随 runtime dispose、注入实例 caller-owned、替换时释放旧默认实例。
- 证据/问题: 精确 IsDisposed 断言（:22、:33、:45-49）。

### tests\battle_runtime\runtime\run_battle_save_resolver_regression.cs — 有效
- 断言数: 约55
- 验证内容: 豁免解析器：免疫/优劣势/天然 1/20、SaveDegree 升降级、typed status save bonus/tags（旧 params 拒绝）、成功率估算（50%/51%/9%/100%）、caster_spell 动态 DC、锁定技能 bonus 提 DC、partial save 减半、状态豁免成功阻挡/失败替换。
- 证据/问题: 全部为精确数值/枚举断言（:47-49、:62-74、:101-156、:182-200、:347-400、:472-519、:539-584）。

### tests\battle_runtime\runtime\run_battle_shield_service_typed_context_regression.cs — 偏弱
- 断言数: 约30
- 验证内容: 护盾服务：roll context 缓存（typed 与 Godot 边界往返）、apply 结果投影、同/异 family 护盾替换策略的原子六字段。
- 证据/问题: 主体断言强（缓存命中 :29-40、替换策略 snapshot 相等 :160-269）。但 `TestApplyResultPublicApiStaysTyped`（:65-68）被 _Initialize 调用却**无任何断言**——方法体只有一个未使用的 `Type type` 局部变量，是事实上的空测试方法；`IsGodotCollectionOrVariant`（:308-316）为死代码。

### tests\battle_runtime\runtime\run_battle_sim_exception_cleanup_regression.cs — 有效
- 断言数: 约9
- 验证内容: 战斗模拟异常清理：execution loop 失败恢复原 AiTraceRecorder 并保留原异常；模拟失败 dispose runtime 且不释放 caller-owned terrain generator。
- 证据/问题: ReferenceEquals 原异常（:86-93、:142-145）、IsDisposed/释放 borrower/sidecar（:147-166）。

### tests\battle_runtime\runtime\run_battle_sim_report_output_regression.cs — 有效
- 断言数: 约25
- 验证内容: 模拟报告输出：同秒两次写路径互不覆盖且 JSON/JSONL 可解析、写失败回滚已写 artifact 并恢复原 output 契约、不可打开路径抛 IOException。
- 证据/问题: 精确断言路径互异（:76-95）、artifact 清理（:140-146）、报告内嵌路径一致（:196-234）。

### tests\battle_runtime\runtime\run_battle_spawn_reachability_regression.cs — 有效
- 断言数: 约20
- 验证内容: 出生可达性：深水隔断判 invalid 并列名单、平地 valid、双向验证、缺武器技能不可达、结果/失败快照投影。
- 证据/问题: 精确断言 InvalidEnemyUnitIds/InvalidPlayerUnitIds 成员（:53-61、:143-150）、武器门禁（:199-206）、投影字段（:215-253）。

### tests\battle_runtime\runtime\run_battle_spawn_side_regression.cs — 有效
- 断言数: 约15
- 验证内容: 出生边选择（宽图上下长边、竖图左右长边）、占位不清除已有 occupant、失败 placement 完整回滚。
- 证据/问题: 精确坐标半区断言（:54-66、:101-108）、占用保持（:131-157）、回滚后 0 单位 0 占用（:180-188）。

### tests\battle_runtime\runtime\run_battle_state_disadvantage_regression.cs — 有效
- 断言数: 约15
- 验证内容: attack disadvantage 判定：双敌包夹/低 HP/强 debuff/恐惧族/场景标签触发，单敌/错元素/坏选择/经济拖延/软 debuff 不触发；read-view 查询不修复脏几何。
- 证据/问题: 正反两路精确断言（:101-205），read-view 无副作用断言（:56-91）。

### tests\battle_runtime\runtime\run_battle_target_collection_service_regression.cs — 有效
- 断言数: 约6
- 验证内容: 目标收集服务：ground diamond 区域按 y/x 序投影、self/unit 模式消费 typed 单位。
- 证据/问题: AssertCoords 精确比较坐标序列（:48-59、:88-112）。

### tests\battle_runtime\runtime\run_battle_unit_factory_weapon_projection_regression.cs — 偏弱
- 断言数: 约55
- 验证内容: 单位工厂武器投影（徒手/单手/双手/versatile 握持切换）、battle-local 装备视图隔离、effective trait 投影与刷新、装备能力源投影。
- 证据/问题: 主体断言强（骰面/握持 :83-257、隔离 :305-350、trait 增删 :406-464、能力源 :493-526）。但 `TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts`（:529-531）是**空方法且在 _Initialize 中被调用**（:23），属于点名空测试。

### tests\battle_runtime\runtime\run_battle_validation_result_projection_regression.cs — 有效
- 断言数: 约35
- 验证内容: 技能等级访问器（0 级保留）、单位/地面 validation 投影与解析、crit_locked 只认 typed 输入不回填 payload、目标收集排序投影、连锁伤害执行次级目标。
- 证据/问题: 精确断言（:56-86、:114-137、:172-182、:205-228、:346-359、:419-427）。

### tests\battle_runtime\runtime\run_bonecrusher_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 碎骨者真实内容加载（名称/价格/骰）、投影与卸装清理、碎甲重击 AC 组件减半、骨骼粉碎对亡灵/构造体加骰、余震破防层数/扣 AP/行动进度减速。
- 证据/问题: 真实 .tres 内容 + 行为双层断言；固定骰下精确伤害 13/8（:262-298）、AC 20→15（:237-247）、层数序列 {1,2,0}（:308-338）。

### tests\battle_runtime\runtime\run_butcher_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 屠夫真实内容与投影、对 beast/animal 加骰、庖丁解牛对半血目标 +2、屠宰艺术只翻倍持有者击杀掉落、血的不适只对人形击杀且豁免失败触发。
- 证据/问题: 精确伤害 12/7（:175-215）、modifier bundle ±2（:256-261）、掉落翻倍 4 vs 2（:284-311）、恶心状态字段（:350-365）及四路正反例（:367-404）。

### tests\battle_runtime\runtime\run_contingency_autocast_origin_regression.cs — 有效
- 断言数: 约50
- 验证内容: 应急矩阵自动施放：burst 立即执行且零成本零成长、非玩家学习来源不建实例、sequential 按持有者回合逐个释放、非法目标 skip/abort 策略、特殊 profile（流星爆）正式提交。
- 证据/问题: 精确断言资源不变（:127-145）、队列计数 2→1→0（:279-330）、report 词汇（:95-114、:289-308）、suppressed origin 元数据（:146-156）。

### tests\battle_runtime\runtime\run_contingency_battle_lifecycle_regression.cs — 有效
- 断言数: 约35
- 验证内容: 战斗结束生命周期：消耗 setup 先于资源 clamp 释放、死亡成员跳过消耗写回、胜利/逃跑持久化释放、失败回滚 finalize 内存（含 flush/resource commit 失败）、低运 sidecar 回滚后可重试。
- 证据/问题: 精确断言 charged/reserved_mp_max 状态（:58-64、:100-109、:184-201）、回滚后 loot/奖励恢复（:267-286）、typed 错误码（:307-324）。

### tests\battle_runtime\runtime\run_contingency_damage_hook_contract_regression.cs — 有效
- 断言数: 约30
- 验证内容: 伤害 hook 契约：incoming_damage_percent 在 HP 变动前触发护盾、fatal 预判、blink 取消当前伤害、cancel 不阻断同技能后续效果、execute/graded-save 保留 hook 上下文、report 到达 batch 与 runtime、0 伤害不触发。
- 证据/问题: 精确 HP/护盾数值（:58-63、:105-110、:138-148）、hook 调用次数与 origin（:197-251）。

### tests\battle_runtime\runtime\run_contingency_target_resolver_regression.cs — 有效
- 断言数: 约45
- 验证内容: 目标解析器全族：self/trigger_source/trigger_target/nearest_enemy/owner_centered_area/attacker_cell/empty_cell 各偏好与 tie-break、失败 reason id、fallback 策略门控、不可变列表、fatal 逃逸标志。
- 证据/问题: 每个解析器均有精确目标 id/坐标断言（:56-73、:92-98、:195-200、:246-260、:309-314、:336-341、:359-364、:391-396、:570-586）。

### tests\battle_runtime\runtime\run_contingency_trigger_contract_regression.cs — 有效
- 断言数: 约60
- 验证内容: 触发契约：battle-local 实例只覆盖出战成员、release overlay 恢复 MP max、owner_turn 只队列触发者、四类 hook fact 冻结源事实、盟友/抑制 origin 不触发、同 source_event 去重、AoE 冻结 trigger target、候选索引排序、suppressed/depleted report 词汇。
- 证据/问题: 精确断言实例数/MP max（:60-101）、队列内容与冻结坐标（:247-311）、去重（:433-437）、索引顺序（:514-536）。

### tests\battle_runtime\runtime\run_control_status_contract_regression.cs — 有效
- 断言数: 约15
- 验证内容: 控制状态契约：petrified 自豁免失败跳回合清 AP/移动、成功移除状态；madness 失败转 AI 控制（any_unit 策略+回合末清理）、成功恢复。
- 证据/问题: 精确断言 SkipTurn/AiControlled/AiTargetPolicy/资源清零（:43-54、:77-85、:108-122、:145-157）。

### tests\battle_runtime\runtime\run_courage_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 勇气之刃真实内容与投影、无畏被动 fear 免疫随装备存亡、鼓舞装备技能（0AP/60TU/一次性 +2 攻击与豁免并消耗）、勇气冲锋对恐惧目标加骰、孤独无盟友 -2。
- 证据/问题: 精确伤害 13（:265）、modifier -2/0（:280-295）、鼓舞消耗语义（:215-244）。

### tests\battle_runtime\runtime\run_cowardice_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 懦弱之刃真实内容与投影、缝隙背刺孤立目标加骰、正面脆弱 -3、逃窜低血量门控与 60TU 反击预备 advantage 且真实攻击后消耗。
- 证据/问题: 精确伤害 13/7（:164-204）、modifier -3/0（:192-223）、可用性禁用原因（:257-262）、advantage 与消耗（:303-311）。

### tests\battle_runtime\runtime\run_double_edged_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 双面刃真实内容与投影（无防御 AC -2）、双刃攻击双目标+固定反伤+双杀治疗、单刃斩无反伤、装备技能每行动回合各限一次。
- 证据/问题: 精确 HP/体力/AP（:163-167、:193-196）、AC 差值（:115、:128-131）、同回合禁用与下回合恢复（:223-252）。

### tests\battle_runtime\runtime\run_dragon_scale_battleaxe_weapon_ability_regression.cs — 有效
- 断言数: 约70
- 验证内容: 龙鳞之斧真实内容与五 trait 投影（含五色 half 抗性）、龙牙锋刃每回合一次、屠龙制衡只对 dragon、龙鳞护面只减近战物理、破鳞裂痕豁免/叠层/来源绑定命中加值、非武器伤害不吃装备骰。
- 证据/问题: 精确减免数值 8/10/5（:236-266）、裂痕字段与 ±2/0 modifier（:288-342）。

### tests\battle_runtime\runtime\run_dragonbone_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 龙骨斧真实内容与投影、龙焰每回合一次、龙族之恨对 dragon 每次命中生效。
- 证据/问题: 投影精确断言（:92-161）；伤害采用相对比较（firstDamage > secondDamage，:196-214、:273-280）而非绝对值，但仍为有效行为断言。

### tests\battle_runtime\runtime\run_echo_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 回音真实内容与投影、回音投掷直线区域（对角拒绝、路径格/目标精确集合）、40 体力/90TU、余音按受伤敌数叠层、回声斩消耗全部余音加 3D6。
- 证据/问题: 精确坐标/单位集合（:211-219）、HP 25/25/30（:224-226）、余音层数 2（:235）、消耗后 39 HP（:270-275）。

### tests\battle_runtime\runtime\run_encounter_roster_builder_typed_boundary_regression.cs — 有效
- 断言数: 约30
- 验证内容: 遭遇名册构建器 typed 边界：两种输入源构建结果一致（summary 对比）、caster MP 资源解锁、模板认知投影、敌方攻击装备能力源与生物标签、loot preview 一致。
- 证据/问题: summary 对比两独立构建路径（:78-83、:392-397）属有效等价性验证；认知枚举精确（:148-159）、能力源字段（:270-354）。

### tests\battle_runtime\runtime\run_encounter_roster_loot_preview_regression.cs — 有效
- 断言数: 约15
- 验证内容: roster 掉落预览：正式 schema 字段（来源 kind/id/label、稳定 entry id、数量聚合）、缺 label 拒绝、按 item_id 合并数量。
- 证据/问题: 精确字段断言（:93-123）、数量 5=2×1+1×3（:271-275）。

### tests\battle_runtime\runtime\run_enemy_template_attribute_projection_regression.cs — 有效
- 断言数: 约20
- 验证内容: 敌方模板 typed 属性覆盖经 EncounterRosterBuilder 投影进战斗单位（六维、hp/stamina/AP/AC/闪避、elite fortune 标记、技能等级）。
- 证据/问题: 逐属性精确断言（:54-74、:99-154）。

### tests\battle_runtime\runtime\run_eternity_edge_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 永恒之刃真实内容与五 trait 投影、永恒伤口阻疗、时间窃取/时间债 3 层封顶与回合末消债、时间闭环消耗老化加 2D8、击杀净消 1 层债。
- 证据/问题: 精确 HP/层数序列（:237-272、:297-312、:345-358）。

### tests\battle_runtime\runtime\run_executioner_axe_weapon_ability_regression.cs — 有效
- 断言数: 约120
- 验证内容: 处刑者之斧全链路：真实内容与内部技能不可见/不可学、死亡判决成本/冷却/未命中保标记、标记命中强制暴击与判决结算、禁暴击抑制、击杀 provenance 归属、普通/elite/boss 阈值分支、死亡保护触发自我处刑、到期/卸装/耐久摧毁清理、并发双来源独立到期与镜像恢复、未标记击杀不触发恐惧。
- 证据/问题: 17 个场景全部精确断言（如 :263-283、:305-326、:589-601、:664-676、:946-970、:1019-1038），AssertNoInternalSkillIdentity 反向验证日志不泄露内部技能（:1473-1507）。

### tests\battle_runtime\runtime\run_flame_morningstar_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 火焰晨星真实内容与投影（含 fire 免疫随装备存亡）、火焰打击固定骰下 1D8+2 + 1D6 fire、roll_gate 10 失败/11 成功控制 burning。
- 证据/问题: 精确伤害 6/8（:172-177）、roll gate 正反例（:197-222）。

### tests\battle_runtime\runtime\run_frost_mace_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 冰霜锤真实内容（含“极地适应未落地”反向断言）与投影、冰冻打击 1D6 cold、封印之力对 undead/fiend 额外 2D6、同目标第三击 slow 且计数不串目标。
- 证据/问题: 精确伤害 6/9/20（:220-257）、计数层数与 slow 语义（:266-318）、负向断言未注册占位 trait/binding（:63-82）。

### tests\battle_runtime\runtime\run_frostbite_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 霜咬真实内容与投影（含寒冷免疫）、霜冻之触 1D6 cold 与第三击 slow 降移动力上限、冰封之路相邻水域验证（非水域/非相邻拒绝）、地形替换 ice、60 体力/120TU 冷却门控。
- 证据/问题: 精确伤害 8（:197）、移动力上限 1（:218-219）、validation 拒绝（:285-293）、地形与冷却（:304-313）。

---

## 统计

- 有效: **50**
- 偏弱: **4**
- 无效: **0**

### 偏弱清单
1. `run_barrier_architecture_contract_regression.cs` — 仅文件存在性检查（:26-53）+ 对源码字符串 Contains 断言（:70-79）+ 死方法 RequireNull（:90-96）。
2. `run_battle_ground_effect_typed_sets_regression.cs` — 4 个空 Test 方法（:21-23、:54-56、:104-106、:217-219）及从未调用的断言帮助方法（:108-114）；其余方法有效。
3. `run_battle_shield_service_typed_context_regression.cs` — `TestApplyResultPublicApiStaysTyped`（:65-68）被调用但无断言（空测试方法）；其余方法有效。
4. `run_battle_unit_factory_weapon_projection_regression.cs` — `TestBattleUnitFactoryUsesTypedSkillLevelsAndResourceCosts`（:529-531）空方法且在 _Initialize 中被调用（:23）；其余方法有效。

### 无效清单
（无）

## 批次 02 明细

# 测试有效性审计报告 — batch02

范围：`tests/battle_runtime/runtime/` 按文件名排序第 55-107 个 `run_*.cs`（run_frostbite_weapon_ability_regression.cs 之后一个文件起至末尾，共 53 个）。每个文件均已打开精读全文并逐个 Test* 方法检查。

通用结构说明：本批绝大多数文件是"武器装备能力回归"测试，固定模式为：真实内容快照断言（item/trait/binding/SkillDef 存在性）→ 原始 .tres 逐字段比对（名称/价格/骰子/属性，期望值是独立手写的设计常量，可真实失败）→ 真实 unit 工厂投影断言 → 固定骰/固定豁免下的真实战斗命令行为断言 → 卸装回滚断言。凡判定"有效"的文件，其断言均针对被测系统行为且具体可失败；不再逐条复述。

### tests/battle_runtime/runtime/run_giants_heel_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 巨人之踵装备需求（body_size）、力量门槛攻击惩罚、巨人杀手附伤/优势、斩踵豁免失败状态链（lockout/prone/hobbled 不刷新、0伤害/中型/免疫边界）。
- 证据/问题: 精确断言 -4/0 modifier delta、2D8 附伤、lockout 50TU 不刷新、豁免成功/免疫/0伤害分支（:182-336）。

### tests/battle_runtime/runtime/run_glory_weapon_ability_regression.cs — 有效
- 断言数: 约45
- 验证内容: 荣耀之刃投影/卸装、众目睽睽计数封顶与死亡单位剔除、孤独之暗 -2、spotlight 光耀附伤、谢幕斩不耗 AP 且只打 5 尺内敌人。
- 证据/问题: 精确断言净值 -1、死亡后 3→2、追击目标 HP<100 而远目标 HP==100（:144-275）。

### tests/battle_runtime/runtime/run_glutton_weapon_ability_regression.cs — 有效
- 断言数: 约40
- 验证内容: 贪食者投影、未击杀叠加饥饿层、吞食斩消耗饥饿追加 1D6、饱食击杀按实际伤害 50% 治疗且击杀路径不再加饥饿。
- 证据/问题: 固定骰精确断言 HP 38/34/46、饥饿层数与 120TU（:187-244）。

### tests/battle_runtime/runtime/run_gorgon_crossbow_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 蛇发女妖之弩投影、石化凝视 DC16 slow 60TU、三次命中完全石化 DC18 并消耗计数、石像崩裂附伤与击杀扩散（30TU 短 slow、不覆盖更长 slow、只影响相邻敌人、豁免成功不施加）。
- 证据/问题: 逐层计数断言、豁免成功/失败双路径、扩散边界（非相邻/友军）精确断言（:189-434）。

### tests/battle_runtime/runtime/run_heartbane_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 噬心者投影、暴击才追加 psychic 骰（对照剥离能力源）、30%HP 阈值 +3、情感撕裂叠层/source-bound 惩罚只作用于来源、心碎爆发 3 层门禁+消耗+每场 1 次。
- 证据/问题: 暴击/非暴击对照、阈值 12/40 边界、门禁失败不消耗次数（:218-565）。

### tests/battle_runtime/runtime/run_hunter_axe_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 猎人之斧固定 3 级猎人标记装备入口（不写已学列表）、未学习时不建进度、已学 4 级取高等级且标记命中后给熟练度。
- 证据/问题: 80TU/100TU 时长差异、熟练度前后对比（:151-258）。

### tests/battle_runtime/runtime/run_last_lesson_weapon_ability_regression.cs — 有效
- 断言数: 约40
- 验证内容: 最后一课投影、无伤害回合才积累教诲（上限 3）、两个装备技能消耗教诲写 next-attack advantage/最大化骰、教室气息 -3 封顶不改基础 AC、空教室 -1。
- 证据/问题: 教诲层数 1→1→3、advantage 命中后清除、对照最大化伤害（:201-469）。

### tests/battle_runtime/runtime/run_lumberjack_axe_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 伐木工之斧劈痕标记（来源绑定、未命中不刷新、卸装失效）、顺纹连斩 +1/1D4、植物杀手叠加、击杀回 AP（无每回合限制、封顶 2、错来源/错装备/非攻击不触发）。
- 证据/问题: 精确伤害 6/9/13/15、AP 0→1→2→2、体力不恢复（:137-337）。

### tests/battle_runtime/runtime/run_lunareclipse_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 月蚀投影、月相 3 层触发盈月裁断 2D8 并回 1 层、月蚀影步 blink 穿阻挡落点/AP/体力/冷却/闪避状态、重甲禁用入口。
- 证据/问题: 精确 HP 82/68、坐标 (3,0)、重甲 DisabledReason（:211-313）。

### tests/battle_runtime/runtime/run_memoryeater_vine_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 噬忆血蔓 6 trait 投影、生命簿持久计数只对活物击杀 +1（构装/亡灵/无 provenance 不增）、血阶 floor_div/10 同步、血阶驱动附伤成长、追刺链击杀也计数。
- 证据/问题: 计数 9→10、血阶 0/1/3 伤害递增、61/6 链式同步（:191-382）。

### tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.cs — 有效
- 断言数: 约20
- 验证内容: committer 从 typed MeteorSwarmCommitResult 提交且 report entry/component 深拷贝不被后续修改污染；DTO 去重/typed 访问契约。
- 证据/问题: 提交前把 text 改 "mutated"、component damage 改 99，断言提交结果仍是 "original summary"/12（:97-132）；去重断言（:153-164）。

### tests/battle_runtime/runtime/run_meteor_swarm_manifest_gate_regression.cs — 有效
- 断言数: 约35
- 验证内容: 陨星雨 skill 切 special profile、registry 校验通过、validator 五类负例（非默认测试入口/未知 save_profile/重复 component/越界 ring）、gate 通过放行与空视图 fail-closed。
- 证据/问题: 每个负例先 Duplicate 再篡改，断言 errors.Count>0；fail-closed 文案与 debug details 精确断言（:160-260）。

### tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.cs — 偏弱
- 断言数: 约25
- 验证内容: 陨星雨 preview facts 与 HUD/AI 共享同一 preview_fact_id、49 格目标、友伤摘要与 AI 评分输入。
- 证据/问题: 存在恒真断言：:114 `_test.Eq(preview.hit_preview?.Source ?? "", preview.hit_preview?.Source ?? "", "preview source 应稳定。")` 自比恒真（同文件 :113 已用字面量 "special_profile_preview_facts" 做了真实断言，:114 是冗余恒真）。其余断言（preview_fact_id 三端一致、49 格、AI 估算）均真实有效，故整体偏弱。

### tests/battle_runtime/runtime/run_mountainbreaker_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 裂山者力量 20 装备门槛、山崩击 prone、断层追击对已倒地目标附伤、地脉定锚 22 点能力检定破坏地形（battle lifetime、+1 移动成本、同格一次、自然 20 自动成功）。
- 证据/问题: 19+3=22 失败 / 19+4=23 成功 / nat20 精确断言、1000TU 后仍保留（:142-226）。

### tests/battle_runtime/runtime/run_movement_query_typed_result_regression.cs — 有效
- 断言数: 约30
- 验证内容: 移动查询服务返回 typed result（反射签名）、typed 坐标内容、缓存 miss/hit 计数、decision rebind 不重建 snapshot、actor workspace 跨 focus target 复用且结果与 overlay 参考一致。
- 证据/问题: 精确坐标 (1,0)、PathTargetCacheHit 1L、workspace build 1L 不重建（:78-276）。

### tests/battle_runtime/runtime/run_oathscar_weapon_ability_regression.cs — 有效
- 断言数: 约90
- 验证内容: 誓约之痕投影与 typed payload、誓约绑定切换目标清旧印、目标死亡解除誓约且防反噬、未配置 mark 不被死亡清理、誓约之印叠层上限 4 与来源绑定 +4、誓约裁决门禁/必中/消耗/每场 1 次、背誓反噬自伤。
- 证据/问题: mark store 计数、+4/0 modifier、门禁失败不扣次数、miss resolver 下仍必中（:166-774）。

### tests/battle_runtime/runtime/run_phoenix_bow_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 凤凰之弓投影/卸装、火焰箭 2D6 fire 附伤、roll_gate 10 不点燃 / 11 点燃 burning 60TU。
- 证据/问题: 对照剥离能力源精确断言 6 vs 14、gate 双分支（:169-219）。

### tests/battle_runtime/runtime/run_plague_tongue_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 瘟疫之舌投影、毒素附伤、斧刃热 DC14 豁免门控与 60TU 周期 1D4、瘟疫云 60TU 延迟生成 5 格 battle lifetime、接触感染与携带者免疫。
- 证据/问题: 55TU 不生成/60TU 生成、首跳 -4HP、豁免成功/友军免疫分支（:324-449）。

### tests/battle_runtime/runtime/run_prismatic_random_chain_breaker_preview_regression.cs — 有效
- 断言数: 约20
- 验证内容: 随机链破层 preview：候选池保留、不伪造确定目标、maxHits=2 时后续命中候选/伤害预览存在、maxHits=1 时不伪造、重复 preview 无副作用、正式执行首击破层后续伤害。
- 证据/问题: 两种 maxHitsPerTarget 分支断言互斥、ActiveLayerId 预览前后不变（:104-193）。

### tests/battle_runtime/runtime/run_prismatic_sphere_regression.cs — 有效
- 断言数: 约200
- 验证内容: 虹光法球全体系：单层 ward、熟练度只记 1 次、7 层有序、层伤害抗性减免、投射类别逐层阻挡、多类别有序破解、未匹配穿透、cast variant projectile 覆盖、装备投射类别边界、地面 AoE/地形/破解/auto-cast/读条裁剪、顺序破解链、几何穿越判定、绿层即死免死链、紫层放逐、石化自检、驱散关系。
- 证据/问题: 每个 Test* 均有精确层 id/坐标/HP 断言，预览不修改正式状态的负断言贯穿全文件。

### tests/battle_runtime/runtime/run_prismatic_sphere_special_entry_regression.cs — 有效
- 断言数: 约35
- 验证内容: 特殊入口与虹光法球交互：冲锋移动/被推动单位跨界触发层伤害、路径步 AoE 裁剪、四类重复攻击入口（手动/自动/读条/随机链）被屏障拦截、连锁伤害逐跳检查。
- 证据/问题: 坐标 (3,2)/(4,2) 精确断言、目标 HP 不变负断言（:51-288）。

### tests/battle_runtime/runtime/run_ravenplume_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 鸦羽投影与 typed summon payload、鸦群召唤真实建单位且 12 只封顶、鸦羽遮蔽 -4/群鸦喧嚣 +2、群鸦之宴消耗 4 鸦 4D6 并写不可转化标记、鸦不足禁用。
- 证据/问题: crows.Count==12、乌鸦 HP1/AC12/心智、前后鸦数差 4（:149-292）。

### tests/battle_runtime/runtime/run_repeat_attack_decay_multiplier_regression.cs — 有效
- 断言数: 约12
- 验证内容: 连击衰减倍率纯函数：50% 复合 100/50/25、整数截断 12（非四舍五入 13）、200% 放大复合、0/负回退 100、阶段效果写入 pre_resistance 倍率、等额不重建效果。
- 证据/问题: 截断行为精确断言（:68-72）、ReferenceEquals 身份断言（:132-135）。

### tests/battle_runtime/runtime/run_rock_halberd_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 岩石之戟投影、石之泪石化豁免免疫+感知豁免 +1 不改属性、石化之触 DC14 slow、三次命中 DC16 paralyzed、计数按目标隔离。
- 证据/问题: 跨目标计数 2/1/3 隔离断言、免疫目标不 slow（:165-349）。

### tests/battle_runtime/runtime/run_rustanchor_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 锈锚投影、沉锚守势状态（forced_move_immune、移动力 -1、物理减免 3 不免元素）、移除后恢复可被推动、锈链入肉 STR 豁免门控、不归斩对已减速/锈链目标附伤、0 伤害不触发。
- 证据/问题: 减免 3/3/0、推 0 步 vs 1 步对照（:178-292）。

### tests/battle_runtime/runtime/run_rustoath_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 锈蚀之誓投影、锈蚀叠 5 层、护甲锈蚀 5 层时只扣金属甲耐久一次（30→6、皮甲不扣、第六击不再扣）、腐朽之刃对锈裂目标追加 acid、锈粉风暴 5 层门禁+消耗。
- 证据/问题: 耐久精确 30/6、日志 "耐久 30 -> 6"、皮革对照（:248-495）。

### tests/battle_runtime/runtime/run_sacred_hammer_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 神圣之锤投影（含占位 trait/binding 不再注册的负断言）、命中追加 1D8 radiant、对 undead 再追加 3D6、神圣治疗 2D8、per_world_day 3 次与同回合一次限制、跨世界日恢复。
- 证据/问题: 精确伤害 6/9/15、治疗 10→19、禁用原因字符串（:195-415）。

### tests/battle_runtime/runtime/run_sands_time_weapon_ability_regression.cs — 有效
- 断言数: 约40
- 验证内容: 时间之沙投影、时间加速 AP 翻倍且每场 1 次（同回合/跨回合同禁用）、时间减速下回合 AP 归零但保留移动、时间错乱 50%/200% 行动与读条进度。
- 证据/问题: AP 2→3→0、强制时序骰 1/20 下的 5/20 进度（:146-256）。

### tests/battle_runtime/runtime/run_save_index_resilience_regression.cs — 有效
- 断言数: 约20
- 验证内容: 损坏 save index 不阻塞新建档并能重建索引（Variant Dictionary 格式）、schema 拒绝字符串时间戳/旧 top-level Array/字符串 version 并回写当前 version=4。
- 证据/问题: 真实写入 0xCC 损坏字节与旧格式夹具后断言恢复行为与文件 schema（:38-167）。

### tests/battle_runtime/runtime/run_scorpion_bow_weapon_ability_regression.cs — 有效
- 断言数: 约40
- 验证内容: 蝎子之弓投影（毒免 damage+save immunity）、蝎毒箭 1D8 poison、DC15 豁免失败 paralyzed、毒血只反伤相邻攻击者且豁免门控。
- 证据/问题: 卸装后免疫不残留负断言、3 格远程攻击者不中毒（:95-356）。

### tests/battle_runtime/runtime/run_shieldbreaker_guard_breaker_regression.cs — 有效
- 断言数: 约30
- 验证内容: 破盾者配置声明（+3 bonus、MaxTargetRarity=COMMON）、只对持盾目标 +3 且进入 RequiredRoll、roll gate 6 成功碎盾/7 失败保留/未命中保留、魔法盾不碎、攻城之斧对 construct 加伤。
- 证据/问题: 耐久 12 不变、off_hand 清空/保留对照（:130-344）。

### tests/battle_runtime/runtime/run_shieldbreaker_weapon_projection_regression.cs — 有效
- 断言数: 约55
- 验证内容: 碎盾 resolved item 模板合并、unit 投影（AC+1、占 off_hand 阻止盾牌共存）、卸装后全部回滚到 baseline。
- 证据/问题: 逐字段 baseline 对照回滚断言（:214-296）。

### tests/battle_runtime/runtime/run_smiths_regret_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 铁匠的悔恨投影、缺陷之美 D100<=20 roll gate + D4 outcome_table、火缺陷 burning+共鸣标记、元素超载要求不同缺陷、摩拉丁 grace +2 只限本武器且预览不消耗/命中失败也消耗。
- 证据/问题: roll gate 值注入精确断言 8/14 伤害、grace stacks 1→0（:133-263）。

### tests/battle_runtime/runtime/run_spider_spear_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 蛛矛投影、蛛丝束缚 SkillDef 形状（STR DC14、rooted 60TU）、豁免失败 rooted/成功不 root、per_world_day 3 次与同回合一次限制。
- 证据/问题: rooted -2 攻击检定语义、使用计数 1/3、禁用原因（:193-296）。

### tests/battle_runtime/runtime/run_starfell_stardust_weapon_ability_regression.cs — 有效
- 断言数: 约45
- 验证内容: 群星之末星尘叠层（当次命中不吃新叠层、上限 5）、5 层触发宇宙恐惧 -2、星图指引耗 2 层换 advantage（命中后清除）、星坠 ≥5 层门禁并优先扣层数最多目标。
- 证据/问题: 逐击伤害阶梯 10/13/16/19/22/31 精确断言、目标 A 清零 B 保留 1 层（:58-299）。

### tests/battle_runtime/runtime/run_starfell_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 星坠 SkillDef 双伤害段（force+fire 3D6、DC16 减半）、对 building/非魔法 construct 双倍且互斥防 4 倍、magical 标签排除、实际 resolver 结算验证。
- 证据/问题: 总伤 15、双倍 30、magicalConstruct 不双倍精确断言（:217-299）。

### tests/battle_runtime/runtime/run_starfragment_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 环境 snapshot 夜间推导（world_step 12 夜/2 昼、显式 tag 覆盖推导）、星辰碎片投影、星爆装备技能 per_world_day 1 次与真实施放扣次数、星尘之触只在夜间附伤。
- 证据/问题: 昼/夜伤害对照、次日恢复可用（:65-547）。

### tests/battle_runtime/runtime/run_storms_eye_weapon_ability_regression.cs — 有效
- 断言数: 约45
- 验证内容: 风暴之眼投影、雷刃 1D6 lightning、雷鸣裂击暴击 2D6 thunder、裂云重劈推动 1 格/被边界挡住时追加 2D6 thunder、体力/冷却消耗。
- 证据/问题: blocked==moved+10、坐标 (2,0)/(1,0) 对照（:199-278）。

### tests/battle_runtime/runtime/run_temporal_status_semantics_regression.cs — 有效
- 断言数: 约60
- 验证内容: time_stasis 冻结个人时间线（DOT/progress/体力/状态/shield duration 全冻结，stasis 自身计时、到期加余波）、冷却冻结与惰性消费、ready 激活消费冷却、time_slow 余数累加、ready 不重复插入、静滞 fail-closed 位移/强制位移/激活、temporal typed 字段构造边界、余波释放原因矩阵。
- 证据/问题: 与对照单位逐步精确数值对照（:55-157）、6 种 release kind 分支断言（:430-496）。

### tests/battle_runtime/runtime/run_threadweaver_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 织命者投影、命运线叠 3 层变脆弱（锁反击/格挡/闪避）+ 来源绑定 +3、断命运线 DC18 execute/豁免成功 4D10 psychic 并清印、缝命术复活盟友 2D8+10 且自伤 10。
- 证据/问题: modifier 只作用于来源/目标组合、复活后 HP 19、持有者 -10（:213-306）。

### tests/battle_runtime/runtime/run_thunder_halberd_weapon_ability_regression.cs — 有效
- 断言数: 约40
- 验证内容: 雷霆之戟投影、雷鸣斩 1D6 thunder 附伤、非暴击不震慑、暴击+CON 豁免失败 stunned 60TU/成功不震慑。
- 证据/问题: 对照剥离能力源 6 vs 9、四条豁免分支（:176-244）。

### tests/battle_runtime/runtime/run_thunderbow_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 雷鸣弓投影（不覆写射程继承 4、thunder immune）、雷鸣矢附伤与震慑门控、蓄雷矢真实武器攻击+3D6 lightning 并触发雷鸣矢、60 体力/300TU 冷却。
- 证据/问题: 总伤 15 精确断言、射程继承负断言（:96-319）。

### tests/battle_runtime/runtime/run_thunderfang_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 雷霆之牙投影、雷鸣斩附伤、暴击震慑门控、托尔的锤打 storm 环境 +2 且伤害取最大（roll mode override random→maximum）、风暴导体只在 storm+近战命中反伤。
- 证据/问题: 晴/风暴伤害 4 vs 20、反伤 44 vs 50 vs 50 三分支（:288-406）。

### tests/battle_runtime/runtime/run_titanbow_weapon_ability_regression.cs — 有效
- 断言数: 约45
- 验证内容: 泰坦之弓投影、巨兽杀手只对 Large+ 追加 2D8、力量 17 -4/18 无惩罚并进入 RequiredRoll、穿透鳞甲只忽略 natural_armor_ac_bonus=4 且不改永久 AC。
- 证据/问题: 伤害 16 vs 8、AC 20→16 对照、snapshot 不变断言（:198-320）。

### tests/battle_runtime/runtime/run_tremor_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 地动投影、震击 SkillDef 单次豁免半伤+prone、真实施放：失败目标全伤+prone、成功盟友半伤不 prone、范围外不受影响、卸装后入口消失。
- 证据/问题: successDamage==failedDamage/2、范围外 HP 不变（:283-304）。

### tests/battle_runtime/runtime/run_twilight_edge_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 暮光之刃投影、暮影步 70TU 门槛（65 禁/70 可用）、暮光切割只限本行动轮（第二次不触发、回合重置消失）、昼夜平衡夜间 +1 伤害+真 advantage、暮光守护夜间 -1/白天用后暴露 +1。
- 证据/问题: 同轮第二次伤害等于未步前、重置后归 0（:230-521）。

### tests/battle_runtime/runtime/run_umbrella_sword_weapon_ability_regression.cs — 有效
- 断言数: 约45
- 验证内容: 伞剑投影、雨幕伞面 fire/freeze 减免（非雨 2/雨 4/湿 4）且不免 force、雨天优势 +2 只在 rain/wet、格挡只对远程攻击 -2。
- 证据/问题: 伤害 8/6/6/10 与 fixed_mitigation_total 精确断言（:140-286）。

### tests/battle_runtime/runtime/run_viper_morningstar_weapon_ability_regression.cs — 有效
- 断言数: 约55
- 验证内容: 毒蛇晨星投影（poison/antidote 免疫）、毒蛇打击 1D6 poison+DC15 麻痹（断读条/锁移动/锁行动）、毒免目标不麻痹、毒液注入每日 3 次与 venom_primed 生命周期（未命中/非本武器不清除、命中后清且总毒 2D6 非 3D6）。
- 证据/问题: primed 命中总伤 11 精确断言、primes 状态 1→0（:210-427）。

### tests/battle_runtime/runtime/run_void_axe_weapon_ability_regression.cs — 有效
- 断言数: 约35
- 验证内容: 虚空之斧投影、断界切口真实命中生成堵路裂隙边（不可跨边移动）、80TU 后过期、只保留 3 条最新裂隙。
- 证据/问题: CanTraverse false、40+40 仍存在/再 40 消失、四方向只留 3 边（:147-201）。

### tests/battle_runtime/runtime/run_wild_encounter_roster_typed_regression.cs — 有效
- 断言数: 约45
- 验证内容: roster 阶段就近选择、schema 校验（actor_id count==1/重复 actor_id/缺失 template 负例）、actor_id 投影不替换 unit_id、护送场景 NPC actor 构建、mist_hollow/wolf_wilds 正式内容编成与技能/brain 挂载。
- 证据/问题: 负例逐条断言错误文本、单位计数 5/3/2 与 actor/unit id 精确断言（:118-462）。

### tests/battle_runtime/runtime/run_windbow_weapon_ability_regression.cs — 有效
- 断言数: 约50
- 验证内容: 风之弓投影（射程覆写 6、风语 perception_modifier +3 不改基础感知）、风引射击把完整 modifier +4 加入检定并降低 RequiredRoll、风压推射 forced_move save 免疫门控与推动方向。
- 证据/问题: +4 与 RequiredRoll 差 4、免疫 0 步/解除 2 步对照（:137-351）。

### tests/battle_runtime/runtime/run_wolf_bow_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 狼牙弓投影、狼灵召唤建完整临时战斗单位（属性/阵营/占用格/60TU 到期清理释放格子）、每日 1 次与同回合限制、幽灵狼真实 1D6 攻击、狼群战术近友 +3/无友 -2/远友不算。
- 证据/问题: 到期前后 IsAlive 与格子占用断言、-2/+3/-2 三分支 modifier（:142-350）。

### tests/battle_runtime/runtime/run_wyrmbreak_weapon_ability_regression.cs — 有效
- 断言数: 约60
- 验证内容: 龙骨断剑投影、屠龙火附伤对 dragon/dragonborn 分级且绕 half 不绕 immune、窃骨之怒只对非龙命中充能（上限 5）、龙魂延伸耗 3 层 range2 武器攻击、龙魂爆发耗 5 层线形 6D6+对龙 2D6。
- 证据/问题: half==无抗、immune==baseline、怒气 0/1/5 与耗后 1 精确断言（:258-420）。

---

## 统计

- 有效: 52
- 偏弱: 1
- 无效: 0

### 偏弱清单
1. `tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.cs` — 唯一问题：:114 `_test.Eq(preview.hit_preview?.Source ?? "", preview.hit_preview?.Source ?? "")` 自比恒真断言；同文件其余断言（preview_fact_id 三端一致、49 格目标、HUD/AI 共享 facts）均真实有效。

### 无效清单
（无）

## 批次 03 明细

# Batch03 测试有效性审计报告

范围：tests/battle_runtime/ai/(45 个 run_*.cs，实际数量多于任务描述的 42)、status/(1)、ui/(1)、presentation/(3)，共 50 个文件。每个文件均已打开精读（大文件按 Test* 方法逐个检查）。

## tests/battle_runtime/ai/

### run_ai_trace_recorder_regression.cs — 偏弱
- 断言数： 约18
- 验证内容： AiTraceRecorder 嵌套 span 平衡、事件开关清理、异常后 instance scope/span 恢复、以及 BattleSkillPreviewService 的 span 使用契约。
- 证据/问题： 前 4 个 Test 方法（L26-170）为精确行为断言（事件数、ph=B/E、ncalls、异常后 recorder 恢复、AssertBalanced）；但 L172-191 `TestBattleSkillPreviewServiceUsesScopedTraceSpans` 为对源码字符串做 Contains/!Contains 断言（`source.Contains("BattleAiTraceSpan")`），属弱断言模式。

### run_battle_ai_action_assembler_plan_regression.cs — 有效
- 断言数： 约20
- 验证内容： ActionAssembler 生成 runtime plan 不回写 authored Resource、按 slot family 生成/抑制 action、generated metadata 稳定 identity。
- 证据/问题： 精确断言 identity_key="engage/offense/bolt/use_unit_skill"（L155）、生成条目类型存在/不存在（L103-128）、authored resource 未被 mutate（L42-46, L78）。

### run_battle_ai_action_intent_safety_gate_regression.cs — 有效
- 断言数： 约30
- 验证内容： intent 合法性校验、slot role 默认映射、SafetyGate 各 intent 的拒绝原因与放行边界。
- 证据/问题： 精确断言拒绝原因字符串（如 L42-46 "offense_post_lethal_from_safe"、L60-63 "escape_not_safer"）与放行/拒绝布尔边界。

### run_battle_ai_advantage_behavior_regression.cs — 有效
- 断言数： 约30
- 验证内容： survival 型 MoveToAdvantagePosition 经 DecisionEngine 的落点距离、already_safe 收束、多目标首选目标语义、evaluator 不改写 battle state。
- 证据/问题： 精确断言 landingDistance>=5、position_safe_distance==5（L105-114）、block reason already_safe==1（L209-212）、mutation snapshot 对比（L148-150）。

### run_battle_ai_charge_path_aoe_behavior_regression.cs — 有效
- 断言数： 约20
- 验证内容： whirlwind 自动装配 charge_path_aoe action、路径 AOE 多次命中计分、preview 异常时 trace 平衡、运行时自动装配参与决策、未命中不加冲锋熟练度。
- 证据/问题： 精确断言 path_step_hit_count>=2（L167）、EvaluationCount<全图格数（L163）、PreviewCommand allowed（L175）、miss 时 mastery==0（L337-341）。

### run_battle_ai_decision_lifetime_regression.cs — 有效
- 断言数： 约45
- 验证内容： BattleAiDecisionResult 深拷贝/去别名、source 清理、异常路径 borrower 清理与 trace 平衡、double dispose 审计基线。
- 证据/问题： 构造 source 后逐项 ReferenceEquals 否定 + source 变异后 result 保持 "captured"（L201-259），并验证 audit owner/lease/scope 基线（L480-491）。

### run_battle_ai_enemy_template_runtime_regression.cs — 有效
- 断言数： 约60
- 验证内容： 正式模板 StartBattle 稳定 id/brain/state/技能、stamina 池为正、无 fallback enemy、pressure AI 出技能指令、depleted fallback、随机技能等级 seeded 可复现且可变。
- 证据/问题： 10 个 Test 方法逐一检查，均为运行时行为断言（如 L241-245 command_type==Skill 且 preview.allowed；L475 同 seed 签名相等、L486-493 跨 24 seed 签名>1）。

### run_battle_ai_failure_policy_regression.cs — 有效
- 断言数： 约20
- 验证内容： BattleAiFailurePolicy/PayloadGuard 事件记录、metadata 快照而非引用、Reset、mode 控制 abort 决策。
- 证据/问题： 精确断言 severity/message/metadata（L68-71）、调用方字典变异后 event 不变（L73-74）、strict_abort/unknown_mode 规整（L123-132）。

### run_battle_ai_ground_reposition_behavior_regression.cs — 有效
- 断言数： 约7
- 验证内容： blink reposition 产出 mage_blink 技能指令、通过正式 preview、落点比当前更远离威胁。
- 证据/问题： 精确断言 skill_id/CommandKind/skill_entry_id（L72-86）与 landingDistance>currentDistance（L98-101）。

### run_battle_ai_melee_charge_behavior_regression.cs — 有效
- 断言数： 约30
- 验证内容： 天生武器回退 basic_attack、engage charge 接敌、状态迁移 patch 只提交一次、嘲讽 ground 指令与保护收益、短距 close_in 优先、charge resolved_anchor、preview 异常 trace 平衡。
- 证据/问题： 精确断言 block reason==MeleeWeaponRequired（L81-85）、迁移字段与 turn_decision_count==1（L177-206）、target_coord==(1,1)（L444-448）、resolved_anchor_coord==(1,1)（L509-513）。

### run_battle_ai_melee_screening_behavior_regression.cs — 有效
- 断言数： 约10
- 验证内容： 健康时守线格选择、低血不偏向守线、按实际路径成本而非几何线守线、锁定移动力 gate。
- 证据/问题： 精确断言 target_coord==(3,4)/(2,3)（L77-89）、(1,2)（L157-161）、锁定未许可 decision==null（L268-271）。

### run_battle_ai_mutation_guard_regression.cs — 有效
- 断言数： 约400
- 验证内容： mutation guard 全量：默认关闭、mutate-then-throw 优先 violation、50+ 种字段 mutation 的 fail-fast 检测、reflection 覆盖门禁、stable projection 精确 diff、double 位级精度。
- 证据/问题： 6287 行逐方法抽查：全部 50+ Test 方法均有具体实现，断言 violation 字段名/stage/InnerException 保留（L231-264）、各字段 mutation 后不回滚（L404-520）、reflection 覆盖门禁漏字段即 Fail（L2180-2333, L3123-3214）、double BitIncrement 必须报 1 条 diff（L5262-5283）。无空方法。

### run_battle_ai_objective_behavior_regression.cs — 有效
- 断言数： 约40
- 验证内容： escort/intercept/defense/rescue/node_operation/control/boss 各 objective AI 的移动/等待/交互/优先目标行为与阻路回退。
- 证据/问题： 11 个 Test 方法逐一检查，精确断言 action_id（如 objective_escort_move/wait，L92-111）、detour 落点 (0,0)（L667-671）、优先攻击 objective 目标 unit_id（L488-492, L613-617, L784-788）。

### run_battle_ai_query_service_regression.cs — 有效
- 断言数： 约15
- 验证内容： BattleAiQueryService typed skill index、SkillRecord 字段、距离/阻挡查询、BuildActionScoreInput 回调透传。
- 证据/问题： 精确断言 range_value==5、ai_tags（L84-86）、DistanceFromAnchorToTarget==2（L69-77）、callback 返回对象逐字段透传（L109-112）。

### run_battle_ai_random_chain_behavior_regression.cs — 有效
- 断言数： 约25
- 验证内容： random-chain 指令不携带确定性 target、preview 暴露 candidate pool、执行实际造成伤害、评分与 trace 元数据。
- 证据/问题： 精确断言 candidate pool 含 A/B（L100-109）、执行后至少一人掉血（L120-123）、pool_count==2、policy 字符串（L142-174）。

### run_battle_ai_retreat_behavior_regression.cs — 有效
- 断言数： 约15
- 验证内容： retreat evaluator 按最不安全远程威胁动态收束安全距离、trace 标识 focus 目标、不改写 battle state。
- 证据/问题： 精确断言 desired_min/max_distance==9（L117-126）、trace focus_target_unit_id==远程威胁（L135-139）、mutation snapshot 对比（L145-148）。

### run_battle_ai_role_threat_scoring_regression.cs — 有效
- 断言数： 约15
- 验证内容： multi-unit/ground 技能对含治疗威胁目标组合评分更高、范围技能按 lethal threat 数优先、低血追加骰只读正式参数。
- 证据/问题： 对照式断言 threatScore>normalScore（L81-88）、lethal count==1 vs >=2（L206-218）、legacy alias 不生效 estimated_damage==10（L284-288）。

### run_battle_ai_runtime_action_plan_regression.cs — 有效
- 断言数： 约25
- 验证内容： plan/entry 无 Resource 所有权、metadata 防御性拷贝、staleness 指纹、Clear/Dispose 释放、assembler 异常清理。
- 证据/问题： reflection 字段类型门禁（L32-44）、source 变异后 entry 保留原值（L75-88）、skill level/brain 形状变化触发 stale（L116-173）、异常后 audit 基线（L232-237）。

### run_battle_ai_score_context_adapter_regression.cs — 有效
- 断言数： 约18
- 验证内容： score context adapter 暴露 typed 视图、构建 score input 剥离 live skill resource、清理后无残留借用、PayloadGuard 拒绝 runtime-like 文本。
- 证据/问题： 精确断言 ap_cost==2/mp_cost==3（L109-110）、清理后 barrier 视图 Count==0（L124-128）、伪造 "<Object:...>"/"<Callable:...>" 被拒绝（L155-174）。

### run_battle_ai_score_execute_regression.cs — 有效
- 断言数： 约20
- 验证内容： execute 技能 AI 估值：高 HP 无收益、kill bps=豁免失败率、execute 免疫/死亡保护归零、不读取 preview 展示文案。
- 证据/问题： 精确断言 kill bps==5000/0、estimated_damage==10/0（L39-46, L60-66, L81-84, L143-144），含毒文案注入反例（L120-133）。

### run_battle_ai_score_input_metrics_regression.cs — 有效
- 断言数： 约120
- 验证内容： AI 评分指标：友伤排除、空地控场、repeat 成功率、抗性/护盾正式结算、多段护盾顺序消耗、法球层/寿命投影、格挡逐段减伤/到期/魔法 fallback/冗余、自 slow 机会成本、挑衅保护收益。
- 证据/问题： 16 个 Test 方法逐一检查，均为精确数值断言（如 L292-295 10/5/5/0、L543-545 8/5/3、L1046-1048 保护收益==2、L983-989 机会成本==3×MovementCostWeight），且反复断言评分不改写真实 HP/护盾/行动进度。

### run_battle_ai_score_ordering_regression.cs — 有效
- 断言数： 约40
- 验证内容： score 排序优先级链（bucket>lethalThreat>lethal>total>hit>cost tie-break）、seal 指纹、decision engine 跨 bucket 生存投影比较、ground control 门槛策略。
- 证据/问题： 正反对称断言 AssertBetter/AssertEngineBetter（L468-497）、生存投影致死风险跨 bucket 压过击杀（L210-223）、seal 后变更/恢复指纹匹配（L41-58）。

### run_battle_ai_score_save_probability_regression.cs — 有效
- 断言数： 约8
- 验证内容： 半伤豁免加权期望伤害、豁免估算暴露、评分不惰性初始化装备视图。
- 证据/问题： 精确断言 estimated_damage==30、SaveSuccessRatePercent==50（L78, L104-113）、equipment_view 保持 null（L114-121）。

### run_battle_ai_score_selection_regression.cs — 有效
- 断言数： 约20
- 验证内容： brain/faction score profile 覆盖优先级、melee AI 选后声明但高分技能、单体 nuke 优于单目标 AOE、共享评分上下文选高收益目标、锁定移动力 gate。
- 证据/问题： 精确断言 DamageWeight 77→12（L50-69）、选中 executeSkill/action_id==score_probe_higher（L152-161）、目标==farScout（L300-304）、锁定未许可 decision==null（L342-345）。

### run_battle_ai_skill_affordance_classifier_regression.cs — 有效
- 断言数： 约17
- 验证内容： 技能 affordance/action family 分类与 passive 不可生成。
- 证据/问题： 7 个 Test 方法逐一检查，精确断言 affordances/families 成员与 skip_reason=="passive_or_no_combat"（L117）。

### run_battle_ai_state_resolver_regression.cs — 有效
- 断言数： 约15
- 验证内容： 状态迁移 resolver：低血切换、ally 低血排除自身、sticky rule、affordance plan 缓存/lazy 缓存、不写 unit_state。
- 证据/问题： 精确断言 StateId/RuleId（L37-40, L93-103）、plan cache 与 lazy 两条路径均命中 aid_ally（L140-148）、空 typed index 不恢复（L168）。

### run_battle_ai_trace_disabled_regression.cs — 有效
- 断言数： 约14
- 验证内容： trace 开关不改变决策、关闭时不记录/不消耗 nonce、开启后 trace 内容完整。
- 证据/问题： 精确断言 disabled 时 traces.Count==0、enabled 后 TraceId=="trace_gate_wait_1"（L89, L106-114）、两次决策 score/reason 相等（L94-103）。

### run_battle_ai_trace_projection_lease_regression.cs — 有效
- 断言数： 约120
- 验证内容： trace 投影 lease：未知深层值 fail、lease 审计计数、固定 key 顺序与 legacy golden、JSON SHA256 指纹、StringName 类型保留、深拷贝隔离、文件边界 JSON-safe、异常清理。
- 证据/问题： golden 为硬编码常量字符串/哈希（L84-89, L414-418）非自生成对比；lease dispose 后访问抛 ObjectDisposedException（L105-108）；FinalUnits setter/getter 双向变异隔离（L161-193）。

### run_battle_ai_trace_summary_regression.cs — 有效
- 断言数： 约20
- 验证内容： AiCommandSummary 拷贝隔离、BattlePreview typed damage preview、AiActionTrace 字典投影形状。
- 证据/问题： source command 变异后 summary 保持 2 个目标（L42-48）、投影字段精确断言 trace_id/evaluation_count/gate_rejection_reason（L148-161）。

### run_battle_ai_unit_skill_candidate_evaluator_regression.cs — 偏弱
- 断言数： 约25
- 验证内容： 技能 entry id 传递、可用性过滤、evaluator 生成指令、fast preview 越界计数、layered barrier 强制 canonical preview、等值目标 unit_id tie-break。
- 证据/问题： 多数方法为精确行为断言（如 L291-300 越界 counter==1、L399-408 tie-break==target_a）；但 L138-165 `TestAuthoredEnemyActionsResolveAvailabilityEntriesBeforeBuildingSkillCommands` 整个方法为对 3 个源码文件做 !Contains 断言（AssertSourceDoesNotContain，L496-506）；L31-38 仅断言类型 IsSealed，均偏弱。

### run_battle_ai_unit_snapshot_regression.cs — 有效
- 断言数： 约25
- 验证内容： BattleAiUnitSnapshot 深拷贝 typed 集合、snapshot 与原 unit 隔离、plain payload 边界投影。
- 证据/问题： 快照后变异原 unit（坐标/技能/等级/冷却/blackboard）再逐项断言 snapshot 不变（L46-70）、payload 不扩展 lock-hit 边界（L89-92）。

### run_battle_ai_wait_behavior_regression.cs — 有效
- 断言数： 约8
- 验证内容： 体力耗尽时 wait 表达 active_rest 及评分区间、无动作 fallback rest 不抬分、active rest 不抢守线移动。
- 证据/问题： 精确断言 action_id==active_rest_wait/fallback_rest_wait（L65-69, L115-119）、total_score==-40（L120-124）、守线落点 (3,4)（L180-184）。

### run_enemy_ai_action_skill_compatibility_regression.cs — 有效
- 断言数： 约40
- 验证内容： action×skill 兼容性门禁：target_mode、castable 契约、专用 selection mode、执行路由与容量、charge/blink 变体单格约束、等级相关变体。
- 证据/问题： 6 个 Test 方法逐一检查，AssertInvalid 带具体期望错误子串（如 L263-268 "random_chain requires UseRandomChainSkillAction"、L436-448 按等级 1 拒绝/5 放行）。

### run_enemy_ai_generation_slots_content_regression.cs — 有效
- 断言数： 约30
- 验证内容： 正式 brain 内容的 generation slots/transition rules 通过完整 schema 校验并完整投影为 plain definition、无重复依赖加载。
- 证据/问题： 对 7 个正式 brain 调系统 ValidateSchema 断言 0 错误（L91-98, L161-165）、投影条数相等（L104-108, L167-171）、重复加载检测（L52-67）。

### run_enemy_ai_generation_slots_schema_regression.cs — 有效
- 断言数： 约10
- 验证内容： generation slot schema：selector 集合、合法通过、重复 id/order、未知 family/template、min>max 距离拒绝。
- 证据/问题： 通过/拒绝双向断言，拒绝路径 errors.Count>=2 并附错误内容（L88, L109, L143）。

### run_enemy_ai_transition_schema_regression.cs — 有效
- 断言数： 约12
- 验证内容： transition rule schema：自定义状态合法、重复 rule 拒绝、空 conditions/未知 predicate/缺失 state 拒绝、condition trace 形状。
- 证据/问题： 拒绝路径 errors.Count>=2/>=4（L73, L104）、trace 字段精确断言含未使用字段固定值（L118-123）。

### run_enemy_multi_unit_skill_command_regression.cs — 有效
- 断言数： 约10
- 验证内容： multi-unit 指令携带 variant 与目标列表、被屏障阻挡的候选不消耗候选池限额。
- 证据/问题： 精确断言 TargetUnitIds==[hero_1,hero_2]（L80-90）、被阻挡组不含 blockedTargetA 且含 validTarget（L204-216）。

### run_enemy_template_runtime_start_regression.cs — 有效
- 断言数： 约45
- 验证内容： 模板 StartBattle 稳定 id、stamina 池、build context item_defs 武器投影、temporal modifier 生效、豁免免疫/抗性标签投影、公式派生 HP/攻击加值。
- 证据/问题： 7 个 Test 方法逐一检查，精确断言武器 ItemId/射程/伤害标签（L217-245）、ConsumeActionProgressGain==5（L349-357）、豁免掷骰前免疫（L442-445）、派生 HP==520/攻击+4（L514-532）。

### run_enemy_template_schema_boundary_regression.cs — 有效
- 断言数： 约40
- 验证内容： EnemyTemplateDef schema 边界：typed 引用表接受/缺失拒绝、cognition 封闭枚举、豁免标签裸 tag/后缀/空/未知/重复、抗性标签与 tier、公式派生。
- 证据/问题： 15 个 Test 方法逐一检查，拒绝路径断言错误文本包含具体 token（L246-249 "removed suffix"、L291-294 "duplicates save tag poison"）、派生公式精确值 520/+4（L374-391）。

### run_meteor_swarm_ai_regression.cs — 有效
- 断言数： 约25
- 验证内容： 陨星雨 AI 评分：敌方/友伤计数、地形收益、meteor use-case 分类（cluster/decapitation/zone_denial/unsafe）、high priority trace、soft/hard 友伤与 protected ally。
- 证据/问题： 精确断言 enemy_target_count==2、terrain>=49、use_case 字符串（L101-107, L155, L165, L189）、protected ally hard reject 前缀（L240-244）。

### run_mist_harrier_level_regression.cs — 偏弱
- 断言数： 约16
- 验证内容： mist_harrier 模板等级/生命骰/六维/技能集合/派生 HP 与攻击加值。
- 证据/问题： L23-30、L31-36 主要为回读 .tres 数据常量（CreatureLevel==5、strength==10 等）；仅 L46-55 的 DerivedHpMax==46/DerivedAttackBonus==3 为公式行为断言。主体是内容常量回读。

### run_move_to_range_progress_regression.cs — 有效
- 断言数： 约25
- 验证内容： candidate request typed 校验、move_to_range 远距离必移动且选最远可达格、阻路绕路、守线路径进度优先于局部贪心、高地 progress gate、move cost 异常 trace 平衡。
- 证据/问题： 7 个 Test 方法逐一检查，精确断言 target_coord==(3,1)（L140-144）、detour 落点集合（L220-225）、Y 坐标方向与禁停格 (2,2)（L306-314）、高地 stall==null/progress==(2,1)（L399-413）。

### run_phantasmal_kill_ai_regression.cs — 有效
- 断言数： 约25
- 验证内容： Phantasmal Kill AI：低血 execute 评分更高、illusion 免疫零收益、豁免优劣势改变期望值、友军波及/致死计数、友伤软配置门槛、affordance 分类。
- 证据/问题： 对照式精确断言（L54-62, L83-94 全零、L167-196 1/0 与 1/1 计数、L228-254 门槛矩阵）。

### run_wolf_alpha_content_regression.cs — 偏弱
- 断言数： 约90
- 验证内容： wolf_alpha 模板契约、三个专属技能各等级效果、生成技能等级 seeded、正式 encounter 生成、pack_leader brain 结构。
- 证据/问题： TestSkillContracts 经 BattleSkillResolutionRules 逐等级解析效果（L107-130, L149-172, L188-208）、TestGeneratedSkillLevels/TestFormalEncounterGeneration 为真实行为验证；但 TestTemplateContract（L33-84）与 TestPackLeaderBrain（L378-431）主体为回读 .tres 数据常量（CreatureLevel==4、strength==16、action 存在性检查），属内容常量回读与行为验证混合。

### run_wolf_alpha_runtime_behavior_regression.cs — 有效
- 断言数： 约25
- 验证内容： 统御长嚎 preview/执行按 wolf 标签过滤且 preview 无副作用、AI 需两只狼才嚎叫、扑杀 selector 按生命比例、邻近敌人时选择进攻技能。
- 证据/问题： 精确断言 preview 含/不含目标且 AP/体力不变（L87-103）、执行后状态与消耗（L107-120）、lone 时 decision==null（L172-175）、目标==lowerRatioTarget（L283-287）。

## tests/battle_runtime/status/

### run_battle_status_effect_typed_state_regression.cs — 有效
- 断言数： 约10
- 验证内容： status collection 拒绝畸形 payload、合法 status roundtrip 保值、status_effects 投影非 live owner。
- 证据/问题： 错误路径断言 ArgumentException 类型（L124-141）、roundtrip 精确值（L60-64）、投影注入变异不落 runtime 且二次投影不残留（L80-93）。

## tests/battle_runtime/ui/

### run_battle_board_ui_small_regression.cs — 有效
- 断言数： 2
- 验证内容： GetVariantIndexForTest 边界（int.MinValue 不为负）与 timeline tooltip 不含字面 "/n"。
- 证据/问题： L12-15 边界值断言、L16-19 针对真实历史 bug 的输出内容断言，具体且可失败。

## tests/battle_runtime/presentation/

### run_battle_board_native_lease_regression.cs — 有效
- 断言数： 约80
- 验证内容： BattleBoard 场景级 native lease：构造失败回基线、owner 精确计数、unit delta 只换 token 不重铺地形、重复 configure 不增生、Clear/double Dispose/退出树审计基线、Dispose 后拒绝配置。
- 证据/问题： 精确断言 owner delta==RenderOwnerCount（L242-246）、token InstanceId 替换而 top cell 数不变（L81-95）、4 次重绘 owner 不变（L120-137）、各阶段 AssertAuditBaseline。

### run_battle_hud_typed_projection_regression.cs — 有效
- 断言数： 约80
- 验证内容： HUD typed snapshot 冻结 schema key 顺序、输入变异隔离、lease 反复构建不受污染、adapter 目标投影（escape/boss/intercept/defense）、panel 灰度材质 lease 生命周期与退出树解绑。
- 证据/问题： 6 个 Test 方法逐一检查，key 顺序与冻结常量精确比对（L205-236）、snapshot 构建后清空/污染输入再断言（L178-191）、panel 退出后 material 已 dispose 而借用 shader/texture 存活（L705-707）。

### run_battle_presentation_delta_regression.cs — 有效
- 断言数： 约25
- 验证内容： presentation delta 投影：log-only 不脏棋盘、保守全刷策略、timeline/objective 刷新语义、MergeFrom 合并去重、log dock 刷新门控、命令间 delta 复位。
- 证据/问题： 9 个 Test 方法逐一检查，精确断言 IsLogOnly/RequiresFullBoardRefresh 矩阵（L37-44, L70-73, L95-101）、BuildRecentLogText=="first\nsecond\nlatest"（L55-59）、Reset 后 HasChanges==false（L181-185）。

## 统计

- 有效： 46
- 偏弱： 4
- 无效： 0

### 偏弱清单
1. tests/battle_runtime/ai/run_ai_trace_recorder_regression.cs — L172-191 一个方法为源码 Contains 断言（其余 4 个方法为强行为断言）。
2. tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs — L138-165 整个方法为源码 !Contains 断言；L31-38 仅断言类型 IsSealed（其余 6 个方法为强行为断言）。
3. tests/battle_runtime/ai/run_mist_harrier_level_regression.cs — 主体为回读 .tres 数据常量（L23-36），仅派生 HP/攻击加值为行为断言（L46-55）。
4. tests/battle_runtime/ai/run_wolf_alpha_content_regression.cs — TestTemplateContract(L33-84)/TestPackLeaderBrain(L378-431) 主体为 .tres 常量回读；TestSkillContracts/TestGeneratedSkillLevels/TestFormalEncounterGeneration 为真实行为验证。

### 无效清单
无。

## 批次 04 明细

# 测试有效性审计报告 — batch04

范围：`tests/battle_runtime/rules/`（25 个 run_*.cs）+ `tests/battle_runtime/skills/`（30 个 run_*.cs）。

> 范围说明：任务书称 skills 目录 24 个、共 49 个文件；实际目录中 skills 有 **30** 个 `run_*.cs`（25+30=55）。本报告覆盖了实际存在的全部 55 个文件，多出的 6 个可能属于其他批次的划分口径差异，请主控方核对。

判定口径：
- 有效：断言针对被测系统的行为/状态/边界/错误路径，具体且可能失败。
- 偏弱：存在弱断言模式（空断言方法、Set后立即Get回读、回读 .tres 常量自比、仅非空断言等），但文件仍有部分真实验证。
- 无效：恒真断言、断言不执行、与即时生成输出自比、空测试方法、catch 吞失败。

说明：本仓库大量技能测试包含 "TestAuthoredContract / TestContentContract" 类方法，用**硬编码期望值**比对 .tres 加载出的内容（技能 ID、数值曲线、门禁配置）。这类断言在内容被改动时会真实失败，属于内容契约钉固，不判为弱；只有"从同一对象读出再与自身比较"或"断言体为空"才判弱。

---

## tests/battle_runtime/rules/

### tests/battle_runtime/rules/run_attack_policy_parity_regression.cs — 有效
- 断言数: 约20
- 验证内容: BattleAttackCheckPolicyService 各 Build* 入口与 BattleHitResolver 的零漂移 parity、注入式装备查询只调用一次且结果进入 canonical attack check、查询路径不得修复 stale footprint。
- 证据/问题: 精确断言 policy 与 resolver 输出逐字段一致（L160-215）、probe 计数与注入值（L316-329）、stale geometry 不被改写（L267-291）。L386 `AssertPreviewEq` 双方 null 时静默通过，属轻微瑕疵，不影响整体判定。

### tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.cs — 偏弱
- 断言数: 约25
- 验证内容: 攻击检定修正堆叠（add/max/min/净值）规则、bundle typed breakdown、exact schema round-trip 与非法字段拒绝。
- 证据/问题: 主体断言（stack 求和 L39-104、schema 拒绝 L160-213）真实有效；但 `TestTypesArePlainCSharp`（L21-27）调用的 `AssertPlainCSharpType` 是**空方法**（L240-242），该 Test 方法实际零断言；`IsGodotPayloadType`（L244）为死代码。

### tests/battle_runtime/rules/run_battle_damage_preview_range_contract_regression.cs — 有效
- 断言数: 约30
- 验证内容: 伤害预览范围服务：空预览契约、power-only 固定伤害、武器骰+技能骰 min/max、多效果求和跳过非伤害、双手武器骰忽略旧 alias、无骰时 dice_bonus 不生效。
- 证据/问题: 精确数值断言 MinDamage/MaxDamage/SummaryText（L74-89、L109-116、L161-179），均为可失败的具体期望。

### tests/battle_runtime/rules/run_battle_damage_resolver_preview_contract_regression.cs — 有效
- 断言数: 约50
- 验证内容: 伤害预览共享伤害数学且不改写活体单位、working set 跨段复用 detached 单位与护盾递减、save 概率不掷骰、compact score 与 full preview 一致、属性缩放恢复骰、heal_fatal/dispel typed 参数。
- 证据/问题: 精确断言 rolled_damage/post_save/shield 数值（L72-98）、working set 护盾序列 5→1→0（L168-172）、活体 HP/护盾/状态不被 preview 污染（L93-108）、heal_fatal 公式值 22（L430-431）。

### tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs — 有效
- 断言数: 约10
- 验证内容: PWK/普通致死 DeathResolutionContext 的优先级（900/100）与识别，CanDeathPreventionBlock 优先级比较。
- 证据/问题: `CanDeathPreventionBlock` 三例行为断言（L64-74）真实；L23-31 中 `context.DeathSource == PowerWordKillExecuteDeathSource` 是同厂常量自证，略弱但被行为断言覆盖。

### tests/battle_runtime/rules/run_battle_effect_category_resolver_contract_regression.cs — 有效
- 断言数: 约20
- 验证内容: 效果类别 resolver 只读显式 delivery/effect categories 与 typed projectile_kind、cast variant override 优先、不读 legacy params、不从 skill_id/tags 猜测。
- 证据/问题: 精确正负断言（L127-142、L155-190、L212-237、L267-274、L300-319），含"不得推断"负向用例。

### tests/battle_runtime/rules/run_battle_equipment_requirement_rules_regression.cs — 有效
- 断言数: 约5
- 验证内容: 盾牌需求读取 typed item index、非盾牌/缺索引/无效槽 fail closed。
- 证据/问题: 正反断言 UnitHasEquippedShield/UnitHasEquippedItemTag（L26-29、L40-50、L61-69）。

### tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs — 有效
- 断言数: 约20
- 验证内容: execute 字段在资源边界投影、legacy params payload 被 schema 拒绝、PWK 阈值=20%HP+等级加成且忽略属性修正、0血/死亡目标不可 execute。
- 证据/问题: 精确阈值 35（L122）、FatalDamage=35（L127）、schema 拒绝消息（L93-96）。L157-159 存在空 `AssertPlainType` 但**从未被调用**（死代码），无空 Test 方法，不降级。

### tests/battle_runtime/rules/run_battle_hit_preview_contract_regression.cs — 有效
- 断言数: 约45
- 验证内容: 黑契推进/10级万刃归一必中预览契约、9级连击层命中加成差值、HUD snapshot/hover 复用 runtime preview（阶段命中率、伤害上下限、fate badges、暴击锁定文案）、无 preview 时 HUD 不自算。
- 证据/问题: 端到端 PreviewCommand/IssueCommand 断言（L100-109、L200-235）、HUD badge 文本包含/不包含（L362-400）、trap resolver 调用次数为 0 防偷取伤害（L330）。

### tests/battle_runtime/rules/run_battle_hit_rate_legacy_cleanup_regression.cs — 有效
- 断言数: 约20
- 验证内容: legacy hit_rate_percent 不再物化为 success_rate_percent：hit resolver、repeat attack 文本、HUD badge、AI score 四路径均只认正式字段。
- 证据/问题: 精确断言 legacy-only 输入 SuccessRatePercent=0（L30-50）、HUD badge 空串（L120-124）、AI score 100/42/50 三分支（L154-190）。

### tests/battle_runtime/rules/run_battle_hit_resolver_bab_regression.cs — 有效
- 断言数: 约25
- 验证内容: BAB 读取/缺失回退/缺 AC 报错、BAB 与 attack bonus 叠加、锁定技能命中加值（普攻与法术检定）、状态命中加值/惩罚累加净值。
- 证据/问题: 精确 RequiredRoll 数值断言（L41、L52、L81、L101、L141、L163、L184）与错误码断言（L63-68）。

### tests/battle_runtime/rules/run_battle_range_service_contract_regression.cs — 有效
- 断言数: 约25
- 验证内容: null skill 回退 0、有效射程读武器投影且状态层只读叠加、地面范围技能威胁距离按外缘（7种 area pattern）、精准射击弓/天生/徒手资格。
- 证据/问题: 精确射程数值（L65-93、L111-120、L272-281）与武器资格正反断言（L233-240）。

### tests/battle_runtime/rules/run_battle_report_formatter_contract_regression.cs — 有效
- 断言数: 约10
- 验证内容: typed attack metadata 构建 fate attack 战报 entry、typed damage result 生成伤害/护盾吸收/护盾破碎三条日志、meteor summary 投影格式化。
- 证据/问题: entry_type/reason_id 精确断言（L50-59）、log 条数 3（L82）。

### tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.cs — 有效
- 断言数: 约40
- 验证内容: 十组 legacy status params → typed 字段迁移契约：lock_crit、lock_dodge_bonus、blind 惩罚、dispel 标志、duration/tick、mitigation_tier、二次豁免加值、outgoing 倍率等，旧 params 失效且被 SkillContentRegistry 静态拒绝。
- 证据/问题: 每组均有"旧 params 不生效 + typed 字段生效 + schema 拒绝"三重断言（如 L128-156、L370-403、L438-469）。

### tests/battle_runtime/rules/run_battle_skill_resolution_rules_regression.cs — 有效
- 断言数: 约20
- 验证内容: 技能解析 policy：unit variant 路由与目标去重、内部不存 Godot Array 只在 projection 边界输出、ground 技能隐式 variant、歧义 variant 阻止执行且不收集效果。
- 证据/问题: 精确断言（L53-78、L97-105、L132-134），含错误消息文本"技能形态不明确。"。

### tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs — 有效
- 断言数: 约15
- 验证内容: legacy status params 不再驱动治疗/护盾倍率，typed 字段驱动（含四舍五入、最低保留1），治疗/护盾应用路径消费倍率。
- 证据/问题: 精确数值 11→6、8→2（L66-78）、写回 HP 10、护盾 5（L127-156）。

### tests/battle_runtime/rules/run_battle_target_team_rules_regression.cs — 有效
- 断言数: 约15
- 验证内容: enemy/ally/self/any 过滤相对来源判定、别名与未知过滤 fail closed、effect 空过滤继承 skill 过滤、madness 选项只放宽 canonical 过滤。
- 证据/问题: 正反断言矩阵（L24-47、L56-71、L114-129）。

### tests/battle_runtime/rules/run_damage_application_projection_regression.cs — 有效
- 断言数: 约45
- 验证内容: 伤害应用投影：null hook 保持旧护盾/HP 行为、吸收百分比投影、部分扣减保 metadata、过期护盾不吸收、Cancel/Modify/StateChanged hook 语义、preview/suppress 不触发 hook、min_hp 与致死语义。
- 证据/问题: 精确数值断言贯穿（L43-51、L61-74、L133-139、L155-161、L183-189、L236-249）。

### tests/battle_runtime/rules/run_damage_context_typed_regression.cs — 有效
- 断言数: 约20
- 验证内容: typed DamageResolutionContext：partial dictionary 边界拒绝、critical/虚拟骰驱动、固定减伤来源结构化、装备端口接收显式 battle state 且同步派发。
- 证据/问题: ExpectArgumentException 错误路径（L149-154）、probe 引用相等与计数（L102-132）、减伤来源标签（L241-251）。

### tests/battle_runtime/rules/run_damage_resistance_regression.cs — 有效
- 断言数: 约20
- 验证内容: 抗性 half/double 抵消、immune 最高优先级、缺 damage_tag / 缺武器投影 fail closed 且不生成零伤害事件、返回 invalid_damage_tag。
- 证据/问题: 精确伤害值与 mitigation_tier/sources 断言（L31-37、L53-58、L78-82、L101-105）。

### tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs — 有效
- 断言数: 约30
- 验证内容: 装备耐久选定目标提交：只改选定实例、stale ref 不回退到替换装备、豁免成功零损失、权重表不给未加权槽默认值、多槽装备保持 entry/occupied 槽身份、typed 槽权重优先于 legacy map。
- 证据/问题: 精确耐久值 20/13（L56-65）、no-op 原因（L97-100）、槽身份断言（L200-249）。

### tests/battle_runtime/rules/run_phantasmal_kill_execution_rules_regression.cs — 有效
- 断言数: 约30
- 验证内容: 分级豁免规则：immune 优先、自然1/20 降级/升级、failure 阈值取固定与百分比较大者、critical failure 只用百分比、平均骰、normal/advantage/disadvantage 分布基点数、roll override 确定性分布。
- 证据/问题: 精确 bps 分布（L137-176、L216-265）与阈值 50/100/70（L89-112）。

### tests/battle_runtime/rules/run_status_effect_semantics_regression.cs — 有效
- 断言数: 约120
- 验证内容: 状态语义表与运行时：refresh/add 堆叠、TU 到期移除、burning timeline tick 锚定与伤害、slow 移动成本、legacy params duration/tick 全面失效、伤害 resolver 只读正式字段、skill turn typed 字段、反序列化严格拒绝。
- 证据/问题: 运行时端到端断言（L206-224、L300-333、L366-378）与逐字段 typed/legacy 对照（L655-846、L848-927）。

### tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs — 有效
- 断言数: 约80
- 验证内容: lock_guard typed 字段 schema、legacy params 不再驱动锁/debuff 语义、序列化 round-trip 顶层字段、guard lock 阻断原因、runtime wrapper 全字段转发。
- 证据/问题: 逐字段转发断言约40条（L379-605）、阻断原因枚举与消息（L290-317）、round-trip（L246-269）。

### tests/battle_runtime/rules/run_weapon_hit_combo_stack_regression.cs — 有效
- 断言数: 约30
- 验证内容: 武器命中连击层：近战/远程分层、命中即给层（含零伤害命中）、徒手/非武器不给层、万刃归一只消费近战层且预览不消费、未命中清空两类层。
- 证据/问题: 精确层数与伤害值（L76-107、L164-171、L271-341、L383-391）。

---

## tests/battle_runtime/skills/

### tests/battle_runtime/skills/run_archer_backstep_shot_regression.cs — 有效
- 断言数: 约90
- 验证内容: 后跃射：内容契约与等级曲线、弓门禁、source_retreat schema 八类非法配置拒绝、方向必须显式正交远离、后撤不耗移动力且被阻挡/墙/屏障截断、未命中/击杀仍后撤、移动锁状态拒绝不扣费、手动两段选择、AI 枚举三个合法后撤方向。
- 证据/问题: 运行时落点坐标精确断言（L370-383、L399-421、L452-505）、AI 方向集合与 trace（L706-728）。

### tests/battle_runtime/skills/run_archer_harrier_mark_regression.cs — 有效
- 断言数: 约40
- 验证内容: 猎印追缉内容契约、等级曲线与状态施加、来源绑定追加骰只对施放者武器/天生武器命中生效。
- 证据/问题: 伤害值三分支 8/8/12（L154-156）与非武器伤害不触发（L166）。

### tests/battle_runtime/skills/run_archer_suppressive_fire_regression.cs — 有效
- 断言数: 约35
- 验证内容: 压制射击内容契约、等级曲线与压制地带生成（移动成本+1、持续、不与 slow 叠加）、天生远程武器门禁与射程。
- 证据/问题: 地形效果字段精确断言（L125-141）、门禁正反（L151-159）。

### tests/battle_runtime/skills/run_battle_on_kill_gain_resources_regression.cs — 有效
- 断言数: 约7
- 验证内容: 死亡收割击杀后返还 AP/移动力、允许行动后移动、标记 changed unit。
- 证据/问题: 击杀与资源精确断言（L84-105）。

### tests/battle_runtime/skills/run_battle_weapon_dice_regression.cs — 有效
- 断言数: 约80
- 验证内容: 武器骰体系：add_weapon_dice 公式、旧 alias 失效、物理伤害默认不加武器骰、暴击额外骰只一次、多段独立、双手/versatile 握法选骰、空手/天生武器、requires_weapon 门禁只认装备、天生武器不触发熟练度、骰事件字段分组与空组 false、重击技能骰样板与破甲契约。
- 证据/问题: 全篇精确数值断言（如 L71-100、L204-271、L575-610）。

### tests/battle_runtime/skills/run_dragon_breath_regression.cs — 有效
- 断言数: 约45
- 验证内容: 六种龙息官方资源 schema 稳定（验证零错误+逐项契约）、冷却 block reason typed、State/ReadView 规则对齐、per-battle/per-turn 次数阻断与消耗、回合刷新。
- 证据/问题: 官方内容逐项比对（L45-91）、次数消耗 1→0 与二次阻断（L244-274、L319-328）。

### tests/battle_runtime/skills/run_hunter_mark_skill_regression.cs — 有效
- 断言数: 约40
- 验证内容: 猎人标记等级曲线与状态施加、来源绑定武器追加伤害（施放者16/盟友10/5级18）且实际扣血。
- 证据/问题: 伤害值与 HP 差值双重断言（L127-152）。

### tests/battle_runtime/skills/run_jump_arc_regression.cs — 有效
- 断言数: 0（包装器）
- 验证内容: 薄包装器，委托 `run_jump_arc_regression_typed.RunForWrapper()`。
- 证据/问题: L7 直接转发 typed runner 的 TestResult；实际断言在 typed 文件中，不重复计。

### tests/battle_runtime/skills/run_jump_arc_regression_typed.cs — 有效
- 断言数: 约12
- 验证内容: 跳跃弧 12 个场景：平地/上台阶/下悬崖/过低障碍/高墙阻挡/落点占用/超射程/路径友军阻挡/小体型加成/大体型惩罚/短跳红利/零距离拒绝。
- 证据/问题: 每场景构造地形后断言 CanJumpArc 布尔（L46-191）。

### tests/battle_runtime/skills/run_magic_backlash_regression.cs — 有效
- 断言数: 约60
- 验证内容: 法术反噬：fumble protection 状态、投影保真、火球友伤/灼烧全队伍、大成功返 MP、受保护大失败吞 MP 不爆、无保护大失败落点偏移、读条启动/完成/失败/取消/维持中断全生命周期。
- 证据/问题: 精确 MP/HP/进度断言（L156、L204、L231、L278-303）。L45-50 `SetFumbleProtectionUsedTyped` 后立即 Get 回读为弱模式，但仅 2 条断言，整体不降级。

### tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs — 有效
- 断言数: 约55
- 验证内容: 陨星雨专用 profile：深拷贝只读视图、7x7 target plan 与边缘裁剪、typed profile 不受 legacy area 污染、战报签名、地形 payload（陨坑/尘土 lifetime 与命中修正）、漂移改变锚点与签名、显式豁免虹光法球。
- 证据/问题: 格数 49/28/16（L122-149）、友伤全量结算（L220-223）、屏障豁免不误伤色层（L499-507）。

### tests/battle_runtime/skills/run_passive_status_orchestrator_regression.cs — 偏弱
- 断言数: 约20
- 验证内容: 被动状态编排：工厂投影种族被动、race/subrace 投影与覆盖顺序、飞升压制原种族、射击专精只对弓 +1 射程。
- 证据/问题: 投影与射程断言真实（L70-81、L102-121、L144-164、L241-270）；但 `TestPassiveContextAndResolversNoLongerRequireGodotRegistration`（L34-41）调用的 `AssertPlainType` 是**空方法**（L418-420），该 Test 方法零断言。

### tests/battle_runtime/skills/run_phantasmal_kill_regression.cs — 有效
- 断言数: 约70
- 验证内容: 幻影杀手全分支：immune/大成功 no-op、成功只上余悸且刷新、failure/critical failure 阈值上下处决或伤害+状态、死亡守护拦截、psychic 抗性只影响非处决伤害、7x7 地面技能不分敌我。
- 证据/问题: 每分支精确 HP/伤害/状态断言（L53-57、L138-143、L214-233、L262-271、L360-367）。

### tests/battle_runtime/skills/run_spell_disjunction_equipment_durability_regression.cs — 有效
- 断言数: 约20
- 验证内容: 裂解术装备耐久：两次失败摧毁普通装备、反序效果需 attack_success、豁免成功零损失、稀有度 +4 豁免加值达 DC。
- 证据/问题: 耐久 56→28→消失（L55-81）、save_result 字段（L199-208）。

### tests/battle_runtime/skills/run_time_stasis_regression.cs — 有效
- 断言数: 约40
- 验证内容: 时间静滞：elite/boss 降级 time_slow、余波豁免加值防连锁、temporal release/dispel 解静滞加余波而 cleanse 不解、静滞目标门禁、读条冻结与恢复、time_slow 读条减半、temporal-only 内容校验五组。
- 证据/问题: 读条进度精确值 500/750（L282-329）、内容校验错误计数（L352-396、L414-532）。

### tests/battle_runtime/skills/run_titan_colossus_form_regression.cs — 有效
- 断言数: 约15
- 验证内容: 泰坦巨神化：体型 large→huge 切换与到期恢复、消耗次数、status 记录前后体型、恢复 footprint 被占时保留 status 且不覆盖占位者。
- 证据/问题: 体型 category/int 双断言（L71-89、L115-124）、阻塞恢复（L154-164）。

### tests/battle_runtime/skills/run_trait_trigger_regression.cs — 有效
- 断言数: 约35
- 验证内容: 特质触发：halfling luck 重掷自然1并耗次数、savage attacks 暴击加一武器骰、relentless endurance 先于 death_ward 且每场一次、turn_start 播种/刷新、effective 实例独立 charge key、dispatch 表与运行时一致。
- 证据/问题: 精确掷骰/次数/触发断言（L56-74、L146-175、L187-221、L235-246）。

### tests/battle_runtime/skills/run_warrior_chain_slashes_regression.cs — 有效
- 断言数: 约35
- 验证内容: 三连斩：内容契约与 0-7 级曲线、近战门禁、三段一次扣费且各给连击层、首段未命中不取消后续、击杀即停。
- 证据/问题: 段日志/体力/层数断言（L119-129）、call_count 3 与 1（L165、L187）。

### tests/battle_runtime/skills/run_warrior_double_strike_regression.cs — 有效
- 断言数: 约30
- 验证内容: 双重打击：fixed_repeat schema 拒绝非法配置、内容契约、近战门禁、两段一次扣费、首段 miss 继续、首段击杀停第二段。
- 证据/问题: schema 拒绝消息（L51-75）、段日志与层数（L145-154、L205-212）。

### tests/battle_runtime/skills/run_warrior_hamstring_regression.cs — 有效
- 断言数: 约50
- 验证内容: 断筋斩成长契约、0-5 级效果/消耗曲线（含减速移动成本语义表验证）、等级描述文本。
- 证据/问题: 逐级期望值数组比对（L76-164）、描述包含/不包含（L198-221）。偏内容钉固但期望值硬编码、可失败。

### tests/battle_runtime/skills/run_warrior_heavy_blow_windup_regression.cs — 有效
- 断言数: 约80
- 验证内容: 重压斩蓄力：heavy 门禁独立数据、等级曲线与挡位上限、蓄力冻结行动但恢复体力且不可取消、伤害不打断硬控打断、time_slow/stasis 语义、目标移动落空、换武器打断、手动循环挡位、AI 挡位枚举/负担校验/quote invariant fail closed/延迟与 reserve 评分/生产评分致死排序、自动路径拒绝蓄力。
- 证据/问题: 全链路精确断言（L190-256、L297-341、L514-552、L584-646、L685-777、L869-895）。

### tests/battle_runtime/skills/run_warrior_night_pressure_regression.cs — 有效
- 断言数: 约30
- 验证内容: 夜幕压迫：定义与等级效果、refresh 保留最强惩罚、正式命令施加状态并扣费、只影响半径内敌人。
- 证据/问题: 合并语义（L79-99）、运行时 AP/斗气/冷却（L175-181）。

### tests/battle_runtime/skills/run_warrior_nine_echo_final_hammer_regression.cs — 有效
- 断言数: 约50
- 验证内容: 九响终槌：内容契约与攻击次数曲线、锤门禁、必中可重击规则（自然1不伪造暴击）、10 次攻击管线探针（前九次 1W 不放大、第十次 3W 只复制物理骰、附伤不复制）、3/6/9 命中状态阶梯、首 miss 继续。
- 证据/问题: 探针计数与倍率逐段断言（L186-237、L116-131）。

### tests/battle_runtime/skills/run_warrior_overhead_chop_regression.cs — 有效
- 断言数: 约45
- 验证内容: 重剑斩：内容契约（精确绑 greatsword 类型）、三档伤害模板、巨剑门禁（锤/长剑均拒绝）、5级主伤害/硬控追加/附伤三事件结构与暴击额外骰。
- 证据/问题: 事件逐字段断言（L182-211）、门禁三例（L107-124）。

### tests/battle_runtime/skills/run_warrior_over_shoulder_regression.cs — 有效
- 断言数: 约30
- 验证内容: 借势越肩：内容契约、近战门禁（长矛不扩射程）、命中伤害+落到目标背后、未命中不位移、落点占用/墙/屏障拒绝且不扣费。
- 证据/问题: 落点坐标与资源不变断言（L111-125、L142-144、L207-215）。

### tests/battle_runtime/skills/run_warrior_perfect_rhythm_regression.cs — 有效
- 断言数: 约35
- 验证内容: 完美节奏：内容契约、命中叠层按等级、连击攻击加成只对近战武器、维持费升档边界（60/65/125TU）、斗气不足与硬控终止+惩罚+冷却、快照 round-trip。
- 证据/问题: 斗气逐档精确值 880/860/640/600（L184-190）、终止断言组（L234-246）。

### tests/battle_runtime/skills/run_warrior_repeat_attack_mastery_bonus_regression.cs — 有效
- 断言数: 约35
- 验证内容: 连击熟练度：typed 资源成本、第五段命中才发 bonus（miss 不发）、weapon_attack_quality 读 payload reason、格挡精通只给物理命中、格挡等级曲线与逐次物理减伤（最低1、非物理不免、不反向造伤）。
- 证据/问题: miss/hit 对照 0/1（L87-115）、减伤预览四例（L539-559）。

### tests/battle_runtime/skills/run_warrior_repeat_skill_tier_regression.cs — 有效
- 断言数: 约25
- 验证内容: 双重打击/连击/圣剑连斩等级档与成长预算、连击免罚段数曲线、线性惩罚在免罚段后从 -1/-2 重启。
- 证据/问题: 阶段惩罚精确值（L60-69、L129-160）。

### tests/battle_runtime/skills/run_warrior_spin_slash_regression.cs — 有效
- 断言数: 约30
- 验证内容: 旋斩：内容契约（配置射程不被武器替换）、近战门禁、原地范围只伤相邻敌人不伤友军、中心不可投到邻格、扣费与 changed unit。
- 证据/问题: HP 三方对照与资源断言（L156-181）。

### tests/battle_runtime/skills/run_warrior_taunt_cognition_regression.cs — 有效
- 断言数: 约70
- 验证内容: 挑衅认知体系：内容契约与等级曲线、认知枚举封闭、效果认知门槛、上限取最严、不合格目标支付前拒绝、4级横排三只打理性目标、疯狂暂停挑衅不删状态且计时继续、认知 clone/round-trip 严格、官方敌人认知分类。
- 证据/问题: 端到端预览/结算断言（L342-395、L459-523、L600-642）、暂停/恢复计时（L725-781）。

---

## 统计

- 有效: 53
- 偏弱: 2
- 无效: 0
- 合计: 55（rules 25 + skills 30；与任务书"49/24"口径不符，skills 实际多 6 个文件）

### 偏弱清单
1. `tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.cs` — `TestTypesArePlainCSharp` 调用的 `AssertPlainCSharpType` 为空方法（L240-242），该测试方法零断言；其余 5 个方法有效。
2. `tests/battle_runtime/skills/run_passive_status_orchestrator_regression.cs` — `TestPassiveContextAndResolversNoLongerRequireGodotRegistration` 调用的 `AssertPlainType` 为空方法（L418-420），该测试方法零断言；其余 4 个方法有效。

### 无效清单
（无）

### 附记（不降级但值得注意）
- `run_battle_execution_rules_contract_regression.cs` L157-159 存在空 `AssertPlainType`，但从未被调用（死代码），四个 Test 方法均有效。
- `run_magic_backlash_regression.cs` L45-50 有一处 Set 后立即 Get 回读（fumble protection 存取对），仅 2 条断言，文件其余约 60 条断言均为行为级。
- `run_attack_policy_parity_regression.cs` L386 `AssertPreviewEq` 在双方均为 null 时静默通过，可能掩盖意外的双 null。
- 多个技能测试的 "AuthoredContract" 方法为 .tres 内容钉固（硬编码期望 vs 资源实际值），可真实失败，按口径判有效；若审计口径将"内容钉固"也视为低价值，则 archer/warrior 系列约 15 个文件会整体降一档，请主控方决定。

## 批次 05 明细

# 测试有效性审计报告 — batch05

范围：`tests/battle_runtime/{fate,objectives,state_schema,rendering,terrain,benchmarks}/run_*.cs`，共 43 个文件，全部逐字精读。

判定口径：
- **有效**：断言针对被测系统的行为/状态/边界/错误路径，具体且可能失败。
- **偏弱**：存在弱断言模式（非空/Count>0、Set 后立即 Get 回读、回读常量、源码 Contains 等），但仍有部分真实验证。
- **无效**：恒真断言、断言不执行、与即时生成输出对比、空方法、catch 吞失败。

---

## tests/battle_runtime/fate/

### tests/battle_runtime/fate/run_black_star_brand_regression.cs — 有效
- 断言数: 约25
- 验证内容: 黑星烙印技能的 calamity 成本递增、普通/精英双形态、格挡/反击封锁与首次受击穿透窗口。
- 证据/问题: 3 个 Test* 方法均做精确行为断言（成本 0→1 切换 L64/L71、烙印打断 guarding L126-129、elite 首击伤害高于二击且 guard_ignore_applied>0 L219-230、非法施放不扣 AP/calamity L85-90）。

### tests/battle_runtime/fate/run_crown_break_regression.cs — 有效
- 断言数: 约30
- 验证内容: 折冠三个分支（断牙/折手/遮目）的状态写入互斥、反击封锁、追击压段、非法目标 preview/issue 双路径拒绝。
- 证据/问题: 精确断言分支状态互斥（L88-90）、折手把连斩预览压成 1 段且 Aura 只扣首段（L179-193）、非法目标不扣 AP/calamity 且不写入状态（L294-312）。文件尾部 SelectionRuntimeProxy（L584-721）为未被使用的死代码，不影响判定。

### tests/battle_runtime/fate/run_doom_sentence_regression.cs — 有效
- 断言数: 约28
- 验证内容: 厄命宣判的 verdict 写入、全队增伤、2 debuff 主技能封锁阈值、每战 1 次限制、calamity cap 不足阻断。
- 证据/问题: 精确断言 debuff 计数阈值（1 个允许 L130-133、2 个阻断 L137-144）、每战一次拒绝后不扣资源不污染第二目标（L192-202）、增伤前后对比（L92-95）。

### tests/battle_runtime/fate/run_fate_attack_formula_regression.cs — 有效
- 断言数: 约28
- 验证内容: FateAttackFormula 的暴击门骰面、失手下限、战斗运气分/暴击阈值、注入 RNG 的劣势骰规则。
- 证据/问题: 表驱动精确等值断言（L27-86 共 17 组用例），StubRollSource 验证劣势取低值且恰好消费 2 次 RNG（L97-104）。

### tests/battle_runtime/fate/run_fate_calamity_drop_regression.cs — 有效
- 断言数: 约15
- 验证内容: 战后 calamity 碎片结算：普通战章节上限裁切、elite/boss 旁路、烙印/宣判固定掉落与返还折算。
- 证据/问题: 精确断言提交数量裁切到章节剩余额度（L61-85）、旁路不受 cap 影响且不污染标记（L135-149）、烙印 elite 固定掉 1 碎片（L173-182）、宣判 boss 返还 5 calamity 折算 2 碎片（L237-261）。

### tests/battle_runtime/fate/run_fate_low_luck_tactical_skills_regression.cs — 有效
- 断言数: 约30
- 验证内容: 低运战术技能组：失手成筹 calamity 加成、黑契推进三分支代价与必中不暴、断命换位、黑冠封印 boss 限定。
- 证据/问题: 精确断言 HP 代价 28-10（L136-140）、preview 命中率=100 且 force_hit_no_crit（L405-412）、换位坐标互换（L229-230）、非 boss elite 目标被拒（L251-254）。

### tests/battle_runtime/fate/run_fate_typed_event_regression.cs — 有效
- 断言数: 约10
- 验证内容: typed fate 事件总线授予 calamity 不依赖字典 key，且公开面不再暴露 GDictionary 参数。
- 证据/问题: 行为断言 calamity=1（L48-52、L78-82）+ 反射遍历 BattleFateEventBus 全部方法/事件参数拒绝 GDictionary（L93-125），两类都可能真实失败。

### tests/battle_runtime/fate/run_low_luck_relic_regression.cs — 有效
- 断言数: 约30
- 验证内容: 四件低运遗物的装备属性注入、固定掉落路径、黑星楔钉穿防+破绽代价、血债披肩减伤/倒地返 AP/恢复减半、亡途灯笼揭路。
- 证据/问题: 精确断言恢复量公式期望值（L292-307）、破绽目标承伤更高（L185-188）、固定掉落 drop_type/source_kind（L611-620）、队友倒地返 1 AP（L239）。

### tests/battle_runtime/fate/run_misfortune_service_regression.cs — 有效
- 断言数: 约18
- 验证内容: MisfortuneService 技能门禁、全理由 calamity 累计与 cap clamp、首次大失败授 reverse_fortune、快照暴露 calamity。
- 证据/问题: 精确断言 cap=6/3、累计=6、clamp 不增长、reverse_fortune duration=60、快照 calamity=6（L73-148）。TestSkillGatesUseTypedRules 中 3 条为非空消息断言（L23-34）偏弱，但同方法含 True/False 门禁判定（L35-42），不影响整档。

---

## tests/battle_runtime/objectives/

### tests/battle_runtime/objectives/run_battle_boss_objective_regression.cs — 有效
- 断言数: 约25
- 验证内容: 首领目标：随从不结算、首领死即胜、持久队员全灭判负、同归于尽平局、缺失/重复绑定拒绝、纯召唤阵容拒绝。
- 证据/问题: 6 个 Test* 方法全部精确断言 flush 结果、终局 outcome/end_reason、召唤物不替代持久队员（L125-143）、原子变更内不提前锁存（L162-170）。

### tests/battle_runtime/objectives/run_battle_control_objective_regression.cs — 有效
- 断言数: 约45
- 验证内容: 区域占领：定义校验、区域冻结/重叠拒绝、独占计分、争夺/中立不计分、部分 footprint 不占领、同刻达标平局、计分优先级、HUD 快照、深拷贝独立性、正式遭遇装配。
- 证据/问题: 12 个 Test* 方法均精确断言分数/outcome/end_reason/HUD 文本（如 L163-201、L458-476），含 Throws 边界校验（L46-79）。

### tests/battle_runtime/objectives/run_battle_defense_objective_regression.cs — 有效
- 断言数: 约30
- 验证内容: 防守目标：守时成功、目标/队伍提前覆灭判负、原子边界优先级（目标死>到时>队伍灭）、定义校验、绑定拒绝、正式遭遇 200 TU 冻结。
- 证据/问题: 精确断言原子竞态判胜/判负（L141-219）、正式遭遇 target 绑定与 DeadlineTu=200（L398-405）。

### tests/battle_runtime/objectives/run_battle_elimination_objective_regression.cs — 有效
- 断言数: 约45
- 验证内容: 歼灭目标：进行中不结算、原子同灭平局且只结算一次、晋升 modal 锁存后完成、非法终局组合拒绝、无 runtime objective 的恢复拒绝。
- 证据/问题: 精确断言幂等性（重复 flush 不再发 battle_ended/日志 L153-176）、锁存决策引用不变（L259-262）、Throws 校验（L326-355）。

### tests/battle_runtime/objectives/run_battle_escape_objective_regression.cs — 有效
- 断言数: 约35
- 验证内容: 逃离目标：全员抵达成功、敌方全灭不替代、大型单位完整 footprint、必需队员死亡判负、召唤物排除、内部边/容量/回溯落点校验、原子抵达+死亡判负。
- 证据/问题: 10 个 Test* 方法全部行为断言，含 2x2 footprint 容量回溯正/反例（L436-495、L563-621）。

### tests/battle_runtime/objectives/run_battle_intercept_objective_regression.cs — 有效
- 断言数: 约30
- 验证内容: 截击目标：目标死即胜、目标进逃脱区判负、队伍覆灭判负、同灭平局、大型目标完整 footprint 才判逃脱、绑定拒绝、正式遭遇 roster 绑定。
- 证据/问题: 精确断言半身进区不判逃脱/全身进区判负（L257-293）、ObjectiveBindingUsesTerrainRetry 正反例（L42-71）。

### tests/battle_runtime/objectives/run_battle_node_operation_objective_regression.cs — 有效
- 断言数: 约30
- 验证内容: 节点作业：定义校验、节点冻结唯一坐标、相邻交互完成耗 AP、远距/重复操作拒绝、队伍覆灭判负、敌军全灭不替代、原子最后节点优先。
- 证据/问题: 精确断言 AP 消耗 1→0（L146）、重复操作拒且不扣 AP（L196-200）、远距 preview 拒绝（L184-187）。

### tests/battle_runtime/objectives/run_battle_rescue_escort_objective_regression.cs — 有效
- 断言数: 约30
- 验证内容: 救援/护送目标：相邻解救成功耗 AP、远距拒绝并提示、目标/队伍死亡判负、护送到点成功、HUD 标题与 TargetSecured/ReachedExit 投影、正式遭遇场景 NPC。
- 证据/问题: 精确断言解救耗 1 AP 且同批结束（L170-171）、远距 preview 拒绝且日志含“相邻位置”（L212-217）、HUD 状态投影（L311-318）。

---

## tests/battle_runtime/state_schema/

### tests/battle_runtime/state_schema/run_battle_cell_state_schema_regression.cs — 有效
- 断言数: 约45
- 验证内容: BattleCellState 严格 schema：合法 roundtrip 全字段保留、缺/多/错类型/字符串数字/坏 entry 一律拒绝、null edge 序列化、owner mutation 规范化。
- 证据/问题: 12 个 Test* 方法覆盖正/反路径，拒绝类断言直接调 FromDictionary 判 null（L108-218），规范化断言 clamp/派生值（L272-298）。

### tests/battle_runtime/state_schema/run_battle_edge_feature_state_schema_regression.cs — 有效
- 断言数: 约30
- 验证内容: BattleEdgeFeatureState schema：wall/none roundtrip、缺/多字段、错类型、字符串 bool/int、空必填 enum、负 render_layers 拒绝、duplicate。
- 证据/问题: 拒绝断言均判 FromDictionary==null（L84-174），roundtrip 精确逐字段等值（L38-57）。

### tests/battle_runtime/state_schema/run_battle_state_owner_api_regression.cs — 有效
- 断言数: 约18
- 验证内容: BattleState SetCell/SetUnits owner API：revision 递增、坐标写回、批量替换过滤空 id、几何派生 footprint/occupied_coords 重建、missing owner 重建。
- 证据/问题: 精确断言 revision 单调增（L23、L56）、SetUnits 后 hero footprint 重建为 1 格 anchor (5,6)（L68-74）、missing geometry 重建为 medium 默认（L75-85）。

### tests/battle_runtime/state_schema/run_battle_status_effect_state_schema_regression.cs — 有效
- 断言数: 约130
- 验证内容: BattleStatusEffectState schema：基础 roundtrip、严格拒绝（缺/多/错类型/字符串/空 id/负 duration/显式 0/显式 false）、20+ 组 typed 字段经 params 边界投影与回读、duplicate。
- 证据/问题: 25 个 Test* 方法逐一断言 typed 字段投影值与 roundtrip 保留值（如 L331-363、L686-731），并验证 formal key 不滞留 @params、string key 不进 typed map（L1002-1014）。

### tests/battle_runtime/state_schema/run_battle_terrain_effect_state_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: BattleTerrainEffectState schema：lifetime_policy 不进 params、applied status/overlay/accuracy spec/不叠堆字段 roundtrip、顶层 lifetime_policy 与非法 team filter 拒绝。
- 证据/问题: 精确逐字段等值断言（L87-177）+ 拒绝断言（L55-64、L179-188）。

### tests/battle_runtime/state_schema/run_battle_timeline_state_schema_regression.cs — 有效
- 断言数: 约20
- 验证内容: BattleTimelineState schema：roundtrip、缺/多字段、错类型、字符串数字、空 ready id、非数组、数值边界。
- 证据/问题: 拒绝断言均判 FromDictionary==null（L57-115），roundtrip 精确等值（L47-54）。

### tests/battle_runtime/state_schema/run_battle_unit_state_owner_api_regression.cs — 有效
- 断言数: 约250
- 验证内容: BattleUnitState 22 组 owner API：HP/资源 clamp、exact seam 保留 raw sentinel、read view detached、clone 不共享、tag 过滤去重保序、cooldown/action clock 推进、turn 生命周期、shield/装备能力/武器投影原子替换。
- 证据/问题: 虽为“写后读”形态，但断言针对 clamp（L69-83）、余数进位（L1525-1551）、detach 隔离（L138-142）、失败 Replace 不提交部分候选（L1918-1970）等可失败行为，非平凡回读。

### tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs — 有效
- 断言数: 约150
- 验证内容: BattleUnitState 完整 schema 契约：roundtrip 全字段、严格加载保留 raw、clone 深拷贝隔离、runtime-only 字段不进 payload、30+ 类畸形 payload 拒绝、geometry 边界 fail-fast、体型映射。
- 证据/问题: 26 个 Test* 方法，拒绝断言覆盖每类畸形（L1206-1607），clone 隔离逐项验证（L695-776）、runtime-only 字段双向验证（L1086-1120）。

---

## tests/battle_runtime/rendering/

### tests/battle_runtime/rendering/run_battle_board_regression.cs — 有效
- 断言数: 约40（含循环放大）
- 验证内容: canyon 地形生成确定性/默认尺寸契约/零尺寸拒绝、四种正式 profile 完整布局、layout cells 单次移交、render profile source spec 贴图存在性、BattleBoard2D 实渲染。
- 证据/问题: 精确断言种子确定性签名（L41-45）、零尺寸 ArgumentOutOfRange（L100-123）、TakeCells 二次抛异常（L202-211）、板上 unit_layer 子节点=2（L246）。TestRenderProfileFormalSourceSpecs 中 profile 字段等值断言（L143-147）是经由 ForTerrainProfileId 生产解析路径的映射验证，可失败。

### tests/battle_runtime/rendering/run_battle_board_render_profile_schema_regression.cs — 偏弱
- 断言数: 2
- 验证内容: BattleBoardRenderProfile.SetSourceSpecs 后 GetPrimaryLandFile/GetSelectedMarkerFile 取回文件。
- 证据/问题: L16-51：典型 Set 后立即 Get 回读，断言只覆盖“刚写入的 spec 被按键取出”，唯一实质逻辑是 getter 的 key/role 查找；无反向用例（如缺失 land spec、多 spec 选择优先级），字段映射错误以外的退化实现也能通过。

### tests/battle_runtime/rendering/run_battle_command_dock_snapshot_regression.cs — 有效
- 断言数: 约25
- 验证内容: BattleHudAdapter 指令坞快照：各状态下 enable 位与 hint 文本、modal 阻断、蓄力挡位切换、自动模式、最近日志裁剪保序。
- 证据/问题: 6 个 Test* 方法精确断言布尔位与完整中文字符串（L42-46、L109-115、L141-144），hint 文案错误即失败。

### tests/battle_runtime/rendering/run_battle_hover_hp_predict_regression.cs — 有效
- 断言数: 10
- 验证内容: hover HP 预扣条：有伤害分层显示剩余区间、无伤害满填充、非法目标不渲染预扣段。
- 证据/问题: 精确断言 ProgressBar Value/MaxValue 数值与 Label 完整文本（L28-31、L44-45、L58-59）。

### tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs — 有效
- 断言数: 约18
- 验证内容: 死亡律令 hover 预览：高 HP 目标非法、低 HP 显示 execute 分支与命中率、HUD 不解析 log_lines/伪造伤害文本。
- 证据/问题: 精确断言分支 kind=execute、文本含“命中率/死亡律令/灵魂裂解”（L70-72）、POISON_LOG/999 反向断言（L114-129、L137-138）。

### tests/battle_runtime/rendering/run_battle_status_badge_regression.cs — 有效
- 断言数: 约18
- 验证内容: 状态徽章投影：语义表标签回落、display_label 优先、debuff 判定与 override、tooltip 时长文本、徽章格式化、hover 渲染显隐。
- 证据/问题: 精确断言标签/层数/剩余 TU/格式化文本（L48-76）、override 判减益（L102-103）、overlay 子节点与文本（L119-125）。

### tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs — 有效
- 断言数: 约25
- 验证内容: Phantasmal Kill 地面 hover：7x7 范围、目标计数按阵营/免疫/处决风险分类、save_branch_preview 为拷贝态、HUD 文本来自结构化摘要。
- 证据/问题: 精确断言 TargetCoords=49、各类计数（L58-79）、篡改返回字典后重读仍为 1（L81-88）、文本含“友军/处决/免疫”且不含 POISON_LOG（L124-132）。

---

## tests/battle_runtime/terrain/

### tests/battle_runtime/terrain/run_battle_cell_state_owner_api_regression.cs — 偏弱
- 断言数: 9
- 验证内容: BattleCellState SetCoord/SetOccupant/ClearOccupant/SetPassable/SetMoveCost。
- 证据/问题: L19-30、L37-38 为 Set 后立即读同一公开字段的纯回读（setter 只有一行赋值，几乎恒真）；仅 L40-41（SetMoveCost(-10)→clamp 1）与 L43-44 是实质规范化验证。且同主题已被 state_schema/run_battle_cell_state_schema_regression.cs 的 TestOwnerMutationApiNormalizesCellFields（含派生 current_height/stack_layer）更严覆盖。

### tests/battle_runtime/terrain/run_battle_edge_face_service_regression.cs — 有效
- 断言数: 约12
- 验证内容: BattleEdgeService：高差/墙面生成 edge face、阻断移动与占位、dirty 标记后缓存重建。
- 证据/问题: 精确断言 height_difference/drop_layers=2（L34-35）、BlocksMove/Occupancy（L38-47）、加墙+MarkRuntimeEdgesDirty 后不再可通行（L73-76）。

### tests/battle_runtime/terrain/run_battle_grid_service_pathfinding_invariants.cs — 有效
- 断言数: 0（自身无断言）
- 验证内容: 纯包装器，委托 run_battle_grid_service_pathfinding_invariants_typed.RunForWrapper() 并透传 TestResult。
- 证据/问题: L7 直接返回 typed 运行器结果，断言全部在 typed 文件中执行；作为入口有效。

### tests/battle_runtime/terrain/run_battle_grid_service_pathfinding_invariants_typed.cs — 有效
- 断言数: 约15（Fail 形式，随机化部分循环放大）
- 验证内容: 网格寻路不变量：步长成本下限、A* 最优性、预算早停、阻挡占位失败消息、A* 与参考 Dijkstra 差分（固定泥带+12 组随机）、路径树差分、占位阻断。
- 证据/问题: 与独立参考实现 ReferenceDijkstraCost 差分对比（L241-245、L312-317、L372-378），随机 0 样本时显式失败（L321-324），早停验证 cost provider 零调用（L156-159）。

### tests/battle_runtime/terrain/run_battle_terrain_lifetime_regression.cs — 有效
- 断言数: 约12
- 验证内容: 地形效果生命周期：battle 级 crater/rubble 不随 TU 消失、timed dust 到期消失、move cost max 叠堆、tick 到点附加 formal duration 的 burning。
- 证据/问题: 精确断言 max 叠堆=3 而非 5（L73-80）、55 TU 后 battle 级仍在 timed 级消失（L86-95）、tick 前后 burning 有无与 duration=40（L146-156）。

### tests/battle_runtime/terrain/run_battle_terrain_topology_service_regression.cs — 有效
- 断言数: 约12
- 验证内容: 水域拓扑重分类：低岸出口→flowing+流向、封闭近岸→shallow、ground effect 服务落地 typed 变更并记录 changed_coords。
- 证据/问题: 精确断言 before/after 地形与流向（L42-56、L81-90）、服务不直接改格（L58-68）、落地后格地形/流向/changed_coords（L109-123）。

### tests/battle_runtime/terrain/run_meteor_swarm_terrain_modifier_regression.cs — 有效
- 断言数: 约12
- 验证内容: 陨石尘土命中修饰：schema 驱动而非 source id、距离门槛、同 stack_key 端点不叠、timed 到期失效而 battle 级保留。
- 证据/问题: 精确断言 SituationalAttackPenalty=2、breakdown 条数与 -2 值（L39-51、L112-121）、相邻无惩罚（L76-80）、到期后 0 条且 rubble 成本仍 2（L151-163）。

---

## tests/battle_runtime/benchmarks/

### tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.cs — 有效
- 断言数: 约10（Fail 形式门槛）
- 验证内容: AI 全战性能基线：夹具契约（终结技定义/武器族/可生成性/施放阻断）、战斗必须 battle_ended 否则失败、停滞检测、与已提交基线文件的 avg/p50/p95 容差对比（回归则 exit 1）。
- 证据/问题: ValidateFinisherContract 对运行时技能定义做 6 类可失败检查（L758-833）；未完成即 _test.Fail（L245-250）；AiBaselineDiff.Compare 超容差返回 exit 1（L166-206）。验证对象是性能而非功能正确性，但门槛真实可失败。

### tests/battle_runtime/benchmarks/run_battle_panel_full_refresh_benchmark.cs — 偏弱
- 断言数: 约5（Fail 形式门槛）
- 验证内容: BattleMapPanel 三种刷新路径（full/overlay/unit_delta）计时对比。
- 证据/问题: 无任何性能阈值断言，计时结果仅打印供人工阅读（L92-95）；真实可失败的仅有 render-ready 有限帧恢复门槛（L61-62、L136-142、L214-220）与单位摆放/选中环构建失败（L45、L415）——这些更接近“不卡死”冒烟，无法捕获性能回退。

### tests/battle_runtime/benchmarks/run_longsword_3v3_mastery_analysis.cs — 无效
- 断言数: 0
- 验证内容: 1000 场 3v3 镜像模拟的 charge/heavy_strike 尝试/成功/熟练度统计聚合。
- 证据/问题: 全文无一条 _test.True/Eq/Fail 行为断言；_test 仅用于 Finish 包装（L24）。仅有两个出口门槛：场景资源加载失败 return 1（L36-40）、未全部 battle_ended return 2（L185），其余输出（JSON 报告 L141-174）为纯统计聚合，不与任何期望值对比。作为“测试”无验证作用，仅完成率冒烟。

### tests/battle_runtime/benchmarks/run_mixed_2s1a_mirror_analysis.cs — 无效
- 断言数: 0
- 验证内容: 混合 2 剑 1 弓镜像模拟的五技能尝试/成功/熟练度与胜负率统计聚合。
- 证据/问题: 与上者同构：无任何 _test 断言（L28 仅 Finish），仅场景加载失败 return 1（L43-47）与未全部结束 return 2（L267）两个完成率门槛，报告为纯聚合输出（L204-256）。roster 不支持仅打印告警不失败（L301-305）。

---

## 统计

| 判定 | 数量 |
|---|---|
| 有效 | 38 |
| 偏弱 | 3 |
| 无效 | 2 |
| 合计 | 43 |

### 偏弱清单
1. `tests/battle_runtime/rendering/run_battle_board_render_profile_schema_regression.cs` — 2 条断言均为 SetSourceSpecs 后立即 Get 回读（L16-51），无反向用例。
2. `tests/battle_runtime/terrain/run_battle_cell_state_owner_api_regression.cs` — 9 条断言中 7 条为单行 setter 的即时回读（L19-38），仅 move_cost clamp（L40-44）为实质验证，且被 state_schema 同名主题更严覆盖。
3. `tests/battle_runtime/benchmarks/run_battle_panel_full_refresh_benchmark.cs` — 无性能阈值断言，仅 render-ready/摆放失败冒烟门槛（L61-62、L136-142、L214-220），计时只打印。

### 无效清单
1. `tests/battle_runtime/benchmarks/run_longsword_3v3_mastery_analysis.cs` — 0 断言，纯统计聚合，仅场景加载/战斗完成率 exit 门槛（L36-40、L185）。
2. `tests/battle_runtime/benchmarks/run_mixed_2s1a_mirror_analysis.cs` — 0 断言，纯统计聚合，仅场景加载/战斗完成率 exit 门槛（L43-47、L267）。

### 备注
- `tests/battle_runtime/terrain/run_battle_grid_service_pathfinding_invariants.cs` 自身 0 断言，但为 typed 运行器的透传入口，计为有效。
- fate/state_schema/objectives 三个目录共 25 个文件全部为强行为断言，无偏弱/无效项。

## 批次 06 明细

# 测试有效性审计报告 — batch06

范围：`tests/battle_runtime/simulation/`（10）、`tests/world_map/runtime/`（29，目录实际为 29 而非任务书所述 28）、`tests/world_map/schema/`（3），共 **42** 个 `run_*.cs`。每个文件均已全文精读（含 106KB 的 settlement_command_handler、74KB 的 npc_quest_offer、58KB 的 persist_failure_rollback 全文）。

## tests/battle_runtime/simulation/

### tests/battle_runtime/simulation/run_battle_ai_vs_ai_simulation_regression.cs — 有效
- 断言数: 约24
- 验证内容: 加载 ai_vs_ai 示例场景跑 2 seed 单 profile entry、双方阵营 AI turn trace、control_mode、summary 阵营指标。
- 证据/问题: 精确断言 `ProfileEntries.Count==1`（L57）、每场 2 runs（L73-77）、`control_mode=="ai"`（L103-109）、player/hostile 双阵营 trace 都出现（L152-153）。瑕疵：L80-83 断言 `run != null` 但消息写"应保留 battle_ended 标记"，L114-117 断言 `summary != null` 但消息写 wins_by_faction 汇总——两处断言消息与内容不符，不影响断言本身有效性。

### tests/battle_runtime/simulation/run_battle_balance_simulation.cs — 偏弱
- 断言数: 0（无 TestHarness 断言）
- 验证内容: 按命令行参数跑 balance scenario，仅在 `report.IsComplete==false` 时返回退出码 2。
- 证据/问题: 全文无任何 `_test.*` 行为断言，唯一检查是 L64-70 的 `report.IsComplete` 完整性门槛；本质是 balance 运行器而非回归测试（AGENTS.md 也将其排除在常规回归之外）。完整性门槛可失败（stall/budget），故不算无效，但无任何行为/数值验证。

### tests/battle_runtime/simulation/run_battle_sim_override_applier_regression.cs — 有效
- 断言数: 约120（含 71 路径循环）
- 验证内容: OverrideApplier 深路径 patch 写回 typed 定义、深拷贝不改原资源、未知路径报错、state 作用域隔离、tuner 71 标量路径 parity。
- 证据/问题: 精确断言 patched=6000/原资源=3000（L86-102）、`ReferenceEquals` 深拷贝（L168-171）、未知路径 `errors.Count>0`（L293）、state-scoped patch 只改 pressure 不改 engage（L408-425）、拒绝未列入契约的标量路径（L249-260）。

### tests/battle_runtime/simulation/run_battle_sim_report_builder_regression.cs — 有效
- 断言数: 约55
- 验证内容: 终止分类（Ended/IdleStall/Budget/InvalidRuntime）、未完成 run 不污染汇总均值/胜场/技能计数、comparison delta、null run 占位投影。
- 证据/问题: 边界精确断言 `AverageFinalTu==20.0f`（只用 completed sample，L157）、`WinRateByFaction==1.0f`（L163）、`termination_kind=="idle_stall"`（L206-210）、delta=-1/-2（L320-323）、缺 typed decision 的 ended run 记为 invalid（L242-246）。

### tests/battle_runtime/simulation/run_battle_sim_start_failure_regression.cs — 有效
- 断言数: 约50
- 验证内容: 空 roster→invalid_start_units、布阵穷尽→placement_exhausted、首轮可达性失败后重试成功并清除失败快照。
- 证据/问题: 精确断言 reason 字符串、Iterations/IdleLoops/TimelineSteps 全为 0（L174-177）、`terrainGenerator.GenerateCallCount==2`（L102-106）、成功重试后 `GetLastStartFailureSnapshot().IsEmpty`（L111-114），三种投影（Godot/file/trace summary）逐一核对。

### tests/battle_runtime/simulation/run_battle_sim_trace_summary_builder_regression.cs — 有效
- 断言数: 约45
- 验证内容: TraceSummaryBuilder 压缩 typed report：计数、outcome/end_reason、focus 回合过滤、screening 字段保留、top candidate 上限、batch 超时事实、invalid run 占位。
- 证据/问题: 输入 fixture 为测试内构造，但断言全部对照硬编码期望值（save 50%、期望伤害 9 L65-66、screening_bonus 45 L90、TopCandidateLimit=1 生效 L113、run_count=3/invalid=2 L157-160），验证的是 builder 的变换行为而非自比自。

### tests/battle_runtime/simulation/run_battle_sim_typed_report_regression.cs — 有效
- 断言数: 约40
- 验证内容: StringName skill_level_map key、malformed unit entry 拒绝、typed metrics 汇总进 profile summary、standalone raw report 排除未完成 run。
- 证据/问题: 精确断言 `GetKnownSkillLevelTyped==4`（L35-39）、异常消息含 `ally_units[0]`（L57-65）、`avg_iterations==12.0`（只用完成局，L275）、未完成局伤害 9999 不污染 `total_damage_done==60`（L285）、`battle_ended=true/false` 投影（L299-305）。

### tests/battle_runtime/simulation/run_battle_sim_unit_spec_defaults_regression.cs — 有效
- 断言数: 约13
- 验证内容: BattleSimUnitSpec 默认攻击加值/AC 初始化、base_attributes 走正式 AttributeService 公式、override 覆盖与 5TU 归一。
- 证据/问题: 全部精确数值断言：默认 attack_bonus=4（L28-32）、HP 公式=16（L52）、AC=11（L56）、忽略最终 armor_class=99 改算组件=14（L100）、threshold 47→45 归一（L122-126）。

### tests/battle_runtime/simulation/run_battle_simulation_regression.cs — 有效
- 断言数: 约22
- 验证内容: 同批 ready AI 在同一 TU 激活不吞 timeline tick；profile patch（stamina_cost=999）后 AI 从 suppressive_fire 回落 pinning_shot。
- 证据/问题: 精确断言 `firstReadyTurns[0]==5 && firstReadyTurns[1]==5`（L69-72）、`IterationBudgetExhaustedRunCount==2`（L117-121）、baseline 含 archer_suppressive_fire 而 patched 不含且含 pinning_shot（L146-157）。

### tests/battle_runtime/simulation/run_battlesim_formal_fixture_regression.cs — 有效（含 1 个空方法，点名）
- 断言数: 约90
- 验证内容: 6v12/2s1a formal fixture 建卡：seed 复现性、高位 seed 不截断、逐 rank 独立生命骰、主角幸运烘焙、装备配置、typed enemy handoff 生命周期 lease 计数。
- 证据/问题: **空方法：`TestFixtureNoLongerRegistersGlobalClass()`（L120-122）方法体为空，无任何断言，属于占位的空调用槽位。** 其余方法均为真实验证：同 seed 属性字符串相等/异 seed 不等（L235/245）、逐 rank HP 重算并与旧逻辑区分（L296-310）、18 单位开战（L534-538）、lease 计数精确 +2/回基线（L496-500/592-595）。

## tests/world_map/runtime/

### tests/world_map/runtime/run_character_info_payload_schema_regression.cs — 有效
- 断言数: 约5
- 验证内容: CharacterInfoWindow 接受 runtime payload 渲染标题/section；tooltip entry 挂为悬停 tooltip 而非内联。
- 证据/问题: 实例化真实场景后断言 `title_label.Text=="Hero"`（L52）、`sections_container.GetChildCount()==1`（L53/94-98）、递归查找 tooltip 文本挂在 Control 上（L99-102）。

### tests/world_map/runtime/run_game_runtime_party_command_handler_regression.cs — 有效
- 断言数: 约28
- 验证内容: party handler 委托面、替补/上阵/队长状态同步、持久化失败不回滚已完成装备变更。
- 证据/问题: 精确断言 active/reserve 成员迁移（L71-78）、主角不可移入替补（L96-105）、队长回退第一上阵（L130-134）、持久化失败后装备与仓库扣减仍生效且状态文案含"但队伍状态持久化失败。"（L156-184）。

### tests/world_map/runtime/run_game_runtime_pending_battle_request_regression.cs — 有效
- 断言数: 约40
- 验证内容: 缺目标/目标绑定失败/布阵穷尽→failed 且无 pending 残留并释放 save lock；pending 请求 typed 状态与 context 克隆隔离；延迟终端失败 flush。
- 证据/问题: 错误路径精确断言 reason=="invalid_objective_binding"/"placement_exhausted"（L135-139/221-225）、context 克隆后外部修改不影响存储值（L292-305）、save lock 在 pending 保持/终端失败释放（L397-400/513-516）、world_step flush 前落后、flush 后一致（L491-529）。

### tests/world_map/runtime/run_game_runtime_reward_flow_handler_regression.cs — 有效
- 断言数: 约40
- 验证内容: reward/promotion modal 路由、各 modal 关闭路径与状态文案、reward 不可普通关闭、character_info 上下文清理。
- 证据/问题: 精确断言错误文案（L41-42/56-57/141-142）、modal kind 切换（L37/86/100）、`GetActiveReward()==null`（L148）、promotion 覆盖时 character_info context 清空（L180-185）。

### tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs — 有效
- 断言数: 约130
- 验证内容: settlement handler 全表面：research 扣费与奖励轮换、契约板条目过滤/接取/领奖/repeatable、悬赏板据点绑定、商店 schema 拒绝、伪造 payload 拒绝、驿站换乘扣费。
- 证据/问题: 全文精读 1811 行。精确断言金币 250→50（L102）、第二条研究奖励切到 research_guard_break（L138）、契约条目列表精确序列（L220/481）、StringName 类型字段拒绝（L954-994）、伪造 interaction_script_id 仍按真实入口（L1097-1099）、无 sell_price 不可出售且金币/库存不变（L920-922）。

### tests/world_map/runtime/run_game_runtime_world_encounter_regression.cs — 有效
- 断言数: 约11
- 验证内容: 附近遭遇条目按距离排序/跳过已清除/limit 生效；成长同步不吞 world_step。
- 证据/问题: 精确断言 `entries.Count==2`、首条 near_anchor（L45-48）、limit=1（L54-56）、推进后 `growth_stage==1` 且 world_step==1（L110-124）。

### tests/world_map/runtime/run_game_runtime_world_event_regression.cs — 有效
- 断言数: 约6
- 验证内容: 附近世界事件仅含已发现、按距离排序、字段投影完整。
- 证据/问题: 精确断言 Count==2（far/near 收录、hidden 排除，L45）、排序与 event_type/target_submap_id 字段（L48-52）。

### tests/world_map/runtime/run_npc_quest_offer_regression.cs — 有效
- 断言数: 约90（20 个 Test 方法）
- 验证内容: NPC 委托面板全流程：打开/渠道过滤/前置锁定/确认流/接取/提交物品/领奖/关闭生命周期/各类拒绝（无面板、错据点、错入口、错任务、错渠道、错 provider、确认绕过）。
- 证据/问题: 全文精读 2019 行，20 个 Test 方法均含具体断言。精确断言 Entries.Count==2（L132）、confirm 两段流消息（L608-612/640-643）、提交后库存=0 且任务 claimable（L516-519）、领奖后 gold==130（L541）、各拒绝路径精确错误文案（L729-732/791-795/1108-1112/1503-1507）。

### tests/world_map/runtime/run_quest_accept_encounter_binding_regression.cs — 有效
- 断言数: 约35
- 验证内容: 任务接取绑定遭遇的操作顺序（capture→spawn→accept→commit）、各失败点回滚、stage 冲突拒绝、cleared 锚点重建、放置确定性。
- 证据/问题: RecordingQuestCommandPort 记录操作序列精确比对 `"capture,spawn,accept,remove,rollback"`（L126-130）、stage 冲突时原锚点 stage/坐标不变（L285-291）、放置优先正南 (6,7)、fallback (5,7)（L345/354-358）、同 coord/同 id 双添加拒绝（L382-393）。

### tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs — 有效
- 断言数: 约4
- 验证内容: 客户端 payload 不能注入 pending_character_rewards、不能抑制服务端任务进度。
- 证据/问题: 注入 `pending_character_rewards` 后断言无 `client_injected_reward`（L50-53）、`emit_default_quest_progress_event=false` 仍断言任务 claimable（L54-57）——真实边界/安全路径验证。

### tests/world_map/runtime/run_settlement_forge_service_regression.cs — 有效
- 断言数: 约45
- 验证内容: 大师重铸/通用锻造成功与缺料路径、handler 路由开 forge modal、配方执行扣料产出、两个世界预设生成服务入口。
- 证据/问题: 精确断言材料消耗与产出计数（L119-121/276-279）、缺料不吞已有材料（L153-154）、default(ForgeActionRequest) 拒绝（L46-49）、modal kind 与 persist 断言（L177/200/202）。

### tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs — 有效
- 断言数: 约90（11 个 Test 方法）
- 验证内容: 驿站/商店买/卖/据点服务/仓储动作在 fail_payload_write 下的完整回滚（金币、坐标、视野、库存、cooldown、session save metadata）、party-only 回滚范围、dispose staging、rollback 类型契约反射检查。
- 证据/问题: 全文精读 1497 行。精确断言回滚后 fog 不含目的地（L83-86）、`cooldowns["rest_basic"]==4` 保留（L279-282）、session 与 runtime 双侧库存回滚（L283-304）、save metadata 9 字段逐一比对（L1325-1371）、LoadSave 后库存仍为 1（L728-736）。

### tests/world_map/runtime/run_settlement_research_service_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: research 服务成功路径（扣 200 金、奖励 entry 字段不回填）与 5 类 malformed payload 拒绝且无副作用。
- 证据/问题: 精确断言 gold 250→50（L37）、gold_delta=-200（L39）、target_label/reason_text 不由他字段回填（L50-51）、各拒绝路径 gold 不变=250 且无奖励写入（L126-130）。

### tests/world_map/runtime/run_settlement_research_typed_catalog_regression.cs — 有效
- 断言数: 约2
- 验证内容: 未知 research reward entry_type 作为 catalog schema error 失败而非静默跳过。
- 证据/问题: 断言 success=false 且 message 含 "entry_type"（L26-30），单测单点但针对真实错误路径。

### tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs — 有效
- 断言数: 约9
- 验证内容: 商店窗口构建不制造镜像状态变更、到期独立刷新不串店、购买写回不覆盖其他商店 seed/刷新步。
- 证据/问题: 精确断言另一商店 seed=22/refresh=5 在三种操作后均不变（L129-134/159-164/190-195）、购买后库存=0（L182-186）。

### tests/world_map/runtime/run_wild_encounter_growth_system_regression.cs — 有效
- 断言数: 约9
- 验证内容: 野怪成长按间隔推进/封顶、战斗胜利降级+压制步、缺 roster 拒绝。
- 证据/问题: 精确断言 growth_stage 0→1→2 封顶（L43/53）、胜利后 stage=1 且 suppressed_until_step=8（L76-81）、无 roster 时不变（L99-100）。

### tests/world_map/runtime/run_world_map_battle_loading_overlay_regression.cs — 有效
- 断言数: 约25
- 验证内容: 根层 loading overlay 跟随 BattleMapPanel 信号显隐、进度/百分比同步、battle_loading modal 期间保持可见至 battle_start_confirm。
- 证据/问题: 精确断言进度 48→"48%"（L62-63）、结束保留 100%（L69-70）、modal id 流转 battle_loading→battle_start_confirm（L119/134/144）、overlay 可见性与 `IsLoadingBattle()` 一致（L145-149）。

### tests/world_map/runtime/run_world_map_battle_start_confirm_regression.cs — 有效
- 断言数: 约20
- 验证内容: battle_start_confirm 不可取消契约：prompt 字段、取消按钮隐藏禁用、遮罩点击/取消回调/stray cancel 信号均不关闭且 timeline 保持冻结。
- 证据/问题: 精确断言 `cancel_visible/dismiss_on_shade==false`（L71-72）、三种取消尝试后窗口仍 Visible 且 modal 不变（L88-89/95-96/103-105）、timeline.frozen（L81/91/98/113）。

### tests/world_map/runtime/run_world_map_data_context_regression.cs — 有效
- 断言数: 约80（12 个 Test 方法）
- 验证内容: WorldMapDataContext typed 查询（事件/据点/NPC/遭遇锚点）、fog 存取与 revision gate、子地图进入/返回/回滚、越界 selected clamp、stale submap 回退。
- 证据/问题: 精确断言 NPC 字段 trim 与非法 faction 拒绝（L357/373-379）、revision 只在新增 revealed 时推进（L401-431）、rollback 后 world_step 恢复 7 且不泄漏事务内 reveal（L601-619）、mutation 同步回公开 payload（L183-187）。

### tests/world_map/runtime/run_world_map_runtime_log_dock_regression.cs — 有效
- 断言数: 约18
- 验证内容: 共享日志窗口世界态/战斗态复用切换、文本内容、尺寸锁定与缩放行为、半透明面板样式。
- 证据/问题: 精确断言标题"运行日志"→"战斗日志"（L53/130）、日志内容 Contains（L54-57/131-135）、缩放后宽度锁定高度收缩字号不变（L92-98）。

### tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs — 有效
- 断言数: 约40
- 验证内容: WorldMapRuntimeProxy getter/快照/命令全部转发 runtime；缺 runtime 返回正式错误；仓库 mutation 只 stage 不写盘，flush 后清 pending。
- 证据/问题: 精确断言各 getter 值（L80-103）、`RuntimeUnavailable` 错误码与文案（L185-192）、fail_payload_write 下 add/use/discard 成功且 dirty scope 含 party_state、last_error=Ok（L236-317）、flush 后 HasPendingSave=false（L328-331）。

### tests/world_map/runtime/run_world_map_save_transaction_regression.cs — 有效
- 断言数: 约18
- 验证内容: 普通移动只标 pending 不写磁盘坐标；采集持久化失败完整回滚（命令失败、消息、仓库、active/root/session 三层剩余次数、pending 状态恢复）。
- 证据/问题: 磁盘 payload 坐标保持原值（L61-67）、失败消息含"操作已回滚"不含"已采集"（L121-128）、三层 world owner 剩余次数均恢复（L142-171）。

### tests/world_map/runtime/run_world_map_settlement_entry_regression.cs — 有效
- 断言数: 约14
- 验证内容: 踏入据点自动开 settlement modal、玩家逻辑坐标保留/选中格指向据点、地图上隐藏玩家，关闭后恢复。
- 证据/问题: 精确断言 modal/坐标/可见性两侧状态（L49-54 vs L68-72）、headless snapshot 的 player_visible_on_map=false（L60）。

### tests/world_map/runtime/run_world_map_shared_content_injection_regression.cs — 有效（一处配置回读，注记）
- 断言数: 约80（15 个 Test 方法）
- 验证内容: 共享内容注入后世界生成（世界据点/大师重铸/契约板分布/north/south 遭遇）、runtime map seed 不落固定 seed、世界切换失败回滚绑定、名称池唯一性与语义后缀、野怪区域规则与密度。
- 证据/问题: 生成行为断言真实：tier>0 据点恰好 1 个契约板、起始村 0 个（L185-198）、map_seed != definition.Seed（L269）、small 世界名称不重复且按 tier 后缀（L574-648）。注记：`TestGenericMainWorldPresetsKeepTemplateShape`（L70-110）为回读 .tres 配置常量（denominator==2、各 Library.Count==0），属 change-detector 弱断言，但仅占 1/15 方法。

### tests/world_map/runtime/run_world_map_spawn_typed_regression.cs — 有效
- 断言数: 约45
- 验证内容: typed world build 投影数量一致性、出生村庄服务集合与 action_id 唯一性、资源点生成契约（唯一坐标/产出映射/初始药园保底）。
- 证据/问题: 精确断言投影数量与 typed 列表一致（L144-173）、三 NPC action_id 两两不同（L86-97）、`occupiedCoords.Add` 查重（L270-273）、farm/herb/mine 产出映射与初始药园距离（L282-314）。

### tests/world_map/runtime/run_world_map_system_surface_regression.cs — 有效
- 断言数: 约12
- 验证内容: 驿站 modal 只认正式 target_settlement_id；warehouse use 回调走正式链让成员学技能并扣库存。
- 证据/问题: legacy settlement_id 不改变状态（L55-59）、正式字段触发旅行命令文案（L66-70）、`is_learned==true` 且库存归零（L145-153）。

### tests/world_map/runtime/run_world_map_view_color_config_regression.cs — 偏弱
- 断言数: 约35
- 验证内容: world_map.tscn 的 WorldMapView 导出颜色字段存在性、默认值、村级贴图路径、tier→颜色映射走导出字段。
- 证据/问题: 主体为回读场景默认常量：L48-78 把 15 个导出颜色的默认值与测试内硬编码常量逐一 Eq（change-detector，场景改色即失败但不验证行为）；L84-92 回读贴图 ResourcePath 常量。真实验证部分：L69-72 导出字段存在性检查、L104-111 `_get_settlement_color(tier)` 与导出字段一致性（验证 draw 不再硬编码）。

### tests/world_map/runtime/run_world_runtime_data_typed_regression.cs — 有效
- 断言数: 约35
- 验证内容: WorldRuntimeData 严格 schema（malformed/缺字段/多余字段/废弃镜像字段拒绝）、typed factory 拒绝重复/null/规范化冲突、settlement/resource 更新投影、save payload roundtrip。
- 证据/问题: 大量错误路径断言（L45-48/138-183）、factory 返回 null 的负路径（L94-116）、projection 精确字段值 seed=99/refresh=5/quantity=2（L256-263）、roundtrip 保留 world_step=3 与 country_id（L338-344）。

### tests/world_map/runtime/run_world_submap_regression.cs — 有效
- 断言数: 约55
- 验证内容: 子地图进入/移动/重载/返回全流程、battle active 与 modal 打开时返回阻断、entry/return 持久化失败回滚、mounted submap 序列器契约与各类坏档拒绝。
- 证据/问题: 精确断言重载后坐标 (14,15)（L229-233）、返回后 (52,49)（L246-250）、回滚后 modal 保持 submap_confirm 可重试（L317-325）、缺 map_seed/world_step/负数/字符串类型均判坏档（L440-480）、未生成子地图带非空 world_data 拒绝（L549-558）。

## tests/world_map/schema/

### tests/world_map/schema/run_encounter_anchor_schema_regression.cs — 有效
- 断言数: 约30（循环展开）
- 验证内容: EncounterAnchorData 序列化 roundtrip 字段保持；缺字段/多字段/错类型/空身份字段/非法 encounter_kind 全部拒绝。
- 证据/问题: 11 个必填字段逐一删除拒绝（L98-106）、11 组错类型逐一拒绝（L136-144）、legacy enemy_roster_template_id 拒绝（L109-117）；roundtrip 用 projection→parse→projection 字典逐键比对（L54-58/195-215），验证解析保真。

### tests/world_map/schema/run_world_map_low_level_defensive_regression.cs — 有效（含 1 个空方法，点名，与任务书所述一致）
- 断言数: 约25
- 验证内容: 预设注册表 typed 查询、grid footprint 注册/冲突/回滚/清理、fog 外阵营视野源隔离、持久化恢复 explored≠visible、persistent revision 只在真实变化时推进。
- 证据/问题: **空方法：`TestRuntimeCommandHandlersNoLongerRequireGodotRegistration()`（L29-31）方法体为空，即任务书所述空调用槽位，核实属实。** 其余方法真实：移动失败恢复原 footprint（L77-85）、外阵营源不可见（L106-109）、恢复后 IsExplored=true 但 IsVisible=false（L144-151）、重复揭示不推 revision（L188-193）。

### tests/world_map/schema/run_world_time_system_regression.cs — 有效
- 断言数: 约14
- 验证内容: step→day/month 派生边界（含负值拒绝）、advance 跨天报告、负 delta 按 0、负 world_step 拒绝。
- 证据/问题: 精确边界断言 449/450/900 step 的 month 归属（L39-41）、跨天 days_elapsed=1（L52）、负 step error_code="invalid_world_step"（L63-67）。

---

## 统计

- 有效: **40**
- 偏弱: **2**
- 无效: **0**

### 偏弱清单
1. `tests/battle_runtime/simulation/run_battle_balance_simulation.cs` — 无任何断言，仅 `report.IsComplete` 完整性门槛（L64-70），本质是 balance 运行器。
2. `tests/world_map/runtime/run_world_map_view_color_config_regression.cs` — 主体为回读 .tscn 导出默认值与硬编码常量比对（L48-78 颜色、L84-92 贴图路径），仅导出字段存在性与 `_get_settlement_color` 映射（L69-72/L104-111）是真实验证。

### 无效清单
无。

### 空测试方法点名（所在文件整体仍判有效）
1. `tests/battle_runtime/simulation/run_battlesim_formal_fixture_regression.cs` L120-122 `TestFixtureNoLongerRegistersGlobalClass()` — 方法体为空。
2. `tests/world_map/schema/run_world_map_low_level_defensive_regression.cs` L29-31 `TestRuntimeCommandHandlersNoLongerRequireGodotRegistration()` — 方法体为空（与任务书所述槽位核实一致）。

### 其他次要瑕疵（不改变判定）
- `run_battle_ai_vs_ai_simulation_regression.cs` L80-83、L114-117：两处断言消息与断言内容不符（消息提 battle_ended/wins_by_faction，实际只断言非 null）。
- `run_world_map_shared_content_injection_regression.cs` L70-110：`TestGenericMainWorldPresetsKeepTemplateShape` 为 .tres 配置常量回读，属 change-detector，但该文件其余 14 个方法均为生成行为验证。

## 批次 07 明细

# 测试有效性审计 — batch07（47 个 run_*.cs）

范围：`tests/world_map/ui/`（9）、`tests/runtime/validation/`（22）、`tests/runtime/persistence/`（8）、`tests/runtime/facade/`（8）。每个文件均逐行通读。

## tests/world_map/ui/

### tests/world_map/ui/run_character_creation_window_payload_regression.cs — 有效
- 断言数: 约20
- 验证内容: 建卡窗口确认后 payload 的字段/属性有效性、age stage 集合的 managed 存储类型、种族/年龄卡片渲染。
- 证据/问题: 精确断言 signal 捕获的 payload（:62-91 `capturedPayload` Eq display_name、属性 >=4、race_id/subrace_id 非空）；:34-45 反射校验 `_ageStageIds` 为 `IReadOnlyList<StringName>`；:112-121 卡片 Count>0 属于弱形态但仅是辅助检查。

### tests/world_map/ui/run_character_info_identity_regression.cs — 有效
- 断言数: 约45
- 验证内容: CharacterInfoBuilder 的状态条目排序/文本、身份 section 内容、StringName 拒绝、typed context 的 plain snapshot schema。
- 证据/问题: 精确断言文本（:54-55 `"burning x2 · 15 TU"`）、HasPairEntry 逐字段（:88-99）、负路径（:131-138 StringName 不渲染）、AssertExactKeys schema 锁定（:197-269）。

### tests/world_map/ui/run_character_info_window_fate_regression.cs — 有效
- 断言数: 约30
- 验证内容: CharacterInfoWindow 渲染 fate 段落的精确文案，以及对缺字段/旧 schema/错类型/StringName payload 的整包拒绝。
- 证据/问题: 精确渲染文本断言（:66-88）+ 5 类拒绝路径均断言 `Visible==false` 且 0 子节点（:101-260）。

### tests/world_map/ui/run_contingency_setup_window_regression.cs — 有效
- 断言数: 约30
- 验证内容: 应急设置窗口的信号发射次数/payload、按钮禁用态、charged/uncharged 渲染差异、UI 不直接改写 PartyMemberState。
- 证据/问题: 信号计数 Eq 1/0（:45-47, :77-91, :157-166）；状态非变异断言（:87-91, :165-166）；禁用态与警告文案（:130-135）。

### tests/world_map/ui/run_modal_window_shell_regression.cs — 有效
- 断言数: 约12
- 验证内容: Esc 对三类模态窗的关闭语义（关闭+信号 / 不关闭 / dismiss flag 分支）。
- 证据/问题: 精确断言 Visible 翻转与 closed/cancelled 信号计数（:54-58, :71-74, :89-98），含 dismiss_on_shade=false 负路径。

### tests/world_map/ui/run_npc_quest_offer_dialog_action_regression.cs — 有效
- 断言数: 约13
- 验证内容: 委托对话动作按钮文案/禁用态随状态变化，以及 action_requested 信号 payload 字段保真。
- 证据/问题: 三态文案+Disabled 精确断言（:40-49）；信号参数 Eq settlement_id/action_id/submission_source/quest_id/confirm_accept（:68-75）。

### tests/world_map/ui/run_party_management_window_regression.cs — 有效
- 断言数: 约30
- 验证内容: 窗口半屏/保底尺寸、队长移替补时 roster/leader 信号顺序与 payload、双手武器去重展示、缺技能定义容错、快照驱动的详情文案。
- 证据/问题: 尺寸数值断言（:33, :48-50）；事件顺序 AssertStringList（:88-98）；渲染文本精确断言（:147-151, :182-189, :223-233）。

### tests/world_map/ui/run_promotion_choice_window_schema_regression.cs — 有效
- 断言数: 约15
- 验证内容: 晋升选择窗接受 formal string payload、提交时保留 member_id/selection、BBCode 形态文本按字面渲染、StringName payload 拒绝。
- 证据/问题: 提交信号参数 Eq（:79-98）；`GetParsedText()` 字面包含 `[b]守卫[/b]` 等（:115-131）；拒绝路径断言 0 子节点（:144-145）。

### tests/world_map/ui/run_settlement_shop_window_schema_regression.cs — 有效
- 断言数: 约45
- 验证内容: 据点/商店窗的 schema 接受与 6 类拒绝路径、确认流按钮文案与信号、forge 强类型事件替代旧 Dictionary signal。
- 证据/问题: 每个 Test* 方法均有 Visible/child count 双向断言；确认流精确断言按钮文案与 confirm_accept（:303-337）；forge typed event 字段 Eq（:364-391）。

## tests/runtime/validation/

### tests/runtime/validation/run_barrier_skill_content_validation_regression.cs — 有效
- 断言数: 约3
- 验证内容: 屏障×技能跨表校验器：正式内容 0 错误 + 两类注入式负路径（缺 profile、缺 breaker skill）。
- 证据/问题: 负路径断言错误文本同时包含 skillId/profileId 与固定短语（:60-67, :105-112），可真实失败。

### tests/runtime/validation/run_battle_sim_definition_patch_regression.cs — 有效
- 断言数: 约25
- 验证内容: BattleSim override patch 的值隔离（深拷贝）、不可变定义补丁应用、runner 遇到非法 override 的 fail-fast。
- 证据/问题: 源变更后 patch 值不变（:43-56）；补丁后字段 Eq 999/6000/5/7/12 且 ReferenceEquals 为 false（:126-172）；异常消息包含 profile id 与字段名且无 partial report（:315-329）。

### tests/runtime/validation/run_battle_sim_scenario_definition_regression.cs — 有效
- 断言数: 约30
- 验证内容: scenario 定义投影的脱钩性（ authored 变更不影响 definition）、运行时状态独立性、roster 单次消费、formal terrain 跳过 cell 解析、runtime 签名拒绝 authored Resource。
- 证据/问题: 变异 authored 对象后 definition 字段保持（:76-107）；二次 ConsumeForStart 抛异常（:255-267）；反射扫描 8 个 runtime 类型签名（:362-423）。

### tests/runtime/validation/run_content_snapshot_session_recreate_regression.cs — 有效
- 断言数: 约15
- 验证内容: 多次 GameSession 关闭/重建期间 process snapshot 的 epoch/identity/root count 稳定，synthetic snapshot 接受与第二 host 拒绝。
- 证据/问题: AssertHostStable 三要素 ReferenceEquals/epoch/rootCount（:94-96）在 3 个生命周期点调用；第二 ProcessContentHost 抛 InvalidOperationException（:79-82）。

### tests/runtime/validation/run_enemy_content_registry_typed_regression.cs — 偏弱
- 断言数: 约50
- 验证内容: 敌人快照不可变性/纯 CLR 图、共享荒狼 roster 阶段结构、invalid seed 清理、action-skill target mode 与等级不匹配的诊断。
- 证据/问题: 方法 1/3/4/5 为真实行为验证（不可写抛 NotSupportedException :71-85；错误诊断精确 Contains :372-384, :499-544）。但 TestOfficialSharedWolfEncounterStages（:115-256）主体是回读 .tres 数据常量：CreatureLevel 0/1（:232-233）、各 stage 敌人数量 2/5/3/2（:193-231）、技能 id 列表（:234-256），属"数据常量回读"弱模式。

### tests/runtime/validation/run_game_root_content_catalog_regression.cs — 有效
- 断言数: 约60
- 验证内容: GameSession/GameRoot/ContentCatalog 的绑定身份、缓存视图、只读防御（downcast+IDictionary 写入拒绝）、dispose 失效、facade 的 stale/foreign catalog 丢弃。
- 证据/问题: ReferenceEquals 身份链（:50-61）；只读视图 NotSupportedException 验证（:230-314）；dispose 后 Count==0 且 revision 前进（:408-439）；stale/foreign catalog 注入后重新解析（:548-616）。

### tests/runtime/validation/run_godot_projection_lease_regression.cs — 有效
- 断言数: 约14
- 验证内容: GodotProjectionLease 的 owner/lease/borrower 审计计数、重复 Own 拒绝、double dispose 幂等、关闭后访问抛 ObjectDisposedException。
- 证据/问题: 审计计数精确 Eq baseline+N（:36-39, :61-64）；Throws 断言覆盖 4 条错误路径（:41-57）。

### tests/runtime/validation/run_item_recipe_registry_typed_regression.cs — 有效
- 断言数: 约50
- 验证内容: item/recipe registry typed/public 边界一致、trait 来源域校验、definition 无 Godot 图、null 嵌套拒绝、Merge 纯函数深拷贝、非法骰子不被归一化。
- 证据/问题: AssertInvalidData 校验异常路径片段（:489-509）；Merge 后 ReferenceEquals false + 源不被修改（:379-413）；错误消息双 needle 定位（:177-194）。

### tests/runtime/validation/run_native_lease_scope_regression.cs — 有效
- 断言数: 约25
- 验证内容: NativeLeaseScope 的重复/跨域 Own 拒绝、TransferTo 移交、逆序 dispose、10 万 owner 线性清理、Node/path-backed Resource 拒绝。
- 证据/问题: dispose 顺序 Eq `"third,second,first"`（:94）；移交后源 dispose 不影响被移交者（:88-93）；域计数精确（:70-79）。

### tests/runtime/validation/run_non_ai_content_snapshot_regression.cs — 有效
- 断言数: 约10+
- 验证内容: 非 AI ContentSnapshot 发布内容存在性、字典不可变、类型图不含 Resource/Godot 集合/legacy enemy 类型。
- 证据/问题: Count>0 断言（:20-24）较弱但仅是前置；核心是不可写 Throws（:29-37）与全类型图反射扫描（:39-60），可真实失败。

### tests/runtime/validation/run_process_content_host_regression.cs — 有效
- 断言数: 约30
- 验证内容: ProcessContentHost seal 幂等、seal 后加载拒绝、诊断不重复、engine asset canonicalize、strict 模式下借用者阻塞 Dispose、publication 失败回滚。
- 证据/问题: 回滚断言 rootCount 回 baseline 且 epoch==0（:161-171）；strict dispose Throws + snapshot 不变（:111-127）。

### tests/runtime/validation/run_progression_content_registry_typed_regression.cs — 有效
- 断言数: 约30
- 验证内容: progression registry typed/public 一致、纯 Definition 替换源喂给 validator（精确诊断文本）、替换产生防御性快照、identity catalog 使用 Definition 索引。
- 证据/问题: 精确错误文本计数断言（:129-157）；调用方字典清空后 registry 仍有数据（:172-178）；不加载正式目录断言 Count==0（:96-103）。

### tests/runtime/validation/run_quest_config_validation.cs — 有效
- 断言数: 约2
- 验证内容: 正式 quest 配置经 registry 加载与跨表 ValidateTyped 后 0 错误（内容有效性门禁）。
- 证据/问题: :19-22, :44-47 errors 非空即 Fail；无负路径，但作为正式内容门禁可真实失败。

### tests/runtime/validation/run_quest_content_validator_typed_regression.cs — 有效
- 断言数: 约40
- 验证内容: quest 校验器的正式内容边界、悬空 encounter 引用、growth stage 四类边界、provider/listing channel/accept requirement/危险度/据点绑定等负路径。
- 证据/问题: 负路径断言错误列表精确 Contains 完整诊断句（:175-180, :262-285）；每个 Append*Errors 断言错误数==1 且消息含提示（:324-368 等）。

### tests/runtime/validation/run_resource_validation_regression.cs — 偏弱
- 断言数: 约100
- 验证内容: ContentValidationRunner 对正式内容与 20+ 非法 fixture 的 domain 归类与判非法、item template 缓存清理、phantasmal kill 技能数据契约。
- 证据/问题: fixture 驱动部分真实有效（AssertInvalid/AssertContainsError :403-451，:500-513）。但 TestFormalPhantasmalKillResource（:516-666，约 60 条断言）整体是回读 .tres 数据常量：mastery_curve 字面数组（:537-541）、ApCost/MpCost/CooldownTu 等字面值（:591-594）、13 个 param 字面值（:630-643），属"回读 .tres 数据常量"弱模式（仅等级描述循环 :645-665 验证了 formatter 行为）。

### tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs — 有效
- 断言数: 约40（另加扫描驱动的动态断言）
- 验证内容: 生命周期架构门禁：runtime 服务非 GodotObject、测试不直接 Quit/GC、GC barrier 调用者精确名单、migration 产物删除、runtime 签名不泄露 authored Resource、autoload 顺序、process snapshot 绑定。
- 证据/问题: 反射全程序集扫描签名（:605-684）与源码扫描（:316-478）可真实失败；project.godot autoload 精确 Eq（:767-776）；ReferenceEquals 绑定验证（:802-813）。注：AssertRawEnemyAuthoringInventory 内 :546-555 有恒真断言（对刚构造的空/2 元素 HashSet 断言 Count==0/2），但占比极小。

### tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs — 有效
- 断言数: 动态（逐文件逐行扫描）
- 验证内容: 全仓库源码扫描：禁用 lifecycle token 清单、禁用文件清单、GC.SuppressFinalize 仅允许纯 CLR this 目标。
- 证据/问题: 命中即 `_test.Fail`（:77-89, :110-112），SuppressFinalize 目标经反射核实非 GodotObject（:116-135）；属源码门禁，token 是其被测对象本身。

### tests/runtime/validation/run_save_projection_lease_regression.cs — 有效
- 断言数: 约45
- 验证内容: save payload projection lease 的成功/注入失败/异常/关闭后访问四条路径的审计基线回归、strict plain payload 契约、index 过滤非法条目、当前存档 schema shape。
- 证据/问题: 注入 fail_payload_write 断言 CantCreate（:84-89）；closed lease 写入抛 ODE 且不留 tmp、target 不变（:147-182）；HasExactKeys 锁定 schema（:314-343, :363-389）。

### tests/runtime/validation/run_skill_catalog_query_regression.cs — 有效
- 断言数: 约100
- 验证内容: ISkillCatalog 门面与 combat profile 直调的逐字段对拍、命中/未命中语义、缺失技能安全默认、revision 驱动的缓存失效。
- 证据/问题: 4 技能×5 等级逐 getter 对拍（:346-415）；缺失技能返回 Zero/0/空（:417-473）；ClearSessionBinding 后 revision 前进且缓存重建（:475-500）。

### tests/runtime/validation/run_skill_definition_plain_value_graph_regression.cs — 有效
- 断言数: 约80
- 验证内容: 内容值规范化（Variant→plain、Int64 归一）、definition 深冻结、非法路径完整报错、strict 拒绝（非 string key/packed/环/重复 key）、默认值不回写、fingerprint/描述稳定性。
- 证据/问题: 变异源后冻结图不变（:221-252）；异常消息精确路径（:299-303, :400-437）；fingerprint 与描述在源变异后不变（:522-532）。

### tests/runtime/validation/run_true_random_seed_test_override_regression.cs — 有效
- 断言数: 约6
- 验证内容: 确定性随机种子的可复现性（重置后序列一致）、有界 roll 范围、非正种子拒绝。
- 证据/问题: 两次采样逐字段 Eq 验证确定性属性（:21-35）；范围断言 [-3,3]（:37-40）；ConfigureDeterministicForTests(0) Throws（:41-46）。非"与自身输出对比"——验证的是重置可复现这一行为属性。

### tests/runtime/validation/run_world_map_content_validator_typed_regression.cs — 有效
- 断言数: 约30
- 验证内容: world preset typed 校验、注入非法定义的报错、catalog id 精确匹配、兄弟 submap 复用路径、definition 脱钩与只读、循环/null 投影拒绝、类型图纯净。
- 证据/问题: catalog id 拒绝/接受双向断言（:123-137）；authored 变异后 definition 不变（:317-328）；循环路径精确报错（:381-385）。

## tests/runtime/persistence/

### tests/runtime/persistence/run_display_settings_service_regression.cs — 有效
- 断言数: 约12
- 验证内容: 显示设置的存读 round-trip、未知分辨率归一化、类型错误配置回退默认。
- 证据/问题: round-trip Eq（:38-47）；归一化保留 fullscreen（:60-65）；畸形 fixture（width 字符串/height bool）回退默认（:93-98）。

### tests/runtime/persistence/run_game_log_service_regression.cs — 有效
- 断言数: 约25
- 验证内容: 日志 ring buffer 丢弃策略、opt-in 文件追加、并发写入不丢条目且序号连续、dispatcher 级别过滤与 sink 隔离（抛错 sink 不影响其他）。
- 证据/问题: 4 条进 3 条出且 seq 连续（:40-47）；512 并发后 Count/seq 精确（:146-157）；重复注册去重 + debug 被过滤 + 格式化尾串精确（:176-201）。

### tests/runtime/persistence/run_game_session_persistence_options_regression.cs — 有效
- 断言数: 约25
- 验证内容: production 持久化路径契约、lifecycle soak run id 的 11 类非法输入拒绝、双 session 隔离根互不影响。
- 证据/问题: AssertInvalidRunId 断言 ArgumentException.ParamName=="runId"（:186-201）；清理 A 后 B 的 index 仍在（:149-163）。

### tests/runtime/persistence/run_game_session_random_start_skill_regression.cs — 有效
- 断言数: 约25
- 验证内容: 随机起始技能与主手武器匹配（含双手武器占副手）、耗蓝法术伴随授予冥想法与 0–40 法力池写入。
- 证据/问题: 装备 item id 与按技能标签推导的期望精确 Eq（:70-88）；固定 roller 注入下 resultingManaPool/mp_max/current_mp 三点 Eq（:180-190）含 0 边界（:117, :191-196）。

### tests/runtime/persistence/run_game_session_transaction_regression.cs — 有效
- 断言数: 约35
- 验证内容: setter 只 stage 不落盘、commit 持久化完整快照、payload 写失败保留 dirty+last_error、index 写失败不回滚 payload commit、卸载自动提交。
- 证据/问题: 未 commit 磁盘读回旧坐标（:54-55）；fail_payload_write 注入断言 CantCreate+dirty_scopes（:129-144）；fail_index_write 下 commit Ok、payload 含 staged、index 可重建（:230-261）。

### tests/runtime/persistence/run_invalid_save_graceful_regression.cs — 有效
- 断言数: 约30
- 验证内容: 各类坏存档输入的优雅拒绝：错类型 generation config、坏 world_data、消失 payload、Vector2I schema、坏建卡 identity、save index 版本类型。
- 证据/问题: 每条路径断言精确 Error 码且无 active world 残留（:42-43, :84, :122-131）；fog 字典坐标拒绝（:255-258）；DoesNotExist 后槽位移除（:128-131）。

### tests/runtime/persistence/run_save_payload_string_minimization_regression.cs — 有效
- 断言数: 约40
- 验证内容: save payload 全图无 TYPE_STRING（StringName 化）、二进制文件格式、解码回运行时 String、runtime 侧拒绝 StringName 字段。
- 证据/问题: CollectStringVariantPaths 全图递归扫描（:240-286）；逐字段 AssertType==StringName（:63-148）；解码后 ActiveSaveId/display_name 回 String（:160-171）；负路径 :177-188。

### tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs — 有效
- 断言数: 约45
- 验证内容: quest schema 完整存读往返（进度/声望/失败原因）、v10/v17/party v8 无迁移拒绝、quest 集合状态错配拒绝、嵌套 settlement_state 缺字段拒绝、损坏进度上下文不抛异常。
- 证据/问题: round-trip 逐字段 Eq（:101-148）；版本门禁 InvalidData（:185-197, :221-236）；缺字段循环逐字段拒绝（:466-511）；catch 仅用于断言"不应抛异常"而非吞失败（:433-458）。

## tests/runtime/facade/

### tests/runtime/facade/run_battle_local_writeback_failure_boundary_regression.cs — 有效
- 断言数: 约15
- 验证内容: 战斗本地写回失败边界：候选校验失败持有 plain DTO、instance 冲突失败码/details 保真、失败投影 schema 与 lease 审计。
- 证据/问题: 构造背包/装备槽共享 instance_id 冲突，断言 Ok=false+error_code+三字段 details（:81-105）；lease 计数 +1/回基线（:127-143）；反射验证内部结果类型（:25-43）。

### tests/runtime/facade/run_battle_permadeath_regression.cs — 有效
- 断言数: 约30
- 验证内容: 非主角战死落盘为真实死亡（roster 移除、HP=0、可重载）；主角战死触发 GameOver（modal、上下文、清 pending、卸载后回滚到战前存档）。
- 证据/问题: 战后 PartyState 逐字段断言 + 重载存档复核（:69-121）；GameOver modal id/上下文/锁状态精确断言（:164-179）；重载坐标与死亡标记双向（:185-203）。

### tests/runtime/facade/run_battle_quest_progress_event_builder_regression.cs — 有效
- 断言数: 约9
- 验证内容: 战斗击败事件按敌方模板聚合并排序、只统计死亡单位、玩家失败时不产出进度。
- 证据/问题: 2 死 1 活 wolf_pack + 1 死 wolf_alpha，断言聚合 2 条、顺序、delta 1/2、上下文字段（:34-47）；PlayerFailure 时 Count==0（:59）。

### tests/runtime/facade/run_battle_seed_source_regression.cs — 有效
- 断言数: 约6
- 验证内容: facade 战斗 seed 委托注入 source（调用次数、anchor 透传）、空 anchor 零 seed 不调用、固定 source 保值。
- 证据/问题: RecordingBattleSeedSource 计数与 LastEncounterAnchor ReferenceEquals（:28-44）。

### tests/runtime/facade/run_battle_session_promotion_prompt_regression.cs — 有效
- 断言数: 约15
- 验证内容: 晋升提示过滤未知职业/零 rank 候选、payload 保持 formal string、batch 事件路径同样捕获提示。
- 证据/问题: 4 候选仅 warrior 合法，choices.Count==1 且 profession_id/类型断言（:68-109）；batch 路径复验（:144-166）。

### tests/runtime/facade/run_confirmed_bugfix_regression.cs — 有效
- 断言数: 约10
- 验证内容: 三个已确认 bug 修复：natural roll 标志关闭后的命中判定、footprint 重注册清旧格（含越界回滚）、缺 item_def 装备可卸下且入仓库。
- 证据/问题: disposition Eq threshold_hit（:37-41）；重注册后旧格空/新格有/越界失败恢复（:57-63）；卸下后槽空且仓库 +1（:94-104）。注：SetStatusParams/SetTypedStatus/BuildDamageEffect/BuildUnit 等 helper 已无调用方（死代码），不影响断言有效性。

### tests/runtime/facade/run_game_runtime_reward_flow_regression.cs — 有效
- 断言数: 约30
- 验证内容: 奖励队列确认后自动展示下一条、research typed 奖励字段保真、settlement modal 阻塞呈现、reward modal 不可直接关闭。
- 证据/问题: 队首→次条 reward_id 精确 Eq 与计数（:33-46）；阻塞/解阻塞 modal 状态机断言（:82-102）；关闭被拒 Code==InvalidState（:128-136）。

### tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs — 有效
- 断言数: 约150
- 验证内容: 快照构建器 20 个 Test* 方法全覆盖：六类战斗目标进度投影、lease 生命周期与指纹稳定、GameTextCommandResult 深拷贝隔离、日志路径脱敏、quest/standing/progression 快照、文本快照拒绝 StringName/旧字段、contract board/forge/npc offer/game over/loot 快照。
- 证据/问题: 逐字段 Eq + 文本行精确匹配（如 :106-129, :1593-1621）；root key 顺序 golden（:729-733）；重复投影 fingerprint/容器数稳定（:750-784）；无空方法，所有 Test* 均被 Run 调用（:29-48）。

## 统计

- 有效: 45
- 偏弱: 2
- 无效: 0

### 偏弱清单
1. `tests/runtime/validation/run_enemy_content_registry_typed_regression.cs` — TestOfficialSharedWolfEncounterStages（:115-256）主体为回读 .tres 数据常量（CreatureLevel、stage 敌人数量、技能 id 列表）；其余 4 个方法为真实行为/诊断验证。
2. `tests/runtime/validation/run_resource_validation_regression.cs` — TestFormalPhantasmalKillResource（:516-666，约 60 条断言）为回读 .tres 数据常量（mastery_curve、ApCost/MpCost/CooldownTu、13 个 param 字面值）；其余 fixture 驱动部分真实有效。

### 无效清单
（无）

## 批次 08 明细

# 测试有效性审计报告 — batch08

审计范围：tests/runtime/lifecycle（8 个 run_*.cs）、tests/runtime/contracts（1）、tests/text_runtime 全部（headless 7 + commands 5 + tools 2）、tests/shared（2）、tests/static_analysis（2）、tests/tooling（3 个 test_*.py）。
审计日期：2026-08-12。所有文件均逐行精读；Python 测试实际运行验证通过情况。

## tests/runtime/lifecycle/

### tests/runtime/lifecycle/run_application_lifecycle_soak_regression.cs — 有效
- 断言数: 约 8 直接断言 + 110 周期 × 统计阈值检查（经 LifecycleSoakStatistics.Evaluate 失败枚举逐条 _test.Fail）
- 验证内容: 进程内容宿主密封/快照单发布、110 轮会话开合后所有权计数/内存增长必须落在契约阈值内
- 证据/问题: 精确断言快照引用身份（41 行 ReferenceEquals）、借用者归零（50-54）、样本数精确等于 110（89-93）、report.Passed（94）

### tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs — 有效
- 断言数: 约 75
- 验证内容: 关停协调器全契约——autoload 顺序、参与者注册/去重/幂等注销、关闭顺序（stage/order/ID）、失败分支（FinalizerBarrierSkipped）、非主线程拒绝、重入请求合并、TestExitCoordinator 退出码映射与异步失败重试
- 证据/问题: 精确关闭顺序字符串断言（775-779、963-966）；关闭后阶段历史精确匹配（787-791）；重入请求共享同一 Task（929-933）；注入异常后重试次数=2（286-290）

### tests/runtime/lifecycle/run_application_shutdown_contract_regression.cs — 有效
- 断言数: 约 70
- 验证内容: 关停状态机合法/非法迁移、ShutdownReport 退出码合并语义、skipped barrier 不变量（通用 API 拒绝专用状态、幂等、非法起始态）、诊断格式化、LifecycleAuditRegistry 计数/严格模式拒绝
- 证据/问题: 状态机正反向迁移断言（28-69）；EffectiveExitCode 合并规则逐条（99-160）；严格模式拒绝+计数单调（309-358）

### tests/runtime/lifecycle/run_application_shutdown_pipeline_regression.cs — 有效
- 断言数: 约 55
- 验证内容: 关停流水线 8 个场景：成功顺序、各 hook 失败点、release/barrier 门禁为假、审计 owner 阻止内容释放、屏障失败不虚报完成
- 证据/问题: hook 调用序列精确匹配（129-133、183-186）；失败时 ContentCalled/BarrierCalled 为假且 PhaseHistory 不含 ContentReleased（187-196）；失败 stage 逐点匹配（197-200 等）

### tests/runtime/lifecycle/run_content_snapshot_query_soak_regression.cs — 有效
- 断言数: 约 75（前置 4 + 10 周期 ×(1+6) + 结束 2）
- 验证内容: 10 会话 × 1000 查询浸泡期间进程快照引用身份/epoch/root 数/审计计数绝对稳定，10000 次 typed 查询全部成功
- 证据/问题: ReferenceEquals 快照身份（180-182）；逐周期 AssertStable 6 项精确相等（168-196）；完成查询数精确等于 10000 且失败数=0（115-120）。前置断言（47-59）带 "soak precondition" 标签，不算弱断言

### tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs — 有效
- 断言数: 约 15
- 验证内容: GameSession 正常关闭/exit-tree 关闭的 catalog revision 恰好 +1、重复 Dispose 幂等、进程内容跨会话复用、finalizer 抑制计数不增长
- 证据/问题: revision 精确 +1 与二次 Dispose 不变（51-67、104-120）；IsInstanceValid 双向断言（109-124）；已知内容解析（133-144）

### tests/runtime/lifecycle/run_lifecycle_audit_activity_snapshot_regression.cs — 有效
- 断言数: 约 35
- 验证内容: 审计注册表活动快照分类计数、域分布、域转移/回滚不计入完成转移、严格模式失败不改变活动总数、违规计数单调且不污染 activity
- 证据/问题: 各域精确计数（75-108）；回滚后 TransfersOut/In 不变（131-141）；drained 后 owned/disposed 配平（205-222）；违规前后 activity 快照整体相等（247-251）

### tests/runtime/lifecycle/run_lifecycle_soak_statistics_regression.cs — 有效
- 断言数: 约 40
- 验证内容: 浸泡统计评估器（测试基础设施自身）：中位数/最小二乘斜率精确值、阈值 max(绝对,百分比) 选择、边界通过、计数器指纹失配/违规/配平/内存增量与斜率必须在指定 cycle 报指定 counter 失败
- 证据/问题: 斜率精确 10.0/-10.0（48-70）；HasFailure(cycle, counterName) 逐条定位失败（175-222、243-302、314-343）。注意：被测对象是测试设施而非生产代码，但断言精确可失败

## tests/runtime/contracts/

### tests/runtime/contracts/run_settlement_service_result_regression.cs — 有效
- 断言数: 约 25
- 验证内容: SettlementServiceResult 投影字典形状（键存在/移除）、typed 结果与输入 mutation 的深拷贝隔离（双向）、非法 Object carrier 载荷被原子拒绝且不清空既有数据
- 证据/问题: 输入 mutation 后投影仍保留原值（98-117）；投影 mutation 不回写 typed result（134-158）；拒绝后 stable 键保留（196-210）

## tests/text_runtime/headless/

### tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs — 有效
- 断言数: 约 30
- 验证内容: 已知技能选择的稳定 entry identity：选择/清除/快照/文本投影/预览 command 均携带或拒绝 selected_skill_entry_id
- 证据/问题: entry id 精确字符串断言（53-62、68-77）；清除后五项状态全空（102-126）；stale entry id 必须使 preview 构建返回 null（128-135）；availability view 拒绝未知 entry id（166-169）

### tests/text_runtime/headless/run_headless_game_test_session_regression.cs — 有效
- 断言数: 约 40
- 验证内容: headless 会话生命周期：无协调器时用显式 owned GameSession、borrowed battle context 非法输入的拒绝与审计基线无泄漏、dispose 清 battle save lock/日志 sink、build_snapshot 不重建 index.dat 且不回写外部 mutation、synthetic 敌人定义进入 facade 索引
- 证据/问题: 审计快照逐项基线对比（176-216、608-661）；Throws<InvalidOperationException>/ArgumentNullException 错误路径（196-244）；文件不存在断言（372-375、402-405）

### tests/text_runtime/headless/run_progression_text_snapshot_regression.cs — 有效
- 断言数: 约 9
- 验证内容: 构造固定 party 快照后文本渲染器逐行精确输出 progression 状态（名望排序、成员资源/核心技能/trigger 状态、职业行）
- 证据/问题: AssertLine 整行相等匹配（164-172），含排序断言（123-128）；非 Contains 而是精确行匹配，强断言

### tests/text_runtime/headless/run_text_command_party_battle_surface_regression.cs — 有效
- 断言数: 约 35
- 验证内容: party equip/unequip 经文本命令落 typed 状态（item_id+instance_id）、战斗内技能选择体力阻断 InvalidState、多形态切换、battle equip self-only 负例、inspect modal 开关、移动扣移动力、wait 交回 timeline
- 证据/问题: 装备实例 id 精确回读（65-75）；负例断言 skipped=false + ok=false + code=InvalidState（117-129、149-161）；移动后 anchor 精确等于目标且移动力下降（228-237）

### tests/text_runtime/headless/run_text_command_quest_progress_regression.cs — 偏弱
- 断言数: 约 45
- 验证内容: quest progress 文本命令端到端（objective 计数、action_id 上下文、claimable 列表）+ QuestProgressCommandPayloadData 解码与 StringName 键拒绝（真实）；但后半段对三个纯数据 fact 类做构造后回读
- 证据/问题: 真实部分：56-101（e2e）、123-157（payload 解码+逐字段 StringName 拒绝）。弱部分：159-214——CharacterKnowledgeChangeFact/CharacterAttributeChangeFact/CharacterMasteryChangeFact 是纯数据载体（构造器仅赋值，见 scripts/systems/progression/CharacterKnowledgeChangeFact.cs:9-18），`new(...)` 后立即 Eq 回读构造参数（167-169、186-194、208-213），属"Set后立即Get回读"，约 20 条断言恒真

### tests/text_runtime/headless/run_text_command_reward_flow_regression.cs — 有效
- 断言数: 约 17
- 验证内容: party modal 开/关、promotion 缺选项负例停留 modal、reward modal 禁止 close 跳过、reward confirm 后 modal 清空且待领数归零
- 证据/问题: 负例均断言 skipped=false+ok=false+code=InvalidState+modal 保持（61-78、90-107）；confirm 后 pending_reward_count=0（116-124）

### tests/text_runtime/headless/run_text_command_warehouse_use_regression.cs — 有效
- 断言数: 约 20
- 验证内容: warehouse add/discard-one/discard-all 库存精确增减、装备 discard-all 拒绝 InvalidArgument 且库存不变、warehouse use 技能书经正式链学会技能并消耗库存
- 证据/问题: 库存精确计数（132-158、169-189）；非法形态双重拒绝（191-207）；学会状态 is_learned=true（89-92）

## tests/text_runtime/commands/

### tests/text_runtime/commands/run_battle_equipment_text_command_regression.cs — 有效
- 断言数: 约 80
- 验证内容: 战斗换装全矩阵：string-key-only 定义拒绝、装备需求泛化错误不泄露 blocker、重复实例必须指定 instance_id、AP 不足/self-only/背包满负例不扣 AP 不动装备、双手/versatile 握法联动、战斗中不写 party 战后写回、HP clamp 不误报
- 证据/问题: 负例均精确断言 error_code+ap_after 不变+背包残留（410-483、534-554）；握法联动精确（556-634）；战斗/战后 party 快照双向断言（636-697）；卸装 round-trip 保留 rarity/durability（526-531）

### tests/text_runtime/commands/run_contingency_text_commands_regression.cs — 有效
- 断言数: 约 70
- 验证内容: 应急矩阵（contingency）命令链：save/charge/edit/clear 状态与材料消耗、战斗锁拒绝、battle 快照 contingency 投影、timeline advance 触发 structured report
- 证据/问题: AssertLastResult 7 字段精确比对（419-431）；MP 预留/有效上限精确数值（256-263）；gem 库存扣减与不退还（57、65、73）；report entry 逐字段断言（526-543）

### tests/text_runtime/commands/run_text_command_parse_regression.cs — 有效
- 断言数: 约 11
- 验证内容: 非法标量参数（移动次数/坐标/tick 秒数/仓库容量）必须失败、返回具体中文校验文案、且不漂移玩家坐标
- 证据/问题: 负例 ok=false 且 message 含具体校验文案（34-59）；失败前后坐标逐分量相等（41-50）

### tests/text_runtime/commands/run_text_save_load_regression.cs — 有效
- 断言数: 约 18
- 验证内容: game new→save slot 登记→game load 回环、未生成子地图空占位保持、forge 锻造产物持久化后重载仍在仓库
- 证据/问题: 重载后 PartyState/snapshot 双侧精确计数铁制大剑=1（94-117）；子地图 world_data 必须为空字典（202-211）

### tests/text_runtime/commands/run_validation_text_surface_regression.cs — 有效
- 断言数: 约 25
- 验证内容: 正式内容 validation 全绿；注入非法 quest/item/world 定义后对应 domain 恰好 1 条错误、错误文案含具体缺失引用、文本快照渲染计数、validation.ok 翻转
- 证据/问题: 精确错误计数（136、168）；AssertErrorContains 匹配具体错误片段（109、137、169）；非法 item 需产生 bind-time 日志（140-146）

## tests/text_runtime/tools/

### tests/text_runtime/tools/run_text_command_repl.cs — 无效
- 断言数: 0
- 验证内容: 无——交互式 REPL 开发工具，无任何行为断言
- 证据/问题: 全文 37 行无一条 _test 断言（15-35 仅循环执行命令并打印）；文件头注释自述被 run_regression_suite.py 跳过（第 4 行）；空输入/exit 即 Finish 通过（17-31）。作为测试恒通过；作为工具是有意的，但审计维度为"无效测试"

### tests/text_runtime/tools/run_text_command_script.cs — 偏弱
- 断言数: 约 1（每行命令 ok 检查）
- 验证内容: 逐行执行场景文件，任一命令失败即 Fail——能当冒烟门，但对系统行为零具体断言
- 证据/问题: 唯一有效检查在 40-46（`!result.ok` → Fail）；skipped 命令直接 continue（36-37）不校验；文件自述被回归套件排除（第 4 行），无人自动运行；默认场景 res://tests/text_runtime/scenarios/smoke_startup.txt（9-10）

## tests/shared/

### tests/shared/run_item_trait_detail_text_regression.cs — 有效
- 断言数: 约 12
- 验证内容: ItemTraitDetailText.Compose/BuildTraitLines 输出内容、flavor 在前 trait 在后的顺序、无 trait 原样返回、未知/无名 trait 跳过
- 证据/问题: 顺序用 IndexOf 比较（42-46）；无 trait 时整串相等（57）；逐行排除 unknown/nameless（71-73）

### tests/shared/run_shared_test_fixture_regression.cs — 有效
- 断言数: 约 17
- 验证内容: 测试设施自身：TestHarness 失败记录→TestResult 映射（退出码/标签/快照）、BattleTestFixture 建格子/单位/AP、FixedRoll/FixedHitResolver 使用注入骰值
- 证据/问题: 故意制造失败后断言 result.Passed=false、ExitCode=1、失败诊断保留（17-41）；注入骰 2→伤害=3 精确（93）；hit.Roll=17（101）

## tests/static_analysis/

注意：两个文件在工作树中均已被删除（`git status` 显示 `D`，仅剩 .uid 文件），以下判定基于 git HEAD 内容；无论判定为何，当前它们都不会被运行。

### tests/static_analysis/run_contingency_autocast_no_known_spoof_regression.cs — 无效
- 断言数: 4（全部可能空转）
- 验证内容: 意图：ExecuteAutoCast 方法体不得含 AddKnownActiveSkill(/SetKnownActiveSkillIds(/SetKnownSkillLevelTyped(/RemoveKnownSkillLevelTyped(
- 证据/问题: 已核实空转缺陷——ExtractMethodBody 在方法不存在时返回空串（48-56：`IndexOf < 0 → return ""`），随后 `"".Contains(forbiddenCall)` 恒为 false，`_test.False(false,...)` 恒通过（29-35）。即 ExecuteAutoCast 被删除/改名则静默通过，正是已知的"方法被删则静默通过"。且断言本质是对源码字符串的负向 Contains。另：该文件当前已在工作树删除（git status D），彻底不运行。被守护源文件 scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.AutoCast.cs:18 目前仍存在 ExecuteAutoCast

### tests/static_analysis/run_direct_field_write_guard_regression.cs — 有效（但当前已从工作树删除，实际不运行）
- 断言数: 约 75（5 个扫描器单测 + 全仓库扫描）
- 验证内容: 直接字段写入守卫扫描器：合成源码正/负例（非 owner 同名字段放行 0 违规、受保护 owner 写入精确 27/26 条违规、成员链解析 1 条、owner 内部写放行、RefreshFootprint 外部调用拦截）+ 对 scripts/ 全仓库真实扫描
- 证据/问题: 违规计数精确相等且逐字段 ContainsViolation（367-410、456-495）；TestRepositoryScripts 对真实仓库逐文件扫描、任一违规即 Fail（584-604）。问题：a) 文件内中文断言消息为乱码（如 322、371 行，GBK/UTF-8 编码损坏，不影响断言逻辑）；b) 该文件当前在工作树已删除（git status D），扫描器单测+仓库扫描目前都不会执行，防护实际处于停用状态

## tests/tooling/

### tests/tooling/test_run_regression_suite.py — 有效
- 断言数: 15 个测试方法，约 60 条断言；已实际运行：15/15 通过
- 验证内容: 回归套件 runner：parser 拒绝已移除选项/非法 timeout、子进程 env 强制 lifecycle 变量且不改原 env、挂死进程 terminate+124、泄漏/finalizer 标记即使退出码 0 也判失败、fail-on-output-error 开关语义、serial/parallel 派发传参
- 证据/问题: 逐方法核实均针对 runner 行为（mock Popen/run_godot_process），断言具体返回值与调用次数（如 99-101、199-200、281-282）。关于已知的"--jobs 16 腐烂用例"：已核实——HEAD 版本的 test_ci_imports_resources_and_runs_one_strict_full_suite 对 .github/workflows/ci.yml 做 assertIn("--jobs 16") 等源码字符串断言，而当前 ci.yml:82-86 已改为动态计算 jobs（`--jobs "${test_jobs}"`），该用例若存在必失败；但当前工作树已将该用例（连同 test_runner_source_has_no_retry_or_shutdown_exemption_path 源码 Contains 守卫、test_run_result_and_printed_summary_have_no_retry_count）整体删除（未提交的本地修改），现存 15 个用例全部有效且通过

### tests/tooling/test_run_e2e_suite.py — 有效
- 断言数: 12 个测试方法，约 50 条断言；已实际运行：通过
- 验证内容: E2E 套件 runner：场景注册表精确形状（5 场景/7 步骤/路径/种子 39208）、选择器去重过滤与未知场景报错、--list 不触达 Godot、沙箱 env（XDG/APPDATA、不改 base env）、确定性种子仅注入声明场景、多步骤同沙箱串行、失败跳过依赖步骤、lifecycle fatal 始终强制
- 证据/问题: 注册表精确元组断言（25-84）；跨平台 env 精确路径（159-203）；依赖失败时调用序列与返回码精确（365-369）

### tests/tooling/test_build_battle_sim_analysis_packet.py — 有效
- 断言数: 7 个测试方法，约 70 条断言；已实际运行：通过
- 验证内容: 战斗模拟分析包构建器：未完成 run 从所有常规指标剔除、跨 run 计数合流、缺失计数图标记 unavailable 而非归零、completed-only 对比不依赖种子配对、6v12 旧形状重算/标记 unavailable、draw 不可分类时不静默推断、CLI 端到端替换 comparisons
- 证据/问题: 用污染值（9999/corrupt）做负向断言（86-91、150-155、302-303）；数值精确（131-149、361-378）；CLI 真实写临时目录并回读产物断言（491-517）

## 统计

- 有效: 26
- 偏弱: 2
- 无效: 2

### 偏弱清单
| 文件 | 问题 |
|---|---|
| tests/text_runtime/headless/run_text_command_quest_progress_regression.cs | 159-214 行对 3 个纯数据 fact 类构造后回读构造参数（约 20 条恒真断言）；文件其余部分（e2e + payload 解码负例）真实有效 |
| tests/text_runtime/tools/run_text_command_script.cs | 仅"命令失败即 Fail"的冒烟检查（40-46），无行为断言；被套件排除，无人自动运行 |

### 无效清单
| 文件 | 问题 |
|---|---|
| tests/static_analysis/run_contingency_autocast_no_known_spoof_regression.cs | ExtractMethodBody 找不到方法返回 ""（48-56）→ 负向 Contains 断言恒真（29-35），"方法被删则静默通过"已核实；且文件已从工作树删除，完全不运行 |
| tests/text_runtime/tools/run_text_command_repl.cs | 全文 0 断言的交互式 REPL 工具，作为测试恒通过；被套件排除 |

### 额外风险提示（不改变上述判定）
- tests/static_analysis/run_direct_field_write_guard_regression.cs 内容判定为有效（含扫描器自身正负例+全仓库扫描），但两个 static_analysis 文件当前在工作树均被删除（git status D，仅剩 .uid），该目录的静态防护实际处于停用状态。
- tests/tooling/test_run_regression_suite.py 的"--jobs 16"腐烂用例确认存在于 git HEAD（对 ci.yml 的 assertIn("--jobs 16")，ci.yml 已改为动态 jobs），工作树已删除该用例，现存版本 15/15 通过。

## 批次 09 明细

# 测试有效性审计报告 — batch09

范围：`tests/progression/core/`（26 个 run_*.cs）+ `tests/progression/schema/`（24 个 run_*.cs，目录实际数量，非任务描述的 20 个），共 50 个文件，全部逐行精读。

判定口径：
- 有效 = 断言针对被测系统行为/状态/边界/错误路径，具体且可失败；
- 偏弱 = 存在弱断言模式但仍有部分真实验证；
- 无效 = 恒真/不执行/自比/空方法/catch 吞失败。

注：本仓大量 schema 测试采用"合成 fixture → 投影/校验边界 → 断言精确错误码或投影字段"的模式。投影/序列化往返断言（Set→投影→Get）验证的是投影边界本身的映射正确性，可失败，判为有效；仅当整段测试只做非空/Count>0 而无任何具体值或负向校验时才判偏弱。

---

## tests/progression/core/

### tests/progression/core/run_attribute_growth_service_regression.cs — 有效
- 断言数: 约17
- 验证内容: AttributeGrowthService 进度累计、100 点转换、属性 20 封顶、无效属性 id 拒绝。
- 证据/问题: 精确断言 ProgressBefore/After、AttributeBefore/After 及写回（L35-72）；负向路径 L84-88。

### tests/progression/core/run_attribute_trait_modifier_regression.cs — 有效
- 断言数: 约1
- 验证内容: AttributeService 应用 trait 属性修正后 strength 10→13。
- 证据/问题: 单条精确 Eq 断言（L47-51），针对快照计算行为，可失败。

### tests/progression/core/run_base_attack_bonus_regression.cs — 有效
- 断言数: 约50
- 验证内容: Full/3/4/1/2 BAB 表、兼职分子累加再取整、rank20 上限、Unknown progression、AttributeService 排除 inactive/hidden 职业。
- 证据/问题: 全表逐点 Eq（L31-73）；兼职精度用例明确给出 per-prof floor 会失败的反例（L86）；inactive/hidden 负向断言 L171-185。

### tests/progression/core/run_bounty_mist_harrier_quest_regression.cs — 有效
- 断言数: 约30
- 验证内容: 真实 bounty_mist_harrier.tres 内容契约 + 跨战进度累计/无关敌人不推进/领奖金币入账/可重复接取归零。
- 证据/问题: 加载真实 tres 做内容断言（L35-110）；运行时行为链断言（L121-180）；catch 仅转为 _test.Fail（L22-25），未吞失败。

### tests/progression/core/run_character_creation_service_regression.cs — 有效
- 断言数: 约45
- 验证内容: 建卡身份选项过滤/默认选择、reroll→hidden_luck 全边界映射、初始 HP 公式、payload 非法 ascension/bloodline/race 对拒绝且不污染成员。
- 证据/问题: 14 个 Test 方法逐个有具体断言；非法 payload 前后快照对比（L277-281 等）；映射表 17 个边界点（L131-157）。无空方法。

### tests/progression/core/run_character_management_achievement_summary_regression.cs — 有效
- 断言数: 约20
- 验证内容: 成就汇总排序（进度降序+名称字典序 tie-break）、计数、最近解锁名、pending reward summary_text meta 与 description 回退。
- 证据/问题: 逐行 AssertEntry 精确断言顺序与数值（L68-71）；回退路径 L107-114。

### tests/progression/core/run_character_management_practice_regression.cs — 有效
- 断言数: 约25
- 验证内容: 修行技能替换需确认、正式学习校验不可绕过、basic3→intermediate2 等级换算、歧义 track 报错码、非法 tag 组合 fail-closed。
- 证据/问题: 6 个 Test 方法均含正/负向状态断言（如 L63-71 确认前不写入；L185-203 歧义拒绝且双旧技能保留）。

### tests/progression/core/run_character_management_quest_materializer_regression.cs — 有效
- 断言数: 约75
- 验证内容: submit_item 目标进度/缺货/错物/缺 target_value 错误码，任务奖励金币物品/溢出/pending reward 物化，属性进度奖励封顶，level trigger 设置/清除/成长/非法条目拒绝，mastery 奖励聚合过滤。
- 证据/问题: 11 个 Test 方法全部具体到错误码与数值（如 L193-197、L347-349、L957-966）。无空方法。

### tests/progression/core/run_character_management_trait_attribute_regression.cs — 有效
- 断言数: 约4
- 验证内容: CharacterManagementModule 注入 trait 属性修正（来源类型/折叠 source_id/快照 10→13）；Dispose 幂等。
- 证据/问题: 主测试 L69-86 精确断言。注意 L21-26 `TestCharacterManagementDisposeIsIdempotent` 无任何断言，属"不抛异常即通过"的冒烟方法（Dispose 抛错会使进程失败，仍有弱信号），点名说明。

### tests/progression/core/run_character_management_weapon_projection_regression.cs — 有效
- 断言数: 约18
- 验证内容: 武器物理伤害标签：缺成员/空手 unarmed blunt/装备武器标签；坏主手状态（无定义/非武器/非法 tag）fail-closed；装备视图 typed DTO 投影字段。
- 证据/问题: 三组正负向精确断言（L33-50、L65-84、L110-155）。

### tests/progression/core/run_character_trait_service_regression.cs — 有效
- 断言数: 约12
- 验证内容: EffectiveTraitSet 聚合 identity/character/equipment 来源、stack 策略（highest_roll 取 6、additive 叠 2 层、stack_by_instance 保留实例键）、属性修正 source key 折叠。
- 证据/问题: FakeGateway 提供确定夹具，断言精确值（L49-63、L75-89）。

### tests/progression/core/run_effective_trait_set_regression.cs — 有效
- 断言数: 约12
- 验证内容: EffectiveTraitSet 按键/trait_id 查找、DeriveTraitIds 去重排序、ToBattleEffectiveInstances 排序与字段反规范化。
- 证据/问题: 精确断言排序结果与投影字段（L60-100）。

### tests/progression/core/run_level_description_template_regression.cs — 有效
- 断言数: 约25
- 验证内容: 模板变量替换、非法/失败/自引用/循环表达式逐字段报错且不中断、条件块有无/数值 0、完整格挡/旋风斩/挑衅模板渲染、缺模板/缺配置返回空、非字典配置按路径抛错、0 值不撑开 optional 块、锁定 cast variant 不合入。
- 证据/问题: 16 个 Test 方法，逐字符串精确 Eq（L45、L54-58、L159-179 等）；负向 InvalidDataException 路径 L297-311。

### tests/progression/core/run_party_state_duplicate_regression.cs — 有效
- 断言数: 约18
- 验证内容: PartyState.DuplicateState 深拷贝：修改副本的金币/仓库/技能熟练/属性/晋升快照/待转职/装备耐久/trait roll/任务进度/待领奖励不影响源。
- 证据/问题: 逐字段改副本后断言源不变（L45-120），另验证副本保留同步字段（L121-131）。

### tests/progression/core/run_profession_assignment_service_regression.cs — 有效
- 断言数: 约10
- 验证内容: 核心技能分配到职业（三处状态同步）与非核心晋升为核心。
- 证据/问题: 精确断言 assigned_profession_id、core_skill_ids、active_core_skill_ids（L37-61、L79-96）。

### tests/progression/core/run_profession_rule_service_regression.cs — 有效
- 断言数: 约15
- 验证内容: 空 check_mode 继承依赖可见性策略、未知 check_mode 投影拒绝、eligible skill 过滤（等级/tag/已分配）、previewAssignedSkillIds、active condition 刷新 inactive/hidden 及自动恢复。
- 证据/问题: 负向+精确 reason 断言（L69-82、L115-141、L174-195）。

### tests/progression/core/run_progression_service_resource_unlock_typed_regression.cs — 有效
- 断言数: 约10
- 验证内容: 学习耗 MP/斗气技能后解锁对应战斗资源、HP/Stamina 默认解锁、互不串扰、存档往返保留。
- 证据/问题: 学习前后 True/False 对照（L38-69）；序列化往返 L71-79。

### tests/progression/core/run_promotion_selection_typed_regression.cs — 有效
- 断言数: 约15
- 验证内容: PromotionSelectionData 归一化（string/StringName/Variant 去重、忽略空）、plain payload 往返、typed 选择驱动正式转职且 HP 不走字典后门。
- 证据/问题: 精确 Eq（L100-124、L164-174）。

### tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs — 有效
- 断言数: 约25
- 验证内容: 任务接取需求评估器：quest_completed/active/not_completed、多需求、缺 quest_id、未知类型、锁定原因 id 与显示名回退。
- 证据/问题: 9 个 Test 方法均有 CanAccept 正负向+LockReasonId 精确断言（如 L62-71、L199-205）。

### tests/progression/core/run_quest_danger_rating_resolver_regression.cs — 有效
- 断言数: 约35
- 验证内容: 危险度星级阈值边界、11 个正式悬赏任务星表、override 优先、单场击败目标推导、未评级边界（null/空 target/缺模板/非战斗）、星级文案渲染。
- 证据/问题: 阈值表逐点 Eq（L30-41）；真实内容快照交叉验证（L46-76）；负向 MissingTargetIds 精确断言 L176-185。

### tests/progression/core/run_quest_progress_service_regression.cs — 有效
- 断言数: 约55
- 验证内容: 正式进度事件 schema（9 种坏事件拒绝）、单场击败目标不跨战累计/不被小 target_value 覆盖、普通击败跨战累计、直接 RecordProgress 迁移 claimable、失败策略 terminal/restartable 全状态机、缺 target_value 不默认 1、负 world_step 拒绝。
- 证据/问题: 8 个 Test 方法，精确进度值与状态集合断言（如 L145-231、L368-418）。

### tests/progression/core/run_racial_skill_grant_service_regression.cs — 有效
- 断言数: 约9
- 验证内容: 种族技能补授（等级/来源字段）、重复补授幂等、grant 移除后孤儿技能撤销。
- 证据/问题: 精确断言 skill_level=2、granted_source（L64-71）、撤销后 null（L106-109）。

### tests/progression/core/run_skill_description_consistency_regression.cs — 有效
- 断言数: 约55
- 验证内容: 链式闪击/精准射击/冲锋/旋风斩真实内容与描述一致性：typed combat 字段、伤害骰、豁免、连锁参数、旧参数键负向、描述文案含/不含特定机制声明。
- 证据/问题: 加载真实 ContentSnapshot 做交叉一致性校验（L61-121）；旧参数键逐条 False（L257-273）；描述与门槛等级交叉（L286-295）。

### tests/progression/core/run_skill_effective_max_level_rules_regression.cs — 有效
- 断言数: 约9
- 验证内容: 斗气斩动态上限（transformation count）、核心锁定前后 non_core 上限、职业 rank/2 整除动态上限多点、未锁定仍受 non_core 限制。
- 证据/问题: 精确等级断言（L45-64、L83-114）。

### tests/progression/core/run_skill_merge_service_regression.cs — 有效
- 断言数: 约20
- 验证内容: 合并移除源技能时清理 trigger 状态、复合升级保留源技能但迁移核心位、无职业分配时降级非核心且不抛异常、结果通过严格存档校验。
- 证据/问题: catch 转 _test.Fail 未吞失败（L237-246）；精确状态断言 L65-86、L144-189。

### tests/progression/core/run_typed_party_quest_state_regression.cs — 有效
- 断言数: 约45
- 验证内容: typed 存档解析负向（非 int/缺字段/越界/long 截断/非字符串键）、损坏 context 安全拒绝、失败任务不跨集合泄漏、查询返回 detached 状态、社会声望饱和/独立/深拷贝/往返、save version=9。
- 证据/问题: 12 个 Test 方法；AssertPartyPayloadRejected 覆盖 12 种坏 payload（L347-421）；ExpectArgumentException 正确地未捕获时记失败（L466-479）。

---

## tests/progression/schema/

### tests/progression/schema/run_achievement_schema_regression.cs — 有效
- 断言数: 约20
- 验证内容: AchievementDef/AchievementRewardDef 往返保留字段 + 9 种负向拒绝（空 payload/缺 threshold/多余字段/非 Array rewards/空 event_type/零 amount 分类规则等）。
- 证据/问题: 往返断言验证序列化对（L29-56）；负向逐条 == null（L61-152）。

### tests/progression/schema/run_barrier_profile_schema_contract_regression.cs — 有效
- 断言数: 约70
- 验证内容: prismatic_sphere 真实 barrier 资源 2E 七层契约（层 id/顺序/breaker/outcome/status）+ 七个单层 profile 与共享层资源 ResourcePath 一致。
- 证据/问题: 加载真实 tres 做跨资源一致性校验（L113-129 ResourcePath 双端相等）；逐层精确值 L203-243。L217-220 有 Count>0 但随后即断言具体 outcome，非空断言占比小。

### tests/progression/schema/run_battle_save_skill_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: 豁免字段校验：合法 damage/status/caster_spell 通过；非法 ability/tag/partial/缺 DC/静态 DC 冲突等精确错误文案；save tag 列表重复/旧后缀/未知拒绝；level_overrides 非 int 字段逐条诊断。
- 证据/问题: AssertExactErrors 精确匹配错误文案（L286-306）；临时 tres 落盘走真实 registry 校验（L347-386，含清理）。

### tests/progression/schema/run_charge_skill_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: charge/path_step_aoe typed 字段投影（7 个字段逐一 Eq）、旧 params 键逐条拒绝、非法 trap immunity 等级拒绝。
- 证据/问题: 投影断言 L37-41、L118-124 验证 FromResource 映射；负向校验 L67-73、L90-95、L155-161 为真实验证。

### tests/progression/schema/run_combat_effect_equipment_durability_schema_regression.cs — 有效
- 断言数: 约11
- 验证内容: 装备耐久 slot weights typed 投影、旧 params.slot_weight_map 不投影且被校验拒绝、重复/未知槽位/非正权重负向。
- 证据/问题: 投影逐字段 Eq（L38-62）；负向精确错误片段 L123-160。

### tests/progression/schema/run_combat_projectile_kind_schema_regression.cs — 有效
- 断言数: 约18
- 验证内容: projectile_kind 枚举映射往返、空值仅表继承、技能级空值/未知值拒绝、派生与已移除类别拒绝、tag 不再推断投射物类别。
- 证据/问题: 负向精确错误片段（L50-75、L92-113、L126-129）；推断移除负向 L139-145。

### tests/progression/schema/run_contingency_content_validator_regression.cs — 有效
- 断言数: 约40
- 验证内容: 真实目录含 chain_contingency 与 6 个 V1 可存储 profile 契约；9 类非法 stored skill 拒绝（缺 profile/not_storable/种族授予/等级门槛/超已知等级/forbidden tag 短路精确诊断/resolver 白名单/未知绑定键）；篡改存档后 LoadSave 报 InvalidData 且原因稳定。
- 证据/问题: 逐规则 ExpectErrorContains（L127-369）；端到端写坏存档再加载断言错误码与 last_error_reason（L372-405）；forbidden tag 断言精确单条错误全文（L298-311）。

### tests/progression/schema/run_contingency_setup_schema_regression.cs — 有效
- 断言数: 约40
- 验证内容: contingency 存档 schema：uncharged/charged 接受、charged=false 带 MP/材料拒绝、disabled+charged 拒绝、双 charged 拒绝、缺 contingency_matrix_setups 拒绝、各层未知字段拒绝、resolver 类型全集解析与精确键往返、版本门禁（party v8/root v10 拒绝）、parameter_bindings 类型白名单。
- 证据/问题: 12 个 Test 方法均为正/负向 payload 级断言（如 L98-119、L272-315）。

### tests/progression/schema/run_equipment_ability_content_registry_regression.cs — 有效
- 断言数: 约90
- 验证内容: 装备能力注册表：AC 组件域常量表契约、内建 handler/trigger 元数据、缺 context fail-closed、封闭域未知引用拒绝、状态声明先于引用校验、空/最小 pack 构建与 FindBindings 匹配/拒绝、projected categories 与保留类别、cognition ceiling、replace_binding 拓扑与冲突、快照不受 Resource 后续突变影响、失败 rebuild 保留上次快照、嵌套条件组稳定错误、约 30 类非法内容精确错误码。
- 证据/问题: 14 个 Test 方法全部具体到错误码+路径片段（L807-944 逐条 AssertErrorContains）；快照隔离断言 L651-676。

### tests/progression/schema/run_equipment_bonus_damage_replacement_schema_regression.cs — 有效
- 断言数: 约8
- 验证内容: add_damage_dice replacement group/priority 校验与投影保留；apply_status mitigation tag/tier 校验；三类非法组合精确错误码。
- 证据/问题: 精确错误码断言（L103-123、L156-176）；投影保留 L84-93；catch 转 Fail（L22-25）。

### tests/progression/schema/run_identity_required_text_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: race/subrace/age/bloodline(+stage)/ascension(+stage)/stage_advancement 六类注册表拒绝空白 display_name/description，精确诊断路径。
- 证据/问题: 临时 tres 落盘走真实 registry.Validate，逐字段精确错误片段（L188-264）；finally 清理（L65-68）。

### tests/progression/schema/run_identity_sub_registry_schema_regression.cs — 偏弱
- 断言数: 约10
- 验证内容: 身份目录加载 + race/subrace save tag 列表（重复/未知/旧后缀）拒绝。
- 证据/问题: L31-52 `TestIdentityCatalogLoadsTypedContent` 整段只有 4 条 `Count > 0` 断言（加载真实目录但无任何具体值或交叉校验），属典型弱断言方法；L54-156 `TestRaceAndSubraceRegistriesRejectInvalidSaveTagLists` 为强负向校验（精确错误片段），文件仍有真实验证，故判偏弱而非无效。

### tests/progression/schema/run_phantasmal_kill_schema_regression.cs — 有效
- 断言数: 约40
- 验证内容: 正式 mage_phantasmal_kill.tres 通过校验；合成合规形状基线通过；篡改豁免/瞄准/绑定形状/params/等级描述覆盖后逐字段命中诊断。
- 证据/问题: 篡改-校验负向矩阵（L67-237）；真实资源加载校验 L49-65。L24-32 用 Failures.Count 门控后续用例，断言仍真实执行，仅失败时级联跳过，不计问题。

### tests/progression/schema/run_phoenix_rebirth_body_cloak_content_regression.cs — 有效
- 断言数: 约60
- 验证内容: 凤凰板甲/披风真实内容契约：item→trait→binding→reaction→action→skill 全链投影，trigger/timing/条件组/状态层数/骰面/授予技能周期等逐项精确值。
- 证据/问题: 真实 ContentSnapshot 跨资源一致性（L36-113、L141-195）；条件组逐 fact 精确断言（L60-63、L177-182）。

### tests/progression/schema/run_phoenix_rebirth_single_item_content_regression.cs — 有效
- 断言数: 约150
- 验证内容: 凤凰重生 10 件套冻结签名（槽位/类型/价格/属性修正/trait 集合逐项）、8 件单装能力契约（grant 周期次数、技能 AP/射程/骰面/分支过滤）、附伤互斥阶梯优先级。
- 证据/问题: 冻结签名逐项 AssertExactStrings（L830-864）；各件精确值断言贯穿全文。属内容冻结式交叉校验，期望值独立书写，可失败。

### tests/progression/schema/run_power_word_kill_execute_schema_regression.cs — 有效
- 断言数: 约35
- 验证内容: execute 技能 schema：合规形状通过；错误瞄准/豁免/兄弟 effect/passive/variant/旧字段/隐藏 trigger/参数载荷逐条拒绝；soul_fracture 0=显式禁用、负值拒绝；真实 tres 通过。
- 证据/问题: 篡改-校验负向矩阵（L35-162）；真实资源校验 L164-180。

### tests/progression/schema/run_skill_attack_defense_mode_schema_regression.cs — 有效
- 断言数: 约12
- 验证内容: attack_defense_mode 枚举往返、基线/level override 非法值拒绝、等级 override 投影到 GetEffectiveAttackDefenseMode、真实奥术飞弹用接触 AC 且描述披露。
- 证据/问题: 负向精确错误片段（L52-71）；真实资源交叉（描述含"接触AC"等）L122-137。

### tests/progression/schema/run_skill_attribute_growth_typed_regression.cs — 有效
- 断言数: 约15
- 验证内容: attribute_growth_progress 校验（tier 总和/非法属性/StringName key/非字符串 key/空 key/非 int value 精确诊断）+ 真实技能 typed 成长值（冲锋 100/20、连珠箭 80/40、无 perception）。
- 证据/问题: 负向精确错误全文（L48-117）；真实内容断言 L143-171（含负向 ContainsKey）。

### tests/progression/schema/run_skill_attribute_modifiers_typed_regression.cs — 有效
- 断言数: 约7
- 验证内容: 真实强健技能两条 typed 属性修正（通道/数值）+ AttributeService 应用 typed 技能修正 10→12。
- 证据/问题: 真实内容精确值（L37-61）；服务行为断言 L95-99。

### tests/progression/schema/run_skill_level_description_typed_regression.cs — 有效
- 断言数: 约12
- 验证内容: 等级描述 schema：合法模板/字面量/运行时变量表达式通过；非法/空/未闭合表达式拒绝；int key/非字典 value/超 max_level/缺等级精确诊断；formatter 从纯 typed effect parameters 渲染。
- 证据/问题: 精确错误全文断言（L144-184）；formatter 端到端 Eq L248-252。

### tests/progression/schema/run_skill_requirements_typed_regression.cs — 有效
- 断言数: 约12
- 验证内容: 技能需求校验（缺前置技能/非 int 等级需求/非法属性/缺升级来源，精确错误全文且仅这四条）+ 真实旋风斩/圣刃连段/金刚不坏的 learn/skill_level/knowledge/achievement/upgrade/mastery 需求投影。
- 证据/问题: AssertOnlyValidationErrors 双端精确匹配（L166-192）；真实内容具体值 L91-124。

### tests/progression/schema/run_skill_tags_typed_regression.cs — 有效
- 断言数: 约6
- 验证内容: 真实注册表暴露 basic_attack/charge/warrior_toughness DTO 且各自带指定 tag（basic/melee/warrior）。
- 证据/问题: 断言薄但具体、可失败（tag 移除或技能缺失即失败，L27-49），不属于"仅 Count>0"。

### tests/progression/schema/run_trait_content_rules_regression.cs — 有效
- 断言数: 约100
- 验证内容: 44 个 trait effect id 枚举映射与往返、5 组策略枚举（stack/source/charge scope/reset timing/roll type）正负向映射、IsSourceKindAllowed 按 TraitDef 声明、roll schema min/max 与 allowed_values 校验。
- 证据/问题: 覆盖表逐条 ToEffectKind/ToStringName 往返（L73-85）；未知值负向贯穿；schema 负向 L256-271。

### tests/progression/schema/run_trait_instance_state_schema_regression.cs — 有效
- 断言数: 约20
- 验证内容: TraitInstanceState 严格 payload 往返与 typed roll 读取器（含缺省回退）、缺/多/错类型字段拒绝、缺 source_id 拒绝、集合来源种类不匹配拒绝、ValidateAgainstDef 精确 roll schema（缺键/越界/多键）、DuplicateState 深拷贝。
- 证据/问题: 6 个 Test 方法均有具体正负向断言（如 L74-95、L185-206、L221-222）。

---

## 统计

- 有效：49
- 偏弱：1
- 无效：0

### 偏弱清单
| 文件 | 问题 |
|---|---|
| tests/progression/schema/run_identity_sub_registry_schema_regression.cs | `TestIdentityCatalogLoadsTypedContent`（L31-52）整段仅 4 条 `Count > 0`；同文件第二方法为强负向校验，故偏弱而非无效 |

### 无效清单
（无）

### 其他点名事项（不改变判定）
- tests/progression/core/run_character_management_trait_attribute_regression.cs L21-26 `TestCharacterManagementDisposeIsIdempotent` 无任何断言，属"不抛异常即通过"的冒烟方法；文件另一方法断言强，整体仍判有效。

## 批次 10 明细

# 测试有效性审计报告 — batch10

范围：tests\progression\fate（9）、tests\progression\identity（6）、tests\progression 根（2）、tests\warehouse（8）、tests\equipment（5）、tests\e2e（7），共 37 个 run_*.cs。每个文件均已打开精读全文。

判定标准：
- 有效：断言针对被测系统的行为/状态/边界/错误路径，具体且可能失败。
- 偏弱：存在弱断言模式，但文件仍有部分真实验证。
- 无效：恒真断言、断言不执行、自产自销对比、空测试方法、catch 吞失败。

---

## tests\progression\fate\（9 个）

### tests/progression/fate/run_faith_service_regression.cs — 有效
- 断言数: 约75
- 验证内容: FaithService 双神（Fortuna/Misfortune）配置骨架、rank 1-5 devotion 结算写 custom stat、上限封顶、非法 reward entry_type 拒绝。
- 证据/问题: 5 个 Test 方法均真实。精确断言 required_gold/level/achievement 占位（:79-108）、逐 rank 结算后 faith_luck_bonus/doom_authority 精确值（:137-141, :294-298）、封顶后 ErrorCode=="max_rank_reached"（:162-163）、非法 entry_type 校验拒绝（:360）。

### tests/progression/fate/run_faith_service_reward_regression.cs — 有效
- 断言数: 约5
- 验证内容: Fortuna rank 1 devotion 生成的 PendingCharacterReward 的 source_type 与 typed reward entry 内容。
- 证据/问题: 精确断言 pending reward source_type==faith rank 常量、entry 指向 faith_luck_bonus +1（:42-50）。

### tests/progression/fate/run_fortuna_guidance_regression.cs — 有效
- 断言数: 约40
- 验证内容: Fortuna guidance 成就链（true→devout→exalted→blessed）逐级门禁 rank 2-5，及 runtime adapter 对 had_permanent_death 正式字段的处理。
- 证据/问题: 每个 rank 先断言被挡且 MissingAchievementId 精确匹配（:79-88 等），再驱动事件解锁后断言放行；adapter 测试分别断言 had_permanent_death=true 不解锁 / false 解锁（:210-228）。

### tests/progression/fate/run_fortune_service_regression.cs — 有效
- 断言数: 约15
- 验证内容: FortuneService 二次确认授予 fortune_marked、失败不授予、周目尝试锁、runtime adapter 从 fate bus payload 授予。
- 证据/问题: 用固定骰源精确断言授予/不授予、写值 1/0、RollSource.CallCount==2 或 0（重复锁定后不消耗骰）（:46, :61, :78）。

### tests/progression/fate/run_low_luck_event_service_regression.cs — 有效
- 断言数: 约22
- 验证内容: 三个低运气事件（断桥生还/灯下无人/死里借来的路）触发条件、固定产出、meta_flags 去重（含 PartyState 序列化 round-trip 后不重复触发）。
- 证据/问题: 精确断言 TriggeredEventIds、loot item_id=="calamity_shard"、drop_source_kind=="low_luck_event"（:41-50）；round-trip 后二次命中断言 0 触发 0 产出（:69-70）。

### tests/progression/fate/run_luck_getter_regression.cs — 有效
- 断言数: 约42
- 验证内容: 幸运 getter 边界计算（软封顶、奇数取整、负 faith）、PartyMemberState 委托与 null 安全、缺 schema 字段的 FromDictionary 拒绝。
- 证据/问题: 6 组参数化用例各自精确断言 5 个派生值（:19-24），如 (-9,0)→effective -6/combat 0/drop -6；矛盾 payload 断言返回 null（:56）。

### tests/progression/fate/run_misfortune_black_omen_regression.cs — 有效
- 断言数: 约33
- 验证内容: MisfortuneBlackOmenService 三类 hook（诅咒遗物/boss 诅咒/亡途灯笼）授予 doom_marked、缺 typed item def 拒绝、条件不全拒绝、已标记不重复授予。
- 证据/问题: 每个 hook 精确断言 Ok/ConditionsMet/Granted 及 doom_marked==1（:44-47 等）；拒绝路径断言 ConditionsMet=false、Granted=false、ErrorCode∈{conditions_not_met,invalid_request}、stat 不变（:346-352）；AlreadyMarked 语义（:222-226）。

### tests/progression/fate/run_misfortune_guidance_regression.cs — 有效
- 断言数: 约28
- 验证内容: Misfortune guidance 成就链门禁 rank 2-5（含打造黑暗装备解锁 exalted），及 String-key-only item_defs 时 forge 不解锁的防御。
- 证据/问题: 逐级 MissingAchievementId 精确断言（:84-89 等）；先断言仅结算 calamity shard 不提前解锁（:160），再断言 forge 后解锁（:166）；string-key 边界断言不解锁（:249-253）。

### tests/progression/fate/run_party_state_fate_regression.cs — 有效
- 断言数: 约20
- 验证内容: PartyState fate_run_flags/meta_flags 序列化 round-trip（含 false 值保留、废弃字段不写入）及坏 schema 严格拒绝。
- 证据/问题: :33-40 为同对象 Set/Get 回读，但随后立即断言序列化 payload 的键与 bool 值（:49-66）并做 FromDictionary round-trip（:68-82），整体验证的是序列化而非回读；拒绝路径覆盖缺字段/错类型/空 key/非 bool/归一化重复 key（:97-143）。

## tests\progression\identity\（6 个）

### tests/progression/identity/run_attribute_source_context_regression.cs — 有效
- 断言数: 约40
- 验证内容: AttributeSourceContext 纯 CLR 边界、六维调整值公式、DerivedAttributeRule 快照与取整、identity/装备/职业/被动技能修正叠加、错误 def key 不补偿、CMM 构建 context。
- 证据/问题: 精确数值断言（如 6 个属性修正值 -1/-1/0/0/1/5，:78-107）；hp_max 96 验证"百分比只放大 persistent HP"（:309-313）；错误 key 的 def 不生效（:374-389）；反射断言字段类型为 IReadOnlyList<AttributeModifierDefinition>（:49-53）。

### tests/progression/identity/run_bloodline_ascension_regression.cs — 偏弱
- 断言数: 约65
- 验证内容: Bloodline/Ascension/StageAdvancement 三个 apply service 与 CMM 委托的应用、拒绝不污染、revoke 回滚、技能授予/撤销、身份摘要投影。
- 证据/问题: 除一个方法外全部强断言（合法/非法 apply、revoke 恢复原 race、stage 推进 adult→old→adult、身份摘要各字段，:46-134, :148-173, :265-305, :332-381）。**问题：TestApplyServicesNoLongerRequireGodotRegistration（:29-37）调用的 AssertPlainService 是空方法（:801-803，方法体无任何断言），该测试方法实际什么都不验证**——等同空测试，需点名。

### tests/progression/identity/run_identity_payload_validator_regression.cs — 偏弱
- 断言数: 约15
- 验证内容: IdentityPayloadValidator 对合法身份放行、对 12 种非法身份（缺 race/subrace、父子不匹配、半设 bloodline/ascension 对、stage 不属于、allowed 门禁）拒绝、body size 缓存不匹配不算身份错误。
- 证据/问题: 夹具构造针对性强且有 TestValidIdentityPasses 正对照（:51-64）。**弱断言模式：全部 12 个拒绝用例只经 AssertHasAnyError 断言 errors.Count>0（:524-527），不断言具体错误内容/错误码**——validator 因错误原因报错也会通过。文件仍有真实验证（每个用例只改一个维度+正对照），故判偏弱而非无效。

### tests/progression/identity/run_party_member_state_owner_api_regression.cs — 有效
- 断言数: 约45
- 验证内容: PartyMemberState 生命周期 API（SetVitals clamp、IsDead 派生、Revive/MarkDead）、HP/is_dead 矛盾 payload 拒绝、identity/age/body size/bloodline/ascension 写接口。
- 证据/问题: 虽多为 Set 后 Get，但断言的是 clamp（-1→0）、派生（HP=0→IsDead）、拒绝（矛盾 payload 返回 null，:73-77, :88-92）、非法 category 不污染（:118-120）等真实逻辑，非纯回读。

### tests/progression/identity/run_protected_custom_stat_regression.cs — 有效
- 断言数: 约18
- 验证内容: hidden_luck_at_birth 受保护写入白名单（非白名单来源拒绝、剧情脚本需显式标记、pending reward 链路拒绝、未保护 stat 仍可写）。
- 证据/问题: 反射断言弱 GDictionary 重载不存在（:25-28）；三类非白名单来源逐一断言 applied=false 且原值不变（:53-67）；pending reward 链路断言 delta.Count==0 且 stat 不变（:154-155）；正对照 storage_space 可写（:170-171）。

### tests/progression/identity/run_trait_content_registry_regression.cs — 有效
- 断言数: 约25
- 验证内容: 官方 trait 内容零校验错误、identity trait 使用泛型 effect/默认 stack policy、非法 fixture 目录产生分类错误。
- 证据/问题: TestOfficialTraitRegistryUsesGenericIdentityDefs 是回读 .tres 常量，但被测对象就是"官方内容经 registry 校验与 typed 投影"，三个具体 trait 的 trigger/charge 策略精确断言（:59-79）属内容契约验证；非法 fixture 断言 12 类具体错误子串（:92-151）。

## tests\progression\ 根目录（2 个）

### tests/progression/run_contingency_charge_transaction_regression.cs — 有效
- 断言数: 约60
- 验证内容: 应急矩阵充能事务全路径：save 不扣材料、战斗守卫、无效内容、覆盖已充能、材料不足、写入失败回滚仓库、成功充能扣材料+clamp MP、clear 不退款、runtime 持久化失败整体回滚、inline payload 拒绝。
- 证据/问题: 11 个 Test 方法全部精确断言 ErrorCode 稳定码与三方状态（warehouse 数量/setup 状态/current_mp）（如 :213-224, :286-299）；ForceSetupWriteFailure 验证补偿回滚（:262-273）；runtime 级 fail_payload_write 验证 session 级回滚（:342-366）。

### tests/progression/run_effective_mp_reservation_regression.cs — 有效
- 断言数: 约25
- 验证内容: MP 预留在属性快照/CMM clamp/日修成长/战斗单位工厂/战斗写回五个环节投影 effective mp_max。
- 证据/问题: 精确断言 30-12=18 链路各点数值（:50-64, :76-119）；战斗单位 mp_max==18 且 current clamp（:163-172）；释放后写回 clamp 到 28（:227-232）。

## tests\warehouse\（8 个）

### tests/warehouse/run_item_price_rules_regression.cs — 有效
- 断言数: 约12
- 验证内容: 价格宽整数计算：schema 上限不溢出、原溢出阈值精确、half-up 取整、负倍率/负价格归零、超 int 饱和。
- 证据/问题: 全部精确数值断言（999999*110%==1099999，:36-40；214748 边界，:67-76；int.MaxValue 饱和不回绕，:120-129）。

### tests/warehouse/run_party_item_use_service_regression.cs — 有效
- 断言数: 约12
- 验证内容: 技能书使用消耗库存并学会技能、重复学习失败且不消耗。
- 证据/问题: 精确断言 Success/Reason=="learn_failed"/ConsumedQuantity/仓库余量/技能已学（:37-54, :78-94）。

### tests/warehouse/run_party_warehouse_batch_swap_regression.cs — 有效
- 断言数: 约25
- 验证内容: batch swap 容量不足与实例 id 分配失败的原子回滚、typed/dictionary 装备实例 entry 克隆隔离、RemoveEquipmentInstance 四类契约。
- 证据/问题: 回滚断言 potion 恢复/herb/gem 为 0/占用格不变（:39-44, :65-76）；ReferenceEquals 断言不共享外部实例且改仓库不影响输入（:113-118）；四种 remove 错误码逐一断言（:172-220）。

### tests/warehouse/run_party_warehouse_quantity_batch_regression.cs — 有效
- 断言数: 约30
- 验证内容: 按数量批量出入库：preview 无副作用、不足拒绝、精确扣减与堆叠顺序、原子性、deposit、快照恢复、非法 entry 拒绝、装备 entry 屏蔽。
- 证据/问题: StackQuantities 精确断言堆叠形态（"2,1"/"3,1"等，:64, :136）；原子失败恢复（:104-107）；稳定错误码逐一断言（:182, :193, :211）。

### tests/warehouse/run_party_warehouse_window_schema_regression.cs — 有效
- 断言数: 约25
- 验证内容: 真实实例化 party_warehouse_window.tscn：正式 payload 渲染、装备仅按实例丢弃、详情纯文本不解析 BBCode、坏 icon 降级、StringName 字段整份拒绝。
- 证据/问题: 真实驱动场景并 EmitSignal 模拟按钮点击，断言信号提交的 item_id/instance_id（:54-60, :89-99）；BBCode 原文保留（:114-119）；三类 StringName 拒绝断言 Visible=false 且列表为空（:145-172）。

### tests/warehouse/run_skill_book_item_helpers_regression.cs — 偏弱
- 断言数: 约14
- 验证内容: SkillBookItemFactory 生成技能书物品（过滤空名/非 book/已存在），SkillBookItemContentValidator 交叉表错误报告。
- 证据/问题: TestSkillBookFactoryGeneratesTypedItemDefs 强断言（生成 id/图标/堆叠/分类/授予技能、三类不生成、不改原索引，:41-68）。**弱断言：TestSkillBookValidatorReportsCrossTableErrors 种植 4 类具体非法 fixture（missing skill/teacher 来源/id 冲突/授予错误），却只断言 errors.Count>=4（:97），不验证任何一类错误被真正检出**——validator 漏报其中 3 类仍可通过。

### tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs — 有效
- 断言数: 约6
- 验证内容: batch swap preview 超容量拒绝且不产生实例序列号分配、仓库写入、特质掷骰等副作用。
- 证据/问题: 精确断言本地 serial 保持 7、装备实例数 0、range/unit 掷骰调用数均 0（:81-88）；catch 仅转 _test.Fail 不吞失败（:90-93）。

### tests/warehouse/run_warehouse_state_item_validator_regression.cs — 有效
- 断言数: 约11
- 验证内容: WarehouseStateItemValidator 合法放行/5 类非法逐一报错/缺失顶层报错，及 StringName id 严格拒绝。
- 证据/问题: 精确断言 errors.Count==5 与 ==1（:68, :75）；String/StringName payload 分别断言接受/拒绝（:80-116）。

## tests\equipment\（5 个）

### tests/equipment/run_equipment_drop_service_regression.cs — 偏弱
- 断言数: 约12
- 验证内容: 3d6+drop_luck 稀有度档位阈值、caller clamp 极值、RollDrops 空表稳定、RollItemInstances typed 输出。
- 证据/问题: 前 4 个方法强断言：5 个稀有度档位门槛逐点验证（:26-55）、±drop_luck 极值（:60-71）、实例稀有度/耐久/特质数（:94-111）。**问题：TestEquipmentDropServiceIsPlainService（:114-116）是空方法，无任何断言，与任务预审一致，点名确认。**

### tests/equipment/run_equipment_rules_regression.cs — 有效
- 断言数: 约15
- 验证内容: 装备槽位表 12 槽顺序稳定且返回副本、NormalizeSlotIds 两个入口过滤非法并保序去重。
- 证据/问题: 首/次/末槽精确断言（:28-42）；篡改返回列表后再取验证内部表不被污染（:44-49）；去重结果精确到顺序（:63-88）。

### tests/equipment/run_equipment_trait_roll_regression.cs — 有效
- 断言数: 约20
- 验证内容: 装备特质铸造：无稳定 instance_id 拒绝、加权选取与 roll value、duplicate/validate 不耗 RNG、仓库 AddItem 后铸造、存入既有实例不重掷。
- 证据/问题: 固定骰源精确断言 trait_instance_id 派生规则 eq_000001_t01、roll 值 5/4（:50-74）；RNG 调用计数断言不重复消耗（:94-95, :187-192）。

### tests/equipment/run_party_equipment_regression.cs — 有效
- 断言数: 约180
- 验证内容: 装备种子数据契约、装备/卸下移动与双手武器占位、属性快照修正、schema 严格拒绝、preview 无副作用、max dex 上限、装备需求（职业/稳定属性快照/排除被顶替与候选装备）、实例 id 全生命周期、ability 持久状态 round-trip 与深拷贝、重复实例 id 拒绝。
- 证据/问题: 26 个 Test 方法逐个人工核查，均为精确行为/状态/错误码断言（如 :368-372 属性差值、:405-437 七类 schema 拒绝、:833-849 被顶替装备不计入需求）。轻微瑕疵：TestTwoItemsOfSameTypeGetDifferentInstanceIds（:955-969）名不符实——只放 1 件 charm 且仅断言项链槽 instance_id 非空，未覆盖"两件同类不同 id"；属个别弱方法，不影响整体判定。

### tests/equipment/run_party_equipment_service_regression.cs — 有效
- 断言数: 约15
- 验证内容: 双手武器 preview typed entry 且无副作用、BattleResolutionResult 接受 formal 随机装备/装备实例 loot 形状（含 drop_luck 规范到 5）、mismatched equipment_instance 被 payload 边界拒绝。
- 证据/问题: preview 后装备状态与仓库不变（:62-77）；drop_luck 8→5 规范化精确断言（:117）；mismatch 断言 typedEntry==null 且仓库不变（:196-201）。

## tests\e2e\（7 个）

> 说明：以下 7 个 e2e 测试均真实驱动场景（加载 project.godot 主场景、经 UI 创建游戏、键盘移动、按钮点击、跨进程存档读档），断言针对运行时状态与 UI 可见性，非自产自销。**注意：这批 e2e 不进 CI**（按任务预审及仓库惯例，未见于常规回归套件），其失败不会被日常 CI 捕获。

### tests/e2e/run_cold_boot_e2e.cs — 有效（不进 CI）
- 断言数: 约18
- 验证内容: 冷启动到登录界面：canonical autoload/coordinator 存在、内容快照有效、登录四按钮可用、无残留 modal、start_scene_path 存在。
- 证据/问题: 加载真实主场景断言 SceneTree.CurrentScene 与 SceneFilePath==project.godot 主场景设置（:31-36）、按钮可见可用与焦点（:47-72）。

### tests/e2e/run_new_game_e2e.cs — 有效（不进 CI）
- 断言数: 约10
- 验证内容: 经真实 UI（TestButton+角色创建 LineEdit）创建新游戏：active world/save id/存档文件、角色名持久化、世界地图可见、坐标一致、非战斗态。
- 证据/问题: 断言 display_name=="E2E Hero"（经真实输入框）（:42-46）、存档文件存在于隔离 user data（:32-35）。

### tests/e2e/run_load_game_e2e.cs — 有效（不进 CI）
- 断言数: 约7
- 验证内容: 冷进程经真实存档列表 UI 读档：恰好 1 个槽、选中槽加载、恢复上一进程创建的角色名、runtime/session 坐标一致。
- 证据/问题: 跨进程验证（依赖 new_game 进程的存档），断言 GetActiveSaveId==UI 选中槽（:71-75）、display_name 恢复（:76-80）。

### tests/e2e/run_enter_battle_e2e.cs — 有效（不进 CI）
- 断言数: 约12
- 验证内容: 键盘沿 BFS 规划路线走到野外遭遇格、战斗确认 UI 出现、timeline 冻结、点击确认后战斗可交互、世界 UI 隐藏、存档锁持有。
- 证据/问题: 逐步断言每格移动生效且不提前进战（:48-57）；确认后断言 timeline 解冻、确认窗关闭、世界视图隐藏（:116-126）；真实 Input.TapKeyAsync/ClickAsync 驱动。

### tests/e2e/run_battle_round_trip_e2e.cs — 有效（不进 CI）
- 断言数: 约20
- 验证内容: 完整战斗往返：经真实 UI 发技能/移动/等待指令（含 promotion/reward modal 处理），战斗以玩家胜利结束，世界分辨率按 encounter 声明应用（Clear/Suppress/Preserve），返回世界后存档锁释放、战斗状态清空、坐标保持。
- 证据/问题: 强制至少 1 次真实技能指令（:201-204）；FinalDecision.Outcome==PlayerSuccess（:213-217）；按 WorldResolution 模式精确断言 anchor 清除/抑制步数/保留（:186-221）；超时/超步/game_over 直接抛异常失败（:94-105, :173-178）。

### tests/e2e/run_world_save_mutation_e2e.cs — 有效（不进 CI）
- 断言数: 约14
- 验证内容: 新档经一次真实键盘移动改变世界状态：坐标变化、world step 恰好 +1、session 暂存 pending save、不进战斗/不开 modal。
- 证据/问题: 断言 initial==PlayerStartCoord 且 step==0（:36-45），移动后 runtime/worldData/session 三处坐标与 step 精确断言（:70-93）、HasPendingSave（:94-97）。

### tests/e2e/run_world_save_reload_e2e.cs — 有效（不进 CI）
- 断言数: 约15
- 验证内容: 冷进程读档恢复 mutation 进程的世界变更：坐标==移动目标、world step==1、角色名恢复、存档锁未携带。
- 证据/问题: 跨进程 round-trip，断言恢复坐标!=起始坐标且==确定性重算的安全目标格（:119-132）、world step 恢复（:133-142）。

---

## 统计

- 有效: 33
- 偏弱: 4
- 无效: 0

### 偏弱清单
| 文件 | 问题 |
|---|---|
| tests/progression/identity/run_bloodline_ascension_regression.cs | AssertPlainService 为空方法（:801-803），TestApplyServicesNoLongerRequireGodotRegistration（:29-37）实际零断言 |
| tests/progression/identity/run_identity_payload_validator_regression.cs | 12 个拒绝用例仅断言 errors.Count>0（:524-527），不校验错误内容 |
| tests/warehouse/run_skill_book_item_helpers_regression.cs | TestSkillBookValidatorReportsCrossTableErrors 仅断言 errors.Count>=4（:97），4 类种植错误是否各自检出未验证 |
| tests/equipment/run_equipment_drop_service_regression.cs | TestEquipmentDropServiceIsPlainService 为空方法（:114-116），零断言 |

### 无效清单
无。

### 备注
- tests/e2e/ 全部 7 个文件真实驱动场景做验证（UI 输入、跨进程存档 round-trip），判定均为有效，但均不进 CI，回归保障依赖手动/专门触发。
- tests/equipment/run_party_equipment_regression.cs 内 TestTwoItemsOfSameTypeGetDifferentInstanceIds（:955-969）测试名与内容不符（只放一件物品、仅断言非空），属个别弱方法，文件整体仍判有效。
