# Codex CLI Ralph Loop

本仓库内置了一套适配 PowerShell 与 Codex CLI 的 Ralph loop 模板，用于把 backlog 切成最小 story，然后按“单 story -> 新开 Codex 上下文 -> 跑检查 -> 写回状态 -> 可选自动提交”的循环推进。

## 入口

- 主脚本：[run_ralph_loop.ps1](</E:/game/magic/tools/run_ralph_loop.ps1>)
- 状态目录：[`/.ralph`](</E:/game/magic/.ralph/README.md>)

## 设计取向

- 适配当前 Windows / PowerShell 工作流，而不是硬塞 Bash 脚本。
- 默认保守：要求起步工作树干净，失败后停止，不做 destructive reset。
- 真正的长期记忆落在磁盘与 git，而不是长会话上下文。
- 用 `codex exec --output-schema` 约束最终输出，减少 brittle parsing。
- 默认把 `$godot-master` 与 `$algorithm-design` 固定写进每轮 prompt，作为仓库级默认 skill。

## 快速开始

1. 编辑 [`/.ralph/prd.json`](</E:/game/magic/.ralph/prd.json>)，填入真实 story。
2. 视需要调整 [`/.ralph/checks.ps1`](</E:/game/magic/.ralph/checks.ps1>)。
3. 运行：

```powershell
pwsh -File tools/run_ralph_loop.ps1 -MaxIterations 5
```

## 常见参数

- `-StoryId <ID>`：只跑指定 story。
- `-NoCommit`：通过校验后不自动提交。
- `-SkipChecks`：跳过外层统一 checks。
- `-CodexModel <MODEL>`：显式指定模型；默认不传，走当前 Codex 默认模型。
- `-ContinueAfterFailure`：失败后继续 loop。
- `-PersistCodexSessions`：不传 `--ephemeral`，保留 Codex 会话文件。

## 说明

- 这套 loop 不改变游戏运行时边界，只是项目级开发自动化。
- 如果未来 Codex CLI 参数形态变化，优先调整 `Invoke-CodexIteration`。
- 如果要改默认固定 skill，编辑 [run_ralph_loop.ps1](</E:/game/magic/tools/run_ralph_loop.ps1>) 顶部的 `$script:DefaultRalphSkills`。
