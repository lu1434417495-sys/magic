# 代码结构治理提案（2026-07-21 校正版）

> 状态：Proposal / Partially Implemented。本文描述尚未完整落地的治理路线，不代表当前实现。
> 来源：[2026-07-19 架构审计](../../reviews/architecture_review_2026-07-19.md)与 2026-07-21 当前工作树复核。
> 当前实现与 owner 真相以 [`project_context_units.md`](../../design/project_context_units.md)、`docs/design/`、源码和测试为准。

## 1. 目标与边界

本提案解决三个问题：

1. 让目录/owner 之间的依赖规则能够被语义工具和 CI 验证，而不是只靠评审者记忆。
2. 缩小 `GameRuntimeFacade`、`BattleRuntimeModule` 等组合根向子服务暴露的能力面，并把状态迁回真正 owner。
3. 后续拆分以职责、状态所有权和生命周期为验收标准，不再以文件数量或行数下降代替架构收益。

以下事项不是本提案的当前目标：

- 不先做全仓 namespace 迁移。单程序集中的 namespace 不限制 `internal` 可见性，也不会形成编译边界。
- 不删除 `equipment_abilities` 下的 42 个空 partial 文件。它们是正式 `.tres` 使用的 Godot 脚本路径锚点。
- 不重建第二套 `RuntimeTransaction`。
- 不为了降低 `WeakReference`、`.Setup(this)`、文件数或总代码行而改变正确的 owner/borrower 关系。
- 不把同步递归的战斗 reaction 顺序改为事件总线或异步流程。
- 不在未确认兼容需求时增加旧 save/schema 迁移或 legacy fallback。

## 2. 当前代码复核结论

| 旧计划假设 | 当前事实（更新至 2026-07-25） | 本提案处理 |
|---|---|---|
| namespace 是编译期边界的地基 | `magic.csproj` 仍是单程序集；namespace 只影响命名，不收紧 `internal` | 取消 namespace P0，改为源码路径驱动的 Roslyn 语义门禁 |
| 普通反射可枚举完整依赖 | 标准 `Type` 没有 `GetReferencedTypes()`；签名反射看不到方法体调用、对象创建、常量访问等依赖 | 不使用普通反射门禁 |
| 42 个空 partial 可直接删除 | 42/42 文件被 53 个正式 `.tres` 以 663 条 `ext_resource` 引用 | 保留全部路径；不再把“文件数 -42”列为任务 |
| production 存在约 15 个散布的 `CommitRuntimeState` 调用 | runtime 业务提交已经经 `RuntimeTransaction`/facade 单点进入 session；资源采集绕过也已在当前工作树收口 | 从待办移除，不新建事务抽象 |
| `BattleRuntimeModule` 尚无统一 borrower 接线 | 当前工作树已有 `BattleRuntimeModuleBorrowerSet`，统一 7 个直属 split borrower 的初绑、重绑和逆序 teardown | 记为组合根治理的已完成前置切片；后续转向状态 owner 和窄能力 |
| `BattleUnitState` 的 status/pending cast 尚待抽取 | `BattleStatusEffectCollection` 与 `BattlePendingCastState` 已是独立 owner | 禁止重复抽取；先盘点剩余直接/反射消费者 |
| 文件数、行数、WeakReference 数可作为主要验收 | 这些数值可能因正确拆分、测试或生命周期修复上升 | 仅作观察值，不设“只能下降”门禁 |

现有 `tools/architecture_checks.py` 继续作为动态 `Call/Set`、Dictionary 泄漏等源码气味扫描器。它是正则报告工具，不是 C# 符号依赖图，也不能替代下述语义门禁。

## 3. 修订后的优先级

```text
P0   路径型 Roslyn 依赖门禁 + 精确债务基线
  ↓
P1-A GameRuntimeFacade 封装与 typed modal context
P1-B BattleRuntimeModule 状态 owner / capability port 深化
P1-C 依据语义基线逐条清理真实越层边
  ↓
P2-A BattleUnitState 单字段簇 owner 迁移
P2-B handler / UI 按运行时职责拆 owner
  ↓
P3   诊断隔离、utils 归位、序列化风险收敛
```

每个切片独立交付；不允许把 namespace、程序集拆分、hub 改造和 save schema 变更放进同一个大改。

## 4. P0：建立真实依赖门禁

### Problem

当前仓库只有目录约定和正则气味扫描。普通反射只能观察已编译类型的部分签名，无法可靠发现方法体内的静态调用、对象创建、字段/常量读取、转换及泛型依赖。

### Recommended Design

新增独立 Roslyn `DiagnosticAnalyzer` 项目，并作为 analyzer 引用接入 `magic.csproj`：

- 依据每个声明的实际源码路径分类，不要求先加 namespace。
- 分层规则使用有序文件 glob；只对确实跨多种职责的类型增加少量 symbol override，不能粗暴地把整个 `scripts/player/**` 当成同一层。
- `sourceRoot` 下参与编译的声明必须命中一个路径映射；新增目录、漏配或拼错 glob 必须报错，不能把“未分类”静默当成无需分析。
- 检查基类、接口、属性、字段、方法签名、泛型参数，以及方法体中的 invocation、object creation、member access、conversion、`typeof` / `nameof` 等符号引用。
- partial 类型按全部 `DeclaringSyntaxReference` 归属处理，避免只看其中一个分片。
- 忽略外部程序集、生成源码和测试发起方；production 对 test 的反向依赖仍应报错。
- 当前债务以精确的 `(source symbol, target symbol, rule)` baseline 登记。已知债务允许存在但禁止新增；修复一条就删除一条 baseline，不允许白名单整个目录或 namespace。
- diagnostic 直接设为 error，使现有 `dotnet build magic.csproj` 在本地和 CI 对新增越层边变红。

建议文件范围：

- `tools/architecture/Magic.ArchitectureAnalyzers/`
- `tools/architecture/Magic.ArchitectureAnalyzers.Tests/`
- `tools/architecture/layer_rules.json`
- `tools/architecture/layer_baseline.json`
- `magic.csproj`
- `.github/workflows/ci.yml`

`tools/architecture/**.cs` 必须从主游戏程序集默认 Compile glob 中排除，再以 `OutputItemType="Analyzer"`、`ReferenceOutputAssembly="false"` 的项目引用接入。
`layer_rules.json` 与 `layer_baseline.json` 必须通过 `magic.csproj` 的 `AdditionalFiles` 传给 analyzer；analyzer 只从 `AnalyzerOptions.AdditionalFiles` 读取，不得按当前工作目录自行打开仓库文件。配置缺失、重复、格式错误或版本不支持时必须产生 error diagnostic，不能静默退化为“无规则”。

### Minimal Slice

1. 先做一个 semantic spike，证明能抓到方法体静态调用、对象创建、常量/字段访问、泛型和继承依赖。
2. 生成完整当前边清单，人工校正路径分类和少量类型 override。
3. 固化精确 baseline，使当前债务不阻断构建、任何新增禁止边都会失败。
4. 给 analyzer 增加独立单元测试，并在 CI 显式执行；不依赖 Godot 的 `run_*.cs` 发现规则。
5. 门禁稳定后再按风险清理 baseline。namespace 只在以后作为可读性/命名冲突治理选项；若需要语言级强制，再单独评估多程序集。

