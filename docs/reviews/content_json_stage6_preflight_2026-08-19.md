# 阶段 6 配置 JSON 迁移 preflight

日期：2026-08-19
状态：Superseded historical input / 不构成 DG-6 证据

> 2026-08-24 状态更正：本文件随阶段 5/6 整块迁移后的文档提交一并进入仓库，且下述逐域完成台账从未形成执行记录。因此它只能保留为历史 implementation input，不能被引用为 contemporaneous preflight、DG-6 PASS 或进入阶段 7 的授权。处置结论与提交时序见 `docs/reviews/content_json_stage5_stage6_gate_retrospective_20260824.md`。

## 范围与基线

本审计只覆盖 `task.html` 阶段 6，不接管阶段 4 的 item / trait / equipment ability / gear set / recipe，也不接管阶段 5 的 enemy / AI brain / roster 或阶段 7 的 world/save schema。

当前 `data/configs/` 下共有 638 个 `.tres`；阶段 6 占 15 个顶层 authoring family、152 个 `.tres` 文件。其中 battle encounter seed 只是 11 个 encounter 的发现清单，不是第 12 个内容 entry。BattleSim 拆成 profile/scenario、special profile 拆成 manifest/profile，因此 production cutover 实际需要 17 个 JSON domain。

| Family | `.tres` | JSON domain | 当前 owner | Resource / path / polymorphism preflight |
|---|---:|---|---|---|
| battle encounters | 12 | `battle_encounters` | `BattleEncounterContentRegistry` | seed 以 11 条 Resource path 枚举 encounter；objective 是 9 个 Resource 子类；zone/node/actor/world-resolution 为嵌套 Resource |
| barrier layers | 7 | `barrier_layers` | `BarrierContentRegistry` | layer 内嵌 outcome Resource；无跨文件 path |
| barrier profiles | 8 | `barriers` | `BarrierContentRegistry` | profile 直接嵌/引用 layer Resource，必须归一化为 layer ID |
| special profiles | 2 | `skill_special_profile_manifests`, `skill_special_profiles` | `BattleSpecialProfileRegistry` | manifest 的 `profile_resource` 指向 Meteor profile；profile 内嵌 impact component；现有 `MeteorSwarmProfileData` 传播 `ResourcePath` |
| quests | 37 | `quests` | `QuestContentRegistry` | 单一 `QuestDef` Resource；objective/requirement/reward/provider 以弱类型集合表达，需在 import 边界转成 typed model |
| professions | 7 | `professions` | `ProfessionContentRegistry` | promotion/rank/granted-skill/tag requirement 为嵌套 Resource |
| races | 11 | `races` | `RaceContentRegistry` | 无嵌套 Resource；引用 trait/age/subrace 等 ID |
| subraces | 31 | `subraces` | `SubraceContentRegistry` | granted skill 为嵌套 Resource；关联 race/trait/skill ID |
| faith | 2 | `faith` | `FaithContentRegistry` | rank 为嵌套 Resource；关联 skill/trait ID |
| age profiles | 11 | `age_profiles` | `AgeContentRegistry` | age stage rule 为嵌套 Resource |
| bloodlines | 3 | `bloodlines` | `BloodlineContentRegistry` | stage 与 attribute modifier 为嵌套 Resource；关联 trait/skill ID |
| ascensions | 3 | `ascensions` | `AscensionContentRegistry` | stage、attribute modifier 与 granted skill 为嵌套 Resource；关联 identity/trait/skill ID |
| stage advancements | 1 | `stage_advancements` | `StageAdvancementContentRegistry` | modifier 是嵌套 Resource；关联 attribute/trait/skill ID |
| contingency templates | 2 | `contingency_templates` | `ContingencyTemplateContentRegistry` | trigger、target resolver、stored spell 与 material cost 当前集中在 Resource 字段/集合；关联 skill ID |
| BattleSim | 15 | `battle_sim_profiles`, `battle_sim_scenarios` | `ContentSnapshotBuilder`, BattleSim runners | 4 个 profile 含 AI score profile/override patch subresource；11 个 scenario 含 unit spec/weapon/equipment seed；builder 与 runner 仍有固定 `.tres` path |

