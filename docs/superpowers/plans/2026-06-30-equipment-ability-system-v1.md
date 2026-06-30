# Equipment Ability System V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the V1 equipment ability framework described by `docs/design/weapons/equipment_ability_system.md` in reviewable slices, while keeping current battle commands, save writeback, AI, and content validation stable at every step.

**Architecture:** Treat the main document as the rule source and the two sub-documents as authoritative prerequisite designs. Implement the framework through feasibility-gated vertical slices: first command identity and durability commit contracts, then content ABI, projection, runtime state, dispatcher, writeback, and representative content.

**Tech Stack:** Godot 4.6 C#, `.tres` Resource authoring ABI, plain C# runtime DTOs/services, headless Godot regression scripts, `dotnet build magic.csproj`.

## Global Constraints

- Read `docs/design/project_context_units.md` before implementation and update it after code changes if runtime relationships, ownership boundaries, or recommended read sets change.
- Do not add compatibility logic, legacy aliases, fallback migrations, or old payload/schema support without explicit user confirmation.
- Combat cannot save active battle state; V1 does not add active battle save/load.
- `known_active_skill_ids` remains known-only and must not receive equipment-granted skills.
- Equipment durability selected-target commit must not perform a second random equipment selection.
- Battle-only equipment ability state must be captured/restored by `BattleAiMutationGuard`.
- World-day and world-month equipment usage derive only from `WorldTimeSystem` and world map `world_step`.
- Full per-weapon `by_family` content conversion is outside V1; V1 proves representative mechanism families.

---

## Landing Verdict

The main design is implementable, but it is not safe to land as one large change. The two previously identified high-risk prerequisite topics are already resolved by the sub-documents below, so this plan treats them as fixed designs rather than open questions:

1. `docs/design/weapons/equipment_ability/battle_skill_availability_migration.md` resolves `SkillEntryId` first-class migration, including command identity, selection state, preview, execution, HUD, AI, and scoped auto-cast source rules.
2. `docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md` resolves equipment durability selector / selected-target commit split, including old spell effect parity, selected target revalidation, no second random roll, and writeback boundaries.

These designs still must be implemented first because later equipment ability modules depend on them. Full per-weapon content conversion remains separate from the framework landing.

## Current Feasibility Snapshot

This snapshot is from the current working tree on 2026-06-30. Re-check it before starting implementation because the workspace is dirty and several files already contain partial work.

| Check | Current result | Landing impact |
| --- | --- | --- |
| `dotnet build magic.csproj` | Passed with 0 warnings and 0 errors | The current workspace compiles, so feasibility blockers are behavioral/contractual rather than syntax-level. |
| `godot --headless -s res://tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs` | Failed with `System.Exception: debug select called` from `scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs` | The durability selector/commit design has partial code, but the implementation is not closed. Task 4 must clean this before downstream equipment actions depend on it. |
| `scripts/systems/battle/core/BattleCommand.cs` | Has `skill_id`, no `skill_entry_id` | `SkillEntryId` design is approved but not implemented in command identity. Tasks 1-3 remain real prerequisite implementation work. |
| `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs` | Has `selected_skill_id`, no `selected_skill_entry_id` | Manual selection cannot yet distinguish learned skill, equipment skill, or scoped auto-cast entry. |
| `rg known_active_skill_ids scripts tests` | Many battle, AI, HUD, text, and test consumers still read the known-only list directly | Availability migration must be broad; adding equipment grants without this migration will produce invisible or stale skills. |
| `scripts/player/warehouse/EquipmentInstanceState.cs` | Exact schema validation requires the current field count and rejects unsupported fields | World-day/month/persistent counters are a save/schema change, not a harmless field append. |
| `scripts/systems/world/WorldTimeSystem.cs` | Has `StepToDay`, no `StepToMonth` | Monthly equipment usage needs a deterministic month helper and tests. |
| `scripts/systems/battle/core/BattleUnitState.cs` | Has equipment view and effective traits, no `creature_type_tags` or equipment ability projection source state | Projection/source work must add battle-unit facts explicitly and update schema/AI snapshot policy. |
| `scripts/systems/battle/ai/BattleAiMutationGuard.cs` | Already snapshots equipment view, current durability, known skill ids, and effective traits | The guard has the right pattern, but new equipment ability stores must be added in capture/restore/stable-map together. |

## Feasibility Gate Policy

Every task has five mandatory gates:

1. **Design basis**: the exact design sections that must be read before editing.
2. **Current code facts**: the owner facts that must be true or refreshed in the working tree.
3. **Prerequisite gate**: conditions that must pass before the task starts.
4. **Implementation slice**: the smallest owner changes that make the task reviewable.
5. **Exit gate**: commands or focused regressions that prove the task can feed the next task.

If any prerequisite gate fails, do not continue by guessing. Update this plan or the source design first, then resume from the same task.

## V1 Scope

V1 includes:

- Content ABI and resource definitions for equipment abilities.
- Static registry and validator.
- Battle projection from player equipment and battle-only enemy equipment.
- First-class battle skill availability entries.
- Scoped auto-cast source validation.
- Equipment durability selector and selected-target commit.
- Runtime dispatcher with deterministic condition/action/dice/target hooks.
- Battle mark/state stores with AI rollback protection.
- Weapon profile overlay and attack/defense modifier hooks.
- Battle-only environment facts.
- World-day, world-month, and persistent equipment counters.
- Battle-end staged writeback for persistent equipment ability state.
- Minimal presentation/query surfaces and regression tests.

V1 excludes:

- Full conversion of every weapon family ability into runtime content.
- Reaction confirmation UI.
- Summons, zones, scheduled delayed effects, movement hooks, stealth, fleeing, disarm, repair, temporary disable.
- Active battle save/load.
- World-map weather.
- Public diagnostic and live MOD hot-reload ABI.
- Compatibility aliases or schema migration paths unless explicitly approved.

## Why This Plan Is Shorter Than The Main Document

This execution plan must not duplicate every design paragraph from `equipment_ability_system.md`. Its job is different:

- The main document is the architecture and rules source.
- The two sub-documents are the approved detailed designs for the two highest-risk prerequisite migrations.
- This plan is the build order, code owner map, validation map, and stop-condition contract.

The plan is only acceptable if every V1 design decision in the main document maps to an implementation task below. The traceability tables in the next sections are therefore part of the plan, not optional commentary.

## Design-To-Task Traceability

