# 战斗地图底座恢复与升降技能界面验证

日期：2026-09-16

> 后续核对发现：本轮仅验证了人工零高度地图，遗漏正式生成地形最低 4 层、外围绘制到 0 层基底的原约定。下面记录是当时的局部修复与验证，不能证明原最低高度要求已满足；当前修正见 [最低高度历史对比](2026-09-16-battle-minimum-height.md)。

## 问题与修复

`BattleBoardController.TerrainArt.DrawTerrainArt` 的外围剖面底线在最低地面为 0 时被截到 0，外围岩面循环因此没有任何厚度。移除外围纹理背景后，这些区域呈现为没有实体底座的薄片。此前障碍截图虽含正高度高台，却未检查零层外围底座。

现在底线始终为最低地面下一层，零层地图也绘制到 -1；保留外围纯色 `#2B4447`。这次生产代码修复只涉及底座计算，没有修改运行时高度、技能效果或移动规则。

本次没有复现升降技能完全失效。新增正式世界场景、runtime proxy、HUD 与棋盘的集成回归，补上过去手动调用 `Board.Configure` 无法覆盖的输入后自动刷新链。

## 验证

- `dotnet build magic.csproj --no-restore`：通过，0 警告、0 错误。
- `run_battle_board_terrain_mutation_regression.cs`：通过。正式技能命令覆盖正负高度至 -5 / 8，并检查 runtime、格线、地面、人物、marker、拾取与资源回收。
- 原生 Vulkan `run_battle_height_skill_presentation_regression.cs`：通过。
  - 加载正式 `world_map.tscn` 与测试世界；以受控战斗地形、技能等级和可用回合作为前置。
  - 检查零层地图存在实际底座岩面。
  - 通过真实技能图标、变体按钮、地图点击及必要的确认按钮执行 `mage_rampart_raise/pillar` 与 `mage_fossil_to_mud/lower_single_1`。
  - 在 1280×720、3840×2160 两种分辨率分别完成 `0 → 3 → 2 → 1 → 0 → -1`。
  - 每次点击提交后不手动重建棋盘；检查 runtime 高度、绘制代次、实际地面顶点和拾取锚点同步更新。相邻格也按正式技能范围生效，截图中的高台轮廓来自正式技能效果。
  - 原生截图显示零层岩石底座、升高后的岩柱和下降到负高度后的凹陷；生命周期退出报告 `failures=0`。
- 相关已跟踪文件 `git diff --check` 通过。

这是受控场景集成验证，不是从生产登录入口开始的完整玩家旅程、完整回归套件或 CI。每个施法案例预先恢复可用回合与冷却，未将其宣称为普通游戏回合中可无代价连续施法。

## 证据

- [4K 零层地图底座](evidence/2026-09-16-battle-height-restoration/flat_base_3840x2160.png)
- [4K 技能升到 3 层](evidence/2026-09-16-battle-height-restoration/raised_3_3840x2160.png)
- [4K 技能降到 -1 层](evidence/2026-09-16-battle-height-restoration/lowered_minus1_3840x2160.png)
- [720p 技能升到 3 层](evidence/2026-09-16-battle-height-restoration/raised_3_1280x720.png)
- [界面施法回归日志](evidence/2026-09-16-battle-height-restoration/run_battle_height_skill_presentation_regression.txt)
- [原有升降回归日志](evidence/2026-09-16-battle-height-restoration/terrain-mutation.log)
