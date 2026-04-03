# Character Attribute System

## Goal

Implement a complete character attribute system for the player, with six base
attributes, derived combat attributes, resistance attributes, and a separate
reputation-state model for morality and similar social values.

## Requirements

- The player must have six base attributes: strength, agility, constitution,
  perception, intelligence, and willpower.
- Morality must not be part of base attributes, and must live in a separate
  reputation-state model.
- The attribute system must provide derived attributes including resource caps,
  combat values, and resistance values.
- Action points must be a character attribute, not a runtime-only field.
- Battle runtime state may consume action points, but the remaining points in a
  turn must not be stored as part of the character attribute model.
- Derived resistance attributes must include fire, bleed, freeze, lightning,
  poison, and negative-energy resistance.
- Attribute aggregation must support multiple modifier sources such as
  professions, skills, and future equipment or temporary effects.

## Files To Touch

- `scripts/player/progression/*.gd`
- `scripts/systems/attribute_service.gd`
- `scripts/systems/profession_rule_service.gd`
- `scripts/systems/world_map_system.gd`
- `scripts/systems/battle_map_generation_system.gd`
- `scripts/ui/battle_map_panel.gd`
- `prompts/attribute_system.md`

## Acceptance Checks

- Player progression serializes and deserializes the six base attributes and the
  reputation-state model correctly.
- Attribute aggregation returns derived resource, combat, and resistance values.
- Morality is read from reputation state instead of base attributes.
- Battle encounters initialize runtime action points from the player attribute
  system instead of a hardcoded value.
- Headless Godot validation succeeds with no script or scene errors.