| Main document section | Execution task | Landing rule |
| --- | --- | --- |
| `目标` | Tasks 1-12 | Framework first; full weapon-family conversion is not required for V1 merge. |
| `当前系统边界` | Tasks 1, 4, 5, 7, 11 | Existing battle command, durability, equipment, and writeback boundaries are changed only through named owners. |
| `Trait 是外壳，Ability 是规则` | Tasks 6, 7, 10, 12 | Trait/binding exposes equipment ability; runtime rule handling lives in ability services. |
| `数据资源和运行时状态分离` | Tasks 5, 6, 8, 11 | Resource definitions, battle runtime state, and persistent equipment state remain separate stores. |
| `不为单件装备写专属代码` | Tasks 6, 9, 10, 12 | Representative content uses generic handler ids and payloads. |
| `MOD 边界` | Task 6 and Task 12 | V1 supports data resources and validation, not public external C# ABI or hot reload. |
| `V1 数据 ABI 与 V4 代码 ABI 边界` | Task 6 | Exported resource fields are the V1 authoring ABI; handler internals are not stable external ABI. |
| `存档兼容` | Tasks 1, 5, 11 | No active battle save/load and no compatibility aliases without approval. Persistent equipment fields are explicit. |
| `总体结构` | Tasks 6-12 | Static content, projection, runtime dispatch, mutation, overlay, environment, and presentation land as separate services. |
| `按代码 owner 的 V1 模块拆分` | All tasks | Each production module is tied to at least one task and test gate. |
| `装备能力绑定` | Tasks 6, 7, 12 | Binding definitions are content ABI and projection inputs, not battle runtime mutable state. |
| `Resource 层结构` | Task 6 | All exported resource classes and arrays land before content resources depend on them. |
| `Runtime definition DTO` | Tasks 6, 10 | Resource objects are normalized into runtime DTOs before dispatch. |
| `Handler spec` | Tasks 6, 10 | Handler id, validation, consumer support, state access, phase compatibility, and diagnostics are registered metadata. |
| `EquipmentAbilityContentRegistry` | Task 6 | Registry is the only runtime lookup entry for ability definitions and handler metadata. |
| `Battle projection` | Task 7 | Battle unit receives projected equipment ability sources; runtime does not read enemy template or warehouse directly. |
| `Battle skill availability` | Tasks 1-3, 10, 12 | `SkillEntryId` is command identity; granted skills become availability entries. |
| `Enemy attack equipment battle-only source` | Task 7 | Enemy equipment source is projected onto battle unit and discarded after battle. |
| `Runtime state owner` | Tasks 5, 8, 11 | Battle-local marks/counters/targets are separate from persistent per-equipment counters. |
| `AI mutation guard contract` | Task 8 | Every equipment ability runtime store must snapshot and restore during AI simulation. |
| `World-bound persistent equipment state` | Tasks 5, 11 | Day/month/permanent counters live on `EquipmentInstanceState` and commit at battle end. |
| `Execution context 和 mutation plan` | Tasks 8, 10, 11 | Actions return mutation plans/results; persistent writes are staged. |
| `触发点分类` | Tasks 7, 10, 11 | V1 implements equipment/projection, battle lifecycle, attack/damage/status/skill hooks, and world-bound usage only. |
| `条件分类` | Tasks 6, 9, 10 | Conditions are handler-backed and read facts from battle unit, equipment source, runtime state, or battle environment. |
| `动作分类` | Tasks 4, 5, 9, 10, 11 | Modifier, state, mark, skill, equipment, and world-bound actions have explicit mutation boundaries. |
| `核心服务` | Tasks 6-11 | Each service has a concrete task and focused regression target. |
| `战斗插入点` | Tasks 7, 9, 10 | Character trait projection, battle unit creation, attack checks, damage resolver, skill turn resolver, and change equipment hooks are integration points. |
| `展示结构` | Task 12 | Presentation reads summaries and availability entries; it does not infer hidden ability state. |
| `能力覆盖分类` | Task 12 plus deferred scope | Representative mechanisms prove framework; full per-weapon ledger remains outside V1. |
| `by_family 机制族覆盖矩阵` | Task 12 plus deferred scope | Cover one representative per V1 mechanism family, not every weapon entry. |
| `MOD 支持分析` | Task 6 and Task 12 | Data resource validation is in V1; diagnostics/ops tooling is deferred. |
| `V1 落地范围` | All tasks | Defines the inclusion boundary for this plan. |
| `V1.5/V2/V3 扩展` | Stop Conditions | If implementation requires excluded systems, stop rather than silently pulling them into V1. |
| `后续实现文件` | All tasks | Task file lists are the execution version of that section. |
| `测试策略` | Tests in each task | Every task includes a verification gate; final task runs broad regressions. |
| `可行性 / 架构审查结论` | Landing Verdict and Tasks 1-4 | The two high-risk prerequisite designs are already resolved in sub-docs and implemented first. |

## Sub-Document Traceability

| Sub-document | Execution tasks | Required interpretation |
| --- | --- | --- |
| `equipment_ability/README.md` | All tasks | Use as the local index for equipment ability design docs. |
| `battle_skill_availability_migration.md` | Tasks 1-3 and Task 10 | This is the authoritative design for `SkillEntryId`; do not invent a second command identity path. |
| `equipment_durability_selector_commit.md` | Task 4 and Task 10 | This is the authoritative design for selector / selected-target commit; no second random selection during commit. |
| `subagent_review_findings.md` | Stop Conditions plus task rules | Findings already accepted into design are treated as requirements; explicitly deferred findings stay out of V1. |

## Task-Specific Design Read Sets

Before implementing a task, read only the listed sections unless code exploration reveals a dependency outside the slice. Use heading search rather than fixed line ranges, because the design docs are still being edited.

| Task | Must read from `equipment_ability_system.md` | Must read from sub-docs |
| --- | --- | --- |
| Task 1: stable skill entry identity | `Battle skill availability`, `与当前代码的精确映射`, `战斗插入点`, `可行性 / 架构审查结论` | `battle_skill_availability_migration.md`: `目标`, `核心数据结构`, `Command and selection`, `推荐落地顺序` phases 1-2 |
| Task 2: known-skill availability parity | `Battle skill availability`, `核心服务`, `BattleRuntimeSkillTurnResolver`, `展示结构`, `测试策略 / Skill availability 测试` | `battle_skill_availability_migration.md`: `BattleSkillAvailabilityService API`, `排序、去重和失效`, `代码迁移矩阵` manual/runtime/range/HUD |
| Task 3: AI and scoped auto-cast | `Battle skill availability`, `Runtime state owner`, `AI mutation guard contract`, `触发点分类 / 技能和动作` | `battle_skill_availability_migration.md`: `Scoped auto-cast source gate`, `AI`, `Phase 4`, `Phase 7`, `必测回归` |
| Task 4: durability selector/commit | `动作分类 / 装备和物品`, `EquipmentMutationAdapter`, `BattleDamageResolver`, `测试策略 / 战斗行为测试` | `equipment_durability_selector_commit.md`: all sections from `结论` through `验收条件` |
| Task 5: world-bound persistent equipment state | `World-bound persistent equipment state`, `Battle-end staged commit pipeline`, `世界层`, `存档兼容`, `测试策略 / 存档测试` | `equipment_durability_selector_commit.md`: `Commit 语义` only if durability events also stage persistent mutations |
| Task 6: static content ABI and validator | `Resource 层结构`, `Runtime definition DTO`, `Handler spec`, `EquipmentAbilityContentRegistry`, `MOD 支持分析`, `内容校验测试` | `subagent_review_findings.md`: accepted ABI/export findings only |
| Task 7: projection sources | `Battle projection`, `Enemy attack equipment battle-only source`, `BattleUnitFactory`, `EquipmentAbilityProjectionService`, `EquipmentAbilitySourceLifecycleService`, `投影测试` | `battle_skill_availability_migration.md`: `Phase 8` only for later granted-skill plug-in shape |
| Task 8: runtime stores and AI guard | `Runtime state owner`, `Source-local charge/cooldown`, `Status-like effects`, `Target mark`, `Battle-level ability state`, `AI mutation guard contract` | `subagent_review_findings.md`: accepted state-protection findings |
| Task 9: overlays, modifiers, environment | `BattleEnvironmentSnapshot`, `EquipmentAttackDefenseModifierDef`, `EquipmentWeaponProfileOverlayDef`, `BattleAttackCheckPolicyService`, `BattleEnvironmentContextProvider` | none unless implementation touches granted skills or durability |
| Task 10: dispatcher and actions | `Execution context 和 mutation plan`, `触发点分类`, `条件分类`, `动作分类`, `BattleEquipmentAbilityDispatcher`, `EquipmentAbilityConditionEvaluator`, `EquipmentAbilityActionExecutor`, `EquipmentAbilityTargetSelectorResolver`, `DiceExpressionEvaluator` | `battle_skill_availability_migration.md`: `Phase 8`; `equipment_durability_selector_commit.md`: `Mutation Adapter Request`, `Mutation Result`, `Preview / AI / Snapshot` |
| Task 11: battle-end commit | `Battle-end staged commit pipeline`, `EquipmentAbilitySourceLifecycleService`, `EquipmentWorldEffectService`, `存档兼容`, `World-bound persistent equipment state` | `equipment_durability_selector_commit.md`: `Commit 语义` for durability payload parity |
| Task 12: presentation/content/regression closure | `展示结构`, `能力覆盖分类`, `by_family 机制族覆盖矩阵`, `by_family 文件级覆盖矩阵`, `V1 落地范围`, `测试策略` | all sub-doc `必测回归` / `回归矩阵` / accepted findings |

