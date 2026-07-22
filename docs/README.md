# 文档目录

更新日期：`2026-07-17`

## 从这里开始

- 当前架构装载索引：[`design/project_context_units.md`](design/project_context_units.md)
- 当前架构与实现文档目录：[`design/README.md`](design/README.md)
- 尚未完整落地的方案：[`proposals/README.md`](proposals/README.md)

## 目录契约

| 目录 | 放什么 | 是否代表当前实现 |
|---|---|---|
| `docs/design/` | 已经落地的架构边界、模块规格和实现说明 | 是 |
| `docs/proposals/` | 待实现、部分实现、仍在选型或混有未来阶段的方案 | 否 |
| `docs/content/` | 剧情、任务、物品、武器、书籍等内容策划与制作资料 | 否 |
| `docs/discussions/` | 探索性讨论、PRD、外部评审输入与尚未收敛的问题 | 否 |
| `docs/reviews/` | 某一时间点的代码审查、目录审计与问题报告 | 否 |
| `docs/reference/` | 外部规则、素材要求和非运行时真相源参考 | 否 |
| `docs/operations/` | 开发流程、迁移工作方式和维护说明 | 否 |
| `docs/archive/` | 已被替代、仅供追溯的旧设计、决策和已落地实施计划 | 否 |

## 维护规则

- `docs/design/` 中的文档必须能由当前源码、数据和测试验证，且明确标记为 `Current / Implemented` 或同义状态。
- 一份文档只要仍包含待落地阶段、未来 owner、未决选项或部分实现，就留在 `docs/proposals/`，不能作为当前架构真相源。
- 功能落地后，应更新或新建对应的当前实现文档；原方案若仍含未来内容则继续留在 `docs/proposals/`，若只剩历史价值则移入 `docs/archive/`。
- `project_context_units.md` 只维护所有权、依赖和推荐读集；字段级规则、方法链和聚焦回归入口写在同系统的当前实现文档中。
- 内容策划不因已经制作了部分 `.tres` 就进入 `docs/design/`；只有跨内容复用的正式运行时架构与实现合同属于 `docs/design/`。
