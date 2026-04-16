# Ralph Guardrails

- 默认从 `docs/design/project_context_units.md` 和相关 `AGENTS.md` 开始重建仓库上下文。
- 单轮只做一个最小可验证 story，不顺手扩 scope。
- 失败时优先把具体坑写回这里，而不是只留在会话上下文里。

- 2026-04-17T01:08:02+08:00 | PVS_01A | run=20260417-010756-PVS_01A
  failure: Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-010756-PVS_01A.stderr.log

- 2026-04-17T01:10:22+08:00 | PVS_01A | run=20260417-011017-PVS_01A
  failure: Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-011017-PVS_01A.stderr.log

- 2026-04-17T01:10:48+08:00 | PVS_01A | run=20260417-011044-PVS_01A
  failure: Codex exec failed. stderr: E:\game\magic\.ralph\runs\20260417-011044-PVS_01A.stderr.log

- 2026-04-17T01:20:59+08:00 | PVS_01B | run=20260417-011631-PVS_01B
  failure: Codex returned blocked: No code changed. I rebuilt context from `docs/design/project_context_units.md` and the repo AGENTS guide, then inspected the accessible snapshot files, but I could not safely patch the story because local workspace shell access is failing and the accessible GitHub `main` copy does not contain the forge modal code referenced by `PVS_01B`.
