# 大地图人物档案七页验收

日期：2026-09-14。检查对象为 `E:/game/magic` 当前工作区，验证结束时 HEAD 为 `49c80870`；本次功能改动尚未提交。

## 交付范围

大地图队伍窗口提供概览、属性、种族、职业、技能、装备、特性七页。新增种族资料和当前生效身份特性，装备实例品质、耐久、随机词条及数值，职业晋升记录；属性和特性与战斗人物详情复用展示投影。独立战斗人物详情继续沿用其现有滚动分区。

主要文件：`scenes/ui/party_management_window.tscn`、`scripts/ui/PartyManagementWindow.cs`、`scripts/ui/PartyManagementWindow.Details.cs`、`scripts/systems/game_runtime/CharacterTraitDisplayText.cs`、`scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.Details.cs`、`scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs`、`scripts/systems/game_runtime/WorldMapSystem.cs`。

编成、仓库、晋升、触发术和关闭继续使用原有信号；无新增信号、存档字段或兼容逻辑，无需编辑器手动配置。当前设计和加载索引已同步。

## 当前验证

| 命令 / 范围 | 结果 |
| --- | --- |
| `dotnet build magic.csproj --no-restore` | PASS，0 警告、0 错误 |
| `python tests/run_regression_suite.py --pattern run_party_ --jobs 2 --fail-on-output-error` | 11/11 PASS，涵盖装备、队伍状态、仓库和两个队伍 UI runner |
| `python tests/run_regression_suite.py --pattern run_character_info_ --jobs 2 --fail-on-output-error` | 4/4 PASS |
| `python tests/run_regression_suite.py --pattern run_chronicle_window_presentation_regression.cs --fail-on-output-error` | 1/1 PASS |
| 原生 `run_party_character_details_regression.cs` 截图模式 | PASS，退出码 0；1280 × 720 与 3840 × 2160 分辨率断言、全部页签的指针点击、长内容滚轮、关闭返回世界地图通过 |
| `git diff --check` | PASS |

新增测试最初的层数断言使用了 `unique_by_trait` fixture，该策略由正式 owner 合并为一层；将 fixture 明确改为 `stack_by_instance` 后验证等级和三层显示，不修改业务聚合规则。

截图模式以独立临时用户数据启动正式世界地图场景，实际点击大地图队伍入口。原生渲染使用 Vulkan / Forward+，4K 保留 1920 × 1080 逻辑 UI。它是场景集成验证，不是从登录开始的完整应用 E2E。本次未运行全套回归、CI、数值战斗模拟或存档兼容测试。

## 视觉证据

截图目录：`docs/reviews/evidence/2026-09-14-party-character-details/`。

- [720p 种族页](evidence/2026-09-14-party-character-details/party-2-720.png)
- [720p 种族页滚动后](evidence/2026-09-14-party-character-details/party-2-720-scrolled.png)
- [720p 装备实例](evidence/2026-09-14-party-character-details/party-5-720.png)
- [720p 生效特性](evidence/2026-09-14-party-character-details/party-6-720.png)
- [4K 技能页](evidence/2026-09-14-party-character-details/party-4-2160.png)

七页均保留当前地图、窗口关闭按钮和编成操作；每页独立竖向滚动，文本在详情栏换行，短内容不出现多余滚动条。原生截图中的新角色暂未取得职业，职业历史与多来源随机特性由独立数据 / UI fixture 验证。
