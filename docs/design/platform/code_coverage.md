# Godot C# 代码覆盖率

> 状态：当前实现真相
> 更新日期：2026-07-28

## 正式入口

从仓库根目录运行：

```powershell
python tests/run_coverage_suite.py --jobs 8
```

额外的普通回归参数放在 `--` 之后：

```powershell
python tests/run_coverage_suite.py --jobs 8 -- --pattern counterattack
```

默认报告写入 `artifacts/coverage/coverage.cobertura.xml`。该目录是本地生成物，不进入 Git。

CI 使用同一入口执行 strict full suite，并在任务结束后上传 `production-code-coverage` artifact；CI 不再另跑一轮重复的无覆盖率 full suite。

## 采集边界

工具版本固定在 `.config/dotnet-tools.json`。入口依次执行：

1. `dotnet tool restore`
2. `dotnet build magic.csproj`
3. 使用唯一 session 静态插桩 `.godot/mono/temp/bin/Debug/magic.dll`
4. 启动 `dotnet-coverage` server
5. 复用 `tests/run_regression_suite.py` 并发执行 Godot 测试
6. shutdown collector，生成 Cobertura
7. 在 `finally` 中 uninstrument，恢复原始 DLL

Godot 是原生宿主，普通动态 profiler 不能可靠附着其托管程序集，因此正式入口使用 Microsoft 支持的静态托管插桩。采集期间不能并行执行另一轮 build 或覆盖率任务；否则会替换已插桩程序集或争用同一输出。

## 分母与结果

`tests/coverage.runsettings` 只包含 `magic.dll`，并从 source 分母排除：

- `tests/`
- `tools/`
- `.godot/`

因此输出的 line coverage 是 production C# 行覆盖率，不把测试实现、架构工具或 Godot 生成物算入分母。入口会拒绝空报告、零 production sequence point，以及含有被排除源码的报告。

当前 Microsoft Cobertura 输出不提供 `branches-valid` / `branches-covered`。入口只报告可验证的行覆盖率，并把分支覆盖率明确标记为 unavailable；不能把 Cobertura 的默认 `branch-rate="1"` 解读为 100%。