## Production Module Delivery Matrix

| Production module from main design | Primary task | Code owner target | Must exist before |
| --- | --- | --- | --- |
| `EquipmentAbilityStaticContent` | Task 6 | New equipment ability content/registry folder under current progression/content conventions | Any content resource or validator test |
| `EquipmentAbilityProjectionSource` | Task 7 | Battle setup / unit projection owners | Dispatcher, overlays, availability grants |
| `EquipmentAbilitySourceLifecycle` | Tasks 7 and 11 | Battle setup, equipment change hooks, battle finish writeback | Runtime state cleanup and staged commit |
| `BattleEquipmentAbilityState` | Task 8 | Battle runtime state container and AI guard | Dispatcher mutation handlers |
| `BattleEquipmentAbilityRuntimeDispatch` | Task 10 | Runtime dispatcher, handler registry, action executor | Representative battle behavior tests |
| `EquipmentWeaponProfileOverlay` | Task 9 | Weapon profile resolution owner | Passive weapon profile content |
| `EquipmentAttackDefenseModifier` | Task 9 | Attack check / defense calculation owners | Hit/defense modifier content |
| `BattleEnvironmentFacts` | Task 9 | Battle runtime environment provider | Environment condition handlers |
| `EquipmentTargetSelector` | Tasks 4 and 10 | Durability selector and target selector resolver | Durability and random equipment actions |
| `EquipmentMutationAdapter` | Tasks 4, 10, 11 | Damage resolver adapter and staged commit owner | Persistent writeback |
| `BattleSkillAvailability` | Tasks 1-3 and 10 | Battle selection, command, runtime, HUD, AI | Equipment-granted skill actions |
| `EquipmentAbilityPresentation` | Task 12 | HUD/text snapshot/equipment inspection query owner | Final user-facing proof |

## Data Structure Delivery Matrix

| Structure family | Main design owner | Execution task | Persistence rule |
| --- | --- | --- | --- |
| Authoring resources | `Resource 层结构` | Task 6 | Saved as Godot resources, not runtime mutable state. |
| Runtime definition DTOs | `Runtime definition DTO` | Task 6 | Built from registry load/normalization; not persisted as battle state. |
| Handler metadata | `Handler spec` | Task 6 | Static registry metadata; validates content before runtime. |
| Skill entry refs | `Battle skill availability` and sub-doc | Tasks 1-3 | Active battle command identity only; no active battle save/load in V1. |
| Projection source state | `Battle projection` | Task 7 | Battle-local copy; persistent source id retained only for player equipment writeback. |
| Battle mark/counter/target state | `Runtime state owner` | Task 8 | Battle-local and AI-guarded. |
| Equipment usage period state | `World-bound persistent equipment state` | Task 5 | Persistent on `EquipmentInstanceState`; committed after battle. |
| Equipment persistent counters | `World-bound persistent equipment state` | Task 5 | Persistent on `EquipmentInstanceState`; never auto-reset. |
| Durability selected target refs | Durability sub-doc | Task 4 | Battle-local explicit ref, revalidated at commit. |
| Staged commit records | `Battle-end staged commit pipeline` | Task 11 | Applied only during battle resolution writeback. |
| Presentation summaries | `展示结构` | Task 12 | Derived query output, not authoritative state. |

## Mechanism Coverage In V1

| Mechanism family | V1 proof task | Representative runtime proof |
| --- | --- | --- |
| Passive weapon profile change | Task 9 and Task 12 | A weapon changes resolved damage/attack profile through overlay service. |
| Conditional attack/defense modifier | Task 9 and Task 12 | A conditionally active modifier affects hit/defense calculation. |
| Equipment-granted active skill | Tasks 1-3, 10, 12 | Skill appears as equipment entry and executes without changing learned skills. |
| Scoped auto-cast source gate | Task 3 | Only truly learned user-owned skill can act as source. |
| Random equipment target | Tasks 4 and 10 | Weighted target is selected once and same target is committed. |
| Durability damage | Tasks 4 and 10 | Existing spell behavior remains; equipment action uses selected-target commit. |
| Creature type condition | Task 7 and Task 10 | Reads `BattleUnitState.creature_type_tags`. |
| Battle environment condition | Task 9 and Task 10 | Reads battle-only environment facts. |
| Per-world-day usage | Tasks 5 and 11 | Commits usage counter to equipment instance after battle. |
| Per-world-month usage | Tasks 5 and 11 | Commits month-scoped usage with deterministic month index. |
| Permanent counter | Tasks 5 and 11 | Counter survives period changes and increments only through staged commit. |
| Framework-only source trace | Task 6 and Task 12 | `source_traces` survive validation and presentation without full ledger enforcement. |

## Explicit Deferred Coverage

These main-document sections are intentionally not expanded into implementation tasks:

- `V1.5 反应确认`: no reaction confirmation UI in V1.
- `V2 移动和地形`: no movement hooks, zones, terrain mutation, or spatial delayed effects.
- `Battle entity / zone / delayed effect`: no summons, zones, or scheduled delayed effects.
- Full `by_family` per-entry conversion: V1 proves mechanism families with representative content only.
- Diagnostics and ops tooling beyond validator errors: public diagnostics schema is deferred.
- Hot MOD reload and external C# plugin ABI: V1 keeps resource ABI only.

## Current Owners To Keep Loaded

- `docs/design/project_context_units.md`
- `docs/design/weapons/equipment_ability_system.md`
- `docs/design/weapons/equipment_ability/battle_skill_availability_migration.md`
- `docs/design/weapons/equipment_ability/equipment_durability_selector_commit.md`
- `scripts/player/equipment/EquipmentState.cs`
- `scripts/player/equipment/EquipmentEntryState.cs`
- `scripts/player/warehouse/EquipmentInstanceState.cs`
- `scripts/player/progression/PlayerProgressionState.cs`
- `scripts/player/progression/SkillState.cs`
- `scripts/systems/battle/core/BattleCommand.cs`
- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator*.cs`
- `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
- `scripts/systems/battle/rules/BattleDamageResolver*.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleSelection*.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`
- `scripts/enemies/EnemyAiAction.cs`
- `scripts/enemies/actions/MoveToRangeAction.cs`
- `tests/battle_runtime/skills/run_spell_disjunction_equipment_durability_regression.cs`

## Implementation Order

Each task below is designed to compile and test independently. Do not start a later task by temporarily mutating old contracts in a way that bypasses the prerequisite sub-document design; that would create a second unofficial contract beside the approved one.

## Task Feasibility Gates

### Task 1 Gate: Stable Skill Entry Identity

**Design basis**

- `equipment_ability_system.md`: `Battle skill availability`, `与当前代码的精确映射`, `战斗插入点`, `可行性 / 架构审查结论`.
- `battle_skill_availability_migration.md`: `目标`, `核心数据结构`, `Command and selection`, `Phase 1`, `Phase 2`.

**Current code facts**

- `BattleCommand` currently exposes `skill_id` and `skill_variant_id`, but no `skill_entry_id`.
- `GameRuntimeBattleSelectionState` currently exposes `selected_skill_id` and `selected_skill_variant_id`, but no `selected_skill_entry_id`.
- `GameRuntimeSnapshotBuilder`, `GameRuntimeCommandLogger`, `GameTextSnapshotRenderer`, and `BattleMapPanel` still surface selected skill by `skill_id`.

**Prerequisite gate**

