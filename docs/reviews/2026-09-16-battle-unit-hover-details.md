# 战斗人物悬停详情

日期：2026-09-16

## 实现方式与结果

复用已有 cell-hover → WorldMapSystem → BattleHudAdapter → BattleHoverPreviewOverlay 链路。修正只按脚下地面识别人物的问题，并把已有简略 HP / 状态预览扩充为人物详情。

现在鼠标移到人物上半身或脚下格子即可查看姓名、阵营、HP、已解锁 MP / 斗气、体力、行动点、移动点、六项基础属性，以及全部增益 / 减益的层数、剩余时间和独立来源说明。详情不改变当前选中单位或技能；选中技能时保留命中、伤害与 HP 预扣预览。

人物 alpha 位图在既有贴图读取时建立，使用实际 Sprite2D 变换与 atlas margin 拾取。前方地面 / 岩壁仍能遮挡后方人物，不为每次鼠标移动重新读图。

## UI 流程与信号

- 沿用 `BattleBoard2D.battle_cell_hovered` 与 `BattleMapPanel.battle_cell_hovered`，悬停人物身体时传递其原坐标，没有新增玩法命令。
- `BattleMapPanel` 新接线 `map_viewport_container.MouseExited`，退出时清掉棋盘 hover 缓存；离树对称解绑。
- 详情固定在人物旁，限制在屏幕及底部技能栏上方。鼠标进入详情可以继续阅读、滚动；离开人物和详情 0.2 秒后收起，离开战斗立即关闭。
- `WorldMapSystem` 在正式展示状态刷新后重投影当前 hover；静止鼠标下的 HP 与状态变化随 unit delta 更新，人物状态刷新不重建地形。

## 文件

- `scripts/ui/BattleBoardController.Hover.cs`、`BattleBoardController.cs`、`BattleBoard2D.cs`：身体拾取、alpha 缓存与旧 hover 信号。
- `scripts/systems/battle/presentation/BattleHoverSnapshot.cs`、`BattleHudAdapter.cs`：detached 资源及基础属性投影。
- `scripts/ui/BattleHoverPreviewOverlay.cs`、`BattleMapPanel.cs`、`scripts/systems/game_runtime/WorldMapSystem.cs`：详情排版、滚动、关闭与状态更新。
- `tests/world_map/runtime/run_battle_unit_hover_details_regression.cs`：正式世界场景中的受控前置与真实指针交互。
- 原有 typed projection / status badge 回归同步新增展示字段与详细状态行。屏障断言同时对齐此前紧凑 HUD 已落地的“完整内容在技能栏悬停说明中显示”，未改变屏障运行逻辑。
- `docs/design/ui/battle_map_presentation.md`、`docs/design/project_context_units.md`：当前展示关系与读集。

## 验证

- `dotnet build magic.csproj --no-restore`：通过，0 警告、0 错误。
- 6 项专项 headless 回归通过：HUD typed projection、status badge、HP 预扣、presentation delta、Power Word Kill 悬停预览、Phantasmal Kill 悬停预览。
- 原生 Vulkan `run_battle_unit_hover_details_regression.cs`：通过；1280×720 与 3840×2160 分别验证人物不透明上半身拾取、无需选技能、友敌切换无残留、HP / 状态静止刷新、10 项状态的真实滚轮操作、移出关闭、退出关闭与面板尺寸边界。退出 `failures=0`。
- 原生截图已人工检查两种分辨率，详情内容清晰且长列表保持在面板内。

原生首次测试发现移入详情后提前关闭，已修正为按 Godot 实际 GUI 悬停控件及子节点判断保留；最终证据在 `verified/`。初次 typed projection 测试中的旧屏障常驻断言已经按此前 HUD 的实际约定修正，最终日志为 `typed-projection-verified.log`。

本次是受控场景集成与专项回归，未执行完整套件、CI 或生产登录入口开始的完整玩家旅程。测试中的属性、状态和人物位置是 fixture 前置；交互使用真实 Viewport 鼠标事件，未直接调用悬停回调。

## 编辑器接线与边界

节点由现有场景和脚本创建，信号在代码中连接并解绑，无需用户在 Godot 编辑器补接。数据仍由 battle runtime 拥有；基础属性直接来自属性快照，攻击情境相关的命中 / 伤害继续由原技能预览提供。没有存档或技能规则改动。

## 画面与日志

- [4K 友方人物详情](evidence/2026-09-16-battle-unit-hover/verified/ally_details_3840x2160.png)
- [720p 敌方人物详情](evidence/2026-09-16-battle-unit-hover/verified/enemy_details_1280x720.png)
- [720p 多状态滚动](evidence/2026-09-16-battle-unit-hover/verified/many_statuses_1280x720.png)
- [原生交互日志](evidence/2026-09-16-battle-unit-hover/verified/run_battle_unit_hover_details_regression.txt)
- [typed projection](evidence/2026-09-16-battle-unit-hover/typed-projection-verified.log)、[状态详情](evidence/2026-09-16-battle-unit-hover/status-details.log)、[HP 预扣](evidence/2026-09-16-battle-unit-hover/hp-preview.log)、[展示增量](evidence/2026-09-16-battle-unit-hover/presentation-delta.log)、[技能悬停预览](evidence/2026-09-16-battle-unit-hover/skill-hover-previews.log)。
