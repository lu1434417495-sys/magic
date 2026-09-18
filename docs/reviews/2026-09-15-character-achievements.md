# 人物成就页签验收

日期：2026-09-15。范围：`E:/game/magic` 当前共享工作区中的人物成就页签增量；既有七页人物档案及其他工作区改动保留，未提交。

## 行为与实现

- 人物管理新增第八个独立滚动页“成就”。仅列出当前成员 `UnitProgress` 中 `is_unlocked = true` 的成就。
- 显示已获得数量、名称、说明、取得时间和配置奖励。按取得时间倒序排列；不显示未激活条目、进度、总完成度或未获得成就的条件。
- 当前成员没有成就时仅显示“尚未获得成就”。切换人物保留页签、清理旧内容并重置滚动；关闭清空详情，重新打开返回概览。
- 沿用 `WorldMapSystem` 已注入的成就定义和所选成员状态。界面不创建进度、不推断解锁、不发放奖励。奖励文字表示内容定义，不表示奖励已结算。

## 本次修改文件

- `scenes/ui/party_management_window.tscn`：成就滚动页及正文节点。
- `scripts/ui/PartyManagementWindow.cs`：绑定、刷新、清理新页及标题说明。
- `scripts/ui/PartyManagementWindow.Details.cs`：只读成就列表和奖励展示。
- `tests/world_map/ui/run_party_character_details_regression.cs`：八页、只显示已获得成就、换人、清理、空态与原生 4K 已获得列表验证。
- `docs/design/ui/character_info_presentation.md`、`docs/design/project_context_units.md`：同步当前呈现和读取关系。

无新增或修改的信号，无存档字段或版本变化，无需编辑器手工接线。编辑器复核入口为人物管理场景的 `DetailsTabs/成就/AchievementsLabel`；运行时可从世界地图队伍按钮打开并点击“成就”。

## 当前验证

| 验证 | 结果 |
| --- | --- |
| `dotnet build magic.csproj --no-restore` | PASS，0 警告、0 错误 |
| `python tests/run_regression_suite.py --pattern tests/world_map/ui/run_party_ --jobs 1 --fail-on-output-error` | 2/2 PASS；人物详情与队伍管理窗口 |
| 原生 `run_party_character_details_regression.cs` 截图模式 | PASS，退出码 0；Vulkan / Forward+，生命周期退出无失败 |
| `git diff --check` | PASS；仅已有 CRLF/LF 提示 |

原生模式在隔离用户数据下实例化正式世界地图场景，通过真实指针输入打开队伍窗口并点击全部八个页签。验证 1280 × 720 和 3840 × 2160；4K 保留项目的 1920 × 1080 逻辑 UI，图片本身为 3840 × 2160。已人工查看 4K 空态、已获得列表和滚动截图，标题、正文、页签与关闭按钮显示正常。

4K 已获得列表使用隔离场景内注入的五项正式成就状态作为展示 fixture；其中时间为固定测试值。额外注入达到阈值但未解锁的“冲锋起步”，确认不会出现在成就页。fixture 不代表该测试角色实际完成了这些玩法事件，也不代表完成了奖励结算。使用真实滚轮验证长列表到底部可读。

本次证据属于当前工作区的聚焦回归与原生场景集成验证。未运行全套回归、CI、数值战斗模拟或从登录开始的完整应用 E2E。

## 4K 截图

- [已获得成就](evidence/2026-09-15-character-achievements/party-achievements-owned-2160.png)
- [滚动后](evidence/2026-09-15-character-achievements/party-achievements-owned-2160-scrolled.png)
- [尚未获得成就](evidence/2026-09-15-character-achievements/party-7-2160.png)
- [原生运行日志](evidence/2026-09-15-character-achievements/native-validation.log)