- `dotnet build magic.csproj` must pass before editing.
- Confirm no existing branch-local implementation of `BattleSkillEntryRef` or `BattleSkillEntryIds` has already been added under a different name by running `rg -n "SkillEntry|skill_entry" scripts tests`.

**Implementation slice**

- Add `BattleSkillEntryRef`, `BattleSkillEntrySourceKind`, and `BattleSkillEntryIds`.
- Add `BattleCommand.skill_entry_id`.
- Add `GameRuntimeBattleSelectionState.selected_skill_entry_id` and clear it wherever skill selection is cleared.
- Keep every existing known-skill command behavior identical by setting known entries to `known_skill:{skill_id}`.
- Expose selected entry id in snapshot/text output beside selected skill id.

**Exit gate**

- `dotnet build magic.csproj` passes.
- A focused selection/snapshot regression proves a known skill records both `selected_skill_entry_id` and `selected_skill_id`.
- A negative assertion proves clearing selected skill clears entry id, skill id, variant id, and target queues together.

### Task 2 Gate: Known-Skill Availability Parity

**Design basis**

- `equipment_ability_system.md`: `BattleSkillAvailabilityService`, `展示结构`, `BattleRuntimeSkillTurnResolver`, `测试策略 / Skill availability 测试`.
- `battle_skill_availability_migration.md`: `BattleSkillAvailabilityService API`, `排序、去重和失效`, `Manual selection`, `Runtime preview and execution`, `Range, hit, cost and effect unlock`, `HUD, snapshot and text`.

**Current code facts**

- `GameRuntimeBattleSelection.SelectBattleSkillSlotTyped(...)` indexes `activeUnit.known_active_skill_ids`.
- `BattleHudAdapter.BuildSkillSlots(...)` reads `activeUnit.known_active_skill_ids` directly.
- `BattleRangeService`, `BattleHitResolver`, `BattleRuntimeSkillTurnResolver`, and several execution helpers use `known_active_skill_ids` as skill-level fallback.

**Prerequisite gate**

- Task 1 exit gate must pass.
- The first availability service implementation must produce known-skill entries only; no equipment entries are introduced in this task.

**Implementation slice**

- Add `BattleSkillAvailabilityService` and its query/view DTOs.
- Wire the service through `BattleRuntimeServices` or the smallest existing service owner.
- Migrate manual selection, selected-skill sync, preview command construction, HUD skill slots, and text/snapshot selected output to entry refs while preserving known-skill order.
- Keep known-only rules such as main-skill status locks reading known skills when their semantics are explicitly known-only.

**Exit gate**

- `dotnet build magic.csproj` passes.
- A known-skill parity regression proves the same known skill can be selected, previewed, and executed through its entry id.
- A stale entry regression rejects an unknown `skill_entry_id` before range, hit, cost, or effect resolution.
- Existing text/battle command snapshot regressions that assert selected skill output are updated to include entry id.

### Task 3 Gate: AI And Scoped Auto-Cast Migration

**Design basis**

- `equipment_ability_system.md`: `Battle skill availability`, `AI mutation guard contract`, `触发点分类 / 技能和动作`.
- `battle_skill_availability_migration.md`: `Scoped auto-cast source gate`, `AI`, `Phase 4`, `Phase 7`, `必测回归`.

**Current code facts**

- `EnemyAiAction`, `BattleAiActionAssembler`, `BattleAiTypedActionHelper`, `BattleAiQueryService`, and multiple evaluator files still enumerate `known_active_skill_ids`.
- `BattleSkillExecutionOrchestrator.AutoCast.cs` has `ExecuteAutoCast(...)`; current behavior must be checked for any temporary known-skill mutation before edits.

**Prerequisite gate**

- Task 2 exit gate must pass.
- `BattleSkillAvailabilityService` must expose explicit consumer modes for manual selection, preview/execution, AI planning, AI scoring, and scoped auto-cast.

**Implementation slice**

- Migrate AI candidate generation and scoring inputs to availability entries.
- Carry `SkillEntryId` through AI plans and generated `BattleCommand`.
- Make scoped auto-cast resolve source skills only from true learned player-owned progression state.
- Reject equipment/species/race/template/status/temporary skill grants as scoped auto-cast source skills.
- Remove or prevent any auto-cast path that temporarily writes source skills into `known_active_skill_ids`.

**Exit gate**

- `dotnet build magic.csproj` passes.
- AI known-skill regression still chooses and executes a normal active skill.
- Scoped auto-cast true-learned regression passes.
- Scoped auto-cast equipment/species/template-granted source regression fails closed without mutating `known_active_skill_ids`.

### Task 4 Gate: Equipment Durability Selector And Commit

**Design basis**

- `equipment_ability_system.md`: `动作分类 / 装备和物品`, `EquipmentMutationAdapter`, `BattleDamageResolver`, `测试策略 / 战斗行为测试`.
- `equipment_durability_selector_commit.md`: all sections.

**Current code facts**

- `EquipmentAbilityEquipmentTargetRef`, `EquipmentDurabilityCommitRequest`, and `EquipmentDurabilityCommitResult` already exist in `scripts/systems/battle/rules/EquipmentDurabilityCommitTypes.cs`.
- `BattleDamageResolver.DtoHelpers.cs` already has `ApplyEquipmentDurabilityDamageToSelection(...)`.
- Current focused regression fails because `SelectEquipmentForDurabilityDamage(...)` still throws `debug select called`.
- `Console.Error.WriteLine` durability debug traces are present near the selector path and must not remain in production code.

**Prerequisite gate**

- `dotnet build magic.csproj` must pass before editing.
- The failing focused regression must be reproduced once so the fix is anchored to the current failure.

**Implementation slice**

- Remove the debug throw and debug console writes from selector code.
- Finish selector query, candidate, weight, roll, and explicit-slot behavior exactly as the durability sub-document specifies.
- Keep old spell effect path as `ApplyEquipmentDurabilityDamageEffect(...) -> SelectEquipmentForDurabilityDamage(...) -> ApplyEquipmentDurabilityDamageToSelection(...)`.
- Ensure equipment ability paths can call selected-target commit without invoking random selection.
- Keep save, rarity bonus, event result, log refresh, destroyed equipment clearing, and battle-local writeback on the existing resolver path.

**Exit gate**

- `dotnet build magic.csproj` passes.
- `godot --headless -s res://tests/battle_runtime/rules/run_equipment_durability_selected_target_regression.cs` passes.
- `godot --headless -s res://tests/battle_runtime/skills/run_spell_disjunction_equipment_durability_regression.cs` passes.
- `rg -n "debug select called|durability debug|Console\\.Error\\.WriteLine" scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs` returns no matches.

### Task 5 Gate: World-Bound Persistent Equipment State

**Design basis**

- `equipment_ability_system.md`: `World-bound persistent equipment state`, `Battle-end staged commit pipeline`, `世界层`, `存档兼容`, `测试策略 / 存档测试`.

**Current code facts**

- `EquipmentInstanceState.ToDictionary()` writes exactly `instance_id`, `item_id`, `rarity`, `current_durability`, and `trait_instances`.
- `EquipmentInstanceState.GetPayloadValidationError(...)` rejects payloads whose field count differs from the current required field list.
- `SaveSerializer` rejects root save versions that differ from the current `_save_version`.
- `WorldTimeSystem` has `StepToDay(...)` but no `StepToMonth(...)`.

**Prerequisite gate**

- User confirmation is required before changing save schema/version if the implementation must invalidate existing saves.
- No compatibility migration or fallback is added unless the user explicitly approves it.
- Task 4 should be complete if staged commits include durability-adjacent payloads.

**Implementation slice**