### 基线建立进度（2026-07-22；收敛更新至 2026-07-25）

- [x] 第 1 项 semantic spike：已新增独立 `Magic.ArchitectureAnalyzers` 与轻量合成编译测试，覆盖 invocation、object creation、const/field、generic argument、inheritance、`nameof`、允许边、精确 baseline、稳定去重、跨层 partial、未分类源码和配置 fail-closed。
- [x] 主程序集隔离：`magic.csproj` 已排除 `tools/architecture/**/*.cs`；工具及其生成源码不会被 Godot 项目默认 glob 编入。
- [x] 第 2 项完整当前边清单：有序路径映射已覆盖主项目参与编译的 C# 源码；混合 authoring/contract 文件只使用经当前 owner 复核的少量 symbol override。外置 MSBuild target 可生成 SARIF，并导出确定性 JSON 清单。
- [x] 第 3 项精确债务 baseline：2026-07-22 初始清单有 39,341 个跨层 symbol pair，其中 172 个禁止边已经逐组回到当时 owner 人工核验并精确登记；不存在目录级或 namespace 级白名单。
- [x] 第 4 项正式门禁：`magic.csproj` 已通过 analyzer `ProjectReference` 和 `AdditionalFiles` 加载规则与 baseline，普通主项目构建会拒绝新增禁止边；CI 显式运行独立 analyzer 测试。完整 inventory request 仍由 `Magic.ArchitectureInventory.targets` 按需注入，不污染日常构建输出。
- [ ] 第 5 项已进入持续清理：只按已核实 owner 的小切片删除精确 baseline tuple；未清完前保持剩余债务可见，不把“已开始”写成“已完成”。

2026-07-22 初始 172 个 baseline tuple 的 owner 分布为：

- 72 个 `domain_runtime → composition`：7 个领域 owner 直接回借 `BattleRuntimeModule` 或 `CharacterManagementModule`；
- 61 个 `domain_state → misplaced_progression_state`：`PartyState`、save snapshot 和 payload 投影依赖仍位于 `scripts/systems/progression/` 的 pending reward DTO；
- 30 个 `domain_runtime → content_authoring`：27 个 progression runtime 对 `ProgressionContentRegistry` 的遗留入口，以及 3 个 runtime 对 `AttributeModifier` authoring converter 的调用；
- 9 个 `content_authoring → domain_runtime`：contingency smoke validation、装备属性常量、misfortune 判定与 temporal status 内容校验直接调用 runtime owner。

172 是 symbol pair 数，不是 172 个独立重构任务；移动一个 DTO owner 或用一个窄 capability 替换 hub，可能同时删除一组 tuple。完成 P1-C/2、P1-C/4 与 attack-check query port 收敛后，61 个临时 `misplaced_progression_state` tuple、1 个装备属性常量 tuple 和 10 个 attack-policy 回借 composition root 的 tuple 已删除；2026-07-24 的 checked-in baseline 为 100 个精确 tuple（62 + 30 + 8）。当前 analyzer 契约验证命令为 `dotnet run --project tools/architecture/Magic.ArchitectureAnalyzers.Tests/Magic.ArchitectureAnalyzers.Tests.csproj`；仓库清单命令记录在 `tools/architecture/README.md`。二者都不属于游戏全量回归。

### Acceptance

- 能捕获当前已知的方法体越层边，而不仅是字段或参数签名。
- 新增一个合成禁止边时，`dotnet build magic.csproj` 必须失败；删除后恢复。
- baseline 精确到 symbol pair，没有目录级大白名单。
- `sourceRoot` 下任一参与编译的源码路径未分类时构建失败。
- 任一规则/baseline `AdditionalFiles` 缺失或无法解析时，构建必须失败并指出配置路径与原因。
- 输出顺序稳定，同一工作树重复运行结果一致。
- CI 执行 analyzer 单元测试；主项目构建天然执行依赖门禁。

## 5. P1-A：收敛 `GameRuntimeFacade` 能力面

### Current Ownership

`GameRuntimeFacade` 是 world/battle/session 的应用层组合根，保留统一命令入口是合理的；问题是部分 handler 仍能经 `internal` 字段和 `SetupRuntime(this)` 访问过宽的 runtime 状态，modal context 也仍有长期弱类型表示。

### Minimal Slices

1. 按真实跨类读写点审计字段，不做批量文本式 `internal → private`。
2. 每次选择一个 handler：
   - 只读依赖改为窄 query/capability；
   - mutation 改为体现事务语义的方法；
   - 字段归回 facade 私有 owner。
3. 按一个 modal kind 一次，把长期 `Dictionary<string, object>` context 改为 typed context；Godot Dictionary 只保留在同步投影边界。
4. 保留现有两阶段构造时序，除非该切片同时给出了独立生命周期证明；不要在可见性 PR 中顺手重写构造模型。

### Acceptance

- 被迁移 handler 不再直接读写 facade 字段。
- capability 只暴露该 handler 实际使用的成员；不允许换成“几乎等于 facade”的大接口。
- party/world/coord 提交仍只走现有 `RuntimeTransaction`。
- 对应 modal、事务或 world runtime 聚焦回归通过。

### 当前进度（2026-07-23）

