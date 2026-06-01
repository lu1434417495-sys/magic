# C# 迁移约束

更新日期：`2026-06-02`

## 目标

- 运行时核心使用 C# 强类型，不再依赖 `Variant`、`GodotObject.Get()`、`Call()`、`GdInterop`。
- Godot 动态类型只允许留在 scene、Resource、save、UI、headless/test 边界。
- 生产代码迁移到 C# typed 形态时，对应测试必须同步迁为 C# runner。
- 每次只迁一个可验证切片，先展示改法，确认后改代码。

## 硬约束

1. 核心逻辑禁止新增 `GdInterop`、`Variant`、`GodotObject.Get()`、`GodotObject.Call()`。
2. 业务状态禁止用 `Godot.Collections.Dictionary` / `Array` 持有。
3. `Godot.Collections.*` 只允许在 Godot export、Resource、scene API、save/UI/test 适配层使用。
4. `GodotObject` 不能作为万能业务模型；必须换成真实类型、接口或 DTO。
5. 数据包不继承 `RefCounted`，不加 `[GlobalClass]`，除非确实需要 Godot 生命周期或编辑器全局类型。
6. 旧 dictionary / Variant payload 兼容不能默认添加；涉及旧存档、旧 schema、旧测试必须先确认。
7. 对应 `.gd` regression 必须跟随业务切片迁为 `.cs`，否则切片不算完成。
8. 测试不得要求 production 代码重新暴露 private state 或 test hook。
9. UI 暂缓时，不阻塞非 UI runtime 清理；但 UI 不能继续向已迁移 service 传 raw dictionary。
10. 不允许新建 `Interop2` / `VariantUtils2` 之类 helper 变相延续动态读取。

## 允许保留的 Godot 类型

- `Node`、`Control`：scene/UI 边界。
- `Resource`：`.tres` 静态内容。
- `StringName`：稳定 ID、Godot 资源 ID、热路径 key。
- `Vector2I`：格子坐标、footprint、chunk。
- `Texture2D`、`Color`、`TileSet` 等展示资源。

## 类型替换

| 旧形态 | 新形态 |
| --- | --- |
| `Godot.Collections.Dictionary` 业务状态 | `Dictionary<TKey,TValue>` |
| `Godot.Collections.Array<T>` 业务列表 | `List<T>` / `IReadOnlyList<T>` / `T[]` |
| `Variant` metadata | typed DTO |
| `GodotObject` 参数 | 真实类型 / interface / DTO |
| `.Get("field")` | 直接字段/属性 |
| `.Call("method")` | 直接方法 / interface |
| `Callable` | delegate / interface |
| 自定义 Godot signal | C# event |
| `RefCounted` 数据包 | plain C# class / struct |
| 不需要全局注册的 `[GlobalClass]` | 移除 `[GlobalClass]` |

## Godot 继承清理

- runtime DTO、state、result、request、metadata 默认用 plain C# class / struct。
- 只有被 `.tscn` 挂脚本、需要 scene tree 生命周期时才继承 `Node` / `Control`。
- 只有需要 `.tres`、export、Inspector 编辑或 ResourceLoader 加载时才继承 `Resource`。
- 只有确实要作为 Godot object 穿过 Godot API、并依赖 Godot 引用生命周期时才保留 `RefCounted`。
- `[GlobalClass]` 只给需要出现在 Godot 编辑器、`.tres`、`.tscn` 或 Godot 类型注册表里的类型。
- 纯 C# 内部 helper/state/DTO/result/request 禁止 `[GlobalClass]`。
- 移除 `RefCounted` 后同步删除 `partial`、Godot factory、`create()` 这种 GDScript 时代构造入口，改 C# constructor。
- 切片完成时，目标核心文件不应再有无理由的 `: RefCounted`、`: GodotObject`、`[GlobalClass]`。

## 字典清理

- 运行时状态表：改为 `Dictionary<TKey,TValue>`；只查存在性时用 `HashSet<T>`。
- 请求/结果/metadata：改为 typed DTO，不用 dictionary 传字段。
- 对外展示或存档需要 dictionary 时，只在 adapter/serializer/UI 边界生成。
- 生成出的 dictionary 禁止在核心逻辑里再回读，不能做 typed -> dictionary -> typed 往返。
- 删除旧 dictionary fallback；需要兼容旧 payload 必须先确认。
- key 选择：ID 用 `StringName`；纯 C# 字符串 key 用 `string` + `StringComparer.Ordinal`；坐标用 `Vector2I`。
- 对外暴露集合时用查询方法、`IReadOnlyList<T>`、`IReadOnlyDictionary<TKey,TValue>`，不要暴露可写字典。
- 切片完成时，目标核心文件不应再有 `GDictionary` alias、`GdInterop.GetDictionary()`、`GdInterop.TryGet()` 读取业务字段。