- Add explicit equipment ability usage period and persistent counter state to `EquipmentInstanceState`.
- Update equipment instance dictionary serialization, duplicate, parse, and exact schema validation.
- Add deterministic `WorldTimeSystem.StepToMonth(...)` and tests for negative/current boundary behavior.
- Add or update save/schema tests so old payloads fail intentionally under the current compatibility policy.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Equipment instance schema regression proves new fields round-trip and unsupported old/new shapes fail as intended.
- World time regression proves `StepToDay` behavior remains and `StepToMonth` behavior is deterministic.
- Save schema/version tests are updated if root or party save version changes.

### Task 6 Gate: Static Content ABI, Registry, And Validator

**Design basis**

- `equipment_ability_system.md`: `Resource 层结构`, `Runtime definition DTO`, `Handler spec`, `EquipmentAbilityContentRegistry`, `MOD 支持分析`, `内容校验测试`.
- `subagent_review_findings.md`: accepted ABI/export findings.

**Current code facts**

- Current content root goes through `ProgressionContentRegistry`, `GameSession`, and `GameContentCatalog`.
- Resource content in this repo is generally projected to plain C# runtime DTOs before runtime consumption.
- V1 diagnostic public schema is intentionally deferred, but blocking errors still need stable code/path fragments.

**Prerequisite gate**

- The file location for equipment ability content must follow current progression/content registry conventions.
- Handler ids are data ABI; do not choose names that conflict with deferred V2/V3 concepts.

**Implementation slice**

- Add exported resource classes for content pack, binding, reactions, conditions, actions, fact queries, target selectors, dice, overlays, state schemas, and granted actions.
- Add runtime DTO normalization separate from Resource wrappers.
- Add handler spec metadata for validation, consumer support, state access, phase compatibility, and diagnostic/trace info.
- Add `EquipmentAbilityContentRegistry` and connect it to the existing content lifecycle without making battle runtime read Resources directly.
- Add fail-fast validator output with stable code/path fragments in string errors.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Content validator regression accepts a minimal valid pack.
- Validator rejects unknown handler id, invalid reference, invalid state access, invalid phase/consumer support, and invalid persistent state owner.
- A lifecycle test proves runtime DTOs do not depend on live Resource mutation after registry build.

### Task 7 Gate: Battle Projection Sources

**Design basis**

- `equipment_ability_system.md`: `Battle projection`, `Enemy attack equipment battle-only source`, `BattleUnitFactory`, `EquipmentAbilityProjectionService`, `EquipmentAbilitySourceLifecycleService`, `投影测试`.
- `battle_skill_availability_migration.md`: `Phase 8` for later equipment-granted skill plug-in shape.

**Current code facts**

- `BattleUnitState` already owns `equipment_view` and `effective_trait_instances`.
- `BattleUnitState` does not yet expose `creature_type_tags` or equipment ability projection source state.
- Existing projection refresh paths should be verified with `rg -n "RefreshEquipmentProjection|SetEquipmentView|effective_trait_instances" scripts/systems scripts/player`.

**Prerequisite gate**

- Task 6 registry must exist so projection can resolve bindings from validated runtime definitions.
- If `BattleUnitState` gains new battle-only fields, decide explicitly whether they are included in `ToDictionary()` or excluded from save/schema.

**Implementation slice**

- Add projection source state to battle unit or the nearest battle runtime projection owner.
- Add `creature_type_tags` to `BattleUnitState` as the formal battle fact source if required by V1 content.
- Project player equipment sources from `equipment_view` plus effective trait instances.
- Project enemy battle-only equipment sources during battle setup and discard them at battle end.
- Add source lifecycle diff/seed/cleanup logic for charges, cooldowns, marks, state, granted skill entries, and overlay invalidation.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Projection test proves player equipment ability source appears on the battle unit with persistent equipment instance id.
- Enemy setup test proves battle-only equipment source appears without persistent writeback id.
- Creature type test proves conditions read `BattleUnitState.creature_type_tags`, not enemy template tags.

### Task 8 Gate: Runtime Stores And AI Mutation Guard

**Design basis**

- `equipment_ability_system.md`: `Runtime state owner`, `Source-local charge/cooldown`, `Status-like effects`, `Target mark`, `Battle-level ability state`, `AI mutation guard contract`.
- `subagent_review_findings.md`: accepted state-protection findings.

**Current code facts**

- `BattleAiMutationGuard` has manual capture/restore/stable-map sections.
- The guard already snapshots equipment view, equipment durability, effective traits, and known skill ids.
- New stores will be invisible to AI rollback unless each capture/restore/stable-map path is updated.

**Prerequisite gate**

- Task 7 projection keys must be stable enough to identify source-local state.
- New stores must have explicit owner: battle state, battle unit, or persistent equipment instance.

**Implementation slice**

- Add battle-only mark store and ability state store through typed APIs.
- Add selected target runtime state needed by delayed commit/action chains.
- Add source-local charge/cooldown key helpers with `equipment_ability:` prefix.
- Update `BattleAiMutationGuard` capture, restore, and stable-map comparison for every new mutable store.

**Exit gate**

- `dotnet build magic.csproj` passes.
- AI guard regression proves simulated mark/state/selected-target mutation is restored.
- AI guard regression proves stable diff reports unguarded equipment ability mutations.
- Battle state/unit schema regression proves battle-only stores are not accidentally persisted unless explicitly intended.

### Task 9 Gate: Weapon Overlay, Attack/Defense Modifiers, Environment Facts

**Design basis**

- `equipment_ability_system.md`: `BattleEnvironmentSnapshot`, `EquipmentAttackDefenseModifierDef`, `EquipmentWeaponProfileOverlayDef`, `BattleAttackCheckPolicyService`, `BattleEnvironmentContextProvider`.

**Current code facts**

- `BattleUnitState` already has weapon profile fields: kind, item id, type id, family, grip, range, dice, and damage tag.
- Attack/hit logic currently has existing owners in `BattleAttackCheckPolicyService`, `BattleHitResolver`, and related battle rules.
- World map has no weather system and V1 must keep environment facts battle-only.

**Prerequisite gate**

- Task 7 projection must expose projected equipment sources.
- Task 8 stores must exist if overlay or modifier state can be consumed or invalidated by AI preview.

**Implementation slice**

- Add overlay service that computes weapon profile changes from projected equipment ability sources.
- Add attack/defense modifier service that participates in existing hit/defense calculations.
- Add battle-only environment facts provider with explicit defaults.
- Add deterministic conflict/priority handling for multiple overlays.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Weapon overlay regression proves resolved weapon range/dice/profile changes through the service.
- Attack/defense regression proves conditional modifier affects calculation only when condition is true.
- Environment regression proves battle-only facts are available in battle and not sourced from world-map weather.

### Task 10 Gate: Dispatcher, Conditions, Actions, Dice, Target Selection

**Design basis**

- `equipment_ability_system.md`: `Execution context 和 mutation plan`, `触发点分类`, `条件分类`, `动作分类`, `BattleEquipmentAbilityDispatcher`, `EquipmentAbilityConditionEvaluator`, `EquipmentAbilityActionExecutor`, `EquipmentAbilityTargetSelectorResolver`, `DiceExpressionEvaluator`.
- `battle_skill_availability_migration.md`: `Phase 8`.
- `equipment_durability_selector_commit.md`: `Mutation Adapter Request`, `Mutation Result`, `Preview / AI / Snapshot`.

**Current code facts**

- Current runtime has battle hook owners across attack, damage, skill execution, status, and turn resolution.
- Durability action must use Task 4 selected-target commit, not a direct durability write.
- Granted skill action must use Tasks 1-3 availability entries, not `known_active_skill_ids`.

**Prerequisite gate**

- Tasks 1-4 must be complete.
- Tasks 6-8 must be complete enough for validated definitions, projection sources, and runtime mutation stores.

**Implementation slice**

