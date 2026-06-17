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

- `run_ai_debate.py`
- `run_ralph_loop.py`
- `run_ralph_review_loop.py`
- `architecture_checks.py`

`architecture_checks.py` 目前是 report-only 架构边界扫描器。`ToDictionary` 检查会跳过明确处在投影/输出边界的文件名片段，例如 `Projection`、`Payload`、`Snapshot`、`Summary`、`Trace`、`Registry`、`Def.cs` 和 `State.cs`，把注意力留给更可能泄漏 Godot 字典的核心运行时代码。

`scripts/utils/` 继续保留给游戏运行时共享工具和紧贴项目内容生产的脚本，例如 `generate_canyon_tiles.py`。
