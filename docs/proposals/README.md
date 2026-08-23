# 待落地方案

本目录保存尚未完整落地、仍在评审、只实现了部分阶段，或同时混有当前事实与未来设计的文档。

默认规则：这里的类型名、文件名、接口和阶段安排都不是当前运行时合同。实施前先从 [`../design/project_context_units.md`](../design/project_context_units.md) 定位真实 owner，再检查当前源码和测试。

## 战斗

- [`battle/objective_mode_extensions.md`](battle/objective_mode_extensions.md)：九种已落地战斗目标模式的未实现扩展；当前暂缓、未排期。
- [`battle/composite_objectives.md`](battle/composite_objectives.md)：`All / Any / Ordered` 与 required/optional 组合目标；当前明确暂不实现、未排期。
- [`battle/counterattack_system.md`](battle/counterattack_system.md)：反击系统运行时架构；以稳定逻辑攻击 ID、单一 root batch、不可变攻击事实、显式反应边界、FIFO 排空、递归熔断与完整即时武器攻击 service 闭合 `lock_counterattack` 消费。batch、AutoCast、outcome、静滞与 snapshot 架构缺口已闭合，代码尚未实现；具体技能、内容来源、属性派生、平衡与 AI 另案处理。

## 跨系统迁移与架构整治

- [`migrations/gd_cleanup.md`](migrations/gd_cleanup.md)：内部 Godot 类型传播的清理提案。Phase A-G 已于 2026-08-15 全部落地：四条窗口输入链改为 detached typed DTO 直达 UI、`ShopWindow` 以 typed C# event 提交、低幸运据点奖励 port 改 typed input/result、`GodotVariantReadExtensions` 收敛到唯一真实边界方法、`scripts/` 下 `dynamic` 归零、`[GlobalClass]` 逐类取证后删除 3 个纯 C# UI 类。
- [`migrations/runtime_hub_decoupling.md`](migrations/runtime_hub_decoupling.md)：两个运行时 hub 的封装收敛与解耦，分阶段。`GameRuntimeFacade` 阶段已完成（internal 字段 52→23）；`BattleRuntimeModule` 按消费者解耦进行中（1/8，已完成 `BattleTimelineDriver`），配 `battle_runtime_isolated` 层 + analyzer 禁则防回潮。开工前先读文档开头三条度量纠正（namespace 无访问控制、internal 声明数≠字段数、按字段名 grep 会误报）。

功能落地后：

1. 在 `docs/design/<system>/` 新建或更新只描述当前实现的文档。
2. 更新 `project_context_units.md` 的“细节文档”链接；只有架构边界改变时才修改 CU 职责与主链。
3. 仍含未来阶段的方案继续留在这里；已完全失去执行价值的方案移入 `docs/archive/`。
