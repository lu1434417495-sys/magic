# 战斗地图最低高度：历史对比与基底修正

日期：2026-09-16

## 结论

修改前的正式地形最低初始高度是 **4**，最高是 **8**；原外围岩壁画到 **0 层基底**。生成数据的要求一直保留，偏离发生在手绘地图的外围剖面绘制：用“最低地面减 1”替代固定基底，把最低 4 层的地面画成只有一层厚的薄板。

上一轮仅用零高度 fixture 检查“有岩面”，未检查正式生成最低 4 层及完整底座，因而没有修正原约定。其局部验证记录保留在 [上一轮记录](2026-09-16-battle-height-restoration.md)，不作为本次完成证据。

## 比较基线与证据

- 修改前：`6a130166a5d6be5645c28e4d1ff231ccfaceb923`，即地图手绘提交 `e5be3c01` 的父提交。
- 手绘改造：`e5be3c0121ffff429ab992d8fe5b8978f4454eab`。
- 当前：HEAD `49c80870` 加共享工作区未提交修改。本次只修正外围基底计算、相关回归和文档，保留同文件内此前树木排序、纯色背景、障碍提示及其他共享修改。

| 约定 | 修改前代码 | 本次核实 |
| --- | --- | --- |
| 正式初始高度 | `BattleTerrainGenerator` 第 54–55 行：`DefaultMinHeight = 4`、`DefaultMaxHeight = 8`；创建 cell 时钳制到该范围 | 当前生成器相同，未修改 |
| 外围基底 | `BattleEdgeService.BoundaryRenderHeight = 0`；原 controller 绘制全部 `DropFaceLayerHeights` | 正高度 4 的边缘应绘制 4→3→2→1→0 |
| 手绘回归点 | `TerrainArt` 跳过 snapshot 外围岩面，另用全图最低地面减 1 生成外围剖面 | 最低 4 时底线变成 3，丢失三层岩面 |
| 技能合法范围 | 当前 `BattleCellState.MinRuntimeHeight/MaxRuntimeHeight` 为 -5～8 | 初始最低 4 与技能下降下限是不同约束；本次未修改技能规则 |

历史原绘制还包含零以下岩面截断的旧缺陷；本次保留后来已修正的有符号高度支持，没有退回整套旧 renderer。

## 当前行为

`BattleBoardController.TerrainArt.DrawTerrainArt` 从 0 层基底开始，仅在存在 0 或负高度地面时把视觉剖面向下延伸到最低地面下一层。

- 初始最低 4：外围完整保留到 0，四层厚度可见。
- 技能把某格从 4 升到 7、再降至正高度：基底仍为 0，不随最低地面上下漂移。
- 地面降到 0 或负值：继续显示实体剖面与跨零高差。
- 外围纯色 `#2B4447`、内部高差、树木前后顺序、障碍边线和左下 HUD 保持此前结果。

## 验证

- `dotnet build magic.csproj --no-restore`：通过，0 警告、0 错误。
- `python tests/run_regression_suite.py --pattern run_battle_board_ --jobs 2 --fail-on-output-error`：5 项通过、0 失败，包括四种正式生成 profile 的初始高度范围、渲染、资源生命周期、有符号升降及小窗口棋盘检查。
- 原生 Vulkan `run_battle_height_skill_presentation_regression.cs`：通过，退出 `failures=0`。
  - 首先保留正式生成的地形，验证初始 4～8 范围及外围逐层岩面实际顶点，不能仅断言“存在某个岩面节点”。
  - 随后使用最低 4 层的受控场景，在 1280×720 与 3840×2160 分别通过真实技能图标、变体按钮和地图点击完成 `4→7→6→5→4→3→2→1→0→-1`。
  - 每次提交后不手动重建棋盘，验证 runtime 高度、绘制代次、地面顶点、拾取锚点与外围基底。
  - 目标位于前沿外角，确保下陷后表面仍可见；内部深坑可能被前方地形正确遮挡，不能固定点击被遮挡的中心像素。
  - 附加零层平地检查保留一层厚度。
- 原生截图已人工检查正式地图、4 层底座、升到 7 层和负高度凹陷。
- 修改前执行新增基底断言，成功复现正式生成地图缺少通向 0 的三层岩面。该次记录也包含中央深坑被遮挡后的输入失败，因此不将其用作技能执行故障证据；最终测试改用前沿可见格。

这是正式世界场景中的受控集成验证，不是完整玩家旅程、完整回归套件或 CI；技能与可用回合为 fixture 前置条件。

## 原生画面与日志

- [正式生成地图，4K](evidence/2026-09-16-battle-minimum-height/after/generated_base_3840x2160.png)
- [最低 4 层底座，720p](evidence/2026-09-16-battle-minimum-height/after/minimum_4_base_1280x720.png)
- [最低 4 层底座，4K](evidence/2026-09-16-battle-minimum-height/after/minimum_4_base_3840x2160.png)
- [技能升到 7 层，4K](evidence/2026-09-16-battle-minimum-height/after/raised_7_3840x2160.png)
- [技能降到 -1 层，720p](evidence/2026-09-16-battle-minimum-height/after/lowered_minus1_1280x720.png)
- [最终界面施法日志](evidence/2026-09-16-battle-minimum-height/after/run_battle_height_skill_presentation_regression.txt)
- [棋盘回归日志](evidence/2026-09-16-battle-minimum-height/board-regressions.log)
- [修正前的失败记录](evidence/2026-09-16-battle-minimum-height/before/run_battle_height_skill_presentation_regression.txt)
