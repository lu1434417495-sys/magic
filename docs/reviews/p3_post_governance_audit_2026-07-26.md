# P3 后置治理审计（2026-07-26）

## 结论

P3 的四项治理目标已落地：

1. AI mutation guard 已从普通 Release 产物隔离，Debug 判定语义不变。
2. `scripts/utils/` 已不再承载 C# runtime owner，仅保留内容生产 Python 脚本。
3. save version、根 schema 与 nested record 字段集合均有明确 owner。
4. contingency authoring/runtime 的重复闭集规则已收敛到 typed 单一 owner。

本批次不改变 save payload，不调整 `SaveSchemaVersions.SaveVersion = 17`，不增加旧版迁移、alias 或 fallback。

## 1. 诊断隔离

| 项目 | 变更前 Release | 变更后 Release |
|---|---:|---:|
| `BattleAiService.ChooseCommand` IL | 517 bytes | 148 bytes |
| `magic.dll` | 12,037,632 bytes | 12,037,120 bytes |

`MAGIC_AI_MUTATION_DIAGNOSTICS` 默认仅在 Debug 定义。需要诊断的特殊构建可显式传入 `-p:MagicEnableAiMutationDiagnostics=true`；普通 Release 若请求非 disabled mutation guard mode 会立即拒绝，避免产生“已启用但实际上未检查”的假象。

## 2. utils owner 审计

| 原职责 | 当前 owner 路径 | 结果 |
|---|---|---|
| 世界 authored Resource/config | `scripts/systems/content/world/` | `.cs`、`.uid` 与全部正式 `.tres` script path 同步迁移 |
| 世界 typed cell/vision data | `scripts/systems/world/` | 进入 world state/runtime 边界 |
| battle board prop typed catalog | `scripts/systems/battle/core/` | 由 battle state/runtime 与 presentation 共用 |
| display settings | `scripts/ui/` | 进入 presentation owner |
| headless text renderer | `scripts/systems/game_runtime/headless/` | 与 headless snapshot consumer 共处 |
| log、Godot wrapper ownership、projection lease、plain payload、共享随机与 projection list | `scripts/systems/platform/` | architecture analyzer 直接按 infrastructure 路径分类 |

同时删除了 12 个没有对应 `.cs` 的旧 `scripts/utils/*.cs.uid`。当前 `scripts/utils/` 只剩 `generate_canyon_tiles.py`，不参与 Godot C# runtime。

## 3. save/schema owner 清单

| schema 范围 | 唯一 owner | serializer 职责 |
|---|---|---|
| 顶层 save/version/meta/index | `SaveSchemaVersions`、`SaveSerializer` | 文件边界、版本拒绝、meta/index 归并 |
| party save graph | `PartyState.BuildSaveSnapshotPlain()` 与各 typed child state | 投影、磁盘读回入口 |
| `world_data` 根字段集合 | `WorldRuntimeSaveSchema` | 根类型检查、错误路径、递归调度 |
| `world_data` canonical 读写 | `WorldRuntimeData` | typed 聚合与 plain snapshot |
| settlement record 字段 | `WorldMapSettlementRecordData` | 边界类型与 nested settlement-state 错误说明 |
| settlement state 子树 | `WorldMapSettlementStateData` 及其 typed child | serializer 委托 `TryFromDictionary` |
| world event 字段 | `WorldMapEventData` | 边界类型检查 |
| resource node 字段与 kind/yield 规则 | `WorldMapResourceNodeData` | 边界类型与范围错误说明 |
| mounted submap 字段 | `WorldMapMountedSubmapData` | 递归校验 mounted `world_data` |
| submap return entry 字段 | `WorldMapSubmapReturnStackEntry` | 数组元素类型与错误路径 |
| encounter anchor | `EncounterAnchorData` | `WorldRuntimeData` 要求 typed strict decode 成功 |
| fog persistent schema | `WorldMapFogSystem` | serializer 委托 owner 校验 |

`SaveSerializer` 保留跨 record 的递归顺序和用户可定位的 `Corrupt save ...` 错误文本；它不再复制上述根字段或五类 nested record 字段集合。

`WorldMapNpcData` 有意保存完整 source payload，因为 NPC runtime 还需要除通用展示字段之外的服务/任务扩展数据；它不是固定字段 schema，因此本轮不把它错误收窄为 exact-key record。若未来要闭合 NPC schema，应先建立 typed NPC payload，而不是在 serializer 增加第二份字段白名单。

## 4. contingency contract owner

`ContingencyContractRules` 现在唯一声明：

- trigger string 与 `ContingencyTriggerKind` 的双向映射；
- timing string 与 `ContingencyTimingKind` 的映射；
- 每类 trigger 的精确字段集合；
- target resolver string/kind 与精确字段集合；
- empty-cell preference 闭集。

`ContingencyTriggerState`、`ContingencyTargetResolverState` 与 `ContingencySetupTemplateDefinition` 共同消费该 owner。数值范围、subject/timing 组合等行为校验仍留在原 runtime/content rule 层，没有因常量收敛改变职责。

## 5. 验证边界

本批次只要求：

- Debug/Release 编译与 architecture analyzer 零错误；
- Godot 正式 world Resource 能按新路径加载；
- save/world、contingency、AI mutation guard 的聚焦回归；
- `git diff --check` 与残留旧 runtime path 扫描。

不运行全量回归，不运行 battle simulation/balance simulation，也不新增测试。

## 6. 精确架构 baseline 最终收敛

同日继续按当前 owner 清理剩余 89 个 tuple：

| 依赖组 | 删除数 | 当前边界 |
|---|---:|---|
| progression runtime → `ProgressionContentRegistry` | 27 | `ProgressionIdentityCatalogData` |
| inventory/fate/progression child → `CharacterManagementModule` | 14 | `ICharacterSkillLearningGateway`、`ICharacterMemberStateQuery`、`IFateCharacterGateway` |
| terrain/fate child → `BattleRuntimeModule` | 48 | `IBattleTerrainEffectRuntime`、`IMisfortuneGuidanceBattleQuery` |

`tools/architecture/layer_baseline.json` 当前为 0 条；2026-07-22 人工复核登记的 172 条禁止边已全部从当前代码删除。空 baseline 下主项目 Debug/Release 构建和 analyzer 9 项契约测试通过；另通过建卡、身份校验、技能书、Fortuna guidance、Misfortune guidance、黑兆、terrain lifetime 与接触型 terrain effect 聚焦回归。未运行全量回归或数值模拟。

显式外部 `enemy_units` payload 是否扩展 69-key codec 来携带 runtime-only projection 仍是独立的外部合同/schema 决策，不是当前已知禁止边或结构清理残项；按兼容策略，在用户明确授权前不增加字段、migration 或 fallback。
