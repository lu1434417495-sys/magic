# Ralph Loop

这个目录保存项目内的 Codex CLI Ralph loop 状态与模板文件。

## 文件

- `prd.json`
  Ralph loop 当前要消费的 backlog。默认提供空骨架，真正运行前先填写。
- `prd.example.json`
  一个贴近本仓库的示例 story，可直接复制到 `prd.json` 后再改。
- `checks.ps1`
  外层编排器的统一校验入口。默认跑本项目最常用的几条 Godot headless 回归。
- `output_schema.json`
  约束 `codex exec --output-schema` 最终输出形状，避免编排器靠脆弱字符串解析。
- `runs/`
  每轮生成的 prompt、Codex JSONL 事件流、最终消息和 checks 日志。已在 `.gitignore` 中忽略。

## `prd.json` 约定

```json
{
  "branchName": "feat/example-story-branch",
  "maxAttemptsPerStory": 3,
  "commitPrefix": "chore(ralph):",
  "userStories": [
    {
      "id": "STORY_1",
      "title": "实现某个最小可验证切片",
      "status": "open",
      "acceptanceCriteria": [
        "验收条件 A",
        "验收条件 B"
      ],
      "notes": [
        "补充背景"
      ]
    }
  ]
}
```

`branchName` 是可选的；为空时，loop 不会主动切换分支。

支持的状态：

- `open`
- `in_progress`
- `done`
- `blocked`

编排器会回写这些字段：

- `attemptCount`
- `startedAt`
- `lastAttemptAt`
- `completedAt`
- `lastRunId`
- `lastFailure`
- `lastChecksRun`
- `lastLearnings`

## 运行

先把 `prd.example.json` 复制或整理成 `prd.json`，再执行：

```powershell
python tools/run_ralph_loop.py -MaxIterations 5
```

常用参数：

- `-StoryId <ID>`：只跑某一个 story
- `-NoCommit`：通过校验后不自动提交
- `-SkipChecks`：跳过 `.ralph/checks.ps1`
- `-CodexModel <MODEL>`：显式指定模型；默认不传，走本机 Codex 默认模型
- `-PersistCodexSessions`：不传 `--ephemeral`
- `-AllowDirtyWorktree`：允许在起步时工作树非干净
- `-ContinueAfterFailure`：失败后继续 loop；默认失败即停
- `-PrdPath <PATH>`：显式指定 PRD 文件
- `-PruneDoneStories`：删除 `status = done` 的 story 并退出

## 默认固定 Skills

当前 loop 会在每轮 prompt 中固定显式带上：

- `$godot-master`
- `$algorithm-design`

这属于“把 skill 固定写进全局模板”的做法。要修改默认 skill 列表，直接编辑 [run_ralph_loop.py](</E:/game/magic/tools/run_ralph_loop.py>) 顶部的 `DEFAULT_RALPH_SKILLS`。

## 默认安全策略

- 起步要求工作树干净，避免把 loop 改动和手工改动混在一起。
- 默认每轮通过后自动提交；失败时不做 destructive reset。
- 默认失败即停，让你先检查 dirty worktree，再决定是否继续。

## 适配 Codex CLI

当前模板基于本机可用的这些命令形态：

- `codex exec`
- `codex exec --json`
- `codex exec --output-schema`
- `codex exec -o`
- `codex exec --sandbox`
- `codex exec --ephemeral`

如果本地 Codex CLI 参数形态将来变化，只需要改 `tools/run_ralph_loop.py` 里的 `invoke_codex_iteration()`。