- [x] 首个 typed modal 切片：GameOver 的 6 个固定字段已从 facade 长期 `Dictionary<string, object>` 迁入私有 `GameRuntimeGameOverContext`；状态文本直接读取 typed owner，headless snapshot 与 Godot UI 仍只在同步边界生成 detached plain graph / Request-domain projection lease。
- [x] 首个只读 projection capability 切片：`GameRuntimeCharacterInfoBuilder` 不再借用整个 `GameRuntimeFacade`，只经弱引用读取坐标文本、技能名、成员存在性、按 ID 查询的物品/特性定义与 detached 身份摘要；未暴露 `PartyState`、完整内容索引、`GameContentCatalog` 或 `CharacterManagementModule` owner。
- [x] CharacterInfo typed modal 切片：facade 的长期 `Dictionary<string, object>` 已迁入私有、nullable 的 `GameRuntimeCharacterInfoContext`；builder 直接产出 typed section/entry/fate，world/battle 打开路径保存 detached 值，plain snapshot 与 Godot Request lease 仅在同步投影边界生成，原 payload schema 与正常关闭顺序保持不变；并补齐 sidecar 覆盖 modal 与 battle resolution 的非 close 清理，避免 hidden stale context。
- [x] 首个 handler command port 切片：`GameRuntimeQuestCommandHandler` 只弱借 `IGameRuntimeQuestCommandPort`；定义/状态查询返回 detached facts，accept/progress/complete/submit/claim mutation 在 port 内按原时机同步 canonical party，逻辑成功后的持久化仍走既有 `RuntimeTransaction.MarkPartyChanged()`，不暴露 `PartyState` 或 `CharacterManagementModule`。
- [x] 首个 battle writeback command port 切片：`GameRuntimeBattleWritebackService` 只弱借 `IGameRuntimeBattleWritebackPort`；detached candidate 的容量与实例唯一性仍由 service 校验，成功后只经语义 mutation 接口安装 canonical party 并保持原有两轮 service rebind 时序，不暴露 session、内容索引或 party 子服务 owner。
- [x] Party command port 切片：`GameRuntimePartyCommandHandler` 只弱借组合后的 query / mutation capability；查询按命令生成 detached roster snapshot，leader/roster、装备与 party modal/reward 副作用由 facade 的语义 port 执行。仓库打开仍由独立 `GameRuntimeWarehouseHandler` 链路负责。装备 mutation 与持久化是单一 port 操作，所有 party 提交仍进入既有 `RuntimeTransaction.MarkPartyChanged()`；未暴露 `PartyState`、`PartyMemberState`、`PartyEquipmentService` 或 session/content owner，原 Setup/Dispose 顺序不变。
- [x] Settlement modal context 批次：contract board、shop、forge、stagecoach 四类长期状态已从 facade 的四个可变裸 `Dictionary<string, object>` 迁入四个不可互换的 immutable context owner；Godot payload 在写入时沿用既有 normalize 语义，读取时按 Request-domain lease 投影，rollback 直接恢复 detached plain snapshot，payload key 与 modal 行为不变。
- [x] Reward/promotion flow 批次：`GameRuntimeRewardFlowHandler` 不再弱借整个 `GameRuntimeFacade`，只经 `IGameRuntimeRewardFlowPort` 访问 modal、promotion commit、reward commit 与状态反馈能力；port 不暴露 `PartyState`、`CharacterManagementModule`、`BattleRuntimeModule`、session 或内容索引。battle/world 两套长期 promotion prompt 从可变 plain Dictionary 迁入 immutable `GameRuntimePromotionPromptContext` / choice context，handler 直接按 typed member/profession/selection 校验；plain payload 只在 snapshot/UI 同步输出边界生成。setup/dispose 仍使用原弱 borrower 时序，battle resolution、runtime dispose 与 world promotion 完成分别清理所属 context。
- [x] Warehouse command port 批次：`GameRuntimeWarehouseHandler` 不再弱借整个 `GameRuntimeFacade`，也不读取 `PartyState`、`PartyWarehouseService`、`PartyItemUseService`、`GameSession`、world context 或内容 catalog。facade 的 `IGameRuntimeWarehousePort` 捕获 detached command/window snapshot，拥有 add/discard/use service mutation、party stage、runtime/party/world/selection rollback、modal setup/close 与 reward 接续；handler 只解释 typed mutation result、构建既有 UI/plain payload 并更新状态。普通仓库 mutation 仍只 stage `party_state` pending dirty，不触发磁盘 flush；stage 失败时的恢复范围和关闭顺序保持不变。
- [x] Battle loot commit port 批次：`GameRuntimeBattleLootCommitService` 不再弱借整个 `GameRuntimeFacade`，也不读取 `PartyState`、`PartyWarehouseService`、`GameSession`、`EquipmentDropService` 或 item catalog。它只经 `IGameRuntimeBattleLootCommitPort` 请求仓库准备、opaque checkpoint 捕获/恢复、typed item 分类、加入仓库、装备随机、fate flag 与显示名能力；facade partial 保留实际 owner/service 接线。service 继续拥有掉落合并、灾厄碎片章节上限、逐条非致命丢弃与整批致命失败判定，原逐条 checkpoint 和整批 checkpoint 的回滚层级不变。
- [x] Command log port/owner 批次：`GameRuntimeCommandLogger` 不再弱借整个 `GameRuntimeFacade`，也不读取 session、world context、battle state、selection 或 facade 字段。`IGameRuntimeCommandLogPort` 只交付 detached runtime/battle/unit snapshot、当前 status 文本和 typed session event sink；日志 schema、command scope、battle batch context 合并及 pending command battle batches 均归 logger。facade 原 pending batch 镜像已删除，batch begin/finish 的清理时机、嵌套 scope 恢复、result fallback、Godot projection lease 与输出 key 保持不变。
- [ ] handler capability 与其余 modal context 继续按单 handler / 单 kind 独立迁移；不把本切片扩大到 save schema、事务模型或两阶段构造。

## 6. P1-B：深化 `BattleRuntimeModule` 的真实 owner

当前 7 个直属 split borrower 已由 owner-local `BattleRuntimeModuleBorrowerSet` 管理生命周期。后续不再扩成全局 service registry，也不把 parent-owned children 扁平登记到 module。

优先切片：

1. [x] `2026-07-23`：已把 `_ai_action_plans_by_unit_id` 迁入 `BattleAiDecisionBindingService` 的私有 per-unit index，让 AI 临时状态与 AI 行为归同一 owner；module 只保留单项借用查询、聚合状态查询与生命周期编排窄入口。content rebind 与 teardown 保持 decision context/helper consumer 先退出、action plan 后清理，service 最终断开 weak module borrower。
2. [x] `2026-07-23`：复用现有 caller-scoped `BattleSkillAvailabilityService` 作为共享规则类型，没有新增或缓存第二套 command-admission owner。preview 继续在当前同步时点校验 entry identity/selectability，execution 现在独立重新校验并解析 entry level，不再经 `BattleRuntimeModule → BattleCommandPreviewService` 反向查询；保留 commit 前重新校验和原同步调用顺序。
3. [x] `2026-07-24`：给 `BattleContingencySystem` 注入弱引用的 `IBattleContingencyRuntimePort`，由 `BattleContingencyBridgeService` 提供 current state/grid/skill 查询、玩家学习来源复核、source-event 编号、同步 auto-cast 与 owner overlay 刷新；移除 system 对 module 的直接依赖和 module 的 auto-cast/overlay 反向转发。批次记录继续由 system 直接写入调用方传入的 `BattleEventBatch`，不增加会改变 report schema 的通用 sink。source-event ordinal 迁到 bridge，teardown 在解绑 bridge borrower 前先清除 system capability；auto-cast 继续在原调用栈、同一 `BattleEventBatch` 与完整 `BattleEffectOrigin.AutoCast` scope 内进入 orchestrator，递归 reaction 顺序不变。
4. [x] `2026-07-24`：复核确认 `BattleTimelineStatusBridgeService` 没有独立状态或 capability，已删除该 bridge，并将直属 borrower 从 8 个收敛为 7 个。timeline phase、current TU、`tu_per_tick`、ready unit、action threshold 与 stamina 直接归 `BattleTimelineDriver`；cooldown 的时序调度、turn timer、状态周期 tick/duration/turn-start 规则及 applied status 的 `next_tick_at_tu` 初始化直接归 `BattleRuntimeSkillTurnResolver`，其 map/anchor 存储随后在 P2-A/4 收敛到 `BattleUnitCooldownState`；module 只在 `MarkAppliedStatusesForTurnTiming(...)` 保留“先初始化 tick anchor，再通知 Fate”的跨 owner 编排。timeline step 与 turn-start 的原同步调用顺序未调整，不为保留拆分数量强留无状态转发层。

验收关注：

- 状态是否归到执行不变量的 owner，而不是 module 字段是否变少。
- 每个 service 实际可访问的 module/capability 成员数和依赖环是否减少。
- initial bind、rebind、正常/异常 teardown 使用同一 borrower 拓扑，AI consumer 仍先退出，最终残留依赖为零。
- preview、execute、pending cast、auto-cast 和特殊入口使用同一规则 owner 时，原同步调用顺序不变。

