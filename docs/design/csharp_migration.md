# C# 迁移执行方案

更新日期：`2026-05-31`

## 目标

本方案的目标不是让 Godot 类型完全从项目中消失，而是把运行时核心从动态 Godot 边界里解出来。

最终状态：

- 运行时核心、规则服务、AI、world runtime、settlement 服务、progression 服务使用 C# 强类型数据。
- `Variant`、`GodotObject.Get()`、`GodotObject.Call()`、`GdInterop` 只允许存在于明确的 Godot 边界适配层。
- `Godot.Collections.Dictionary` / `Godot.Collections.Array` 不再作为业务状态、业务请求或业务结果在核心逻辑中流动。
- UI、scene、Resource、存档、测试入口可以有边界适配，但适配后进入核心必须是 typed DTO / typed service / typed state。
- 生产代码切片对应的测试用例必须同步迁为 C# runner，不能留下依赖旧 GDScript / Variant / private dictionary 注入的回归入口。
- 每个迁移切片都能独立构建、独立验证、独立回退，不靠一次性大扫除。

非目标：

- 不移除 Godot C# 项目必须使用的 `Node`、`Control`、`Resource`、`Vector2I`、`StringName` 等类型。
- 不把 `.tres` 静态内容强行改成非 Godot 资源。
- 不默认加入旧 payload、旧 schema、旧存档兼容。涉及兼容必须先确认。
- 不为了减少计数而把动态转换藏进新的 helper。
- 不接受“生产代码已 C# 化，但对应测试仍靠 GDScript 动态 payload 驱动”的半完成状态。

## 当前基线

截至 `2026-05-31`：

- `scripts/`：407 个 `.cs`，0 个 `.gd`
- `tests/`：38 个 `.cs`，178 个 `.gd`
- C# 动态边界计数：

| 模式 | 数量 |
| --- | ---: |
| `GdInterop.` | 2455 |
| `Variant` | 1475 |
| `GodotObject` | 834 |
| `Godot.Collections.Dictionary` / `Godot.Collections.Array` | 3127 |
| `Callable` | 8 |
| `EmitSignal(` | 44 |
| `SignalName.` | 49 |
| `.Call(` | 2 |
| `.Get(` | 124 |

`GdInterop` 热点：

| 文件 | 数量 |
| --- | ---: |
| `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` | 265 |
| `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs` | 189 |
| `scripts/systems/battle/runtime/BattleGroundEffectService.cs` | 155 |
| `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs` | 150 |
| `scripts/systems/progression/CharacterManagementModule.cs` | 134 |
| `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs` | 124 |
| `scripts/systems/battle/core/AttackEffectResolutionResult.cs` | 98 |
| `scripts/systems/game_runtime/GameRuntimeFacade.cs` | 93 |
| `scripts/systems/battle/runtime/BattleChangeEquipmentResolver.cs` | 90 |
| `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs` | 75 |
| `scripts/systems/battle/runtime/BattleMeteorSwarmResolver.cs` | 73 |
| `scripts/systems/settlement/SettlementForgeService.cs` | 55 |

已知前置问题：

- 当前 `dotnet build magic.csproj` 被 unrelated battle 测试编译错误阻塞：
  `tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.cs`
  将 `BattleEventBatch` 传给了期望 `Godot.Collections.Array<string>` 的参数。
- 继续大规模迁移前，应先让构建重新成为有效闸门，或者明确记录该阻塞与迁移切片无关。

## 术语

核心逻辑：

- battle rules/runtime
- game runtime facade / command handler
- world map runtime
- settlement / inventory / progression 服务
- AI 决策、评分、行动生成
- persistence serializer 的 schema 组装逻辑

Godot 边界：

- `Node` / `Control` 场景脚本
- `Resource` / `.tres` 静态内容
- `project.godot` autoload 入口
- UI 展示 payload 输入输出
- save/load 文件格式输入输出
- headless / GDScript 测试入口

边界适配：

- 从 Godot `Resource` / `Dictionary` / `Variant` 转成 C# DTO。
- 从 C# DTO 转成 UI 或存档需要的 Godot 容器。
- 适配代码必须集中在入口附近，不能散落到业务规则内部。

## 设计原则

1. 状态只能有一个所有者

   例如 world grid 的占格表由 `WorldMapGridSystem` 拥有。外部不能通过 Godot dictionary 或 test hook 直接塞内部状态。

