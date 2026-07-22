# C# 迁移 Goal 约束

更新日期：`2026-06-03`

## 使用方式

用户只描述迁移目标，不需要指定文件。

Codex 负责自行判断：

- 该目标涉及哪些 `scripts/` 生产代码。
- 哪个迁移包边界清晰、收益足够、风险可控。
- 哪些 Godot 动态边界应一起清理。
- 哪些直接受影响的 `.gd` regression 需要同步迁为 C#。
- 应运行哪些构建和回归验证。

Codex 应自主执行可验证迁移包，不需要用户逐个确认。只有涉及 save/schema 兼容、UI 暂缓范围、高风险大改、或必须触碰 unrelated dirty changes 时，才先说明风险并等待确认。

## 执行目标

整体目标是把项目运行时核心收敛为 C# typed 形态。

每个迁移 goal 都应以 `scripts/` 生产代码动态边界净减少为主，在同一 owner / runtime 边界内批量清理：

- `GdInterop`
- `Variant`
- `GodotObject.Get()` / `.Set()` / `.Call()`
- `Godot.Collections.Dictionary` / `Array` 业务状态
- `GDictionary` alias 和 dictionary fallback
- 新增 `ToDictionary()` 兼容桥
- 不必要的 `RefCounted`
- 不必要的 `[GlobalClass]`
- 直接受影响的 GDScript regression

## 执行边界

允许保留 Godot 类型的地方：

- scene / UI 节点：`Node`、`Control`
- `.tres` 静态内容：`Resource`
- Godot 稳定 ID：`StringName`
- 格子坐标：`Vector2I`
- 展示资源：`Texture2D`、`Color`、`TileSet`
- save / UI / headless / test 的最外层适配

不允许留在核心逻辑里的东西：

- 用 `GodotObject` 当万能业务模型
- 用 `Variant` 或 dictionary 传 request / result / metadata
- typed -> dictionary -> typed 的往返
- 把 `GdInterop` / `GDictionary` 改成 `GodotObject.Set()` 直调
- 新增 `ToDictionary()` 桥接后再被核心逻辑消费
- 为旧 payload 自动添加 fallback / migration / alias
- 为测试重新暴露 private state 或 test hook

## 反增长红线

- `.Set()` 增长视为迁移失败信号；不能用 `GodotObject.Set()` 直调替代 `GdInterop` 或 `GDictionary`。
- `ToDictionary()` 增长视为兼容桥扩散信号；不能在核心逻辑新增显式 dictionary 转换桥。
- 允许的 `ToDictionary()` 只限 adapter / serializer / UI / public Godot API 投影，且不得被核心逻辑回读。
- 迁移包完成时，`scripts/` 中 `.Set()` 与 `ToDictionary()` 不应净增长；确有边界投影新增必须说明位置和原因。

## 字典清理要求

- 运行时状态表改为 `Dictionary<TKey,TValue>`。
- 只查存在性时用 `HashSet<T>`。
- request / result / metadata 改为 typed DTO。
- 对外需要 dictionary 时，只在 adapter / serializer / UI 边界投影。
- 投影出来的 dictionary 不允许被核心逻辑回读。
- 不允许用 `.Set()` 直调或新增 `ToDictionary()` 桥来替代字典清理。
- key 选择由 Codex 判断并说明：常见规则是 ID 用 `StringName`，纯 C# 字符串 key 用 `string + StringComparer.Ordinal`，坐标用 `Vector2I`。
- 对外集合只暴露查询方法、`IReadOnlyList<T>` 或 `IReadOnlyDictionary<TKey,TValue>`。

## 继承清理要求

- runtime DTO / state / result / request / metadata 默认是 plain C# class / struct。
- 只有 scene tree 生命周期需要时才继承 `Node` / `Control`。
- 只有 `.tres`、export、Inspector 或 ResourceLoader 需要时才继承 `Resource`。
- 只有确实要穿过 Godot API 并依赖 Godot 引用生命周期时才保留 `RefCounted`。
- `[GlobalClass]` 只给需要进入 Godot 编辑器、`.tres`、`.tscn` 或 Godot 类型注册表的类型。
- 移除 `RefCounted` 时同步移除无意义的 `partial`、Godot factory、旧 `create()` 构造入口。

## 测试清理要求

- 测试迁移服务于生产代码迁移，不单独作为主要产出。
- 生产代码迁移时，只处理直接依赖被删除边界、会被本迁移包破坏、或直接验证本迁移包行为的 regression。
- 新增或重写测试默认写 C#。
- 旧测试如果依赖 private dictionary、旧 `create()`、Variant/dictionary payload，必须改为 public behavior 断言。
- 被 C# runner 覆盖的旧 `.gd` 入口应删除或停用。
- 不为了降低 `.gd` 数量做纯测试迁移；若一个 goal 的主要改动集中在测试，应重新选择迁移包。
- 不迁移邻近但未受影响的 `.gd` 测试；测试目录清扫另行处理，不混入生产迁移 goal。
- 只有 scene smoke、截图、benchmark/simulation、Godot 启动脚本限制这类特殊情况允许暂留 GDScript，并且必须说明原因。

## Codex 选择口径

Codex 自主选择迁移包，优先级如下：

- 非 UI、非 save/schema 优先，除非目标明确要求。
- 同一 owner / 同一 runtime 边界优先。
- 生产代码动态边界清理收益优先。
- 能同步迁移直接受影响测试的迁移包优先。
- 批量清理一组强相关动态边界，避免只改零散单点。
- 可同时处理同一 service / system / helper 及其 DTO、state、adapter、测试。
- 大文件可以按方法族、DTO 族、resolver 输入输出拆包，但同包内要闭环处理相关调用点和测试。
- 工作区有 unrelated dirty changes 时，避开；无法避开时先说明。
- 不因过度追求“最小”制造重复扫描、重复构建、重复测试迁移。

## 效率口径

- 迁移收益只以 `scripts/` 中动态边界净减少为主指标，`tests/` 计数下降不算主收益。
- 有效迁移包必须减少 `scripts/` 中至少一类动态边界；只改测试不是有效迁移包。
- 优先选择能一次清掉一类生产边界的迁移包，而不是只清一个调用点。
- 单次 goal 应覆盖足够多的生产相关调用点，除非该单点是后续批量迁移的前置阻塞。
- `.Set()` 或 `ToDictionary()` 净增长时，该迁移包不计为有效去兼容化。
- 如果发现候选过碎，应主动合并同 owner 的 helper、state、DTO、测试一起处理。
- 如果预计改动主要集中在测试，说明生产清理收益不足，应换迁移包。
- 批量迁移仍必须保持 owner 清晰，不跨 save/schema/UI 高风险边界混改。

## 完成口径

一个 goal 迁移包完成时必须满足：

- 本迁移包内计划清理的动态边界已移除，且未新增同类边界。
- `scripts/` 生产代码动态边界有明确净下降。
- 业务状态、请求、结果、metadata 已改为 C# typed 形态。
- 不必要的 `RefCounted` / `GodotObject` / `[GlobalClass]` 已移除。
- 直接受影响的 regression 已迁为 C# runner，或确认没有受影响测试。
- `scripts/` 中 `.Set()` 与 `ToDictionary()` 没有净增长，或只在边界投影中说明例外。
- 必要构建和相关回归已执行；失败时说明是否与本迁移包相关。
