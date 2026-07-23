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

| 旧计划假设 | 当前事实（更新至 2026-07-24） | 本提案处理 |
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

### 基线建立进度（2026-07-22；收敛更新至 2026-07-24）

- [x] 第 1 项 semantic spike：已新增独立 `Magic.ArchitectureAnalyzers` 与轻量合成编译测试，覆盖 invocation、object creation、const/field、generic argument、inheritance、`nameof`、允许边、精确 baseline、稳定去重、跨层 partial、未分类源码和配置 fail-closed。
- [x] 主程序集隔离：`magic.csproj` 已排除 `tools/architecture/**/*.cs`；工具及其生成源码不会被 Godot 项目默认 glob 编入。
- [x] 第 2 项完整当前边清单：有序路径映射已覆盖主项目参与编译的 C# 源码；混合 authoring/contract 文件只使用经当前 owner 复核的少量 symbol override。外置 MSBuild target 可生成 SARIF，并导出确定性 JSON 清单。
- [x] 第 3 项精确债务 baseline：2026-07-22 初始清单有 39,341 个跨层 symbol pair，其中 172 个禁止边已经逐组回到当时 owner 人工核验并精确登记；不存在目录级或 namespace 级白名单。
- [x] 第 4 项正式门禁：`magic.csproj` 已通过 analyzer `ProjectReference` 和 `AdditionalFiles` 加载规则与 baseline，普通主项目构建会拒绝新增禁止边；CI 显式运行独立 analyzer 测试。完整 inventory request 仍由 `Magic.ArchitectureInventory.targets` 按需注入，不污染日常构建输出。
- [ ] 第 5 项必须等待正式门禁稳定后再开始。

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
- [ ] handler capability 与其余 modal context 继续按单 handler / 单 kind 独立迁移；不把本切片扩大到 save schema、事务模型或两阶段构造。

## 6. P1-B：深化 `BattleRuntimeModule` 的真实 owner

当前 7 个直属 split borrower 已由 owner-local `BattleRuntimeModuleBorrowerSet` 管理生命周期。后续不再扩成全局 service registry，也不把 parent-owned children 扁平登记到 module。

优先切片：

1. [x] `2026-07-23`：已把 `_ai_action_plans_by_unit_id` 迁入 `BattleAiDecisionBindingService` 的私有 per-unit index，让 AI 临时状态与 AI 行为归同一 owner；module 只保留单项借用查询、聚合状态查询与生命周期编排窄入口。content rebind 与 teardown 保持 decision context/helper consumer 先退出、action plan 后清理，service 最终断开 weak module borrower。
2. [x] `2026-07-23`：复用现有 caller-scoped `BattleSkillAvailabilityService` 作为共享规则类型，没有新增或缓存第二套 command-admission owner。preview 继续在当前同步时点校验 entry identity/selectability，execution 现在独立重新校验并解析 entry level，不再经 `BattleRuntimeModule → BattleCommandPreviewService` 反向查询；保留 commit 前重新校验和原同步调用顺序。
3. [x] `2026-07-24`：给 `BattleContingencySystem` 注入弱引用的 `IBattleContingencyRuntimePort`，由 `BattleContingencyBridgeService` 提供 current state/grid/skill 查询、玩家学习来源复核、source-event 编号、同步 auto-cast 与 owner overlay 刷新；移除 system 对 module 的直接依赖和 module 的 auto-cast/overlay 反向转发。批次记录继续由 system 直接写入调用方传入的 `BattleEventBatch`，不增加会改变 report schema 的通用 sink。source-event ordinal 迁到 bridge，teardown 在解绑 bridge borrower 前先清除 system capability；auto-cast 继续在原调用栈、同一 `BattleEventBatch` 与完整 `BattleEffectOrigin.AutoCast` scope 内进入 orchestrator，递归 reaction 顺序不变。
4. [x] `2026-07-24`：复核确认 `BattleTimelineStatusBridgeService` 没有独立状态或 capability，已删除该 bridge，并将直属 borrower 从 8 个收敛为 7 个。timeline phase、current TU、`tu_per_tick`、ready unit、action threshold 与 stamina 直接归 `BattleTimelineDriver`；cooldown anchor、turn timer、状态周期 tick/duration/turn-start 规则及 applied status 的 `next_tick_at_tu` 初始化直接归 `BattleRuntimeSkillTurnResolver`；module 只在 `MarkAppliedStatusesForTurnTiming(...)` 保留“先初始化 tick anchor，再通知 Fate”的跨 owner 编排。timeline step 与 turn-start 的原同步调用顺序未调整，不为保留拆分数量强留无状态转发层。

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
| per-battle / per-turn charge 与 limit / fumble counter | `TraitTriggerHooks` 仍有 direct map 操作；skill/equipment runtime 多数已走 typed helper；clone 与 mutation exact 覆盖 public nullable map | runtime-only，但必须保留 null/key/value exact diagnostic 与 map 深拷贝 | 下一候选 |
| shield 六字段 | `BattleShieldService`、`BattleDamageResolver`、AI score/trace、read view、game runtime snapshot | 同时进入 clone、strict codec、detached trace 和 mutation snapshot，且 Normalize 有可见副作用 | 后置 |
| action clock / turn flags | timeline、casting、turn resolver、AI 与 snapshot | reset/推进顺序敏感，部分字段进入 codec | 后置 |
| known skills / cooldowns | unit factory、availability、turn resolver、AI 与 snapshot | cooldown 进入 canonical codec，不能与 runtime-only charge 簇混抽 | 后置 |
| weapon / body / footprint / resources / tags 与投影 | factory、grid、movement、damage、equipment、AI、UI/trace 等广泛消费者 | strict codec、detached snapshot、mutable nested value 与归一化规则交叠 | 最后分成更小切片 |

P2-A/1 将原 `_consumedContingencySetupIds` 唯一存储迁入 plain C# `BattleConsumedContingencySetupCollection`；保留首次插入顺序、去重、空 id 过滤、typed gateway、clone 隔离与 stable diff key，不修改 `ToDictFields`、`BuildSnapshotPlain()`、`BuildPlainSnapshotDetached()` 或 `FromDictionary()`。字段级 owner 仍属于 CU-16，未改变 context-unit 读集。

### P2-B handler / UI

- `BattleMapPanel` 按“只读 render snapshot”“typed input intent”“signal/native owner 生命周期”拆职责，不以 partial 主文件是否小于某行数判断完成。
- `GameRuntimeSettlementCommandHandler` 优先外移已有 typed payload 构建和校验 owner；不为缩短文件再增加一层只转发 handler。
- 验收关注 UI 是否只消费稳定 snapshot、是否只提交 typed intent，以及退出后 signal/borrower/native owner 是否归零。

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

namespace 或多程序集不在关键路径：前者只能改善组织性，后者才提供语言级边界，但必须在语义依赖图稳定后另立提案评估 GodotSharp、autoload、资源脚本和测试程序集影响。
