# Application E2E

Use application E2E only for contracts that require the configured main scene, canonical autoloads, real input propagation, scene transitions, or cold-process persistence.

## Journey Contract

- Start from `project.godot` through the production main scene.
- Drive keyboard/actions with the shared input driver and pointer input through the viewport.
- Do not call UI callbacks, command handlers, or runtime services directly.
- Assert stable semantic state, not pixel positions or incidental labels.

## User Data Isolation

- Let `tests/run_e2e_suite.py` allocate isolated user-data roots.
- Share a sandbox only for steps in one declared multi-process journey.
- Serialize steps that depend on the same save.
- Never reuse the developer's normal `user://`.

## Waiting

Poll a stable semantic condition with an explicit timeout:

- scene identity or modal state
- save-list entry
- enabled control or focus target
- runtime snapshot fact

Do not use a fixed sleep as the only readiness condition.

## Determinism

A deterministic seed may fix the allowed random stream before main-scene startup. It must not:

- invoke callbacks or gameplay commands
- inject a victory or reward
- bypass validation
- replace real input

Headless E2E proves semantic flow. It does not prove pixels, DPI scaling, native dialogs, or final visual layout.

## Validation

Build first, run the outer-runner tooling regression, then run the narrow scenario. Keep E2E opt-in and outside the routine regression suite.
