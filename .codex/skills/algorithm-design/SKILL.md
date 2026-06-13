---
name: algorithm-design
description: Design algorithms and implementation plans for this Godot 4.6 C# repository before coding. Use when a request needs problem modeling, option comparison, state ownership decisions, AI or battle logic design, world-system changes, progression/data modeling, performance-sensitive logic, GodotSharp scene/runtime boundary decisions, or turning a prompt in prompts/ into a concrete implementation slice.
---

# Algorithm Design

Use this skill to turn a feature request into a repo-grounded design packet. Keep the result concrete. Do not drift into generic algorithm surveys.

## Workflow

1. Rebuild the local context first.
- Read the relevant prompt in `prompts/` if the request came from one.
- Read `docs/design/project_context_units.md` first as the repo's architecture loading index.
- Use it to choose the relevant context units, ownership boundaries, adjacent modules, and preferred file-loading scope.
- Do not treat it as the implementation truth source. After choosing scope, load the actual owner code, data, tests, and design docs.
- Read only the files that own the behavior.
- Read [references/repo-architecture.md](references/repo-architecture.md) when the affected ownership boundaries are unclear.

2. Frame the problem before proposing code.
- State the requested behavior, hard constraints, and non-goals.
- List where state lives today, where orchestration lives today, and what UI or save/load paths depend on it.
- Name the invariants that must remain true after the change.
- Name any fixed value sets, modes, tags, or schema constraints and identify the enum, typed rule utility, value object, or typed DTO that should own them.
- Name the existing tests that would catch a bad design, if any.

3. Compare implementation options.
- Produce 2 or 3 viable options for non-trivial changes.
- For each option, state where data lives, where logic lives, what files change, and the main failure mode.
- For each option, state how constraints are represented. Prefer C# enums, typed converters, typed rule utilities, value objects, and typed request/result DTOs over ad hoc strings, public `HashSet<StringName>` lists, or `Godot.Collections.Dictionary` validation.
- Prefer options that keep domain rules out of oversized coordinators such as `GameRuntimeFacade` and `WorldMapSystem` unless those nodes truly own the behavior.

4. Choose the smallest durable slice.
- Pick the option with the clearest ownership and the lowest coupling.
- Prefer adding or extending small services or state objects over stuffing more logic into UI nodes.
- Prefer typed C# owners and service APIs for formal runtime state. Use `Godot.Collections` payloads only at scene/resource/projection boundaries unless the existing owner already requires them.
- Prefer enums for closed domains. If Godot resources must expose `StringName`, pair that boundary field with an internal enum/typed converter and keep validation in the typed owner.
- Prefer strong types for multi-field constraints. Add a small options/result/value object instead of passing unrelated primitives or dictionary payloads through runtime code.
- Keep scenes, scripts, and data in the top-level folders required by `AGENTS.md`.
- Keep scene and script naming aligned.

5. Stress-test the design before coding.
- Check runtime cost for `_process`, `_draw`, world scans, battle scans, and AI loops.
- Check dictionary key stability, `StringName` usage, `Vector2I` usage, typed collection ownership, Godot projection boundaries, and cache rebuild points.
- Check that new constraints are enforced by enum conversion, typed parsing, or typed value validation, not repeated string comparisons or copied whitelist sets.
- Check save/load or historical data compatibility if stored state changes.
- Check scene-script contracts, C# exported members, callable/signal boundaries, and autoload interactions.
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
  State whether `docs/design/project_context_units.md` stays valid as-is as an architecture loading index or must be updated.
  Update it only if the design changes repo ownership boundaries, main runtime chains, context-unit responsibilities, or recommended read-sets.
  Do not put field-level implementation notes, migration status, or regression-script inventories into that file.

## Notes

- Collapse the option-comparison step for tiny bug fixes, but still explain why the direct fix is safe.
- If the user wants implementation rather than brainstorming, do the minimum design work needed to avoid a wrong edit, then move into coding.
