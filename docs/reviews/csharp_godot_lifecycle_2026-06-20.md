# C# / Godot Object Lifecycle Audit 闭环记录

原始检视日期：`2026-06-20`  
当前代码复核：`2026-08-14`
复核基线：`codex/tactical-skills-world-quest`，`6545d837189c3e233a3f1eb1f23ff457e14824dc`

当前 lifecycle 架构真相以 `docs/design/platform/godotsharp_lifecycle.md` 和 `docs/design/project_context_units.md` 为准。本文保留 2026-06-20 原始检视项及后续闭环证据；截至 2026-08-14，本审计没有仍处于活跃状态的问题。

2026-06-20 报告中的 CharacterCreationWindow registry ownership、EquipmentTraitRollService / EquipmentDropService Godot RNG ownership、BattleTerrainGenerator RNG cleanup、WorldMapSpawnSystem Godot object field、通用 `DisposeOwned<T GodotObject>` 以及 BattleSim fixture 外部 party ownership 风险均已由当前实现消除，因此已从活跃问题清单移除。

2026-08-14 复核另外确认以下问题已闭环或已由当前 owner 取代：

- `BattleSimExecutionLoop` 通过 `AiTraceRecorder.PushInstance(...)` 的 scoped owner 恢复外层 recorder；execution step 抛异常也不会把本场 recorder 留给后续 run。
- `BattleSimRunner` 在 `StartBattle`、terrain generation 或 execution loop 抛异常时释放单场 `BattleRuntimeModule`；清理也失败时保留原始异常并组合报告 cleanup failure。
- `tests/battle_runtime/runtime/run_battle_sim_exception_cleanup_regression.cs` 会故意触发 execution-loop 与 terrain-generation 异常，并分别断言 recorder 恢复、runtime disposed、battle state/AI borrower/sidecar binding 释放。2026-08-14 当前文件系统聚焦运行通过。
- 旧的 `tests/runtime/validation/run_quest_config_validation.cs` 已移除；正式 Quest validation 已并入 `run_resource_validation_regression.cs` 与 typed validator regression，前者使用显式 `using TestContentResourceLoader` / `using ProgressionContentRegistry` ownership，后者借用 process `ContentSnapshot`。

## 低优先级项闭环：测试 content registry / loader 确定 cleanup

2026-08-14 已为以下 progression schema fixture 补齐显式 loader/registry scope：

- `tests/progression/schema/run_skill_tags_typed_regression.cs`
- `tests/progression/schema/run_skill_attribute_modifiers_typed_regression.cs`
- `tests/progression/schema/run_skill_attribute_growth_typed_regression.cs`
- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`

每个 fixture 都先声明 `TestContentResourceLoader`、再声明对应 registry；C# 逆序释放保证 registry 先清除引用，loader 后释放 path-backed Resource borrow anchor。equipment-ability runner 的各 case 继续拥有独立 registry，没有为了减少 fixture 创建而共享可变 registry。此次修复没有改变 production registry API、runtime ownership 或 shutdown pipeline，也没有新增局部 GC / `Quit()` 逻辑。

## 2026-08-14 聚焦复核证据

- `python tools/magic_dev.py test --path tests/battle_runtime/runtime/run_battle_sim_exception_cleanup_regression.cs`
  - `PASS 1/1`
  - build `0` warnings / `0` errors
  - lifecycle shutdown report：`barrier_skipped=False`、`failures=0`、`legacy_debt=0`
- 以下四个 progression schema runner 逐个使用 `python tools/magic_dev.py test --path <path>` 运行：
  - `run_skill_tags_typed_regression.cs`
  - `run_skill_attribute_modifiers_typed_regression.cs`
  - `run_skill_attribute_growth_typed_regression.cs`
  - `run_equipment_ability_content_registry_regression.cs`
  - 结果：`PASS 4/4`；首次 build `0` warnings / `0` errors；四个 shutdown report 均为 `barrier_skipped=False`、`failures=0`、`legacy_debt=0`。
- 本次没有运行 routine full suite、battle simulation 数值入口、E2E 或远端 CI，不能把本节解释为全仓当前 PASS。

## 当前状态

- 本审计列出的 runtime、BattleSim fixture、异常路径与 test fixture ownership 问题均已闭环。
- 当前没有尚待实现的 active claim。
- 后续如改变 registry/loader ownership、把独立 case 合并为共享 fixture 或同进程 soak，需要重新验证逐轮 owner/activity 向量不增长，并按当前代码重新建立验证证据。

## Project Context Units Impact

本次 test fixture cleanup 没有改变 runtime ownership 边界或推荐 read set，不需要修改 `docs/design/project_context_units.md`。
