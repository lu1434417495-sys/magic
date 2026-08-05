---
name: design-quest-content
description: Design, audit, repair, or implement quest content and progression-safe rewards for this Godot 4.6 C# repository. Use when changing `QuestDef` `.tres` resources, providers or listing channels, accept requirements, objectives, failure/restart behavior, NPC or contract-board offers, quest progress and claim flow, pending character rewards, skill mastery or unlock rewards, item/gold rewards, quest save transactions, or when checking whether a quest respects the skill-driven progression economy.
---

# Design Quest Content

Treat a quest as a complete provider-to-save transaction, not only a loadable `QuestDef`.

## Operating Modes

- For an existing quest audit, inspect and present field-level findings before editing `.tres` unless the user already requested implementation.
- For a new quest or an approved repair, implement the smallest complete provider, progress, reward, and persistence slice.
- Keep proposal review read-only. Do not silently turn a design question into content or runtime edits.
- Do not add legacy quest payload support, aliases, defaults, or migrations without explicit user approval and a concrete compatibility need.

## Load Context

1. Read `docs/design/project_context_units.md`.
2. Start with CU-02, CU-06, CU-11, CU-12, CU-13, and CU-19. Add CU-08 for settlement UI, CU-14 for skill/attribute growth rules, and CU-21 for text/headless surfaces.
3. Read `docs/design/foundations/skill_centric_game_architecture.md` for reward intent.
4. Read the current resource plus the actual authoring, definition, validator, provider, progress, reward, transaction, save, UI, and test owners. Do not use this skill as project truth.
5. Read [references/provider-runtime-chain.md](references/provider-runtime-chain.md) for the owner checklist.

## Audit Existing Content

Run the read-only inventory:

```powershell
python .codex/skills/design-quest-content/scripts/audit_quest_content.py --repo-root . --summary-only
```

Rerun without `--summary-only` only when individual quest records are needed. Treat its warnings as review candidates. The production loader and validators decide schema validity.

For every quest, record:

- identity, provider kind/id, listing channels, settlement restrictions, tags, prerequisites, confirmation, failure policy, repeatability, and danger override
- objective types, target ids, values, and the runtime event that advances each objective
- reward types, quantities, target member, skill/item references, and duplicate-acquisition behavior
- offer surface, accept evaluation, progress owner, claim owner, rollback boundary, and save projection
- current focused tests and any missing user-visible surface

## Design Workflow

1. Define the player-facing purpose and progression gate.
2. Choose a supported provider/listing combination and verify the offer surface materializes it.
3. Choose objective types with real runtime event producers. A validator accepting an objective type does not prove gameplay can advance it.
4. Trace accept, progress, complete, claim, failure, and restart through the typed owners.
5. Design rewards against [references/reward-economy.md](references/reward-economy.md).
6. Verify transaction and persistence behavior with [references/save-transaction.md](references/save-transaction.md).
7. Preview exact `.tres`, C#, UI, test, and documentation changes.
8. Implement only after the mode's approval boundary is satisfied.

## Reward Rules

- Treat skills as the main power economy. Inspect `growth_tier`, tactical leverage, learning gates, `learn_source`, starting pools, duplicate acquisition, target character, and build choice.
- Prefer modest mastery for a guaranteed known skill, a deliberate basic skill milestone, gentle attribute progress, or resources that support the skill loop.
- Do not place intermediate, advanced, ultimate, extra-action, long-mobility, summon, strong-control, or broad-AOE skills into ordinary early rewards without an explicit milestone rationale.
- Confirm the target member exists in the relevant journey and the reward remains meaningful when already owned.
- Gold, consumables, items, materials, and reputation may support progression but should not conceal a disconnected or unusable reward path.
- Verify the current runtime can consume any item or state that the quest awards.

## Cross-Path Completion Matrix

Confirm each applicable row:

| Surface | Required evidence |
|---|---|
| Authoring | `QuestDef` field and exact production validation |
| Definition | immutable typed projection in the process content snapshot |
| Provider | provider kind/id and listing channel reach a current offer builder |
| Accept | typed requirements and confirmation state |
| Progress | objective event reaches `QuestProgressService` |
| Failure | terminal/restartable semantics use the current typed rule |
| Claim | rewards apply through the canonical progression/inventory gateway |
| Transaction | party/world changes capture, stage, commit, and rollback together |
| Persistence | current save schema round-trips journal and pending rewards |
| Presentation | settlement/NPC/text snapshot surfaces expose stable feedback |
| Tests | validator plus focused runtime/offer/claim/save regression |

## Validation

1. Run `dotnet build magic.csproj` after C# or C# test changes.
2. Run the production resource validator for authored quest changes.
3. Run the closest progression/runtime provider, accept, progress, claim, and persistence regressions discovered under `tests/`.
4. Add NPC/settlement UI or text/headless coverage only when those surfaces changed.
5. Do not add battle simulation or benchmark runners to routine quest validation.
6. Update `docs/design/project_context_units.md` only when ownership, runtime relationships, or recommended read sets changed.

## Resources

- `scripts/audit_quest_content.py`: read-only quest/reward inventory with skill-reference review candidates.
- `references/provider-runtime-chain.md`: provider, accept, progress, and presentation owner map.
- `references/reward-economy.md`: progression and build-choice review rubric.
- `references/save-transaction.md`: claim, rollback, idempotency, and persistence checklist.
