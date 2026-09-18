# 2026-09-17 当前工作区全量回归与失败分析

## 修复后结果

用户授权修复后，8 项失败已修复。当前工作区验证结果：

- `dotnet build magic.csproj`：通过，**0 警告、0 错误**。
- 原 8 个失败入口定向复测：**8/8 通过**。
- 完整 Godot 常规回归：**547/547 通过，0 失败**；开启 `--fail-on-output-error --lifecycle-correctness`，主进程 exit=0。
- Python 工具测试：**68/68 通过**；架构分析器测试：**9/9 通过**。
- 已记录的 2184 个源码、配置、场景和测试输入在本轮全量执行期间哈希未变；本次修改文件的 `git diff --check` 通过。

修改限于 8 个测试文件和 exporter 生成的 `data/schemas/content/skills.schema.json`。生产运行时代码与技能数值未修改，运行时关系和所有权边界未变化，因此无需更新项目上下文索引。以下首轮诊断保留为历史证据；其中行号对应修复前工作区。

### 已落地修复

1. **移除文本外形基准**：删除 AI trace runner 中全部 6 组 JSON 长度/SHA256 基准，保留字段、引用投影一致性、所有权与释放检查。为两项新增战术分数设置非零 fixture 值，并验证完整 trace、独立 score、报告嵌套 trace 的字段值。
2. **修复 schema 与导入契约**：使用正式 exporter 重生成 skills schema，包含 `advance_status_ticks`、`incoming_attack_roll_disadvantage` 等当前定义。移除效果种类和 DTO 属性个数的硬编码，改为比对注册表与实际 schema 分支、字段映射；补齐新效果合法 payload、三个必填字段分别缺失的拒绝用例，以及诊断 Resource 到嵌套 payload 的值保留验证。
3. **补齐静态登记**：将已有的 `mage_molten_burst` 自动施法测试加入反应契约 manifest。
4. **修复装备数量假设**：卸装断言改为背包数量相对操作前增加 1，并验证返回的正是此前装备的实例且只出现一次。
5. **补齐测试依赖与异常退出**：三个手工 fixture 注入正式内容构造的 `EncounterChallengeCatalog`；世界地图上下文 runner 在异常时记录失败，并保证清理与结果提交、退出流程执行。原 180 秒超时入口定向复测约 19 秒正常通过。

### 修复后命令与证据

```text
dotnet build magic.csproj
godot --headless -s res://scripts/tools/run_content_json_schema_export.cs -- --write --domain=skills
python .tmp/full-regression-20260917/verify_fixes.py
python tests/run_regression_suite.py --jobs 16 --fail-on-output-error --lifecycle-correctness --log-file .tmp/full-regression-fixed-20260917/regression.log
python -m unittest discover -s tests/tooling -p 'test_*.py' -v
dotnet run --project tools/architecture/Magic.ArchitectureAnalyzers.Tests/Magic.ArchitectureAnalyzers.Tests.csproj
```

- 全量日志：`.tmp/full-regression-fixed-20260917/regression.log`，末尾 `Passed: 547 / Failed: 0`。
- 定向复测：`.tmp/full-regression-20260917/focused-fix.log`、`focused-fix-results.json`。
- Python、架构与输入快照：`.tmp/full-regression-fixed-20260917/tooling.log`、`architecture.log`、`input-hashes.json`。
- 本次是当前工作区的常规回归证据；按仓库约定不含数值战斗模拟、benchmark/analysis、application E2E、导出 smoke、覆盖率或远程 CI。547 是 runner 数，不是断言数。

## 首轮结果与范围（修复前）

- 工作区：`E:/game/magic`，HEAD `49c80870c9a8d8e0f415544b25ed00be2ebb29f8`，包含原有未提交及未跟踪文件。
- Godot：`4.6.2.stable.mono.official.71f334935`，Windows 本地运行。
- `dotnet build magic.csproj`：通过，0 警告、0 错误。
- Godot 常规回归：**539/547 通过，8 项失败**，其中 7 项 exit=1，1 项 180 秒超时、exit=124。
- Python 工具测试：**68/68 通过**。
- 架构分析器测试：**9/9 通过**。
- **8 项失败均已逐项复现**：7 项再次 exit=1，最后一项独立复跑仍抛出相同异常，并在 180.11 秒后 exit=124。

首轮诊断没有修改生产代码、技能内容、schema 或正式测试。普通全量入口按仓库约定排除数值战斗模拟、benchmark/analysis 和 application E2E；未运行导出 smoke、覆盖率采集或远程 CI。547 是 runner 数，不是断言数；首轮异常提前终止的 runner 后续用例当时尚未得到验证，现已由修复后完整复跑覆盖。

## 执行命令

