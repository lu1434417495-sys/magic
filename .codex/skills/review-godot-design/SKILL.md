---
name: review-godot-design
description: Review design documents, proposals, specifications, test matrices, architecture plans, and claimed feature landings against the current Godot 4.6 C# repository. Use when Codex must assess design feasibility or completeness, verify a proposal against current owners and tests, classify landed versus planned work, audit cross-system parity, or identify the smallest landing gaps before implementation. This skill is findings-first and read-only by default; use algorithm-design to create a new implementation plan and godot-code-review to review a diff, commit, branch, or PR.
---

# Review Godot Design

Verify claims against current code, resources, tests, and current-design documents. Do not treat a proposal, review, task list, test name, or historical path as implementation truth.

## Boundaries

- Remain read-only unless the user explicitly asks to implement or repair findings.
- Complete the review before handing a confirmed gap to `algorithm-design` or an implementation skill.
- Use `godot-code-review` when the primary evidence is a diff, commit, branch, or PR.
- Follow `AGENTS.md` for documentation placement and compatibility decisions. Never invent legacy aliases, migrations, or fallback schemas.

## Workflow

1. Establish the artifact and review mode.
   - Record the current repository root, branch, HEAD, upstream, dirty state, and any commit or version anchor claimed by the artifact. Keep checkout facts separate from another worktree or historical review.
   - Identify whether each input is current design, proposal, content guidance, point-in-time review, discussion, or archive material.
   - Select a mode from [references/review-modes.md](references/review-modes.md).
   - For broad documents, run:

```powershell
powershell -ExecutionPolicy Bypass -File .codex/skills/review-godot-design/scripts/build_review_scope.ps1 `
  -Artifact docs/proposals/path/to/proposal.md
```

   The first run is a lexical scope seed, not the review. For a long artifact, select the material claim or section and rerun with `-LineStart <n> -LineEnd <n>` so explicit paths and context units come from that bounded slice. Markdown table body rows are emitted as `table-row` candidates.

2. Load current ownership.
   - Read `AGENTS.md` and `docs/design/project_context_units.md`.
   - Load only the owning context units, adjacent units crossed by the claim, their current detail documents, owner code/resources, and focused tests.
   - Resolve stale paths and names to current owners before concluding that implementation is absent.

3. Turn prose into verifiable claims.
   - Assign stable claim IDs.
   - Separate behavior, data shape, ownership, lifecycle, ordering, UI/headless visibility, persistence, compatibility, and test assertions.
   - Record the exact current evidence or the missing evidence for every material claim.

4. Verify end-to-end parity.
   - Use [references/end-to-end-parity.md](references/end-to-end-parity.md) when a claim crosses authoring, projection, runtime, preview, AI, presentation, persistence, or tests.
   - Verify semantics and call order, not merely that a class, field, resource, or test exists.

5. Classify truth.
   - Apply [references/landed-vs-planned.md](references/landed-vs-planned.md).
   - `mixed` describes a document, not an atomic claim status. Split a composite row into separate claims; use `partially-landed` only when one behavior is present on some required surfaces and absent on others.
   - Do not label a mixed document or partial call chain as current implementation truth.

6. Report findings first.
   - Order findings by concrete correctness, architecture, lifecycle, save/schema, and verification risk.
   - Cite current `path:line` evidence.
   - Then give claim classifications, open questions, and residual gaps.
   - If there are no findings, say so and name the unverified surfaces.

## Output Contract

For each finding, use:

`[severity] [claim-id] path:line - mismatch, failure mode, and missing landing surface`

Use `P0` for deterministic corruption/data loss or an unusable design foundation, `P1` for a major runtime or ownership contract break, `P2` for a material incompleteness, owner mismatch, or verification gap, and `P3` for a low-risk clarity or maintainability issue with no known behavior break.

Then include:

- `Claim status`: claim ID, classification, evidence, and next boundary
- `Open questions / assumptions`
- `Residual risks / unverified surfaces`