## 7. P1-C：按语义基线清理越层边

下表是当前已知候选，不是最终完整清单；P0 语义图可能补充或重新定性其他边。

| 建议顺序 | 当前边 | 处理方向 | 风险 |
|---|---|---|---|
| 1（已完成，2026-07-24） | `ContentSnapshotBuilder → scripts/systems/content/world WorldMapContentValidator/WorldPresetRegistry` | 两个真实 owner 已从 `scripts/utils` 物理迁入 world content 边界；保留原 typed API，删除旧路径的 layer 特例，不增加 namespace wrapper | 中 |
| 2（已完成，2026-07-24） | `EquipmentAbilityPayloadValidators → AttributeContentRules` | 五种 AC component id、typed kind、双向映射、只读顺序与 membership 已从 `AttributeService` 下沉到 content-definition 规则；authoring、attribute、world、battle 共用同一 owner，并删除原精确 baseline tuple | 中 |
| 3（已完成，2026-07-24） | `CombatEffectDef → BattleAttackRollModifierSpec` | 已确认 spec 只包含 typed 字段、枚举映射、克隆与字典编解码，不持有 battle state/service 或执行战斗规则；文件已从 `battle/core` 物理迁入 `scripts/systems/content/skills`，复用同一类型与 payload schema，并删除旧路径的 layer 特判 | 中 |
| 4（已完成，2026-07-24） | `PartyState → PendingCharacterReward` | `PendingCharacterReward` / `PendingCharacterRewardEntry` 已从 `scripts/systems/progression` 物理迁入 `scripts/player/progression`，与 `PartyState` 和 payload codec 共处 save graph owner；类型名、字段、PartyState v7、顶层 SaveVersion 15 与 payload 形状均未改变，progression service 逻辑保持原位；删除临时 quarantine layer 及其 61 条精确 baseline | 高，涉及 save |

每条边均独立处理，并删除对应的精确 baseline 或路径特判。第四条经核对只移动源码物理 owner，没有改变任何持久化身份或 schema，因此不需要兼容路径、迁移逻辑或 SaveVersion 调整。

## 8. P2：按状态与职责拆 owner

### P2-A `BattleUnitState`

- 先生成剩余字段簇的直接读写、反射和 snapshot 消费者清单。
- 已有 `BattleStatusEffectCollection` 与 `BattlePendingCastState` 不重复抽取。
- 每次只选一个尚未独立的字段簇，先建立 typed API、迁移消费者，再移动存储 owner。
- 不承诺“公开字段完全不变且透明转发”；这会让新 owner 只成为第二份存储或无意义壳层。
- 验收使用字段不变量、snapshot 等价和对应战斗规则回归，不使用主文件行数目标。结构改造默认不跑 battle simulation。

2026-07-24 已完成首轮字段簇与消费者盘点：

| 字段簇 | 主要直接消费者 / 边界 | snapshot 与风险 | 顺序 |
|---|---|---|---|
| status effects / pending cast | 已分别归 `BattleStatusEffectCollection` / `BattlePendingCastState` | 前者进 canonical codec，后者仅 runtime clone / exact diagnostic | 已有 owner，不重复抽取 |
| consumed contingency setup ids | `BattleContingencySystem`、`BattleContingencyBridgeService`、headless overlay、AI mutation snapshot；生产消费者已全部走 typed gateway | 有序去重、gameplay clone、stable diff；不进 69-key unit codec 或 detached trace | **P2-A/1 已完成** |
| per-battle / per-turn charge 与 limit / fumble counter | `TraitTriggerHooks`、skill/equipment runtime、timeline reset、AI mutation snapshot；生产消费者已全部走 typed gateway | runtime-only；保留 missing-vs-zero、clone 深拷贝及 null/key/value exact diagnostic | **P2-A/2 已完成** |
| shield 六字段 | `BattleShieldService`、`BattleDamageResolver`、AI score/trace、read view、game runtime snapshot；生产消费者已全部走 typed gateway | 同时进入 clone、strict codec、detached trace 和 mutation snapshot；raw/canonical/exact 三类读取语义已分别保留 | **P2-A/3 已完成** |
| activation turn flags | timeline、casting、movement、metrics、AI 与 snapshot | 激活开始/结束 reset 不对称，且只有两项进入 codec | **P2-A/5 已完成** |
| action clock / temporal remainders | timeline、casting、temporal status 与 AI exact snapshot | action progress/threshold/rate remainder 已保持 TU 推进、静滞倍率和 flat snapshot；cast remainder 仍有跨 cast carry 语义 | **action clock 为 P2-A/6 已完成；cast remainder 后置** |
| cooldowns / last-turn TU anchor | turn resolver、AI、canonical codec 与 detached snapshot | StringName key、惰性消费、静滞 anchor、raw/canonical/exact 三类语义 | **P2-A/4 已完成** |
| known active skills / level / lock-hit bonus | unit factory、availability、AI、HUD 与 snapshot | 顺序、missing-vs-zero、主技能首项语义及广泛只读消费者 | **P2-A/7 已完成** |
| combat resource unlock capability set | unit factory、skill turn resolver、read view、canonical/detached snapshot 与 AI mutation snapshot | 首现顺序、非法/重复过滤、默认 hp/stamina、raw null 与 strict codec | **P2-A/8 已完成** |
| weapon projection 十二字段 | factory、range/damage/equipment/mastery、AI、UI/trace 与 snapshot | strict codec、detached snapshot、两组 mutable dice、normal/canonical/exact 三类语义 | **P2-A/9 已完成** |
| current resources / stamina recovery progress | factory、timeline、movement、damage、AI、UI/trace 与 snapshot | hp/alive 联动、资源原子写、恢复余数、strict raw 与 AI exact | **P2-A/10 已完成** |
| coord / body / footprint | factory、grid、movement、damage、AI、UI/trace 等广泛消费者 | anchor + body → footprint → occupied 的跨字段不变量、strict codec 与 raw exact snapshot | **P2-A/11 已完成** |
| resting | timeline、metrics、AI 与 snapshot | 跨 activation 保留、实际行动清理、strict codec 与 AI exact | **P2-A/12 已完成** |
| creature type tags | encounter/summon factory、equipment/skill/damage rules、codec 与 AI snapshot | 首现顺序、空值/重复过滤、strict codec、clone null 归一与 raw exact | **P2-A/13 已完成** |
| movement tags | unit factory、summon、grid/charge/movement query、codec 与 AI snapshot | 地形通行/成本热路径、首现顺序、strict codec、clone null 归一、raw exact 与 query cache revision | **P2-A/14 已完成** |
| vision / proficiency tags | passive identity projection、codec 与 AI snapshot | 两组件同源同刷新、原子替换、strict codec、clone null 归一与双组件 raw exact | **P2-A/15 已完成** |
| unit save modifier | passive identity/trait projection、enemy template、save resolver、AI 与 snapshot | 三组有序 tag + ability bonus map 同刷新、同规则域，strict codec、clone 分组件 null 归一与四组件 raw exact | **P2-A/16 已完成** |
| effective trait instances / derived ids | character gateway、factory/equipment refresh、trait trigger/passive/equipment projection、terrain、codec 与 AI snapshot | mutable entry/roll 深拷贝、实例保序、ids 排序去重、strict raw ids 保序、clone/canonical 重派生与双组件 exact | **P2-A/17 已完成** |
| damage resistance | passive identity/trait projection、enemy template、damage resolver、codec 与 AI snapshot | 后写覆盖与 stronger-only 策略分离、规则热路径单点查询、strict codec、clone null 归一与 raw exact | **P2-A/18 已完成** |
| equipment ability projection | factory/roster、equipment runtime、timeline/casting、codec 与 AI snapshot | sources 与 temporal modifiers 同源投影、原子刷新、不可变读视图、runtime-only temporal 与双组件 exact | **P2-A/19 已完成** |