## 目标 ownership 与依赖顺序

### 方案比较

1. 通用反射/Dictionary importer：文件少，但会把字段、kind 和约束退回字符串/形状推断，schema、诊断与 Definition parity 无法逐域 fail closed；不采用。
2. 每域 strict DTO + plain import model + 单一 projector：复用共享 document/template/diagnostic contract，同时让 fixed kind、嵌套关系与跨域 ID 各有 typed owner；采用。
3. JSON 反序列化为 pathless Resource 后复用旧 validator/projector：改动表面最小，但保留 Resource authoring ABI、双入口和生命周期债务，并直接命中迁移停止条件；不采用。

采用方案 2，保留现有 immutable Definition 和 runtime consumer 接口。阶段 6 是 authoring/import source 迁移，不改变 runtime state 或 save schema。

每个 JSON domain 固定走：

1. code-owned JSON directory；
2. shared document loader 与 file-local template merger；
3. strict DTO parser；
4. plain typed import model 与 domain-local validator；
5. 唯一 import-model-to-Definition projector；
6. cross-domain Definition ID validation；
7. immutable process/runtime Definition publication。

共享编排顺序为：skill/trait/item/enemy 等既有 Definition index → identity/faith/barrier/special profile → encounter → quest/contingency → BattleSim profile；BattleSim scenario 由 code-owned catalog 按 ID 提供给 opt-in runner，不把 authoring DTO/Resource 发布进 process snapshot。

跨域依赖只使用 ID，不允许 JSON 保存 `res://` path、CLR/Godot 类型名、通用引用表达式或 ResourcePath。source label 只用于诊断，不能进入 Definition、snapshot、runtime state 或 save。

## 并行文件所有权

| Slice | 独占 owner | 共享 chokepoint（主线独占） |
|---|---|---|
| encounter / barrier / special profile | 对应 battle content/objective、barrier、special profile 源码、数据、schema、聚焦测试 | `ContentSnapshotBuilder`, schema/offline catalog, CLI project, `task.html`, current design docs |
| profession / race / subrace / faith / age / bloodline / ascension / stage advancement | 对应 progression registry/Definition、数据、schema、聚焦测试 | `ProgressionContentRegistry` 及上述共享文件 |
| quest / contingency / BattleSim | 对应 registry/Definition/BattleSim authoring catalog、数据、schema、聚焦测试 | `ProgressionContentRegistry`, `ContentSnapshotBuilder` 及上述共享文件 |

共享工作树在 preflight 时已有阶段 5、装备、测试审计与其他修改；本轮不清理、重写或宽泛暂存这些改动。`.git/index.lock` 在 2026-08-19 14:35:37 已存在，因此本轮停止所有 Git 写操作，直到外部 owner 释放并重新完成只读 inventory。

## 逐域完成定义

每个 domain 必须分别满足：

- strict schema 与离线 CLI 可独立验证；
- production JSON entry 数与原 Resource entry 数一致；
- import-model round-trip / Definition parity 通过；
- 重复 ID、未知 kind、额外字段、非法值和缺失跨域 ID fail closed；
- production registry 只做 code-owned JSON discovery；
- runtime consumer 只拿 immutable Definition，不拿 DTO/import model/Resource/path；
- 原 `.tres`、seed、固定路径与只服务该 authoring path 的 adapter/source 删除；
- 聚焦行为回归、process snapshot/lifecycle 与 Windows export JSON 打包证据通过。

仅有 build PASS、单个聚焦测试 PASS 或某个合并批次 PASS 都不能代替逐域完成证据。BattleSim 数值模拟仍是 opt-in；routine full suite 不包含 balance/simulation runner。

## 停止条件

若任何域需要路径、类型名、pathless Resource、字段形状/ID 猜 kind、兼容 alias/fallback、第二套 projector，或无法以 JSON 无损表达现有 authoring 语义，则停止扩大范围并报告，不用临时桥接掩盖。