2. Godot 动态结构只能停在边界

   `Variant`、`GodotObject`、`Godot.Collections.Dictionary` 可以从 Resource、UI、存档入口进来，但进入 service / runtime 前必须转换成 typed 数据。

3. C# 内部优先使用不可变或受控可变对象

   小型状态包优先 `sealed class` / `readonly struct` / `readonly` 字段。集合由 owner 私有持有，对外暴露 `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey,TValue>` 或查询方法。

4. 字典按语义选 key

   - Godot 资源 ID、技能 ID、状态 ID：优先 `StringName`
   - 纯 C# 外部 ID 或保存路径：可用 `string`，指定 `StringComparer.Ordinal`
   - 坐标：`Vector2I`
   - 只做存在性判断：`HashSet<T>`
   - key 到状态映射：`Dictionary<TKey,TValue>`

5. 不把旧兼容伪装成迁移

   如果旧 dictionary payload 仍要支持，必须先说明旧入口是谁、为什么仍会出现、放弃兼容会破什么，再确认是否保留。

6. 每个切片先展示改法再落代码

   对 `GdInterop` 清理、payload 形态变化、signal/event 替换、RefCounted 移除等操作，先展示目标形态和风险点，确认后再改。

## 类型替换规则

| 当前形态 | 目标形态 | 备注 |
| --- | --- | --- |
| `Godot.Collections.Dictionary` 存业务状态 | `Dictionary<TKey,TValue>` | owner 私有持有；必要时对外只读 |
| `Godot.Collections.Array<T>` 存业务列表 | `List<T>` / `T[]` / `IReadOnlyList<T>` | Resource export 例外 |
| `Variant` metadata | 明确 DTO | 例如 `BattleActionMetadata`、`SettlementCommandRequest` |
| `GodotObject` 万能参数 | 真实类型 / interface / DTO | 不用 `.Get()` 猜字段 |
| `GodotObject.Get("field")` | 直接字段/属性访问 | Resource 边界可保留集中读取 |
| `GodotObject.Call("method")` | 直接方法 / interface | `.Call()` 只允许临时桥接 |
| `Callable` provider | C# delegate / interface | AI 和 battle hot path 必须强类型 |
| Godot 自定义 signal 传内部业务事件 | C# event | UI 节点内部 Godot signal 另议 |
| `RefCounted` 数据包 | 普通 C# class / struct | 只有 Godot 生命周期需要时保留 |
| `GdInterop.Get*` | typed mapper / direct access | mapper 只放边界 |

## 测试迁移硬要求

每个生产代码迁移切片必须同步处理对应测试。代码和测试必须一起完成，不能先把 runtime 切 C#，再长期保留旧 GDScript 测试入口。

硬性规则：

- 改动的生产代码如果删除或收紧了 `Variant`、`GodotObject`、`Godot.Collections.Dictionary`、`GdInterop` 边界，对应 regression 必须在同一切片改为 C# runner。
- 如果旧 GDScript 测试直接写 private dictionary、调用旧 `create()`、依赖旧 Godot signal、构造旧 dictionary payload，该测试必须重写为 public behavior 断言。
- C# 测试不得为了迁移方便恢复 production test hook，也不得要求生产代码重新暴露 private runtime state。
- 新增测试默认写 C#。只有 Godot 启动、scene smoke、截图、benchmark/simulation 工具确实需要 GDScript 脚本入口时，才允许保留 `.gd` runner，并且必须在切片记录中说明原因。
- 每个迁移 PR / 切片说明必须列出“对应测试从哪些 `.gd` 迁到了哪些 `.cs`”，或者说明没有对应测试。

测试命名建议：

- world：`tests/world_map/**/run_*_regression.cs`
- settlement/runtime：`tests/world_map/runtime/run_*_regression.cs`
- progression：`tests/progression/**/run_*_tests.cs`
- warehouse/equipment：`tests/warehouse/**/run_*_regression.cs`、`tests/equipment/**/run_*_regression.cs`
- battle rules/runtime：`tests/battle_runtime/**/run_*_regression.cs`
- shared fixture：`tests/shared/*.cs`

迁移后的 C# runner 仍应保持 headless 可执行。优先复用现有测试 helper；如果必须新增 helper，应放在 `tests/shared/`，不要把测试辅助接口加回 production runtime。

