---
name: algorithm-design
description: Design algorithms and implementation plans for this Godot repository before coding. Use when a request needs problem modeling, option comparison, state ownership decisions, AI or battle logic design, world-system changes, progression/data modeling, performance-sensitive logic, or turning a prompt in prompts/ into a concrete implementation slice.
---

# Algorithm Design

Use this skill to turn a feature request into a repo-grounded design packet. Keep the result concrete. Do not drift into generic algorithm surveys.

## Workflow

1. Rebuild the local context first.
- Read the relevant prompt in `prompts/` if the request came from one.
- Read `docs/design/project_context_units.md` first to rebuild the current repo context units, ownership boundaries, and preferred file-loading scope.
- Read only the files that own the behavior.
- Read [references/repo-architecture.md](references/repo-architecture.md) when the affected ownership boundaries are unclear.

2. Frame the problem before proposing code.
- State the requested behavior, hard constraints, and non-goals.
- List where state lives today, where orchestration lives today, and what UI or save/load paths depend on it.
- Name the invariants that must remain true after the change.
- Name the existing tests that would catch a bad design, if any.

3. Compare implementation options.
- Produce 2 or 3 viable options for non-trivial changes.
- For each option, state where data lives, where logic lives, what files change, and the main failure mode.
- Prefer options that keep domain rules out of oversized coordinators such as `GameRuntimeFacade` and `WorldMapSystem` unless those nodes truly own the behavior.

4. Choose the smallest durable slice.
- Pick the option with the clearest ownership and the lowest coupling.
- Prefer adding or extending small services or state objects over stuffing more logic into UI nodes.
- Keep scenes, scripts, and data in the top-level folders required by `AGENTS.md`.
- Keep scene and script naming aligned.

5. Stress-test the design before coding.
- Check runtime cost for `_process`, `_draw`, world scans, battle scans, and AI loops.
- Check dictionary key stability, `StringName` usage, `Vector2i` usage, and cache rebuild points.
- Check save/load or historical data compatibility if stored state changes.
- Check scene-script contracts, signal boundaries, and autoload interactions.
- Check whether headless snapshot and regression-test entry points remain stable.

6. Finish with an implementation packet.
- `Problem`
- `Current Ownership`
- `Options`
- `Recommended Design`
- `Minimal Slice`
- `Files To Change`
- `Tests To Add Or Run`
- `Project Context Units Impact`
  State whether `docs/design/project_context_units.md` stays valid as-is or must be updated.
  If the design changes repo ownership boundaries, main runtime chains, preferred read-sets, or core context units, update `docs/design/project_context_units.md` in the same task.

## Notes

- Collapse the option-comparison step for tiny bug fixes, but still explain why the direct fix is safe.
- If the user wants implementation rather than brainstorming, do the minimum design work needed to avoid a wrong edit, then move into coding.