P2-A/1 将原 `_consumedContingencySetupIds` 唯一存储迁入 plain C# `BattleConsumedContingencySetupCollection`；保留首次插入顺序、去重、空 id 过滤、typed gateway、clone 隔离与 stable diff key，不修改 `ToDictFields`、`BuildSnapshotPlain()`、`BuildPlainSnapshotDetached()` 或 `FromDictionary()`。字段级 owner 仍属于 CU-16，未改变 context-unit 读集。

P2-A/2 将四个 public runtime map 迁入 plain C# `BattleUnitChargeState`；per-turn current/limit 由同一 owner 原子 reset/remove，所有生产消费者改走 `BattleUnitState` typed gateway。typed 写继续过滤空 key 并把负值夹为 `0`，missing key 与显式 `0` 不合并；gameplay clone 深拷贝并把非法 null 归一为空 map，AI mutation exact diagnostic 仍区分 null 与空 map并保留原四个 stable key。该字段簇继续不进入 69-key canonical codec、detached trace 或 `FromDictionary()`，CU-16 归属与推荐读集不变。

P2-A/3 将六个 public shield 字段迁入 plain C# `BattleUnitShieldState`，并以六字段原子 replace/drain/clear/normalize 防止逐字段写入时提前清空 metadata。同 family 合并、不同 family 替换与来源刷新策略仍由 `BattleShieldService` 决策；side-effect-free damage projection 读取 raw snapshot，canonical codec、detached snapshot 与 gameplay clone 保留原有 Normalize 副作用，AI mutation diagnostic 使用 exact snapshot 保留非法数值与 null/empty 差异。69-key codec 中六个 flat key 的名称、顺序、类型，以及 AI 的六个 stable key 均未改变；CU-16 归属和推荐读集不变。

P2-A/4 将 `cooldowns` 与 `last_turn_tu` 的唯一存储迁入 plain C# `BattleUnitCooldownState`。owner 负责 typed 单项/批量写入、detached 读取，并原子执行 anchor 初始化/重基准、5 TU 粒度判定与合法冷却推进；`BattleRuntimeSkillTurnResolver` 继续负责 current-TU/静滞/turn-start 调用顺序、非法粒度日志和 changed-unit 提交。typed 写仍过滤空 key，并以非正值删除 cooldown；codec/load、detached trace、gameplay clone 与 AI exact 不经过该归一化写入口，继续保留既有 raw 0/负值、StringName key 及 null-vs-empty 诊断语义。69-key codec 的 `cooldowns` / `last_turn_tu` flat key、顺序、类型和 AI stable key 均未改变，不调整 save/schema 版本，也不增加兼容路径。known-skill 三字段不与本切片混抽；CU-15/CU-16 归属和推荐读集不变。

P2-A/5 将 `has_taken_action_this_turn`、`has_moved_this_turn`、`can_use_locked_move_points_this_turn` 与 `turn_casting_exhausted` 的唯一存储迁入 plain C# `BattleUnitTurnState`。owner 只表达单次 activation 内的行动/移动/锁定移动点授权/施法耗尽事实：activation start 原子清除四项；activation end 继续只清除施法耗尽，其他三项保留到下次 activation start。movement、metrics、special-skill、casting、timeline 与 AI 生产消费者均经 `BattleUnitState` 的语义 gateway 读写，普通移动锁定仍由“已行动或已移动”联合判定。gameplay clone 深拷贝四项；AI mutation exact 以原四个 stable key 覆盖 owner，并用 owner-presence 区分“合法全 false”和“owner 丢失”。69-key canonical codec、detached trace 与 `FromDictionary()` 仍只投影原有的 `has_taken_action_this_turn`、`can_use_locked_move_points_this_turn` 两个 flat key，`has_moved_this_turn` 与 `turn_casting_exhausted` 保持 runtime-only；不调整 schema/version，也不增加兼容路径。action clock、casting remainder 与 `is_resting` 均不属于本 owner。

P2-A/6 将 `action_progress`、`action_threshold` 与 runtime-only `action_progress_rate_remainder` 的唯一存储迁入 plain C# `BattleUnitActionClockState`。owner 负责按百分比累计余数、写入本 tick gain 并扣除全部 threshold crossing；`BattleTemporalStatusService` 继续决定时间倍率，`BattleTimelineDriver` 继续负责静滞/读条跳过、非法 threshold 归一与日志、ready queue 去重。`rate <= 0` 时余数不消费，正速率才把负 raw 余数按 `0` 参与计算；即使本 tick gain 为 `0`，已有 progress 仍会消费全部 crossing。gameplay clone 保留 progress/threshold/remainder 三项 raw 值；AI mutation exact 另保留这三项 raw 值和 owner-presence。69-key canonical codec、detached trace 与 load 仍只使用原 `action_progress`、`action_threshold` flat key，余数不序列化且 load 后为 `0`。不调整 schema/version，不增加兼容入口。`cast_progress_rate_remainder` 继续保持 unit-level、跨 pending-cast carry 的现状，待独立 casting clock 切片；known-skill 三字段因顺序、主技能首项、missing-vs-explicit-zero 与 AI 热路径读取而继续后置。

P2-A/7 将有序 active skill ids、skill level map 与 lock-hit bonus map 的唯一存储迁入 plain C# `BattleUnitKnownSkillState`，但三组件保持彼此独立，不根据 active ids 裁剪或补齐任一 map。生产热路径通过 struct readonly view、`KnowsActiveSkill(...)`、首项查询和无额外 map 副本的 entry-copy gateway 读取；canonical、detached 与 AI mutation snapshot 从借用只读组合视图直接填充各自最终容器，只有显式 mutation-exact capture/restore seam 才创建拥有型 raw 副本，不保留可变兼容 facade。normal 写继续保持 active 空值过滤与首次去重、level 负值夹为 `0` 且默认删除非正值（显式 `preserveZero` 才保留 `0`）、lock-hit 仅保留正值；strict codec 仍按旧契约接受 level 任意 int、lock-hit `0` 并拒绝负值。gameplay clone 分别深拷贝三组件并把各自 null 归一为空；AI mutation exact 同时保留 owner presence、三组件独立 null-vs-empty、active 原始顺序/重复/空哨兵和 map 原始整数，并以 diagnostic-only 内部哨兵区分 owner 缺失和 owner 存在但三组件均为 null，不新增 stable key 或 payload 字段。69-key codec、detached trace 与 AI unit snapshot 的旧 flat key、顺序和投影范围均未改变，不调整 schema/version，也不增加兼容入口。

