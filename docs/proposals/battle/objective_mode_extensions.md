# 战斗目标模式扩展提案

> 状态：`Proposal / Deferred; core objective modes implemented (9/9)`
> 更新日期：`2026-08-15`

## 文档边界

九种核心模式 `elimination`、`boss`、`rescue`、`escape`、`escort`、`defense`、`intercept`、`node_operation` 与 `control` 已经落地。当前 Resource schema、immutable definition、运行时求值、原子终局、HUD/快照、AI、正式内容和回归的实现真相见 [`../../design/battle/objective_runtime.md`](../../design/battle/objective_runtime.md)；玩家体验与内容制作规范见 [`../../content/battle/objective_modes.md`](../../content/battle/objective_modes.md)。

本文只记录单个既有模式尚未实现的扩展，不重述当前运行时合同，也不代表这些扩展已经排期。跨多个目标的 `All / Any / Ordered` 组合规则单独见 [`composite_objectives.md`](./composite_objectives.md)。

## 未实现的模式扩展

### `elimination`

当前没有单独排期的扩展。若以后需要限定特定敌对阵营、目标批次或其他歼灭子规则，必须显式增加 typed schema 与终局理由，不能从单位标签或显示名隐式推断。

### `boss`

- 多阶段首领与阶段切换规则。
- 首领护卫、护盾或阶段目标对终局条件的显式影响。

### `rescue`

- 解救后还必须护送到安全区。
- 救援超时。

### `escape`

- 单位逐个撤离并从 active battle index 安全离场。
- 撤离超时。

逐个离场需要独立的 evacuated state，并明确 timeline、targeting、战后 HP/MP 与死亡写回语义；不得把离场伪装为死亡或直接删除 canonical unit state。

### `escort`

- 内容可配置的中途检查点链。
- 护送跟随对象切换。
- 护送超时。

### `defense`

- 敌人波次及波次结束事实。
- 静态节点或区域作为防守目标。
- 显式配置的提前成功条件。

这些能力需要独立 schema，不能复用 scenario actor 字段表达静态节点、区域或波次。

### `intercept`

- 目标完成投送动作作为失败条件。
- 截击超时或伤害阈值等替代截停条件。

### `node_operation`

- 顺序节点链。
- 搬运物、节点毁坏与多阶段作业。
- 作业超时。

### `control`

- 总时限与到时领先判定。
- 连续控制计时、占领衰减和锁定进度。
- 阵营权重或非对称计分规则。

这些扩展必须增加显式运行事实与终局理由，不能从当前分数或 timeline 隐式推断。

## 独立的内容投放决策

已进入 canonical encounter seed 不等于进入默认世界随机权重。新 encounter 的随机投放、出现条件和权重属于世界内容与平衡决策，不由目标框架自动决定。

## 落地准入条件

任何扩展进入正式 encounter seed 前，必须同时补齐：

1. Resource schema 与内容 validator。
2. immutable definition 与 runtime state/evaluator。
3. 原子 success/failure/draw 优先级与 typed end reason。
4. HUD、棋盘 marker、headless snapshot 与日志投影。
5. AI legality、寻路或目标选择行为。
6. world resolution 与 battle-to-world 写回边界。
7. 正向、非法、边界、原子冲突和生命周期回归。

## 必须保持的不变量

- 目标求值只发生在最外层 mutation flush，不能破坏同步递归反应顺序。
- actor、出口、区域和节点使用稳定 typed id；显示名不参与规则判断。
- success、failure、draw 都有明确 end reason 和世界处理分支。
- scenario actor 与召唤物不能写回玩家队伍成长、装备、HP/MP 或死亡状态。
- 不增加旧 encounter/save schema 兼容路径。
