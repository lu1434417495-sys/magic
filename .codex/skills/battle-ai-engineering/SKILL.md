---
name: battle-ai-engineering
description: Design, implement, review, or optimize battle AI runtime behavior in this Godot 4.6 C# repository. Use when work touches enemy AI action Resources or definitions, action assembly and dispatch, BattleAi evaluators or score inputs/profiles, canonical command preview, mutation diagnostics, decision lifetime, movement/path caches, AI regressions, or behavior-preserving AI performance changes. Do not use this skill for numeric BattleSim balance runs or score-weight tuning; use battle-sim-analysis for those tasks.
---

# Battle AI Engineering

Engineer AI behavior as one typed, read-only decision pipeline. Keep content authoring, runtime evaluation, canonical battle rules, scoring, mutation safety, and validation in sync.

## Load Context

1. Read `docs/design/project_context_units.md`.
2. Load CU-15 and CU-16. Load CU-20 for action/profile authoring or definitions, CU-13 for skill semantics, and CU-19 for tests or benchmarks.
3. Read `docs/design/battle/ai_score_parameters.md`. Read `docs/design/battle/skill_runtime.md` when an evaluator selects or estimates skills.
4. Read the current owners and nearest tests discovered from the repository. Treat `docs/design/` and current code as truth; proposals express intent only.
5. Run the read-only inventory before planning:

```powershell
python .codex/skills/battle-ai-engineering/scripts/build_ai_change_packet.py --root . --term <action-or-concept>
```

Omit `--term` for the complete AI surface. Add `--changed-only` when auditing a dirty worktree.
Term filtering is lexical and returns a scope seed, not a dependency graph. Always expand the packet to the direct canonical rule owners, shared state/projection types, mutation/lifetime guards, and focused tests reached by the changed call path; omit `--term` when that ownership is unclear.

## Classify the Change

- **Action contract**: authoring Resource, immutable definition, validation, assembly, dispatch.
- **Candidate behavior**: typed evaluator, target/position query, action intent, failure policy.
- **Scoring**: typed score input, profile definition, score breakdown, ordering, trace.
- **Canonical-rule-sensitive**: legality, target set, range, resource cost, barriers, movement, hit/save/damage, or special resolution.
- **Read-only safety**: preview lifetime, detached output, mutation snapshot/guard.
- **Path/performance**: movement query, cache key/epoch/invalidation, bounded versus formal evidence.

Use `battle-sim-analysis` instead when the requested outcome is a numeric simulation, balance diagnosis, or parameter search rather than an AI runtime contract.

## Workflow

1. State the user-visible decision claim and the canonical battle rule that decides whether it is legal.
2. Trace the complete action path with [ai-pipeline.md](references/ai-pipeline.md). Do not implement only the Resource, evaluator, or scorer slice.
3. Keep authored Resources declarative. Project them to immutable typed definitions before runtime; reject unknown action shapes during content validation.
4. Assemble a typed runtime action plan once per owning scope. Keep evaluators focused on candidate facts and return detached intents/results.
5. Delegate legality-changing questions to canonical preview. A fast evaluator may approximate ranking only when it cannot change the legal target or command set.
6. Preserve preview read-only behavior and exact mutation diagnostics with [readonly-preview-and-mutation.md](references/readonly-preview-and-mutation.md).
7. For movement or performance work, establish correctness first, then follow [path-cache-performance.md](references/path-cache-performance.md). If the user requested performance analysis only, remain read-only until they approve implementation.
8. Validate the affected lanes using [validation-matrix.md](references/validation-matrix.md). Build before running C# headless runners.
   - If the user requires strict zero-write inspection, do not run build, Godot, coverage, benchmark, or simulation commands that can create `bin/`, `obj/`, `.godot/`, reports, or user state. Use static owner/test inspection and read-only Git checks, then state exactly what was not executed.
9. Update `docs/design/project_context_units.md` only when runtime ownership, dependencies, or recommended read sets changed.

## Non-Negotiable Boundaries

- Do not branch on a concrete enemy, action, skill, or equipment id for a cross-system mechanic.
- Do not reimplement targeting, range, barrier, hit, save, damage, or movement legality in scoring code.
- Do not consume production RNG, charges, resources, cooldowns, state, or event batches during evaluation.
- Do not let decision context, mutable commands, profile borrowers, or mutation snapshots escape the decision scope.
- Do not treat a canonicalized clone as raw-exact mutation evidence when canonicalization can erase null, order, owner-presence, or invalid diagnostic distinctions.
- Do not change score defaults casually. Neutral defaults and ordering regressions are required for behavior-preserving profile additions.
- Do not use benchmark or simulation output as routine regression evidence unless the user requested that class of validation.

## Deliverable

Report:

- the action/evaluator/score/preview path changed;
- canonical rules reused;
- mutation and lifetime guarantees;
- cache key/invalidation impact, when applicable;
- focused commands run and whether evidence is regression, bounded diagnostic, benchmark, or simulation.