P2-A/8 将 `unlocked_combat_resource_ids` 的唯一存储迁入 plain C# `BattleUnitCombatResourceUnlockState`。normal replace 继续按首现顺序过滤空值、未知资源和重复项，再在调用方有效项之后依次补入缺失的 `hp`、`stamina`；单项 unlock 继续拒绝非法值和重复值。生产读取只通过单项 gateway 或零副本 struct view，canonical/detached 与 gameplay clone 仍先同步默认资源，strict load 仍按旧 schema 拒绝空集合、非法项、重复项或缺少默认项，并保留已验证的 payload 顺序。显式 mutation-exact capture/restore 才创建拥有型 raw 副本，保留 null、顺序、重复和非法诊断哨兵，并以 owner-presence 区分 owner 缺失与 owner 存在但组件为 null。69-key codec 的旧 flat key、相对顺序、类型和 AI mutation stable key 均未改变，不调整 schema/version，也不增加兼容入口；current hp/mp/stamina/aura/ap/move-points 不属于本 owner。

P2-A/9 将 profile kind、item/profile/range/family、grip/range、单手/双手骰、versatile/双手使用与物理伤害标签十二字段的唯一存储迁入 plain C# `BattleUnitWeaponProjectionState`。normal apply 继续校验 profile/grip、clamp 射程、复制并规范化两组骰，且按 `uses_two_hands` 与零射程修正 grip；canonical codec、detached snapshot 与 gameplay clone 继续先在 live unit 上执行相同 canonical 规范化。strict codec 仍只校验字段类型、profile/grip 枚举和 dice schema，再原样安装类型合法但跨字段不一致或负射程的 raw 状态；AI mutation exact 则额外保留 owner presence、nullable `StringName`、null-vs-present-empty dice 与非法 raw dice 哨兵。生产消费者统一读取不可变 value view，旧十二个 canonical flat key及十二个 AI stable key的名称、顺序和投影范围均不改变，不调整 schema/version，也不保留旧字段转发别名。

P2-A/10 将 current hp/mp/stamina/aura/ap/move-points、hp 绑定的 alive fact 与 stamina recovery progress 的唯一存储迁入 plain C# `BattleUnitCombatResourceState`。normal HP 写继续 clamp 下限并同步 alive，默认态仍允许 `hp = 0 / alive = true`；其他资源写只 clamp 下限，cap、damage/heal、dead/revive、cost/refund 及逐 tick stamina/余数推进由同一 owner 原子执行。`is_resting` 仍是 timeline/activation 输入，不并入资源 owner，也不借结构迁移修复 pending-cast/resting 的既有行为候选。canonical codec、detached snapshot 与 gameplay clone 不规范化资源 raw 值；strict codec 仍只额外拒绝负 move-points，并保留其他负资源、负恢复余数及 hp/alive 不一致。AI mutation exact 以原八个 stable key保留 raw 值和 owner presence，69-key codec 的旧 key 名称、顺序、类型与独立 unlock capability key 均不改变，不调整 schema/version，也不保留旧字段转发别名。

P2-A/11 将 `coord`、`body_size`、`body_size_category`、`footprint_size` 与 `occupied_coords` 的唯一存储迁入 plain C# `BattleUnitGeometryState`。五项必须同属一个 owner，因为 occupied 同时依赖 anchor 与 body；cell occupant 拓扑和 movement geometry revision 仍由 `BattleGridService` / `BattleState` 事务负责。normal anchor/body 写原子刷新 row-major footprint，非法 body 写零修改；admission 先校验 category/size 身份再修复派生投影，clone、canonical 与 strict load 对不一致投影 fail-fast。mutation exact 保留 owner presence、非法 raw、null/empty、重复和顺序，AI stable projection 继续使用原五个 key并仅借 `coord` key表达 owner-missing sentinel；69-key codec 的 flat key、顺序、类型与 schema/version 均不改变，不保留旧字段转发别名。交换位置和 charge preview 临时改 live geometry 的既有行为风险不混入本结构切片。

P2-A/12 将 `is_resting` 的唯一存储迁入 plain C# `BattleUnitRestState`。resting 是跨 activation 保留的恢复倍率输入，不属于只在单次 activation 内 reset 的 `BattleUnitTurnState`，也不并入只拥有资源数值与恢复余数的 `BattleUnitCombatResourceState`。timeline 在活跃单位未行动的回合结束时标记 resting；metrics 在非 wait 且 AP cost 大于 `0` 的实际行动提交时清除，原判断时机不变。gameplay clone 深拷贝 owner，canonical codec、detached snapshot 与 AI stable projection 继续使用原 `is_resting` flat key、顺序和 bool 类型；mutation exact 以同一 key 区分 owner 缺失与合法 `false`。不调整 schema/version，也不保留旧 public 字段 facade。

P2-A/13 将 `creature_type_tags` 的唯一存储迁入 plain C# `BattleUnitCreatureTypeState`，不与 movement、vision、proficiency、save 或 effective-trait 投影合并。normal replace/add 继续过滤空值、按首次出现去重并保序；encounter、summon、equipment condition、skill multiplier 与 damage bonus 消费者全部改走 `BattleUnitState` typed gateway。gameplay clone 深拷贝非 null raw 集合并把非法 null 归一为空；strict codec 仍按旧 schema 拒绝空项、重复项和错误类型，再把已验证顺序原样安装。canonical codec、detached snapshot 与 AI stable projection 继续使用原 `creature_type_tags` flat key、相对顺序和数组类型；mutation exact 以该既有 key 区分 owner 缺失、owner 存在但集合为 null、present-empty，并保留 raw 空项、重复项与顺序。不调整 schema/version，不增加兼容 facade。

P2-A/14 将 `movement_tags` 的唯一存储迁入 plain C# `BattleUnitMovementTagState`，不与 geometry、vision、proficiency 或 save tags 合并。normal replace/add 继续过滤空值、按首次出现去重并保序；factory 与 simulation caller 仍先执行既有 Variant → `StringName` 转换，strict codec 继续拒绝错误类型、空项和重复项并原样安装已验证顺序。grid、charge 与 movement query 通过专用零副本 read view 读取，`BattleTerrainRules` 保留 fly/amphibious/wade 的规则所有权和专用重载。gameplay clone 深拷贝非 null raw 集合并把 null 或 missing owner 归一为空 owner；canonical codec、detached snapshot 与 AI stable projection 继续使用原 `movement_tags` flat key、相对顺序和数组类型，mutation exact 用同一 key 区分 missing owner、present-null 与 present-empty，并保留 raw 空项、重复项和顺序。当前 normal 写均发生在 `BattleState.SetUnit(...)` 前；以后若增加入场后的动态 movement tag，必须由 state-level mutation gateway 同步推进 movement geometry revision，不能只写 unit owner。不调整 schema/version，不增加兼容 facade。

