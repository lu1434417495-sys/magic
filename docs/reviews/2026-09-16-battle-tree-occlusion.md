# 战斗地图树木遮挡修正

日期：2026-09-16。验证对象为当前共享工作树，不代表提交、完整回归或 CI。

## 原因与修正

原绘制顺序以高度作为全图 Z 优先级，树木使用 `height * 10 + 5`，格线使用 `height * 10 + 6`。因此格线画在树冠上，后方高台也会整块覆盖前方低处的树。

`BattleBoardController.TerrainArt` 现在按等距地图前后对角线拆分 retained 绘制命令。树木、岩壁、地面效果、marker、人物和道具共用前后顺序；高度继续控制实际几何位置。后方高台不能切断前方树冠，前方岩壁保留正常遮挡。可见 marker 和地面效果继续借用既有 atlas、origin、region、modulate 和材质；原 TileMap 保留 source / 格子表示。鼠标拾取同步采用前后顺序。

保留此前已完成的全屏战场、左下角角色卡与技能栏布局。没有改变战斗规则、存档或素材内容。

## 当前验证

- `dotnet build magic.csproj --no-restore --nologo -v:q`：通过，0 警告、0 错误。
- `run_battle_board_regression.cs`：通过，包含地表覆盖、格线、人物及拾取。
- `run_battle_board_terrain_mutation_regression.cs`：通过，包含正式技能升降跨过零层、到达 -5 / 8、森林升高和替换清理、可见 marker 与单位落点。
- `run_battle_board_native_lease_regression.cs`：通过，包含完整重建、单位增量、clear / rebind / exit 资源回收。
- `run_battle_map_panel_schema_regression.cs` headless 与原生 Vulkan：通过，包含实际输入、技能栏布局与 720p / 4K 面板截图。
- `run_battle_tree_occlusion_regression.cs` 原生 Vulkan：通过。固定森林后方高台高 3 层，分别以 0、-5、5 为基础高度，在 1280×720 和 3840×2160 比对孤立树冠与完整地图像素。随后升起前方岩壁，要求重叠树冠像素发生正常遮挡。
- 隔离 user data 的原生 `run_world_map_runtime_log_dock_regression.cs`：通过，采集真实战斗面板与人物画面。
- 本次修改路径的 `git diff --check`：通过。

树图实心叶片原始 alpha 主要为 250～253，像素对照排除柔边并允许小幅背景透色。六种组合各采样约 2554～2575 点，超过 RGB 差值阈值 0.10 的点为 0～3 个（门槛为低于 1%）。前方岩壁对照要求至少 10% 树冠采样点被遮挡，避免把全部树木固定到最上层造成假通过。

本轮像素测试开发中修正过正式树图 alpha 阈值和 4K fixture 的逻辑缩放设置。既有 HUD 截图 fixture 在直接设置高度后补齐 cell columns / edge cache 更新，避免测试截图保留陈旧岩壁；该设置修正后再次运行原生面板验证。最终通过日志对应修正后的当前代码。未运行完整回归、CI、数值战斗模拟或从登录开始的完整应用 E2E。

## 原生截图与日志

- [树冠位于后方高台前，4K](evidence/2026-09-16-battle-tree-occlusion/trees_height_0_3840x2160.png)
- [前方岩壁保留正常遮挡，720p](evidence/2026-09-16-battle-tree-occlusion/foreground_cliff_height_0_1280x720.png)
- [真实战斗面板，4K](evidence/2026-09-16-battle-tree-occlusion/live/battle_live_3840x2160.png)
- [含技能栏的固定面板场景，4K](evidence/2026-09-16-battle-tree-occlusion/hud/battle_hud_3840x2160.png)
- [原生树冠回归日志](evidence/2026-09-16-battle-tree-occlusion/native_tree_log.txt)
- headless 日志：同目录下 `battle_tree_board.log`、`battle_tree_mutation.log`、`battle_tree_lease.log`、`battle_tree_hud.log`。
