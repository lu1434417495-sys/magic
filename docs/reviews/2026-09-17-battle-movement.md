# 普通移动逐格呈现

日期：2026-09-17。基于本地 `49c80870` 及当前共享工作区；该工作区另有其他未提交修改，本记录仅覆盖普通移动呈现。

## 结果与边界

普通移动现在沿实际执行路径逐段播放，保留转弯和途中拦停，不直接把人物重建到终点。每格约 0.14 秒，卡顿时限制单帧推进，确保中间格可见。人物、血条与阴影共同移动。播放期间暂停通过场景代理发起的后续命令和自动时间推进，结束后重建最终 HUD / 棋盘。技能瞬移 / 跳跃保持原有呈现语义。

选用同步规则结算后回放路径的方案，避免把费用、屏障和地形接触改成依赖画面帧率的规则。没有存档字段、版本或兼容路径变化；headless / simulation 的命令仍同步完成，不等待画面。

## 改动与生命周期

- `BattleMovementService.HandleMoveCommand` 发布 `ExecutedPath` 的独立副本；`BattleMovementPresentation` 是只含 unit ID 与坐标的不可变事实。
- `BattleEventBatch` 合并路径时保留顺序，`BattlePresentationDeltaFactory` 把本次路径复制到展示增量。
- `BattleBoardController.Movement` 持有当前 token 的播放队列，利用旧 detached board snapshot 的 footprint 与高度计算每格位置。刷新、重建和释放会清队列。
- `BattleMapPanel.Movement` 驱动播放，每帧请求 SubViewport 绘制。新增 C# `MovementPlaybackFinished` 事件，由 `WorldMapSystem` 在代码中订阅 / 解绑；完成后重新投影 runtime。
- `WorldMapRuntimeProxy` 在场景播放期间阻止命令 / advance。场景 hover 暂停，普通刷新合并到完成时；退出场景或 application shutdown 先取消播放。
- 新增场景集成回归使用独享 `GameSessionPersistenceOptions` 路径并在结束时清理；测试前置为受控战斗，移动点击走真实 Godot 输入。

无需编辑器补接信号或设置导出属性。手动复核入口：进入战斗，点击多格可达目标，观察绕路 / 转弯，以及移动期间点击结算不会抢先执行。

## 验证

`dotnet build magic.csproj`：通过，0 警告、0 错误。

以下五个聚焦回归通过，启用 `--fail-on-output-error`：

1. `tests/world_map/runtime/run_battle_movement_presentation_regression.cs`：真实鼠标点击、逐帧路径位置、全部中间格、相邻格插值、费用一次结算、播放门禁、最终自动刷新、多路径顺序、隐藏 / 重建 / 退出清理。
2. `tests/battle_runtime/presentation/run_battle_presentation_delta_regression.cs`：路径独立副本、批次合并顺序、下一命令不复用旧路径。
3. `tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs`：屏障前一格的真实路径，排除未抵达目标与放逐落点。
4. `tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs`。
5. `tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs`。

同一个 movement 场景集成回归使用原生 Vulkan / Forward+ 运行，并完成 1280×720、3840×2160 两种分辨率的真实鼠标交互。实际路线为 `(7,5) → (7,6) → (8,6) → (9,6) → (10,6) → (10,5)`，逐帧断言经过全部四个中间格。原生截图已人工查看：

- [720p 起点](evidence/2026-09-17-battle-movement/start_1280x720.png)、[途中](evidence/2026-09-17-battle-movement/middle_1280x720.png)、[终点](evidence/2026-09-17-battle-movement/finished_1280x720.png)。
- [4K 起点](evidence/2026-09-17-battle-movement/start_3840x2160.png)、[途中](evidence/2026-09-17-battle-movement/middle_3840x2160.png)、[终点](evidence/2026-09-17-battle-movement/finished_3840x2160.png)。

检查结果限于本地构建、上述聚焦回归和原生场景集成；未运行全量回归、CI、数值模拟或完整玩家旅程。途中伤害 / 结算细节仍在路径播放结束后统一刷新，本次没有逐个攻击事件的视觉回放系统。
