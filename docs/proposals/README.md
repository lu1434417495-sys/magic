# 待落地方案

本目录保存尚未完整落地、仍在评审、只实现了部分阶段，或同时混有当前事实与未来设计的文档。

默认规则：这里的类型名、文件名、接口和阶段安排都不是当前运行时合同。实施前先从 [`../design/project_context_units.md`](../design/project_context_units.md) 定位真实 owner，再检查当前源码和测试。

## 战斗

- [`battle/multi_objective_modes.md`](battle/multi_objective_modes.md)：九种战斗模式定义与 P1-P4 落地顺序；当前只有 P0 歼灭模式进入实现真相。
- [`battle/counterattack_system.md`](battle/counterattack_system.md)：反击系统设计；状态驱动姿态 + 敏捷派生反应预算 + 入队/排空编排，闭合当前悬空的 `lock_counterattack`。设计决策已全部关闭，P1 可实施；代码全部未实现。
- [`battle/action_cadence_agility_derivation.md`](battle/action_cadence_agility_derivation.md)：行动节奏由敏捷派生到 `action_progress_rate_percent`（15%/点，全区间 2.94 倍）。改动时间线系统，影响全部 TU 计价内容与现存敌人强度；两条待决未关闭。

## 跨系统迁移与架构整治

- [`migrations/gd_cleanup.md`](migrations/gd_cleanup.md)：内部 Godot 类型传播的清理提案。

功能落地后：

1. 在 `docs/design/<system>/` 新建或更新只描述当前实现的文档。
2. 更新 `project_context_units.md` 的“细节文档”链接；只有架构边界改变时才修改 CU 职责与主链。
3. 仍含未来阶段的方案继续留在这里；已完全失去执行价值的方案移入 `docs/archive/`。
