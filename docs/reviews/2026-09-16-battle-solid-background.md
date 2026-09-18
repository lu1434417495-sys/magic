# 战斗地图纯色背景

按用户参考图取深青灰色 `#2B4447`，统一棋盘与面板背景，移除地图外的地表纹理和散落碎石灌木。颜色由 `BattleBoardRenderProfile.BackgroundColor` 提供；场景初始底色同步。地图表面、岩壁、树木遮挡及 HUD 布局沿用当前实现。

当前共享工作树验证：

- `dotnet build magic.csproj --no-restore --nologo -v:q`：通过，0 警告、0 错误。
- `run_battle_board_regression.cs` headless：通过。
- `run_battle_map_panel_schema_regression.cs` 原生 Vulkan：通过，包含 720p / 4K 渲染与输入、技能栏点击和滚动。
- 4K 背景空白区域像素采样为统一 RGB `(43, 68, 71)`。
- 修改路径 `git diff --check`：通过。未运行完整回归或 CI。

[4K 原生截图](evidence/2026-09-16-battle-solid-background/battle_hud_3840x2160.png)；[720p 原生截图](evidence/2026-09-16-battle-solid-background/battle_hud_1280x720.png)。验证日志位于同一 evidence 目录。