## 允许保留的 Godot 类型

这些不是迁移债务：

- `Node`、`Control`、`CanvasItem`、`TileMapLayer` 等场景节点类型。
- `Resource` 及其派生类型，用于 `.tres` 静态内容。
- `StringName`，用于稳定 ID、Godot enum-like 字段、资源 ID。
- `Vector2I`，用于格子坐标、footprint、chunk 坐标。
- `Color`、`Texture2D`、`TileSet` 等展示层资源。

这些需要限制使用范围：

- `Godot.Collections.Array<T>`：只用于 Godot export、Resource、scene API、UI 适配、测试入口。
- `Godot.Collections.Dictionary`：只用于存档/Resource/UI/headless 外层 payload。
- `Variant`：只用于边界读取，不进入核心状态。
- `GodotObject`：只用于无法避免的 Godot API 或 Resource 基类入口，不作为业务模型。

## 推荐目录与命名

新增 typed DTO / state 时按所有权放置：

- battle core 数据：`scripts/systems/battle/core/`
- battle runtime 请求/结果：`scripts/systems/battle/runtime/`
- battle AI 请求/结果：`scripts/systems/battle/ai/`
- world runtime state：`scripts/systems/world/`
- settlement 服务 DTO：`scripts/systems/settlement/`
- game runtime view model / command DTO：`scripts/systems/game_runtime/`
- progression 数据或规则上下文：`scripts/systems/progression/` 或 `scripts/player/progression/`
- persistence mapper：`scripts/systems/persistence/`

命名建议：

- 输入：`*Request`、`*Command`、`*Input`
- 输出：`*Result`、`*Outcome`
- 展示数据：`*View`、`*Snapshot`
- 运行时状态：`*State`
- 只读查询接口：`I*Query`、`I*Resolver`
- mapper：`*Mapper`、`*Adapter`，只放边界层

## 总体迁移顺序

### 阶段 0：建立闸门

目标：

- `dotnet build magic.csproj` 可用。
- 有固定命令统计动态边界剩余量。
- 每次切片能看到计数下降或边界收缩理由。

动作：

1. 修复或隔离当前 unrelated build blocker。
2. 将本文档作为迁移总入口。
3. 每次切片前后记录以下统计。

统计命令：

```powershell
$patterns = @(
    'GdInterop\.',
    '\bVariant\b',
    '\bGodotObject\b',
    'Godot\.Collections\.(Dictionary|Array)',
    '\bCallable\b',
    'EmitSignal\(',
    'SignalName\.',
    '\.Call\(',
    '\.Get\('
)
foreach ($p in $patterns) {
    $matches = rg -n --glob '*.cs' $p scripts tests 2>$null
    $count = if ($matches) { ($matches | Measure-Object).Count } else { 0 }
    "$p`t$count"
}
```

热点命令：

```powershell
rg -n --glob '*.cs' "GdInterop\." scripts tests |
    ForEach-Object { ($_ -split ':')[0] } |
    Group-Object |
    Sort-Object Count -Descending |
    Select-Object -First 30 Count,Name |
    Format-Table -AutoSize
