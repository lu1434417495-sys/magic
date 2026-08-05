# Battle AI Pipeline Checklist

Use this checklist for action types, evaluators, score signals, target routes, transition behavior, and decision dispatch. Re-read `docs/design/battle/ai_score_parameters.md`, `docs/design/battle/skill_runtime.md`, and the current owners before relying on class names or fields.

## End-to-End Path

| Stage | Required evidence |
|---|---|
| Behavior claim | Define what the unit should prefer, when it should decline, and its legal fallback. |
| Authoring | Express configuration in an action/profile Resource. Avoid executable battle logic and runtime state in the Resource. |
| Definition | Project every authored field into an immutable typed definition. Preserve optional-value semantics deliberately. |
| Validation | Reject unknown action kinds, invalid route/selection combinations, incompatible skills, and malformed profile values before snapshot publication. |
| Dispatch | Ensure the Resource-to-definition dispatcher and action-kind mapping recognize the new shape. |
| Assembly | Convert the definition into a typed runtime action entry/plan. Bind only borrowed immutable definitions and explicit services. |
| Evaluation | Produce candidate facts or a detached action intent without committing state. Define empty-target and invalid-candidate behavior. |
| Canonical preview | Use the formal command preview whenever legality, targets, path clipping, cost, barriers, or special resolution can differ. |
| Scoring | Populate typed score input, apply the active immutable profile, and preserve deterministic tie ordering. |
| Safety/failure | Pass payload/safety gates and select an explicit failure or fallback policy. Do not silently turn an invalid action into another semantic action. |
| Decision result | Deep-copy the chosen command, score breakdown, and trace facts needed after the decision scope closes. |
| Commit | Revalidate current state before issuing the command. A prior preview is evidence, not authorization to commit. |
| Validation | Cover schema/definition, assembly, evaluator behavior, preview parity, mutation safety, decision lifetime, and trace/ordering as applicable. |

## Adding or Changing an Action Type

Audit all of these surfaces:

1. Authored action Resource and exported fields.
2. Typed action kind and immutable definition.
3. Resource-to-definition dispatch and content validation.
4. Runtime action entry/plan assembly.
5. Evaluator selection and typed input.
6. Target route, selection mode, and skill compatibility.
7. Canonical preview requirements.
8. Score input and profile consumption.
9. Safety gate, failure policy, decision trace, and commit dispatch.
10. Tests for definition/assembly plus user-visible behavior.

A change is incomplete if any required consumer infers a missing field from an id, tag, display name, or unrelated payload.

## Changing a Score Parameter

Check the live repository for:

- authoring export and neutral/default value;
- immutable profile-definition projection;
- typed score input and consuming score component;
- score breakdown and stable trace/projection;
- simulation-local override/copy-on-write surface;
- tuner search-space field, if the parameter is tunable;
- ordering regression proving unchanged defaults when compatibility is intended.

Do not copy the current parameter list into this skill. The current design document and code own that list.

## Evaluator Design

- Separate candidate generation, legality, utility facts, and score aggregation.
- Prefer small typed records to weak dictionaries or general-purpose parameter bags.
- Reuse availability and canonical battle services shared by manual commands.
- Define behavior for no target, no path, unaffordable action, stale entry, and preview rejection.
- Keep tie-breaking stable and explicit; do not rely on dictionary or filesystem enumeration order.
- Keep objective-specific behavior in typed objective facts/evaluators rather than parsing UI or report payloads.