- Add dispatcher trigger index lookup and early-out behavior.
- Add condition evaluator with fact provider registry and no mutation side effects.
- Add action executor with mutation plan/result outputs.
- Add target selector resolver, including deterministic weighted equipment target selection.
- Add dice expression evaluator using existing battle RNG conventions.
- Add granted skill and durability action handlers wired to approved services.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Dispatcher regression proves trigger candidate order, condition false no-op, and action execution.
- Weighted equipment target regression proves one selected target is committed.
- Granted skill regression proves equipment skill is available and executable by entry id without becoming known.
- Preview/AI regression proves no RNG consumption or durability mutation during non-execution consumers.

### Task 11 Gate: Battle-End Staged Commit Pipeline

**Design basis**

- `equipment_ability_system.md`: `Battle-end staged commit pipeline`, `EquipmentAbilitySourceLifecycleService`, `EquipmentWorldEffectService`, `存档兼容`, `World-bound persistent equipment state`.
- `equipment_durability_selector_commit.md`: `Commit 语义` only for payload parity when durability events interact with staged writeback.

**Current code facts**

- `GameRuntimeBattleWritebackService` owns battle-local view writeback.
- `GameRuntimeFacade.BattleResolution.cs` calls battle resolution/writeback flow.
- Battle save lock exists and V1 must not add active battle save/load.

**Prerequisite gate**

- Task 5 persistent equipment state must exist.
- Task 8 runtime staged commit records must exist.
- Current battle resolution loss/abort writeback policy must be inspected before adding equipment ability commits.

**Implementation slice**

- Add staged persistent equipment ability commit records.
- Apply staged commits only during battle resolution writeback.
- Discard enemy battle-only equipment commits.
- Ensure failed/aborted battle resolution follows current writeback policy and does not partially apply equipment ability commits.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Battle victory regression commits per-world-day usage to persistent player equipment.
- Battle month/persistent-counter regression commits the correct period/counter.
- Enemy battle-only equipment regression proves no persistent writeback.
- Save-lock regression proves combat still cannot save active battle state.

### Task 12 Gate: Presentation, Representative Content, Closure Regressions

**Design basis**

- `equipment_ability_system.md`: `展示结构`, `能力覆盖分类`, `by_family 机制族覆盖矩阵`, `by_family 文件级覆盖矩阵`, `V1 落地范围`, `测试策略`.
- Sub-docs: all `必测回归`, `回归矩阵`, and accepted findings.

**Current code facts**

- `BattleHudAdapter`, `BattleMapPanel`, `GameTextSnapshotRenderer`, and headless text/runtime tests are the current user-facing proof surfaces.
- HUD currently has finite skill slot capacity; V1 uses existing capacity clipping plus hidden count rather than adding skill pagination.
- Full weapon-family conversion is explicitly not part of framework landing.

**Prerequisite gate**

- Tasks 1-11 must pass their exit gates.
- Representative content must be enough to cover each V1 mechanism family without pulling in V1.5/V2/V3 excluded systems.

**Implementation slice**

- Add presentation query/snapshot for equipment ability summaries and granted skill source labels.
- Add representative content for passive overlay, conditional modifier, granted active skill, durability selected-target commit, day/month usage, persistent counter, creature type condition, and environment condition.
- Update text/headless commands only where needed to inspect or select equipment-granted skills by entry id.
- Keep unsupported weapon designs marked framework-only/deferred in docs.

**Exit gate**

- `dotnet build magic.csproj` passes.
- Static validator passes representative content.
- Headless battle regression executes representative equipment ability flow.
- HUD/text snapshot distinguishes equipment-granted skills from learned skills.
- Final broad checks run:

```bash
python tests/run_regression_suite.py
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs
godot --headless -s res://tests/world_map/schema/run_world_map_low_level_defensive_regression.cs
```

## Task 1: Add Stable Skill Entry Identity Without Changing Behavior

Purpose: introduce first-class skill entries while all existing learned-skill commands still behave exactly as before.

Edit:

- `scripts/systems/battle/core/BattleCommand.cs`
- `scripts/systems/battle/core/` new DTO file for entry refs if local naming favors one type per file.
- `scripts/systems/game_runtime/GameRuntimeBattleSelectionState.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `scripts/utils/GameTextSnapshotRenderer.cs`

Add these concrete contracts:

```csharp
public enum BattleSkillEntrySourceKind
{
    KnownSkill,
    EquipmentSkill,
    ScopedAutoCast
}

public readonly struct BattleSkillEntryRef
{
    public string EntryId { get; }
    public string SkillId { get; }
    public BattleSkillEntrySourceKind SourceKind { get; }
    public string? SourceEquipmentInstanceId { get; }
}

public static class BattleSkillEntryIds
{
    public static string KnownSkill(string skillId);
    public static string EquipmentSkill(string bindingId, string sourceEquipmentInstanceId, string effectiveInstanceKey, string grantedActionId, string skillId);
    public static string ScopedAutoCast(string scopeId, string skillId);
}
```

Change `BattleCommand` so `skill_entry_id` is the command identity and `skill_id` remains the resolved skill definition id:

```csharp
public string skill_entry_id = "";
public string skill_id = "";
```

Rules:

- Existing command creation paths must set `skill_entry_id = BattleSkillEntryIds.KnownSkill(skill_id)`.
- Snapshot/text output may expose both `selected_skill_entry_id` and `selected_skill_id`.
- No compatibility alias is added for saved payloads because active battle save/load is outside V1.

Tests:

- Add or update a narrow selection snapshot regression that selects a known active skill and asserts both entry id and skill id are present.
- Run `dotnet build magic.csproj`.

## Task 2: Build BattleSkillAvailabilityService With Known-Skill Parity

Purpose: create the shared query surface before equipment grants exist.

Edit:

- `scripts/systems/battle/runtime/` new `BattleSkillAvailabilityService.cs`
- `scripts/systems/battle/runtime/BattleRuntimeServices.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator*.cs`
- `scripts/systems/battle/rules/BattleRangeService.cs`
- `scripts/systems/battle/rules/BattleHitResolver.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`.

Add:

```csharp
public sealed class BattleSkillAvailabilityQuery
{
    public BattleUnitState User { get; init; }
    public bool IncludeKnownSkills { get; init; } = true;
    public bool IncludeEquipmentSkills { get; init; } = true;
    public bool IncludeScopedAutoCast { get; init; } = false;
}

public sealed class BattleAvailableSkillEntry
{
    public BattleSkillEntryRef EntryRef { get; init; }
    public SkillDef SkillDef { get; init; }
    public int VariantIndex { get; init; }
    public bool IsSelectable { get; init; }
    public string DisabledReason { get; init; } = "";
}

public sealed class BattleSkillCommandContext
{
    public BattleAvailableSkillEntry Entry { get; init; }
    public BattleCommand Command { get; init; }
}
```

Rules:

- Direct consumers of `known_active_skill_ids` become availability queries.
- Keep a known-only helper only for true progression checks, not battle command listing.
- A command with unknown `skill_entry_id` must fail before range, hit, or cost resolution.
- Known-skill parity is mandatory before equipment-skill entries are introduced.

Tests:

- Selection regression: known skills listed by entry id.
- Command regression: issuing a known skill by entry id executes the same result as before.
- Negative regression: command with stale or unknown entry id is rejected.
- Run `dotnet build magic.csproj`.

## Task 3: Migrate AI And Scoped Auto-Cast To Availability Entries

Purpose: prevent AI and auto-cast paths from bypassing the new entry contract.

Edit:

- `scripts/enemies/EnemyAiAction.cs`
- `scripts/enemies/actions/MoveToRangeAction.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.AutoCast.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Current AI candidate scoring files discovered by `rg -n "known_active_skill_ids|skill_id" scripts/enemies scripts/systems/battle`.

Rules:

- AI candidates carry `BattleSkillEntryRef`, not only `skill_id`.
- `ExecuteAutoCast(...)` must not temporarily add source skills into `known_active_skill_ids`.
- Scoped auto-cast source validation accepts only skills truly learned by the acting unit owner.
- Skills granted by equipment, species, race, template, traits, or temporary effects cannot become scoped auto-cast source skills.
- Default allowed true-learned source is `UnitSkillGrantSourceType.Player`; additional allowed source kinds require explicit design approval.

Tests:

- Auto-cast regression: source learned by player succeeds without mutating known skill collections.
- Auto-cast regression: source granted by equipment/species/template is rejected.
- AI regression: an enemy can still select and execute a normal known active skill after entry migration.
- Run `dotnet build magic.csproj`.

## Task 4: Split Equipment Durability Selection From Commit

Purpose: support equipment abilities that random-select equipment once and later apply durability loss to that same selected item.

Edit:

- `scripts/systems/battle/rules/BattleDamageResolver.DtoHelpers.cs`
- `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- `scripts/systems/battle/core/AttackEffectResolutionResult.cs`
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `scripts/player/equipment/EquipmentState.cs`
- `scripts/player/equipment/EquipmentEntryState.cs`
- `scripts/player/warehouse/EquipmentInstanceState.cs`

Add:

```csharp
public sealed class EquipmentDurabilitySelectionQuery
{
    public BattleUnitState TargetUnit { get; init; }
    public string TargetSlot { get; init; } = "";
    public string? TargetItemFamily { get; init; }
    public bool IncludeBrokenEquipment { get; init; }
    public int RandomSeedSalt { get; init; }
}

public sealed class EquipmentDurabilitySelectionResult
{
    public bool HasSelection { get; init; }
    public EquipmentAbilityEquipmentTargetRef? SelectedTarget { get; init; }
    public string Reason { get; init; } = "";
}

public sealed class EquipmentDurabilityCommitRequest
{
    public EquipmentAbilityEquipmentTargetRef SelectedTarget { get; init; }
    public int DurabilityDelta { get; init; }
    public string SourceAbilityId { get; init; } = "";
}
```

Rules:

- Current spell disjunction behavior is preserved by wrapping the existing effect in the new selector plus immediate commit.
- Equipment ability handlers call selector first, store the selected target in action context/result, then commit only that target.
- No second random selection is allowed during commit.
- Battle-local equipment projections must be able to map commit results back to persistent `EquipmentInstanceState` when the source is player-owned persistent equipment.

Tests:

- Existing `run_spell_disjunction_equipment_durability_regression.cs` still passes.
- New regression: weighted/random equipment selection is committed to the originally selected target.
- New regression: no valid equipment target produces no mutation and a stable reason.
- Run `dotnet build magic.csproj`.

## Task 5: Add World-Bound Equipment Persistent State

Purpose: support per-world-day, per-world-month, and permanent counters on equipment instances.

Edit:

- `scripts/player/warehouse/EquipmentInstanceState.cs`
- `scripts/systems/world/WorldTimeSystem.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`

Add:

```csharp
public sealed class EquipmentAbilityUsagePeriodState
{
    public string AbilityId { get; set; } = "";
    public string PeriodKind { get; set; } = "";
    public int PeriodIndex { get; set; }
    public int UsedCount { get; set; }
}

public sealed class EquipmentAbilityPersistentCounterState
{
    public string CounterId { get; set; } = "";
    public long Value { get; set; }
}
```

Extend `EquipmentInstanceState` with explicit lists or dictionaries using the repo's existing serialization style:

```csharp
public List<EquipmentAbilityUsagePeriodState> ability_usage_periods = new();
public List<EquipmentAbilityPersistentCounterState> ability_persistent_counters = new();
```

Rules:

- `per_world_day` uses existing day step semantics.
- `per_world_month` uses a deterministic world time helper such as `StepToMonth(step)`.
- Permanent counters never reset automatically.
- These counters are committed only after battle resolution; combat cannot save active battle state.

Tests:

- World time regression for `StepToMonth`.
- Equipment instance clone/serialization regression for ability usage and persistent counters.
- Battle writeback regression for a staged equipment usage increment.
- Run `dotnet build magic.csproj`.

## Task 6: Add Static Content ABI, Registry, And Validator

Purpose: make equipment ability content loadable and statically checkable before runtime projection.

Edit:

- New folder under `scripts/player/progression/equipment_abilities/` or the closest existing content/runtime split after checking local style.
- `docs/design/project_context_units.md` only if actual runtime ownership differs from current documented read sets.
- Add text/static-analysis test under `tests/` matching current content validator conventions.

Core definitions:

```csharp
public sealed partial class EquipmentAbilityDef : Resource
{
    [Export] public string ability_id = "";
    [Export] public string binding_id = "";
    [Export] public string trigger_id = "";
    [Export] public string timing_id = "";
    [Export] public string source_scope = "";
    [Export] public Godot.Collections.Array<EquipmentAbilityConditionDef> conditions = new();
    [Export] public Godot.Collections.Array<EquipmentAbilityActionDef> actions = new();
    [Export] public Godot.Collections.Array<EquipmentAbilitySourceTraceDef> source_traces = new();
}
```

Rules:

- Resource ABI is the editor/content ABI.
- No public external C# ABI stability is promised in V1.
- Validator rejects unknown trigger/action/condition ids.
- Validator rejects equipment-granted skill definitions without a stable `skill_entry_id` composition source.
- Validator rejects persistent usage scopes that are not backed by `EquipmentInstanceState`.
- `source_traces` are optional framework metadata, not a full weapon-family coverage gate.

Tests:

- Valid minimal ability resource passes.
- Unknown handler id fails.
- Invalid persistent scope fails.
- Invalid equipment granted skill source fails.
- Run `dotnet build magic.csproj`.

## Task 7: Project Equipment Sources Onto Battle Units

Purpose: make active equipment ability sources queryable from `BattleUnitState`.

Edit:

- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/runtime/` new projection source/lifecycle service.
- `scripts/systems/game_runtime/GameRuntimeBattleBuilder*.cs` or the current battle setup owner discovered in code.
- `scripts/enemies/` enemy battle setup owner for synthetic battle-only equipment.

Add:

```csharp
public enum EquipmentAbilitySourceKind
{
    PlayerPersistentEquipment,
    EnemyBattleOnlyEquipment
}

public sealed class BattleEquipmentAbilitySourceState
{
    public string EffectiveInstanceKey { get; set; } = "";
    public string EquipmentDefId { get; set; } = "";
    public string? SourceEquipmentInstanceId { get; set; }
    public EquipmentAbilitySourceKind SourceKind { get; set; }
    public List<string> AbilityIds { get; set; } = new();
}
```

Rules:

- Once projected, battle code reads equipment abilities from the unit, not from enemy templates or warehouse state.
- Player equipment sources retain `SourceEquipmentInstanceId` for writeback.
- Enemy equipment sources are battle-only and never persist outside battle.
- Creature taxonomy is projected onto `BattleUnitState.creature_type_tags`; equipment ability checks read tags from the battle unit only.

Tests:

- Player equipped item projects an ability source onto the battle unit.
- Enemy template setup projects battle-only equipment to the battle unit without persistent instance id.
- Creature type tag condition reads from `BattleUnitState`, not from enemy template.
- Run `dotnet build magic.csproj`.

## Task 8: Add Runtime State Stores And AI Mutation Guard Snapshots

Purpose: support marks, per-battle ability state, selected targets, and AI simulation rollback.

Edit:

