# 九种战斗模式后续落地提案

> 状态：`Proposal / Partially implemented (7/9)`
> 更新日期：`2026-07-24`

## 已落地的公共底座

P0 已实现正式 `BattleEncounter`、typed objective mode、原子终局、typed outcome/end reason、按结果分支的世界处理和歼灭模式。P1 已落地 `boss` 与 `escape`；P2 已落地 `rescue`、`escort` 与 `intercept`；P3 已先落地 `defense` 的 scenario actor、冻结 TU deadline、目标 AI、HUD/快照和正式回归。其余两种模式仍只属于 proposal；当前实现真相见 `docs/design/battle/objective_runtime.md`。

## 九种模式定义

| Mode | 玩家成功条件 | 典型失败条件 | 核心运行事实 |
|---|---|---|---|
| `elimination` | 指定敌对阵营无存活单位 | 玩家要求存活的阵营无存活单位 | 阵营存活数；P0 已实现 |
| `boss` | 指定首领目标被击败 | 玩家持久队伍覆灭 | boss actor id、冻结目标 unit；P1 已实现，阶段/护卫规则未实现 |
| `rescue` | 被困目标被初始持久队员相邻交互解救 | 救援目标死亡或初始持久队伍覆灭 | scenario actor、稳定目标、typed 交互；P2 已实现，安全区/超时未实现 |
| `escape` | 所有初始持久队员完整进入出口区 | 任一必须撤离者死亡 | required party、类型化出口；P1 已实现“同时到达”，逐个离场/超时未实现 |
| `escort` | 被护送者从入口抵达出口 | 护送目标死亡或初始持久队伍覆灭 | scenario actor、入口、出口、自动寻路；P2 已实现，中途检查点/超时未实现 |
| `defense` | 保护指定场景单位存活到冻结的截止 TU | 防守目标死亡或初始持久队伍在到时前覆灭 | scenario actor、冻结目标/队伍、start/deadline TU；P3 已实现，波次/静态节点未实现 |
| `intercept` | 在目标抵达逃脱点前将其击败 | 截击目标逃离或初始持久队伍覆灭 | roster actor、冻结目标/队伍、类型化出口；P2 已实现，投送动作/超时未实现 |
| `node_operation` | 对一组战场节点完成规定交互流程 | 操作者全灭、关键节点毁坏或超时 | node ids、每节点进度、顺序/并行规则 |
| `control` | 在区域中累计达到占领分数或连续控制时长 | 对方先达阈值或时间结束时领先 | control zones、各阵营占领进度、争夺状态 |

`node_operation` 不是“必须固定八个节点”。它是第 8 类模式：玩家需要在战场节点上执行启动、拆除、净化、封印、搬运等作业；节点数量由 encounter 内容决定。它与 `control` 的区别是：前者完成离散作业进度，后者持续比较区域控制权。

`escort` 必须保持独立模式。它要求保护指定 NPC 从入口移动到出口，不能并入“救出后即成功”的 rescue，也不能并入“玩家自己撤离”的 escape。

## 建议实现顺序

1. P1（已落地）：`boss`、`escape`。复用稳定 actor id、类型化出口区域和现有单位死亡/移动事实。
2. P2（已落地）：`rescue`、`escort`、`intercept`。公共底座包含 scenario actor、稳定 roster actor、入口/出口、相邻交互、目标 AI 与到达事件。
3. P3（部分落地）：`defense` 已实现单位保护与 TU 计时；后续实现 `node_operation`、`control` 的 objective node、交互命令、计分和 HUD 进度。
4. P4：组合目标。定义 `All / Any / Ordered` 组合器和 required/optional objective，仍只产生一个 final decision。

P1 明确不包含：多阶段 Boss、逐个撤离后从 active battle index 离场、撤离超时，以及把 canonical encounter 加入默认世界随机权重。这些扩展都需要独立规则和回归，不能通过隐藏 fallback 混入当前模式。

当前 P2 明确不包含：救援后再移动到安全区、内容可配置的护送中途检查点链、护送跟随对象切换、救援/护送/截击超时和波次事件，以及截击目标的“完成投送动作”替代失败条件。当前护送目标在类型化入口区生成，护送和截击目标都使用正式 footprint 寻路到类型化出口；阻路时等待并在下一次行动重新寻路。

当前 Defense 切片只保护 encounter 自有 battle-only scenario actor，并以相对 `duration_tu` 冻结绝对 deadline。它不生成敌人波次、不接受静态节点或区域作为目标，也不把“敌军全灭”当成提前成功；这些能力需要独立 schema、运行事实与回归，不能塞进 actor 字段。

每个切片都必须同时增加 Resource schema、immutable definition、runtime state/evaluator、HUD snapshot、AI affordance、world resolution、内容 validator 和 headless regression。未完成这些层级的 mode 不得加入正式 encounter seed。

## 必须保持的不变量

- 目标求值只发生在最外层 mutation flush，不能破坏同步递归反应顺序。
- actor、出口、区域和节点都使用稳定 typed id；显示名不参与规则判断。
- success、failure、draw 都有明确 end reason 和世界处理分支。
- scenario actor 与召唤物不能写回玩家队伍成长、装备、HP/MP 或死亡状态。
- 不增加旧 encounter/save schema 兼容路径。
