# JSON 迁移修复分主题提交复核

日期：2026-09-12

状态：Current checkout verification / local evidence

范围：`b22e4c6b` 之后本次修复提交；不追认历史 DG-5/DG-6，不代表外部 CI

## 结果

本次将已完成的修复按模板校验、敌人投影、退出/存档保护、命中规则、发布包、配置域、旧敌人类型删除、测试确定性、战斗强类型边界和审计文档拆分提交。

| 主题 | 代码提交 |
|---|---|
| 拒绝无人引用的 file-local template，并删除死声明 | `0ceddb62` |
| 敌人 template 直接投影；score profile 显式字段映射 | `d196eea5` |
| 非法队伍归一化失败保留原状态；GameOver 不持久化；观察退出异常 | `34fbec78` |
| 执行阶段尊重显式禁暴击标志 | `fea30ec6` |
| PCK 只包含生产内容目录下的 JSON | `399d842e` |
| gameplay configuration、跨域校验及 typed 内容角色 | `73092378` |
| 删除旧敌人 authoring 类型与 test-only 投影旁路；复用 AI score projector | `1165704e` |
| 固定骰注入真实 hit resolver；稳定文本测试的手动回合前置条件 | `2a883f94` |
| effect payload 与施法变体保持强类型，贯通 validator/runtime/AI | `ea99b7cf` |

GameOver 只终止当前旅程、丢弃 pending save 并卸载运行时世界，不写回死亡后的队伍。本批修复没有追加 save version bump，也没有增加旧 schema 兼容路径。

## 本次实跑证据

| 检查 | 命令 / 方法 | 结果 |
|---|---|---|
| 主项目构建 | `dotnet build magic.csproj` | 0 warning / 0 error |
| 常规全量回归 | `python tests/run_regression_suite.py --jobs 4` | 520/520 PASS |
| 生命周期严格 lane | `python tests/run_regression_suite.py --pattern tests/runtime/lifecycle/ --lifecycle-correctness --jobs 4` | 8/8 PASS |
| 生命周期 soak | 上述 lane 内的 application lifecycle soak | 110/110；owner/borrower/job/scope/lease、违规、suppression、quarantine 均清零，activity 配对无失衡 |
| Windows 实际导出 | `python tests/export/run_windows_export_smoke.py` | success 退出 0；missing_file / missing_entry / type_mismatch 按预期各退出 1；JSON 白名单通过 |
| 离线 CLI 构建 | `dotnet build tools/content_json_validation/Magic.ContentJsonValidation.Cli.csproj` | 0 warning / 0 error |
| 生产域离线校验 | 对每个生产域运行 CLI 的 `--domain <domain> --input <directory> --format json` | 30/30，全部退出 0、diagnostic_count=0 |
| schema exporter | `run_content_json_schema_export_regression.cs` | PASS；遍历注册 schema，包括 test-only fixture |
| 技能 diagnostic golden | `run_skill_validator_diagnostic_golden_regression.cs` | PASS |

CLI 的关键 entry 数为 skills 706、traits 246、items 134、quests 37、enemy brains 9、templates 40、rosters 5、gameplay configuration 1。单域 CLI 只证明 schema/domain-local 有效性，不替代 snapshot 的跨域引用验证。

全量回归是在完整修复工作树上运行；另把暂存区物化为独立 detached worktree，验证它不借用主工作树尚未纳入提交的源码。最终代码快照 `ea99b7cf` 对应内容在该干净工作树中构建通过。拆分中发现的漏纳入测试调用方均在提交前补齐；没有改动主工作树实现来迁就提交拆分。

独立工作树还验证了：

- 敌人直接投影回归；退出保护的 permadeath 回归和 8 个 persistence 回归。
- 配置 validator、schema exporter、diagnostic golden、基础攻击角色、应急充能事务、世界分带及该阶段 Definition 对象图 golden。
- 旧敌人类型清理后的 47/47 AI 回归。
- 固定骰、装备命中反应、武器骰与文本技能入口 4 个回归。
- 最终强类型快照的地面四角/裁剪、Definition 不可变图、projector golden、JSON import、effect category、敌人多目标命令、diagnostic golden、虹光球 8 个回归。

配置提交与最终强类型提交分别使用对应对象图形状的 SHA256 golden；最终值为 `D7AAC4D932184171477982D4F1FC64B8013869CFBDA95C17D567B58C622B8512`。这不是把运行时回归失败改成期望成功，diagnostic golden 的预期诊断集合没有修改。

## 交付边界

- 每个主题使用显式路径/代码块暂存，并检查 staged diff、冲突标记和 `git diff --cached --check`；新增必需 C# 文件随所属主题提交。
- 本次没有运行数值 BattleSim、benchmark、完整应用 E2E 或外部 CI，不声明这些通过。
- 原 DG-5/DG-6 缺口按历史纠偏记录保留，不补写倒签 PASS。图标清单是按迁移提交重建的字段级历史证据，不伪装成当时已留存的产物。
- 模板仍使用单个 selector；已消除死声明并加入可达性校验，没有顺带引入多模板组合或兼容格式。
- `docs/proposals/progression/human_race_subrace_design.md` 和核对期间新增的 `tests/e2e/ui_audit_capture.cs` 不属于本批修复，保留未提交。
- 仅本地提交，没有推送远端。
