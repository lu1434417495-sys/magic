# 阶段 8 当前 checkout 复核报告

日期：2026-08-25
状态：Dated corrective audit / 只证明当前 checkout 的技术状态
范围：未提交工作树（T8.7 / T8.8 与 `task.html` 状态更新）对 T8.6 验收与 DG-8 闸门声明的复跑核对

> 依据 `content_json_stage5_stage6_gate_retrospective_20260824.md` 的后续闸门规则：本文件是
> 复跑形成的 dated corrective audit，不改写任何历史闸门的发生时序，也不追认 DG-5 / DG-6。

## 基线

- 分支 `codex/tactical-skills-world-quest`，HEAD `b22e4c6b merge: integrate terminal JSON migration`。
- 工作树未提交：234 个跟踪文件改动（+4328 / −4479），另有 12 个未跟踪路径。
- 被核对的声明来自工作树版 `task.html` 阶段 8 状态段与 T8.6 验收。

## 复跑结果

| 项目 | 命令 | 结果 |
|---|---|---|
| build | `dotnet build magic.csproj` | 0 警告 / 0 错误 |
| CLI build | `dotnet build tools/content_json_validation/Magic.ContentJsonValidation.Cli.csproj` | 0 警告 / 0 错误 |
| 离线 CLI | 逐域 `--domain <id> --input <dir> --format json` | 30/30 退出码 0，累计 1366 entry，diagnostic 0 |
| schema | `--pattern run_content_json_schema_export_regression` | PASS |
| schema 清点 | `data/schemas/content/*.schema.json` 与 `magic.code-workspace` 的 `json.schemas` | 31 / 31 / 31 三方一致（30 生产 + `schema_fixture`） |
| routine 全量 | `run_regression_suite.py --jobs 1`（分块 + 补跑 1 例） | 520/520 PASS，0 FAIL |
| lifecycle lane | `--lifecycle-correctness --pattern tests/runtime/lifecycle` | 8/8 PASS |
| lifecycle 边界 | `--lifecycle-correctness --pattern run_runtime_lifecycle_boundary_regression` | PASS |
| soak | 同 lifecycle lane 内 `run_application_lifecycle_soak_regression` | cycle 1..110 全部输出；owner/borrower/job/scope/lease/violation/suppression/quarantine 均为 0；activity 计数无失配 |
| Windows export | `python tests/export/run_windows_export_smoke.py` | PASS（success 退出 0；missing_file / missing_entry / type_mismatch 三个预期失败各退出 1） |

各域 entry 数：skills 706、traits 246、items 134、equipment_abilities 56、enemy_templates 40、
quests 37、subraces 31、battle_encounters 11、age_profiles 11、races 11、battle_sim_scenarios 11、
enemy_ai_brains 9、barriers 8、barrier_layers 7、professions 7、world_generations 6、
encounter_rosters 5、world_presets 5、battle_sim_profiles 4、recipes 4、ascensions 3、bloodlines 3、
faith 2、gear_sets 2、contingency_templates 2、gameplay_configuration 1、stage_advancements 1、
skill_special_profile_manifests 1、skill_special_profiles 1、world_shared 1。

## 静态残留核对

- `data/` 仅剩 `data/configs/engine_assets/engine_asset_catalog.tres`；`tests/` 仅剩 4 个
  engine asset catalog fixture `.tres`。与 T8.3 验收一致。
- 生产源码中 `TresAdapter`、`FromResource`、`ResourceProjectionAdapter`、`IContentResourceLoader`
  命中数为 0；`ResourceLoader.Load` 仅出现在 `EngineAssetResolver.cs`（2 处）。与 T8.2 验收一致。
- `"basic_attack"` 字面量仅出现在 `BattleSimFormalCombatFixture.cs`（11 处），属 T8.8 显式排除的
  benchmark 身份。运行时/AI/registry 一律走注入的 `basic_attack_skill_id`，单点声明在
  `gameplay_configuration.battle_skill_roles`。
- `tools/architecture/layer_rules.json` 无 baseline/豁免键；本轮新增项是
  `GameplayConfigurationDefinition.cs` 的 layer 归类，不是抑制项。

## 语义 fail-closed 抽查

- `ProjectSettlementNamePools` 对未知 `settlement_tier` 抛 `InvalidDataException`；
  `WorldMapContentValidator.ValidateNamePoolDefinitions` 另有 `Unknown` 与 key/definition 失配检查。
- `WorldContentKinds.ToVerticalBand` 调用点本身无 `Unknown` 守卫，但
  `WorldJsonImport.cs:518` 在投影前已按 closed 词表产出 unknown 诊断，schema 侧另有
  `WorldVerticalBandJsonValues` 稳定值表，因此不存在静默 fallback。
- `GameplayConfigurationContentRegistry.Rebuild` 先累计诊断再 early-return，投影只在零诊断时执行，
  配置诊断不会被投影异常吞掉。
- `GameplayConfigurationCrossDomainValidator` 与 `ContingencyTemplateCrossDomainValidator` 均在
  `ContentSnapshotBuilder` 的 `ThrowIfInvalid` 之前、`new ContentSnapshot(...)` 之外执行。

## 未运行 / 不声明通过

- 外部 CI：未运行。
- BattleSim 数值模拟与平衡 runner（`--include-simulation` / `--include-benchmarks`）：按既有约定不属于
  routine 全量，本轮未运行。

## 方法学记录

首轮用 `--offset/--limit` 分块跑时，块边界处的
`tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs` 未被执行：各块自身报告
`Passed: N/N` 且 `Failed: 0`，但 5 块合计只有 519 个唯一用例路径，而选择集为 520。该用例随后单独补跑
PASS。分块跑必须用"执行路径并集 vs `--list` 全集"对账，不能只看每块的 N/N。
