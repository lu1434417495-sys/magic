# 战斗障碍地形辨识

日期：2026-09-16

## 结果

- 陡坎与阻挡边特征增加橙红断行边线，贴在相邻两侧地面的内沿；可跨越的一层台阶不标记。
- 快照现在包含背向镜头、没有可见下落岩面的阻挡上坡。阻挡事实直接来自 `BattleEdgeService.IsTraversableBetween`，与正式跨边判定共用规则。
- 临时墙从两侧较高地面向上绘制，包含墙身、端面、顶盖和砖缝，避免此前墙体画在地面下方。
- 深水使用深蓝色，浅水 / 普通水域保留明亮青绿与沙底。深水仍按原规则允许飞行 / 两栖进入；森林与装饰碎石没有新增阻挡。
- 保留纯色背景、左下角 HUD 和既有树木前后遮挡。没有移动、技能、保存格式或序列化变化。

## 当前工作树验证

1. `dotnet build magic.csproj --no-restore`：通过，0 警告、0 错误。
2. `python tests/run_regression_suite.py --pattern run_battle_board_ --jobs 2 --fail-on-output-error --log-file E:/game/magic/docs/reviews/evidence/2026-09-16-battle-obstacle-readability/board-regressions.log`：5 项通过、0 失败，覆盖棋盘、render profile、动态地形、native lease 和小型 UI。
3. 原生 Vulkan `run_battle_obstacle_readability_regression.cs`：通过。
   - 对比全部内部边的 snapshot 阻挡事实与正式 grid 判定，包含可通行台阶、双向陡坎和墙；通过正式单位跨格入口验证森林 / 浅水可通行、普通单位不能进入深水。
   - 1280×720 / 3840×2160 像素验证：深水明度约 0.176，浅水约 0.610；墙体可见像素超过 2100 个标准化采样点。
   - 移除临时墙、恢复平地后，旧墙体与阻挡提示释放；先前 detached snapshot 不受后续修改影响。
4. 原生 Vulkan `run_battle_tree_occlusion_regression.cs`：通过，720p / 4K、基准高度 0 / -5 / 5 的树冠像素和前方岩壁遮挡检查均通过。
5. 相关已跟踪文件 `git diff --check` 通过。

原生检查使用正式棋盘场景与确定性战斗 fixture；不是生产主场景全流程、完整回归套件、CI 或战斗数值模拟。

## 证据

- [720p 原生棋盘](evidence/2026-09-16-battle-obstacle-readability/obstacles_1280x720.png)
- [4K 原生棋盘](evidence/2026-09-16-battle-obstacle-readability/obstacles_3840x2160.png)
- [5 项棋盘回归](evidence/2026-09-16-battle-obstacle-readability/board-regressions.log)
- [障碍辨识与像素检查](evidence/2026-09-16-battle-obstacle-readability/run_battle_obstacle_readability_regression.txt)
- [树木遮挡像素检查](evidence/2026-09-16-battle-obstacle-readability/run_battle_tree_occlusion_regression.txt)
