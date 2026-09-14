# 晋升自动弹窗与持续提示验收

日期：2026-09-14。范围：当前共享工作区中的晋升呈现行为；未提交 Git。

## 当前行为

- 完整晋升条件满足后自动打开选择窗口；已有窗口时先保留提示，等空闲 modal 边界再打开。
- 世界与战斗使用同一队列。战斗自动弹窗暂停时间线，暂缓／确认恢复时间，继续遵守战斗保存锁。
- 同一窗口展示过的机会暂缓后不反复弹出；新技能、目标职业或目标 rank 的新机会再次提示。多成员逐个展示，死亡成员不进入提示。
- 剩余机会通过常驻“可晋升（N人）· G”按钮、状态文字和 `promotion.available` 日志提示；点击按钮或 G 可重开。世界按钮位于下方操作栏旁，战斗按钮位于地图左上方。
- 读档后的安全调度重新发现并自动展示剩余机会。展示队列是会话状态，未改变存档结构或成长结算规则。

## 实现边界

`GameRuntimePromotionNotifications` 保存当前机会和已展示机会，以成员、职业、目标 rank、成长技能为键。CharacterManagement 的成长 revision、命令完成和战斗 batch 边界触发资格重新查询；空闲帧仅检查 revision 与已排队成员。界面不拥有资格或消费事实。

新增通知不会绕过原有完整请求校验、旧 token 拒绝、一次性成长历史或保存失败处理。战斗加载状态变化同步刷新提醒按钮可用性。

## 验证

- `dotnet build magic.csproj`：0 警告、0 错误。
- `python tests/run_regression_suite.py --pattern promotion --jobs 2 --fail-on-output-error --test-timeout-seconds 150`：6/6 PASS，含自动弹窗、多成员排队、死亡排除、读档、世界／战斗暂缓、保存失败与实际场景操作。
- `python tests/run_regression_suite.py --pattern reward_flow --jobs 2 --fail-on-output-error --test-timeout-seconds 120`：3/3 PASS。
- 最后只调整提醒按钮位置后，再次构建并复跑 `run_promotion_window_flow_regression.cs`：PASS。
- 实际 Vulkan Forward+ 窗口再次运行同一场景回归：PASS，退出码 0，生命周期 failures=0、legacy_debt=0。检查了 720p 世界／战斗提示和弹窗；同时保留 4K 晋升窗口截图。
- 未运行全量回归、BattleSim 或 CI；加速熟练度夹具不代表实际游玩节奏或完整主场景 E2E。

## 证据

- [晋升回归](evidence/2026-09-14-promotion-auto/promotion-regressions.txt)
- [奖励流程回归](evidence/2026-09-14-promotion-auto/reward-flow-regressions.txt)
- [最终场景回归](evidence/2026-09-14-promotion-auto/window-final.txt)
- [实际窗口输出](evidence/2026-09-14-promotion-auto/native-window.txt)
- [世界持续提示](evidence/2026-09-14-promotion-auto/promotion-reminder-720.png)
- [战斗持续提示](evidence/2026-09-14-promotion-auto/promotion-reminder-battle-720.png)
- [战斗自动弹窗](evidence/2026-09-14-promotion-auto/promotion-auto-battle-720.png)
- [4K 晋升窗口](evidence/2026-09-14-promotion-auto/promotion-2160.png)