P2-A/15 将 `vision_tags` 与 `proficiency_tags` 的唯一存储迁入 plain C# `BattleUnitVisionProficiencyState`。二者都只由 race/subrace identity projection 产生，并在同一 passive refresh envelope 内同时清空和重建，因此共享一个 owner；owner 内仍保留两个独立有序组件，不提供混合集合。`RaceTraitResolver` 先按 race → subrace 顺序在临时集合中完成空值过滤、首次去重与保序，再原子替换两个组件；升格抑制原种族特性时由 orchestrator 同时清空二者。save tags、damage resistance、save bonus 与 effective trait 具有额外写入方及规则热路径，不属于该 owner。strict codec、gameplay clone、canonical/detached 与 AI mutation exact 分别保留原有拒绝规则、逐组件 null 归一、两个 flat array 和 raw 诊断语义；原 `vision_tags` / `proficiency_tags` key、相对顺序、类型和 stable diff 面均不改变。mutation exact 额外区分 owner missing 与 owner-present 下两组件各自的 null/empty/raw 状态。不调整 schema/version，不增加兼容 facade。

P2-A/16 将 unit-level `save_advantage_tags`、`save_disadvantage_tags`、`save_immunity_tags` 与 `save_bonus_by_ability` 的唯一存储迁入 plain C# `BattleUnitSaveModifierState`。四组件都在 passive refresh envelope 内一起清空，并由 `BattleSaveResolver` 作为单位静态 saving-throw modifier 消费；race/subrace 与 effective trait 继续按原顺序追加 tag，trait 的同 ability bonus 继续相加并保留抵消后的显式 `0`，敌人模板继续只初始化三组 tag、bonus 保持空。`damage_resistances` 属于伤害减免规则，effective trait 属于上游源事实，均不并入本 owner。normal 写过滤空 tag、首次去重并保序；strict codec 继续拒绝三组 tag 的空项/重复/错误类型，并保留 `save_bonus_by_ability` malformed payload 回落为空 map 的既有特殊语义。gameplay clone 分组件深拷贝并把各自 null 归一为空；canonical/detached 与 mutation exact 继续使用原四个 flat/stable key、相对顺序和类型，exact 额外区分 owner missing、组件 null/empty/raw 顺序及 bonus 的显式 `0`/负值。不调整 schema/version，不增加兼容 facade。

P2-A/17 将 `effective_trait_instances` 与 `effective_trait_ids` 的唯一存储迁入 plain C# `BattleUnitEffectiveTraitState`。两者同属一份 battle-local effective trait 源事实，但 normal 与 exact 采用不同入口：normal replace 只接收 instances，过滤 null entry、按原顺序深拷贝 mutable entry/roll values，并从 instances 生成 ordinal 排序、去重 ids；不新增 rank/stacks clamp、instance-key 去重或实例重排。生产 gateway、trait trigger/passive、equipment binding 与 terrain gate 全部改走 immutable scalar read view、原子 replace 或 `HasEffectiveTrait(...)`，`BattleEffectiveTraitProjection` 不再公开 `IReadOnlyList<mutable entry>`。strict codec 继续先校验 entry schema、effective key 唯一及 ids 集合等价，再以 raw 入口分别安装两个组件，保留合法 payload ids 的原始顺序；gameplay clone、canonical 与 detached 仍忽略 raw ids 并从 instances 重派生。AI mutation exact 以原两个 stable key独立保留 owner presence、null/empty、null entry、顺序、重复、非法 scalar/nested roll sentinel 与 ids mismatch。save modifier、damage resistance、trait passive status、equipment ability source 和 charge counter 都是不同生命周期的下游或旁系状态，不并入本 owner；不调整 69-key schema/version，不增加兼容 facade。

P2-A/18 将 `damage_resistances` 的唯一存储迁入 plain C# `BattleUnitDamageResistanceState`，不与 save modifier、effective trait source、status effect 或 equipment projection 合并。owner 的 normal replace/set/merge 只保留旧容器的空 key/value 过滤和后写覆盖，不在容器层新增 damage tag/tier 闭集校验，也不统一为“最强 tier”策略：race → subrace 继续无条件后写覆盖，effective trait 的 `immune > half > normal > double > unknown` stronger-only 规则仍由 `BattleTraitPassiveProjectionService` 持有，敌人模板继续整体替换。`BattleDamageResolver` 改走零分配单 tag `TryGet`，不再复制或扫描整表。strict codec 继续要求原 `damage_resistances` flat dictionary、接受任意非空 tag 但只接受已知 mitigation tier，并保留合法输入顺序；canonical/detached 与 gameplay clone 继续把 null 或 missing owner 归一为空 map。AI mutation exact 仍只使用原 stable key，并区分 missing owner、present-null、present-empty 与 entry mutation。schema/version 不变，不保留旧 public map facade。

P2-A/19 将 `equipment_ability_sources` 与 runtime-only `temporal_progress_modifiers` 的唯一存储迁入 plain C# `BattleUnitEquipmentAbilityProjectionState`。两组件都由同一次 equipment binding 匹配产生，因此共享原子 replace owner，但仍保留独立列表、顺序与 exact 状态；`BattleEquipmentAbilityProjectionService` 改为纯计算 `BattleEquipmentAbilityProjectionResult`，玩家 factory 与 enemy roster 只在完整结果返回后一次提交，投影或防御复制异常时旧两组件均不变，player target-mark source-missing 清理继续发生在提交之后。normal replace 深拷贝、过滤 null entry、保留 source/trait → matched binding → authored modifier 顺序，每个 source 的 ability ids 仍由投影服务 ordinal 排序；owner 缓存 action/cast 各自 `ModifierId` ordinal 最小且同 ID 首项优先的 immutable scalar view，timeline/casting 不再逐次扫描列表，但每次进度结算的属性读取、d20 掷骰和 rate clamp 仍归 `BattleTemporalStatusService`。gameplay clone 把 missing owner、null outer list、null entry 与 nested-null ability ids 归一为 present-empty/过滤后的深拷贝；canonical、strict 与 detached 继续只输出既有 `equipment_ability_sources` flat key，strict load 把 temporal 组件初始化为空，69-key schema/version 不变。正式 encounter roster 启动不再把 builder 已构造的单位投影成 canonical `GArray` 后重新 strict parse，而是由 `BattleRuntimeModule` 直接接收 typed `BattleUnitState` 列表，避免 runtime-only temporal 在 world→battle handoff 中丢失；Godot projection lease 只保留给需要 collection 的同步边界。AI mutation exact 继续输出原 `equipment_ability_sources` / `temporal_progress_modifiers` 两个 stable key，并通过共同 owner 区分 missing owner、两个组件各自的 null/empty、null entry、nested-null ability ids、nullable label、非法 scalar 与原始顺序。不保留旧可写字段或兼容 facade。显式外部 `enemy_units` payload 仍是 canonical-only 边界，是否携带 runtime projection 需要单独定义外部合同，未借本结构切片扩张 69-key schema。

