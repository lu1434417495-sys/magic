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

- `run_ai_debate.ps1`
- `run_ralph_loop.py`
- `run_ralph_review_loop.py`

`scripts/utils/` 继续保留给游戏运行时共享工具和紧贴项目内容生产的脚本，例如 `generate_canyon_tiles.ps1`。