- `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
- Battle runtime state container owner discovered around `BattleRuntimeModule` and `BattleRuntimeServices`.
- Add focused tests near existing battle runtime tests.

Add stores:

```csharp
public sealed class BattleEquipmentAbilityRuntimeState
{
    public Dictionary<string, EquipmentAbilityMarkState> MarksByKey { get; } = new();
    public Dictionary<string, EquipmentAbilityRuntimeCounterState> CountersByKey { get; } = new();
    public Dictionary<string, EquipmentAbilitySelectedTargetState> SelectedTargetsByKey { get; } = new();
    public List<EquipmentAbilityPendingCommitState> PendingCommits { get; } = new();
}
```

Rules:

- AI scoring/simulation must snapshot and restore every mutable equipment ability runtime store.
- Random selected target state used for a later commit is part of the guarded runtime state.
- Persistent writeback staging is not committed during AI simulation.

Tests:

- AI guard regression: simulated equipment ability mutation is rolled back.
- AI guard regression: selected target state is rolled back.
- Run `dotnet build magic.csproj`.

## Task 9: Add Weapon Overlay, Attack/Defense Modifiers, And Environment Facts

Purpose: connect passive equipment effects to existing battle calculations without converting all content.

Edit:

- Existing weapon profile calculation owner discovered by `rg -n "weapon_profile|damage_dice|weapon" scripts/systems scripts/player`.
- Existing attack/defense calculation owners discovered by `rg -n "attack_bonus|defense|armor_class|saving_throw" scripts/systems/battle`.
- Battle runtime environment owner or new service under `scripts/systems/battle/runtime/`.

Contracts:

```csharp
public sealed class EquipmentWeaponProfileOverlay
{
    public string SourceAbilityId { get; init; } = "";
    public string DamageDiceOverride { get; init; } = "";
    public int AttackBonusDelta { get; init; }
    public int DamageBonusDelta { get; init; }
}

public sealed class EquipmentAttackDefenseModifier
{
    public string SourceAbilityId { get; init; } = "";
    public string ModifierKind { get; init; } = "";
    public int ValueDelta { get; init; }
}

public sealed class BattleEnvironmentFacts
{
    public string WeatherId { get; init; } = "";
    public string TerrainId { get; init; } = "";
    public string LightLevelId { get; init; } = "";
}
```

Rules:

- Environment facts are battle-only. World map does not gain weather.
- Overlay services are queryable from the unit's projected equipment sources.
- If multiple overlays conflict, use deterministic priority from content ABI.
- Passive effects that create selectable skills still go through `BattleSkillAvailabilityService`.

Tests:

- A representative weapon overlay changes resolved weapon profile.
- A representative attack/defense modifier changes hit or defense calculation only in its condition.
- Battle environment condition is active in battle and absent outside battle.
- Run `dotnet build magic.csproj`.

## Task 10: Add Dispatcher, Conditions, Actions, Dice, And Target Selection

Purpose: execute representative equipment abilities through a common runtime path.

Edit:

- New dispatcher files under equipment ability runtime folder.
- Existing battle event/timing owner discovered by `rg -n "trigger|timing|on_battle|on_attack|on_hit" scripts/systems/battle`.
- `BattleRuntimeServices.cs` for service wiring.

Contracts:

```csharp
public sealed class EquipmentAbilityDispatchContext
{
    public BattleUnitState SourceUnit { get; init; }
    public BattleUnitState? TargetUnit { get; init; }
    public BattleEquipmentAbilitySourceState SourceEquipment { get; init; }
    public EquipmentAbilityDef Ability { get; init; }
    public BattleEnvironmentFacts EnvironmentFacts { get; init; }
    public BattleEquipmentAbilityRuntimeState RuntimeState { get; init; }
}

public interface IEquipmentAbilityConditionHandler
{
    bool Evaluate(EquipmentAbilityDispatchContext context, EquipmentAbilityConditionDef condition);
}

public interface IEquipmentAbilityActionHandler
{
    EquipmentAbilityActionResult Execute(EquipmentAbilityDispatchContext context, EquipmentAbilityActionDef action);
}
```

Rules:

- Target selection supports deterministic weighted random selection for equipment targets.
- Dice rolls use existing battle RNG service and are recorded in action result where current result structures allow it.
- Durability actions use the selector/commit contract from Task 4.
- Granted skill actions register availability entries; they do not mutate learned skills.
- Daily/monthly/persistent usage changes stage writeback instead of directly mutating persistent equipment during battle.

Tests:

- Trigger dispatch executes a simple on-hit equipment ability.
- Condition false prevents action.
- Weighted equipment target selector is deterministic with seeded RNG.
- Granted equipment skill appears in availability and can be selected by entry id.
- Run `dotnet build magic.csproj`.

## Task 11: Add Battle-End Staged Commit Pipeline

Purpose: safely persist equipment ability state only after battle resolution.

Edit:

- `scripts/systems/game_runtime/GameRuntimeBattleWritebackService.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`
- Runtime state/result DTOs touched by battle finish.

Rules:

- Combat cannot save active battle state.
- Equipment ability persistent changes are collected during battle as staged commits.
- Final battle resolution applies staged commits after normal battle-local views are committed.
- Failed/aborted battle resolution must not partially apply equipment ability staged commits.
- Enemy battle-only equipment commits are discarded.

Tests:

- Battle victory commits per-day usage to persistent player equipment.
- Battle loss/abort behavior follows current battle writeback policy and does not save active combat state.
- Enemy battle-only equipment usage does not write to player warehouse.
- Run `dotnet build magic.csproj`.

## Task 12: Add Presentation Queries, Representative Content, And Closure Tests

Purpose: prove the framework can support docs weapon abilities without requiring full content conversion.

Edit:

- UI/presentation query owner discovered around battle HUD and equipment inspection.
- Add a small set of representative equipment ability resources under the existing data folder style.
- Add static content tests and headless runtime regressions.

Representative content should cover:

- Passive weapon profile overlay.
- Conditional attack/defense modifier.
- Equipment-granted active skill.
- Durability damage with selected target commit.
- Per-world-day usage.
- Per-world-month usage.
- Persistent counter.
- Creature-type condition from `BattleUnitState`.
- Battle environment condition.

Rules:

- Presentation reads projected equipment ability summaries from the unit/equipment state.
- Weapons whose design depends on unsupported current systems remain marked framework-only/deferred in docs.
- The framework does not require converting every file under `docs/design/weapons/by_family` before merge.

Tests:

- Static validator passes representative V1 content.
- Headless battle executes representative equipment ability flow.
- HUD/text snapshot exposes equipment granted skills distinctly from learned skills.
- Run:

```bash
dotnet build magic.csproj
python tests/run_regression_suite.py
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs
godot --headless -s res://tests/world_map/schema/run_world_map_low_level_defensive_regression.cs
```

## Cross-Cutting Acceptance Criteria

- Learned skills, equipment-granted skills, and scoped auto-cast skills are never conflated.
- Equipment abilities can be queried directly from battle units after projection.
- Persistent equipment state mutates only through battle-end writeback or normal non-combat equipment flows.
- AI simulation rollback covers every equipment ability runtime mutation.
- Random target selection is deterministic and commits the originally selected target.
- Creature classification is read from `BattleUnitState`.
- Battle environment facts exist only inside battle.
- No active battle save/load is introduced.
- No compatibility aliases or migrations are added without explicit approval.

## Recommended PR Split

1. Skill entry identity and known-skill availability parity.
2. AI and scoped auto-cast migration.
3. Durability selector/commit split.
4. Static content ABI and validator.
5. Projection sources, creature tags, enemy battle-only equipment.
6. Runtime state stores and AI guard.
7. Overlay/modifier/environment services.
8. Dispatcher/action handlers and granted skill integration.
9. World-bound usage counters and battle-end commit.
10. Representative content, presentation queries, and closure regressions.

## Stop Conditions

Stop and ask for design confirmation if implementation reveals any of these:

- Existing save/load requires preserving old active battle payloads.
- Current battle command UI cannot carry `skill_entry_id` without changing public save or network-like payloads.
- Equipment instance state is not the true owner for world-bound equipment counters.
- Existing battle writeback policy applies loss/abort persistence differently than assumed here.
- A representative weapon requires summons, zones, movement hooks, reaction confirmation, or another V1-excluded mechanism to prove the framework.
