# Tools

这个目录放仓库级的开发与编排脚本。

适合放在这里的脚本：

- AI 编排器
- 开发辅助 CLI 包装器
- 与具体运行时模块无关的 repo-level 自动化

不适合放在这里的脚本：

- Godot 运行时会加载或依赖的脚本
- 明显绑定某个游戏子系统、资源流水线或场景数据的脚本

当前示例：

- `dev_check.py`
- `run_ai_debate.py`
- `run_ralph_loop.py`
- `run_ralph_review_loop.py`
- `architecture_checks.py`

## 规范开发 CLI

`magic_dev.py` 是仓库级检查与验证的规范入口。它直接以 Python 子进程参数调用 Git、.NET 和 Godot，不依赖 PowerShell 管道，并提供机器可读的能力声明。默认不运行 BattleSim、benchmark 或 E2E。

```powershell
# Codex 在选择命令前读取这一能力声明
python tools/magic_dev.py capabilities --json

# 工作树、分支、上游、ahead/behind、冲突与 diff check
python tools/magic_dev.py status

# 根据 staged、unstaged、untracked 选择保守的聚焦验证目标
python tools/magic_dev.py affected

# 运行一个 Python tooling test 或 Godot 回归路径
python tools/magic_dev.py test --path tests/battle_runtime/skills

# 验证当前影响面；未知源码路径会自动升级为 routine full
python tools/magic_dev.py verify

# 常规全量；并行度默认是逻辑 CPU 的一半，最多 16
python tools/magic_dev.py full
```

`affected --base <ref>` 还会纳入 `<ref>...HEAD` 的已提交改动。`test` 只有在显式路径命中 simulation 或 benchmark 时才允许传 `--include-simulation` / `--include-benchmarks`。E2E 继续使用独立的 `tests/run_e2e_suite.py`，不由此 CLI 隐式纳入。

`dev_check.py` 是早期实验入口，不是 Codex 或日常开发的规范入口。

`architecture_checks.py` 目前是 report-only 架构边界扫描器。动态 `.Call/.Set` 检查会跳过已知 typed helper 与 AI stable snapshot builder，保留真正的 Godot 动态属性写入。`ToDictionary` 检查会跳过明确处在投影/输出边界的文件名片段，例如 `Projection`、`Payload`、`Snapshot`、`Summary`、`Trace`、`Registry`、`Def.cs` 和 `State.cs`。`GDictionary` 字段检查会跳过 UI、dev tools、persistence/headless、battle sim、内容 catalog/registry 和投影类文件，把注意力留给更可能泄漏 Godot 字典的核心运行时代码。

`scripts/utils/` 不再承载 C# runtime owner；内容资源、状态、presentation 与平台基础设施分别放入对应 `scripts/systems/**` / `scripts/ui/` 目录。目前仅保留不进入 Godot C# 运行时的内容生产脚本，例如 `generate_canyon_tiles.py`。