`2026-07-25` 已补齐普通 `BattleSimScenarioDefinition` / `BattleSimRunner` 的 in-process 保留基础设施：`BattleSimUnitDefinition` 在 canonical plain snapshot 之外私有持有 defensive、normalized equipment-projection seed，每局先重建 fresh unit 再原子安装 seed；plain/programmatic producer 可通过 `BattleSimScenarioUnitEntry.FromProjectedState(...)` 把已投影单位带入 scenario。scenario 为双方生成一次性 `BattleStartUnitRoster`，start context 不再重复生成 `battle_party` / `enemy_units`，`BattleRuntimeModule` 拒绝同阵营双来源并直接接收 unit graph 所有权；聚焦回归覆盖 Scenario→Roster 行为保留、每局隔离、一次性消费和双来源拒绝，不检查源码或展示文本。该路径不改变 codec/schema。当前 authored `BattleSimUnitSpec` 仍不会生成非空 equipment/temporal seed；formal combat fixture 的 hostile 单位来源已改为 enemy-only typed handoff，两个实际 formal benchmark 也已补齐 trait/equipment-binding catalog 注入，但默认 loadout 本身仍不保证产生非空 temporal 内容。

`2026-07-25` 已完成 pending-cast/resting 行为修复：`BattleUnitState.CommitActionTakenThisTurnTyped()` 作为跨 turn/rest owner 的聚合提交网关，原子记录本 activation 已行动并清除跨 activation 的 resting；metrics 的既有非 wait、正 AP 行动改复用该网关。`BattleCastingTimeService` 只在目标与资源校验均成功、pending cast 已建立的提交点调用该网关，再清空 AP 并记录 metrics，因此不伪造 AP cost，也不影响 preview、启动失败、法术控制失败、取消或完成阶段。读条启动触发的同步 activation end 现在能观察到已行动事实，不再把施法者重新标为 resting；不调整 canonical/save schema 或 owner 生命周期。

### P2-B handler / UI

- `BattleMapPanel` 按“只读 render snapshot”“typed input intent”“signal/native owner 生命周期”拆职责，不以 partial 主文件是否小于某行数判断完成。
- `GameRuntimeSettlementCommandHandler` 优先外移已有 typed payload 构建和校验 owner；不为缩短文件再增加一层只转发 handler。
- 验收关注 UI 是否只消费稳定 snapshot、是否只提交 typed intent，以及退出后 signal/borrower/native owner 是否归零。

`2026-07-26` 已完成战斗展示 owner 收敛：同步展示入口将 runtime facts 投影为 immutable `BattleBoardRenderSnapshot`，首帧 reveal 的 pending payload 只保存 detached HUD/board snapshot；`BattleBoard2D` 与 `BattleBoardController` 不再持有 `BattleState`。单位增量通过 `BattleBoardUnitUpdateSnapshot` 合并，不重建 cell/edge/terrain；hide/teardown 清空展示快照并通过无 signal 的 reveal ticket 失效路径阻止异步 continuation 回写离树 UI。scene、signal、battle owner 与规则语义不变。

`2026-07-25` 已完成 Settlement payload/validation owner 批次：`SettlementActionPayloadBuilder` 统一拥有 Godot boundary payload → `SettlementActionRequest`、typed request → dispatch payload、modal override 白名单复制和 validation payload 投影；`SettlementActionValidationPolicy` 与 detached result/service-resolution DTO 统一拥有 service found/enabled 的决策顺序。`GameRuntimeSettlementCommandHandler` 继续拥有 runtime/modal 可见性查询、事务、dispatch 和状态反馈，不增加转发 handler，也不改变 payload schema。

### Equipment ability 空 partial 的处理

42 个同名文件保持原路径，不列删除任务。若未来 `EquipmentAbilityAuthoringDefs.cs` 的导航成本确实阻塞维护，可以按小批次把真实类体迁回相应锚点文件，再删除聚合分片；类名、文件名、`[GlobalClass]` 与所有 `.tres` 路径必须保持不变。该清理不是当前关键路径，也没有“文件数 -42”收益目标。

## 9. P3：后置治理

1. **诊断隔离**：先测量 `BattleAiMutationGuard` 等诊断代码的生产成本，再选择条件编译或独立诊断程序集；判定语义逐字保持，不因物理迁移重写规则。
2. **utils 归位**：只随真实 owner/依赖边修复迁移文件。config/validator 归内容 owner，渲染归 presentation/UI，lease/log 归 platform；不做纯 namespace 式搬家。
3. **序列化收敛**：先建立 schema owner 与版本影响清单，再评估 accessor、集中 schema 或生成方案。任何 payload 变化遵守 SaveVersion 和兼容性确认要求；这是最后阶段，不与普通结构拆分混做。
4. **Def/Definition 规则常量**：只在确认 authoring 与 runtime 规则确实重复且语义相同后收敛到 typed 单一 owner。

## 10. 跨阶段验收纪律

主要指标：

- 新增禁止依赖边为 0；精确 baseline 只减不增。
- 被迁移子服务/handler 的 broad hub 成员访问减少，状态归属有唯一 owner。
- teardown 后 borrower、owner、sibling 和 native lease 残留为 0。
- runtime 业务提交不绕过 `RuntimeTransaction`。
- Godot resource/script path 破坏数为 0。

观察指标（不能单独决定成败）：

- C# 文件数、总代码行、超大文件数。
- `WeakReference`、`.Setup(this)`、partial 数量。
- 单个主文件长度。

验证策略：

- 每个实现切片只跑编译和受影响 owner 的最窄聚焦回归。
- 结构改造不默认运行 battle simulation、balance simulation 或全量回归。
- 只有跨域里程碑、CI 集成或风险确实扩散时才跑全套；不写死测试发现数量。
- save/schema 切片必须跑对应读写/版本回归；UI 可视行为变化才需要截图。
- ownership、主链或推荐读集发生变化时同步 `project_context_units.md`；字段级迁移进度不写入该索引。

## 11. 建议执行顺序

```text
1. P0 Roslyn semantic spike
2. P0 完整依赖基线 + CI 禁止新增边
3. P1-A Facade 单 handler / 单 modal 切片
4. P1-B Battle 单状态 owner / 单 capability port 切片
5. P1-C 从低风险越层边开始逐条删除 baseline
6. P2 BattleUnitState 单字段簇，再做 UI/handler owner
7. P3 后置治理
```

当前 P2-A 字段 owner 切片已经收敛到 `/19`；其后与本轮 temporal projection 直接相关的 residual 顺序是：

1. [x] formal combat fixture 已把 hostile unit 从 canonical `enemy_units` payload 改为绑定 context lease 的 enemy-only typed one-shot handoff，并同步两个实际专项 benchmark 调用方；ally 继续由 character gateway 构造，行为回归用真实 temporal modifier 证明开战交接不再剥离 runtime-only projection。
2. [x] 两个实际 formal benchmark 的 BattleSim runtime setup 已在保留 item definitions 的同时注入 process snapshot 的 traits 与 equipment bindings，使正式单位 factory 能按内容声明生成装备能力与 temporal 投影；catalog 由 runtime setup 同步复制，fixture/default loadout 不伪造 temporal 内容。
3. [ ] 显式外部 payload 是否携带 runtime-only projection 继续作为独立合同决策；未获得兼容/schema 授权前不扩 69-key codec，也不增加 fallback。

namespace 或多程序集不在关键路径：前者只能改善组织性，后者才提供语言级边界，但必须在语义依赖图稳定后另立提案评估 GodotSharp、autoload、资源脚本和测试程序集影响。
