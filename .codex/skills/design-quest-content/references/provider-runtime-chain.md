# Provider And Runtime Chain

Use this as a checklist, then reopen current source. Names and ownership may evolve.

## Authoring And Snapshot

Trace:

```text
data/configs/quests/*.tres
-> QuestDef
-> QuestContentRegistry / ProgressionContentRegistry
-> QuestContentValidator
-> QuestDefinition
-> ContentSnapshot / GameContentCatalog
```

Check exact schema, cross-table references, provider rules, objective support, reward support, and immutable projection. Resource validation proves loadability and declared constraints only.

## Provider And Offer

Verify the configured provider kind, provider interaction id, listing channels, and optional settlement filters agree with a current offer producer.

Inspect the relevant path:

- service contract board
- service bounty registry
- NPC interaction and `NpcQuestOfferWindowData`

Confirm the offer builder applies availability, prerequisite, already-active, completed, repeatability, and modal-confirmation rules consistently. Do not infer an NPC offer from resource presence alone.

## Accept And Progress

Trace typed acceptance through the current requirement evaluator and command handler into the quest journal owner. Then identify the real producer for every objective event:

- item submission
- enemy defeat
- settlement action
- any newly proposed objective kind

The progress owner must update the canonical journal, not a detached query result or UI payload.

## Completion, Failure, And Restart

Confirm:

- completion moves the quest to the canonical claimable state
- failure policy comes from the typed failure-policy owner
- restartability does not reuse repeatability semantics
- terminal and restartable paths clean modal/offer state
- repeated events cannot apply progress after terminal state

## Presentation

Check the relevant settlement, NPC, headless, and text snapshot surfaces. UI should render detached window data and submit typed intent; it should not mutate `PartyState` or `QuestJournalState` directly.
