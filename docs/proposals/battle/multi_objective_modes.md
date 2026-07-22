# 九种战斗模式后续落地提案

> 状态：`Proposal / Partially implemented`
> 更新日期：`2026-07-23`

## 已落地的公共底座

P0 已实现正式 `BattleEncounter`、typed objective mode、原子终局、typed outcome/end reason、按结果分支的世界处理，以及歼灭模式。以下八种模式仍未实现 authoring schema、运行态和 UI；本文件只描述后续切片，不是当前实现真相。

## 九种模式定义

| Mode | 玩家成功条件 | 典型失败条件 | 核心运行事实 |
|---|---|---|---|
| `elimination` | 指定敌对阵营无存活单位 | 玩家要求存活的阵营无存活单位 | 阵营存活数；P0 已实现 |
| `boss` | 指定首领目标被击败 | 玩家队伍覆灭，或首领达成特殊阶段条件 | boss actor id、阶段、可选护卫规则 |
| `rescue` | 被困目标获救并到达安全状态/区域 | 救援目标死亡或超时 | rescue actor、牢笼/交互状态、安全区 |
| `escape` | 要求撤离的玩家单位到达出口 | 必须撤离者死亡、出口失效或超时 | required evac actors、出口、已撤离集合 |
| `escort` | 被护送者从入口抵达出口 | 护送目标死亡或超时 | escort actor、入口、出口、路径/跟随状态 |
| `defense` | 保护目标存活到指定 TU/波次结束 | 防守目标被摧毁或关键区域失守 | protected actor/node、deadline、wave facts |
| `intercept` | 在目标抵达逃脱点或完成动作前将其阻止 | 截击目标逃离/完成投送 | moving target、逃脱点、deadline |
| `node_operation` | 对一组战场节点完成规定交互流程 | 操作者全灭、关键节点毁坏或超时 | node ids、每节点进度、顺序/并行规则 |
| `control` | 在区域中累计达到占领分数或连续控制时长 | 对方先达阈值或时间结束时领先 | control zones、各阵营占领进度、争夺状态 |

`node_operation` 不是“必须固定八个节点”。它是第 8 类模式：玩家需要在战场节点上执行启动、拆除、净化、封印、搬运等作业；节点数量由 encounter 内容决定。它与 `control` 的区别是：前者完成离散作业进度，后者持续比较区域控制权。

`escort` 必须保持独立模式。它要求保护指定 NPC 从入口移动到出口，不能并入“救出后即成功”的 rescue，也不能并入“玩家自己撤离”的 escape。

## 建议实现顺序

1. P1：`boss`、`escape`。它们主要复用 actor id、出口区域和现有单位死亡/移动事实。
2. P2：`rescue`、`escort`、`intercept`。加入 scenario actor、入口/出口、AI 跟随与到达事件。
3. P3：`defense`、`node_operation`、`control`。加入 objective node、交互命令、计时/计分和 HUD 进度。
4. P4：组合目标。定义 `All / Any / Ordered` 组合器和 required/optional objective，仍只产生一个 final decision。

每个切片都必须同时增加 Resource schema、immutable definition、runtime state/evaluator、HUD snapshot、AI affordance、world resolution、内容 validator 和 headless regression。未完成这些层级的 mode 不得加入正式 encounter seed。

## 必须保持的不变量

- 目标求值只发生在最外层 mutation flush，不能破坏同步递归反应顺序。
- actor、出口、区域和节点都使用稳定 typed id；显示名不参与规则判断。
- success、failure、draw 都有明确 end reason 和世界处理分支。
- scenario actor 与召唤物不能写回玩家队伍成长、装备、HP/MP 或死亡状态。
- 不增加旧 encounter/save schema 兼容路径。