```text
dotnet build magic.csproj
python tests/run_regression_suite.py --jobs 8 --fail-on-output-error --lifecycle-correctness --log-file .tmp/full-regression-20260917/regression.log
python -m unittest discover -s tests/tooling -p 'test_*.py' -v
dotnet run --project tools/architecture/Magic.ArchitectureAnalyzers.Tests/Magic.ArchitectureAnalyzers.Tests.csproj
```

失败复跑使用本次 `.tmp/full-regression-20260917/inspect_suite.py`，调用正式 runner 的 `run_one_test`，保留相同严格检查与 180 秒超时，为每项创建独立用户数据目录。前 7 项按顺序复跑；前几项复跑开始时，首轮最后一项异常挂起的进程仍在等待超时。该进程退出后，最后一项另行独立复跑。

## 失败清单

| 入口（文件名省略 `.cs`） | 首轮结果 | 原因分类 |
| --- | --- | --- |
| `run_battle_ai_trace_projection_lease_regression` | exit=1，6 条失败断言 | AI trace 新字段未同步长度和哈希基准 |
| `run_content_json_schema_export_regression` | exit=1，3 条失败断言 | 新效果/字段未同步受版本控制的 skills schema，种类数量仍写死 28 |
| `run_full_combat_effect_import_contract_regression` | exit=1，6 条失败断言 | 旧数量、缺失新 payload fixture、旧平铺字段对应假设 |
| `run_battle_reaction_contract_static_regression` | exit=1，1 条失败断言 | 新自动施法测试缺少 manifest 登记 |
| `run_battle_equipment_text_command_regression` | exit=1，1 条失败断言 | 开局自带护甲后，测试仍假设总共只有一件皮甲 |
| `run_game_runtime_settlement_command_handler_regression` | exit=1，异常中断 | 手工 fixture 未注入 `EncounterChallengeCatalog` |
| `run_settlement_persist_failure_rollback_regression` | exit=1，异常中断 | 同上 |
| `run_world_map_data_context_regression` | exit=124，180.12 秒 | 同上，并且异常绕过结果提交和退出流程 |

## 具体原因与处理方向

### 1. AI trace：三个旧基准均相差 89 个字符

`scripts/systems/battle/ai/BattleAiScoreInput.cs:58` 新增 `estimated_incoming_attack_damage_relief`，`:59` 新增 `estimated_vulnerability_follow_up_damage`；`:312`、`:313` 将两者写入 trace。fixture 中值均为 0，序列化增量分别为 44、45 个字符，合计 89。

`tests/battle_runtime/ai/run_battle_ai_trace_projection_lease_regression.cs:87`、`:707`、`:993` 的基准未更新：

| 输出 | 旧长度 | 实际长度 |
| --- | ---: | ---: |
| 完整 AI trace | 13090 | 13179 |
| 独立 AI score | 10458 | 10547 |
| simulation report 投影 | 17564 | 17653 |

三个 SHA256 断言也随之变化。该 runner 的其他投影、资源所有权、释放回基线断言没有失败；shutdown report 为 `failures=0 legacy_debt=0`。正式 `mage_blur` 和 `mage_cold_snap` 专项回归通过，包含对应新增分数的行为断言。

修复决策：按用户要求移除长度与哈希基准，显式断言正式输出字段和值；保留新字段和生命周期检查。

### 2. skills schema 与效果导入契约尚未同步

`SkillFullCombatEffectClosedSpec.cs:32` 注册了第 29 种效果 `advance_status_ticks`；`SkillCombatEffectJsonDtos.cs:351` 的 payload 强制要求 `max_ticks`、`max_sources`、`required_source_tag`。当前 `data/schemas/content/skills.schema.json` 仍只有 28 个效果分支，且缺少 `advance_status_ticks` 和 `incoming_attack_roll_disadvantage`。这会令依赖该 schema 的作者工具拒绝当前有效的新内容。

导入契约测试还存在三组旧假设：

1. `run_full_combat_effect_import_contract_regression.cs:40` 和 schema 测试 `:564` 写死 28。
2. 导入测试 `:378` 的 `PayloadFor` 没有新效果分支，默认返回 `{}`；因此触发 `skill.dto.effect_payload.invalid@/entries/5/combat_profile/effect_defs/0/payload`，不是正式内容的有效 payload 无法解析。
3. `:304` 假设 `CombatEffectDef` 每个 Export 都与顶层 DTO/model 一对一。新增的 `status_tick_limit/status_source_limit/status_source_tag` 实际由 `SkillDiagnosticFixtureProjection.Effects.cs:70` 转成嵌套 typed payload，不能按顶层字段名直接比较。此外 `incoming_attack_roll_disadvantage` 令顶层 DTO/model 从 194 增至 195，`:184`、`:185` 仍断言 194。

