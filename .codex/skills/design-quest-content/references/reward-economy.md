# Quest Reward Economy

## Skill Reward Review

For every mastery, unlock, or skill-related reward, inspect:

- `growth_tier` and the power budget it implies
- tactical leverage: mobility, extra actions, summon, control, AOE, execution, or resource generation
- normal learning requirements and whether the quest is intended to bypass them
- `learn_source` and whether the skill is internal, identity/profession granted, or normally learnable
- random starting pools and whether exclusivity is intentional
- duplicate acquisition behavior
- target character and whether that member is guaranteed to exist
- whether the reward creates a build choice or merely overwrites the default path

Do not approve a reward solely because the schema accepts `skill_unlock`.

## Milestone Scale

- **Teaching quest**: mastery for an already-known skill, gentle growth, or consumable/material support.
- **Basic milestone**: one deterministic, immediately usable basic tactical concept.
- **Major milestone**: stronger build-defining skill, equipment, profession, or resource change with an explicit gate.

Intermediate, advanced, ultimate, long-movement, extra-action, summon, strong-control, and broad-AOE rewards normally require the major-milestone treatment.

## Supporting Rewards

For gold, items, consumables, materials, equipment, reputation, or other state:

1. Confirm a current runtime consumer exists.
2. Confirm inventory/capacity/equipment requirements and duplicate behavior.
3. Confirm the reward supports a growth pillar rather than becoming an isolated power loop.
4. Confirm the amount matches quest timing and repeatability.

## Output

Classify each reward as:

- coherent and usable
- schema-valid but progression-risky
- unreachable or unusable in current runtime
- duplicate-sensitive
- target-character-sensitive
- requires an explicit milestone decision
