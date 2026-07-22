# C# / Godot Object Lifecycle Audit 剩余问题

原始检视日期：`2026-06-20`  
当前代码复核：`2026-07-20`

当前 lifecycle 架构真相以 `docs/design/platform/godotsharp_lifecycle.md` 和 `docs/design/project_context_units.md` 为准。本文只保留对当前 C# owner 重新核验后尚未闭环的检视项。

2026-06-20 报告中的 CharacterCreationWindow registry ownership、EquipmentTraitRollService / EquipmentDropService Godot RNG ownership、BattleTerrainGenerator RNG cleanup、WorldMapSpawnSystem Godot object field、通用 `DisposeOwned<T GodotObject>` 以及 BattleSim fixture 外部 party ownership 风险均已由当前实现消除，因此已从活跃问题清单移除。

## 中优先级：BattleSim 异常路径没有可靠恢复全局 owner

- `BattleSimExecutionLoop.cs:75-100` 会替换全局 `AiTraceRecorder`，但没有用 `finally` 恢复；执行中抛异常会把本场 recorder 留给后续 run。
- `BattleSimRunner.cs:231-287` 创建单场 `BattleRuntimeModule` 后只在成功尾部 dispose；`StartBattle` 或 execution loop 异常时会跳过 cleanup。

这不是旧 fixture 外部 party ownership 的残留，而是迁移后当前 simulation orchestration 的异常安全缺口。应在同一个 try/finally owner 中恢复 recorder 并释放 runtime，并用“故意抛异常后再跑第二场”的回归证明没有跨 run 污染。

## 低优先级：部分测试 content registry / loader 没有确定 cleanup

当前仍能看到测试直接创建 content registry 或 `TestContentResourceLoader`，但没有 `using`、`try/finally` 或显式 `Dispose()`。独立子进程结束时通常会回收，但在同进程 soak 或 early-return 路径中会积累 wrapper / loader owner，与当前测试 lifecycle 契约不一致。

已确认的实例包括：

- `tests/progression/schema/run_skill_tags_typed_regression.cs`
- `tests/progression/schema/run_skill_attribute_modifiers_typed_regression.cs`
- `tests/progression/schema/run_skill_attribute_growth_typed_regression.cs`
- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/runtime/validation/run_quest_config_validation.cs`

处理原则：

- 优先复用 `TestContentResourceLoader` / `TestResourceOwnership` 的现有 ownership 边界。
- 如果 registry 投影后仍返回 borrowed Resource，必须在 dispose loader 前证明测试只保留 detached definition / snapshot，不能机械地提前关闭 owner。
- 补充或调整回归时，使用现有 `LifecycleTestSceneTree` shutdown pipeline，不在 runner 里新增局部 GC / `Quit()` 逻辑。

## 验证

- `dotnet build magic.csproj`
- 最窄的相关 schema runner
- 如果改变 test resource owner，再跑 lifecycle correctness profile

## Project Context Units Impact

本次只清理过时检视结论，没有改变 runtime ownership 边界，不需要修改 `docs/design/project_context_units.md`。