```

### 阶段 1：低风险基础设施

优先清理小系统，建立可复制样板。

候选：

- `WorldMapGridSystem`
- `WorldMapFogSystem`
- `WorldTimeSystem`
- `AttributeService`
- 小型 battle rule，例如 `BattleDeathResolutionRules`

目标形态：

- private C# collection 持有运行时状态。
- 状态包不继承 `RefCounted`，除非 Godot 生命周期确实需要。
- 无 dictionary fallback，除非明确确认兼容需求。
- 对应测试同步迁为 C#，不再从 GDScript 插入 private runtime dictionary。

完成定义：

- 该系统中 `GdInterop.` 为 0。
- 该系统的 `Godot.Collections.Dictionary` 不再存业务状态。
- `dotnet build` 通过，或 blocker 明确与该切片无关。
- 对应 regression 已迁为 C# runner，并改为 public 行为断言。

### 阶段 2：world / settlement typed payload

重点：

- `GameRuntimeSettlementCommandHandler`
- `SettlementForgeService`
- `SettlementResearchService`
- `WorldMapDataContext`
- `WorldMapRuntimeProxy`

目标：

- settlement 命令入口使用 typed request。
- service result 使用 typed result。
- UI/headless 需要 dictionary 时，由 adapter 从 typed view 生成。
- handler 内部不拆 UI dictionary。

建议 DTO：

- `SettlementCommandRequest`
- `SettlementServiceContext`
- `SettlementServiceResult<TPayload>`
- `SettlementFacilityView`
- `SettlementMemberOption`
- `ShopEntryView`
- `ForgeRecipeView`
- `ResearchProjectView`

风险：

- UI 目前可能直接消费 dictionary 字段名。
- headless 文本命令可能依赖 snapshot 结构。
- save/load 如果记录 settlement payload，要单独确认兼容。

完成定义：

- handler 内部不再对同一个 payload 多处 `GdInterop.Get*`。
- UI 字段名集中在 adapter。
- service 测试覆盖 typed service，而不是 dictionary shape。

### 阶段 3：progression / party / inventory 数据层

重点：

- `CharacterManagementModule`
- `ProfessionAssignmentService`
- `ProgressionService`
- `PartyState`
- `UnitProgress`
- inventory / equipment services

目标：

- registry 对外提供 typed 查询。
- 成长、技能、职业、奖励数据用 typed state / DTO。
- `Godot.Collections.Array` 只保留在 Resource export 或 Godot-facing API。

建议动作：

- 给 content registry 增加 `TryGet*` / `GetAll*` typed 查询。
- 将 reward / achievement / profession 选择 payload 从 dictionary 改为 typed view。
- 将 UI 选项列表从 `Array<Dictionary>` 改为 `IReadOnlyList<TOption>`，UI adapter 负责显示。

完成定义：

- progression service 内部不读取 `Variant`。
- registry 内部可以从 Resource 读取，但对外不暴露 raw dictionary。
- save serializer 是唯一主要持有 schema dictionary 的层。

### 阶段 4：battle core typed pipeline

重点：

- `AttackEffectResolutionResult`
- `BattleState`
- `BattleUnitState`
- `BattleCellState`
- `BattleAiBlackboard`
- terrain/grid/core state

目标：

- battle core state 不使用 `Variant` metadata 表达核心语义。
- effect result、attack metadata、repeat stage、resource cost 都是 typed 模型。
- `BattleTypedEnums` 只做边界解析，不在核心反复解析字符串。

完成定义：

- core state 内的 `Variant` 只剩 Resource/serialization 边界字段，且有注释说明。
- hot path 不出现 `.Get()` / `GdInterop.Get*`。
- battle rule 单元测试覆盖 typed 分支。

### 阶段 5：battle runtime resolver

重点：

- `BattleRuntimeSkillTurnResolver`
- `BattleGroundEffectService`
- `BattleSkillExecutionOrchestrator`
- `BattleSpecialSkillResolver`
- `BattleChangeEquipmentResolver`
- `BattleMeteorSwarmResolver`
- `BattleShieldService`
- `BattleChargeResolver`

目标：

- resolver 输入输出 typed。
- runtime module 直接暴露 typed service / typed state，不通过 `GodotObject.Get()`。
- 地形、屏障、移动、伤害、技能执行之间用接口或直接类型调用。

切片策略：

1. 先清 resolver 的输入 request。
2. 再清 resolver 内部临时 metadata。
3. 最后清 resolver 输出 result / event batch。

不要做：

- 不要一次性扫完整 battle runtime。
- 不要把 dictionary mapper 放进 resolver 深处。
- 不要为了 UI/report 保留核心 dictionary payload。

完成定义：

- 每个 resolver 切片至少降低一个文件的 `GdInterop` 数量。
- battle smoke 或对应 focused regression 通过。
- AI / report / UI 需要旧 shape 时由 adapter 生成。

### 阶段 6：AI action / trace / scoring

重点：

- `BattleAiRuntimeActionPlan`
- `BattleAiContext`
- `BattleAiMutationGuard`
- `EnemyAiAction`
- `EnemyAiActionHelper`
- `AiActionTrace`
- `AiCandidateSummary`
- `AiCommandSummary`

目标：

- AI 决策输入为 typed snapshot。
- action plan metadata 为 typed class，不是 dictionary。
- trace/report 可以在最终输出时转 dictionary/json。
- mutation guard 检查 typed state，不解析 Variant。

完成定义：

- AI hot path 不出现 `Variant` 分支。
- trace 输出保持可读，但转换集中在 recorder/formatter。
- AI regression 覆盖 typed action path。

### 阶段 7：UI / presentation 边界

UI 不是第一优先级，但最终也要收敛。

重点：

- `BattleMapPanel`
- `BattleBoardController`
- `SettlementWindow`
- `ShopWindow`
- `PartyManagementWindow`
- `DisplaySettingsWindow`
- `WorldMapView`

目标：

- UI 接收 typed view model。
- UI 内部局部状态使用 C# collection。
- 自定义 Godot signal 改 C# event，除非信号需要被 `.tscn` 或 GDScript 外部连接。

完成定义：

- UI 不拆 runtime dictionary。
- UI 只负责展示和用户输入，不持有业务真相。
- UI 事件是 typed event / typed callback。

### 阶段 8：persistence / snapshot / headless

重点：

- `SaveSerializer`
- `GameSession`
- `GameTextSnapshotRenderer`
- `GameTextCommandRunner`
- `HeadlessGameTestSession`

目标：

- save schema dictionary 只存在 serializer 内。
- snapshot dictionary 只存在 renderer/adapter 内。
- text command 输入先 parse 成 typed command，再进入 runtime。

兼容要求：

- 改 save schema 前必须确认是否保留旧存档兼容。
- 不自动添加 legacy alias、fallback migration、旧 payload 支持。
- 如果不保留兼容，必须明确哪些旧存档或旧测试会失效。

完成定义：

- runtime 不因为 headless/text command 暴露 raw dictionary。
- serializer round-trip regression 通过。
- snapshot 结构变化有明确测试更新。

### 阶段 9：测试迁移

当前 `tests/` 仍有大量 `.gd`。测试迁移跟随业务切片，并且是业务切片的完成条件，不是迁移结束后的统一清扫。

规则：

- 改哪个业务域，就同步处理该域直接依赖 private dictionary / Variant 的 GDScript 测试；未处理则该业务切片不算完成。
- 新增或重写测试优先 C# runner。
- 测试不能为了方便恢复 production test hook。
- 测试应断言 public behavior，而不是插入 private state。
- C# runner 应和生产代码切片同提交进入构建验证。
- 暂时保留 GDScript runner 必须有明确原因，例如 scene 截图、benchmark/simulation、Godot 启动脚本限制；普通业务 regression 不使用这个例外。

优先顺序：

1. world map schema/runtime tests
2. settlement service/runtime tests
3. progression core tests
4. battle rules/runtime focused tests
5. AI tests
6. UI rendering tests
7. benchmark/simulation tests

## 单切片执行流程

每个切片固定按下面流程走。

### 1. 选定范围

说明：

- 目标文件
- 目标调用点数量
- 是否涉及 UI
- 是否涉及 save/schema
- 是否涉及测试入口
- 对应 `.gd` 测试文件和目标 `.cs` 测试文件

示例：

```text
切片：WorldMapFogSystem
目标：移除 fog runtime 内部 Godot dictionary 和 GdInterop
不包含：WorldMapView UI、save schema
测试：tests/world_map/runtime/run_*_fog*_regression.gd -> tests/world_map/runtime/run_*_fog*_regression.cs
```

### 2. 展示目标改法

在落代码前先展示：

- 旧数据形态
- 新数据形态
- 替换表
- 预期删除的 `GdInterop`
- 可能破坏的测试

不得直接把多个系统一起改。

### 3. 落正式代码

只改该切片需要的文件。

允许：

- 新增小型 typed state / DTO。
- 将 private Godot dictionary 换成 C# dictionary。
- 将 `RefCounted` 数据包换成普通 C# 类型。
- 将 `.Get()` / `.Call()` 换成直接调用。

不允许：

- 顺手重构 unrelated 文件。
- 添加旧兼容 fallback，除非已确认。
- 把 `GdInterop` 包一层新 helper 继续调用。

### 4. 更新测试

优先顺序：

1. 找到该业务域所有直接关联的 `.gd` regression。
2. 将关联 `.gd` regression 迁为 C# runner。
3. 如果测试断言 public behavior，只更新类型和入口。
4. 如果测试直接插 private dictionary，改成 public API 行为断言。
5. 如果测试覆盖的是旧兼容路径，确认是否删除。
6. 删除或停用被 C# runner 覆盖的旧 `.gd` 入口，避免同一行为维护两套测试。

如果本切片没有对应测试，必须在结果记录中写明“无对应测试”，并说明是否需要新增 C# regression。

### 5. 验证

最低验证：

```powershell
dotnet build magic.csproj
```

按域追加：

```powershell
godot --headless --script tests/world_map/runtime/run_world_map_runtime_proxy_regression.gd
godot --headless --script tests/progression/core/run_progression_tests.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
python tests/run_regression_suite.py
```

不要默认运行 battle simulation、balance simulation、benchmark，除非任务明确要求。

### 6. 记录结果

每个切片结束记录：

- 改了哪些文件
- 删除了多少 `GdInterop`
- 剩余动态边界是否合理
- 哪些 `.gd` 测试迁成了哪些 `.cs` 测试
- 跑了哪些测试
- 哪些测试未跑以及原因
- 是否需要更新 `docs/design/project_context_units.md`

## 每片完成定义

一个迁移切片完成必须满足：

- 目标文件中不再有被选中的动态调用点，或者剩余调用点都标明边界原因。
- 不新增 `GdInterop` 调用。
- 不新增 `GodotObject.Get()` / `.Call()`。
- 业务状态不再由 Godot dictionary/array 持有。
- 新 DTO / state 的 owner 清楚。
- 对应业务 regression 已迁为 C# runner，或者明确记录无对应测试。
- 旧 `.gd` regression 不再依赖被移除的动态边界。
- 构建通过，或 blocker 与本切片无关且已记录。
- 不引入未经确认的兼容逻辑。

## 审查清单

提交前逐项检查：

- 这个类型是否必须继承 `RefCounted`、`Resource`、`Node`？
- 这个字段是否需要 Godot export/Inspector？
- 这个 collection 是否只是 C# 内部状态？
- key 类型是否稳定？
- `StringName` 与 `string` 是否有明确选择理由？
- 是否还有旧 dictionary fallback？
- 是否还有 `Variant.Type` 分支在核心逻辑里？
- 是否有 UI 或 headless 依赖旧字段名？
- save schema 是否变化？
- 是否需要用户确认兼容？
- 测试是否还在直接写 private state？

## 迁移状态表

| 域 | 状态 | 下一步 |
| --- | --- | --- |
| world grid | 代码迁移中 | 补测试边界，确认 `WorldMapGridSystem` public behavior 回归 |
| world fog | 未开始 | 用 world grid 样板替换内部 dictionary |
| world time | 未开始 | 清理少量 `GdInterop` |
| settlement command | 未开始 | 先设计 typed request/result，不直接扫大文件 |
| progression service | 未开始 | registry typed 查询优先 |
| battle core | 部分 typed | 继续收 `AttackEffectResolutionResult` 和 core metadata |
| battle runtime | 未系统化 | 按 resolver 分片迁移 |
| AI action/trace | 未系统化 | action plan metadata typed 化 |
| UI | 暂缓 | runtime typed view 稳定后再改 |
| persistence | 暂缓 | save 兼容确认后再动 |
| tests | 强制跟随迁移 | 业务切片改到哪里，对应 `.gd` regression 同步迁为 `.cs` |

## 反模式

不要做这些：

- 新建 `Interop2`、`VariantUtils2` 之类 helper 继续扩散动态读取。
- 在 core/rules/runtime 里接受 `GodotObject` 后再 `.Get()`。
- 为了测试方便把 private dictionary 暴露出来。
- 生产代码切到 C# 后，对应 regression 继续留在 GDScript 里构造旧 Variant/dictionary payload。
- 把 UI dictionary 直接传进 service。
- 一次性改 battle runtime 多个 resolver。
- 在未确认的情况下保留旧 schema fallback。
- 用普通 public 可写字段暴露 owner 内部状态。
- 把 `HashSet` 用成 key-value 状态表。
- 在 hot path 中反复 `StringName.ToString()` 后用 string 比较。

## 推荐的下一步

1. 恢复 `dotnet build magic.csproj` baseline。
2. 收尾 `WorldMapGridSystem` 测试边界，并把对应 GDScript regression 迁为 C#。
3. 迁移 `WorldMapFogSystem`。
4. 迁移 `WorldTimeSystem`。
5. 开始 settlement typed request/result 设计，不直接动完整 `GameRuntimeSettlementCommandHandler`。

这条顺序能先打通小切片闭环，再进入大协调器。大协调器不是不能动，只是要先把刀磨利一点。