## 切片流程

每次迁移固定按这个顺序：

1. 选范围：文件、调用点、是否涉及 UI/save/test。
2. 展示改法：旧字典形态、新 typed 形态、会删哪些 `GdInterop`、会破哪些测试。
3. 确认后改 production 代码。
4. 同步把对应 `.gd` regression 迁为 `.cs` runner。
5. 运行 `dotnet build magic.csproj` 和该域最小回归。
6. 记录结果：改了什么、测试迁移了什么、剩余动态边界是否合理。

没有对应测试时，结果里必须写明“无对应测试”，并说明是否需要新增 C# regression。

## 测试约束

- 新增或重写测试默认写 C#。
- 迁移 production 切片时，直接关联的 `.gd` 测试必须同切片改成 `.cs`。
- 旧测试如果直接写 private dictionary、调用旧 `create()`、构造旧 Variant/dictionary payload，必须改成 public behavior 断言。
- 被 C# runner 覆盖的旧 `.gd` 入口要删除或停用，避免维护两套测试。
- 只有 scene smoke、截图、benchmark/simulation、Godot 启动脚本限制这类特殊情况允许暂留 GDScript，并且必须写明原因。

## 完成定义

一个切片完成必须同时满足：

- 目标文件不再有计划删除的动态调用点。
- 不新增 `GdInterop`、`Variant`、`GodotObject.Get()`、`.Call()`。
- 业务状态已换成 C# collection / DTO / state。
- 不需要 Godot 注册/生命周期的类型已移除 `RefCounted`、`GodotObject`、`[GlobalClass]`。
- 核心逻辑没有 dictionary round-trip 或 dictionary fallback。
- owner 清楚，外部不能直接写 private runtime state。
- 对应测试已迁为 C# runner，或明确无对应测试。
- 旧 `.gd` 测试不再依赖被移除的动态边界。
- 构建通过；若失败，必须确认 blocker 与本切片无关。
- 未引入未经确认的旧兼容逻辑。

## 阶段顺序

1. 恢复 `dotnet build magic.csproj` baseline。
2. world 小切片：`WorldMapGridSystem` 测试收尾、`WorldMapFogSystem`、`WorldTimeSystem`。
3. 小型非 UI service：`AttributeService`、小型 battle rules。
4. settlement typed request/result，再处理 `GameRuntimeSettlementCommandHandler`。
5. progression / inventory registry 和 service typed 查询。
6. battle core state / metadata。
7. battle runtime resolver，按 resolver 分片。
8. AI action / trace / scoring typed 化。
9. UI typed view model。
10. persistence / snapshot / headless，save 兼容另行确认。

## 统计命令

```powershell
$patterns = @(
    'GdInterop\.',
    'GdInterop\.(GetDictionary|TryGet)',
    '\bVariant\b',
    '\bGodotObject\b',
    'Godot\.Collections\.(Dictionary|Array)',
    '\bGDictionary\b',
    '\[GlobalClass\]',
    ':\s*(RefCounted|GodotObject)\b',
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

热点文件：

```powershell
rg -n --glob '*.cs' "GdInterop\." scripts tests |
    ForEach-Object { ($_ -split ':')[0] } |
    Group-Object |
    Sort-Object Count -Descending |
    Select-Object -First 30 Count,Name |
    Format-Table -AutoSize
```

## 常用验证

```powershell
dotnet build magic.csproj
python tests/run_regression_suite.py
godot --headless --script tests/world_map/runtime/run_world_map_runtime_proxy_regression.gd
godot --headless --script tests/progression/core/run_progression_tests.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
```

默认不要运行 battle simulation、balance simulation、benchmark，除非任务明确要求。

## 下一步

1. 处理当前 build blocker。
2. 收尾 `WorldMapGridSystem` 对应测试，并迁为 C#。
3. 迁 `WorldMapFogSystem`。
4. 迁 `WorldTimeSystem`。
5. 进入 settlement typed request/result 设计。
