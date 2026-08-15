# 组合战斗目标提案

> 状态：`Proposal / Deferred; not scheduled`
> 更新日期：`2026-08-15`

## 当前决定

组合目标暂不实现、暂不排期，也不得加入正式 encounter seed。当前正式 encounter 继续只选择一种主要 objective；任务文本、奖励条件或敌方编队不能把第二个未被运行时求值的条件伪装成正式胜负目标。

九种已落地模式的当前实现真相见 [`../../design/battle/objective_runtime.md`](../../design/battle/objective_runtime.md)。单个模式自身的未来扩展见 [`objective_mode_extensions.md`](./objective_mode_extensions.md)。

## 保留的概念方向

未来若恢复设计，组合目标预计需要支持：

- `All`：所有 required 子目标完成后成功。
- `Any`：任一 required 子目标完成后成功。
- `Ordered`：按显式顺序激活并完成 required 子目标。
- `required`：参与最终胜负判断的子目标。
- `optional`：不直接改变主要胜负条件的可选目标。

无论包含多少子目标，一场战斗仍只能锁存一个 `BattleFinalDecision`。本文不预先决定具体 Resource 字段、失败合并规则或 optional 奖励语义。

## 恢复设计前必须关闭的问题

1. 组合器和子目标的 authoring schema、稳定 id 与交叉引用规则。
2. 多个子目标在同一原子 mutation 中同时成功、失败或 Draw 时的唯一优先级。
3. `Ordered` 子目标的激活、不可交互、跳过与取消语义。
4. optional 目标是否影响奖励、评价、任务推进或世界处理，以及由哪个系统拥有这些事实。
5. HUD、棋盘 marker、文本快照和日志如何展示活动、完成、失败与未激活子目标。
6. AI 如何在多个合法目标间选择，并保证 preview、寻路、评分和正式命令一致。
7. pending cast、同步反应、召唤物、scenario actor 与战斗结束冻结期间的子目标生命周期。
8. 正式内容 validator、正向/非法/原子冲突回归，以及跨模式组合测试矩阵。

## 禁止的临时替代

- 不用 objective id、encounter id 或任务文本硬编码组合逻辑。
- 不用多个互相竞争的 final decision owner。
- 不把 optional 目标失败隐式转换成主目标失败。
- 不为未确定的 schema 增加兼容 alias、fallback 或旧 payload 迁移。