正式 `mage_molten_burst` 的有效 payload 位于 `data/configs/json/skills/mage_04.json:1227` 起，三个必填字段齐全，其专项回归通过。

处理方向：用已有 exporter 重生成 skills schema；补齐合法 payload fixture 与必填项拒绝用例；让字段契约显式检查顶层和嵌套映射，保留“没有丢字段”的验证目标。仅替换数字仍无法解决空 payload 和错误映射假设。

### 3. 自动施法 manifest 漏登记

`tests/battle_runtime/skills/run_mage_molten_burst_regression.cs:322`、`:327` 通过 `BattleReactionRootTestHelper.ExecuteInReactionRoot` 和 `BattleEffectOrigin.AutoCast(request)` 正确包裹了 `ExecuteAutoCast`。

`tests/static_analysis/run_battle_reaction_contract_static_regression.cs:37` 的 `ExecuteAutoCast` 清单没有包含该新 runner。失败仅是登记遗漏，未报告缺少反应根或错误 origin。处理方向是补清单登记。

### 4. 皮甲数量断言受新开局装备影响

`data/configs/json/gameplay_configuration/core.json:225` 将开局身体护甲设为 `leather_jerkin`；`GameSession.CharacterCreation.cs:410` 为新角色装备它。文本测试 `:35` 又向仓库加入一件同款皮甲，随后 `:149` 换装、`:157` 卸装。

`BattleChangeEquipmentResolver.cs:741` 把被替换的初始皮甲放回战斗背包，`:783` 再把卸下的第二件皮甲放回。总数为 2 符合这条实例流转链；测试 `run_battle_equipment_text_command_regression.cs:767` 仍断言 1。其他换装、AP、身体槽清空、HP 和战后写回断言没有失败。

处理方向：按操作前数量增量和装备实例 ID 验证卸装与所有权守恒，避免依赖新角色初始身体槽为空。

### 5. 三个世界地图/据点 fixture 漏注入新依赖

正式 `GameRuntimeFacade.cs:290` 会配置 `EncounterChallengeCatalog`。三个测试直接构造 `GameRuntimeFacade` 或 `WorldMapDataContext`，绕过该初始化：

- 据点命令测试 `run_game_runtime_settlement_command_handler_regression.cs:1280`。
- 持久化回滚测试 `run_settlement_persist_failure_rollback_regression.cs:828`。
- 世界上下文测试 `run_world_map_data_context_regression.cs:649`。

前两者在 `rest_full → AdvanceWorldTimeBySteps → GetStartingAreaRules` 时失败；第三者在 `EnsureSubmapGenerated → BuildWorldTyped` 时失败。`WorldStartingAreaRules.cs:20` 在起始安全区启用且目录为 null 时主动抛出 `ArgumentNullException("challenges")`。

第三个 runner 的 `Run()`（`:17`）没有异常兜底，异常发生后没有执行末尾的 `DisposePayloadLeases` 和 `RequestTestExit`，Godot 记录异常但进程继续运行，外层 180 秒后将其终止。因此该超时有明确异常原因，不是性能变慢。

处理方向：fixture 使用正式内容目录构造并注入所需依赖；世界上下文 runner 需要保证异常记为失败且清理、结果提交和退出始终执行。不要通过给生产目录添加 null fallback 来掩盖缺失配置。生产初始化已配置该依赖，目前证据将这三项定位为测试搭建遗漏，不能据此宣称正式游戏休息/子地图路径必然崩溃。

## 证据与验证边界

- 全量日志：`.tmp/full-regression-20260917/regression.log`。
- 提取的失败输出：`.tmp/full-regression-20260917/failures.log`。
- 前 7 项复跑：`retry.log`、`retry-results.json`；最后一项：`retry-world-map-context.log`、`retry-world-map-context-results.json`（同目录）。
- Python/架构日志：`tooling.log`、`architecture.log`（同目录）。
- 初始工作区清单与执行期间输入快照：`worktree-status.log`、`input-hashes.json`（同目录）。2184 个已记录源码、配置、场景和测试文件在后续执行期间哈希未变。
- 全量日志未检出 GodotSharp fatal/finalizer marker、ObjectDB 泄漏或 shutdown report 非零 legacy debt。超时 runner 没有正常完成退出，不能把本次结果写为生命周期全通过。
- AI mutation guard、缺失护甲值等负向用例会主动输出结构化 ERROR；需要结合断言和退出结果判断，不能把每一条 ERROR 文本都算作新的失败项。

以上问题现已完成修复：先跑原 8 个失败入口，再重新执行完整常规回归，均通过。首轮被异常跳过的后续用例已执行，未出现新的失败；详见本文开头的修复后结果。
