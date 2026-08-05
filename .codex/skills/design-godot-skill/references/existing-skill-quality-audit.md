# Existing Skill Quality Audit

## Audit Sequence

1. Run the static inventory script for breadth.
2. Run the production resource validator for schema truth.
3. Group candidates by pattern without editing.
4. Exclude training, equipment-granted, race, profession, internal, dynamic, and special-profile cases individually.
5. Read the resource, current typed owner, runtime consumer, AI path, descriptions, and focused tests for each remaining candidate.
6. Present a field-level preview and wait for approval before changing existing `.tres` content unless direct implementation was already authorized.

## Finding Classes

- **Hard schema error**: production validator rejects the resource or reference.
- **Runtime gap**: authored data loads but no complete execution/targeting/preview consumer exists.
- **Description mismatch**: user-facing text disagrees with effective values or runtime semantics.
- **Progression candidate**: cap ladder, growth tier, reward timing, mastery, or learn source deserves review.
- **AI/presentation gap**: execution works but AI cannot value/use it or the HUD/log cannot explain it.
- **Balance/design question**: legal and implemented, but role, cost, leverage, or growth remains a product decision.

Never convert a candidate count into a bulk-fix count.

## Level And Growth Review

Check together:

- non-core cap, absolute cap, and any dynamic maximum
- trigger-lock timing and core promotion path
- effect and variant level windows
- `level_overrides` and effective getters
- `level_description_configs`
- mastery curve and reward facts
- `growth_tier` and attribute-growth budget
- `learn_source` and all acquisition pools

Changing a maximum without adding real effects, descriptions, mastery coverage, and progression meaning is not a complete repair.

## Evidence Packet

For each proposed change, include:

- resource path and `skill_id`
- current field values
- current effective runtime behavior
- validator and regression evidence
- similar valid resource, if used
- exact proposed fields and owner code
- compatibility or content-migration question
- validation commands

Resource validation proves loading and declared constraints. It does not prove content quality, balance, description accuracy, AI behavior, or acquisition safety.
