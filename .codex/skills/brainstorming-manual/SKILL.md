---
name: brainstorming-manual
description: Use only when the user explicitly asks to brainstorm, explore approaches, compare options, design a solution, or write a spec. Never use for implementation, bug fixes, refactors, code review, routine development, or test repair.
---

# Brainstorming Manual

This skill is a lightweight, manual-only design workflow.

Use it to think through a problem before implementation:
- clarify the problem
- surface constraints
- compare 2-3 viable approaches
- recommend one approach
- optionally write a short design spec

Keep the skill focused on decision-making and structure.
Do not let it take over normal coding work.

## Use this skill only when

Use this skill only if the user explicitly asks to do one or more of the following:
- brainstorm ideas
- think through options first
- compare approaches
- design before coding
- write a spec
- discuss trade-offs before implementation

Typical trigger phrases:
- "Use brainstorming on this"
- "Let's think through the design first"
- "Give me 2-3 options before coding"
- "Do not implement yet"
- "Write a short design spec"

## Do not use this skill when

Do not use this skill for:
- direct implementation requests
- bug fixes
- routine refactors
- code review
- test fixes
- config changes
- dependency updates
- straightforward CRUD or glue code
- tasks that are already clear and small enough to execute directly

If the task is obvious and low-risk, do not create process overhead.

## Core behavior

### 1) Ground yourself first

Before asking broad questions, inspect the available context:
- relevant files
- existing architecture
- nearby code
- tests
- README or docs
- AGENTS.md or repository instructions

Do not ask questions that the repository already answers.

### 2) Keep interaction efficient

Ask at most one focused question at a time, and only if it is genuinely needed.

If there is already enough context to propose options, do that immediately.

Avoid long interrogations.
Avoid generic discovery questions.
Avoid acting like the project is a blank slate when the repo already provides context.

### 3) Produce options

Present 2-3 realistic approaches.

For each option, include:
- a short description
- why it would work here
- main benefits
- main drawbacks
- key risks
- rough implementation complexity

Prefer options that are actually compatible with the current codebase, not generic textbook patterns.

### 4) Recommend one option

After listing options, recommend one.

Explain:
- why it is the best fit
- what assumptions it depends on
- what trade-offs the user is accepting
- what could still change the decision

Be decisive, but do not hide uncertainty.

### 5) Stop before implementation

Use this skill for design, not execution.

Unless the user explicitly changes the task:
- do not write production code
- do not edit source files
- do not refactor
- do not commit
- do not automatically transition into implementation

If the user wants a spec, write the spec.
If the user wants code, move the task out of this skill.

## Default response structure

Prefer this structure in chat:

1. **Problem framing**  
   Briefly restate the problem, goals, and important constraints.

2. **Options**  
   Present 2-3 viable approaches with trade-offs.

3. **Recommendation**  
   State the preferred option and why.

4. **Assumptions / open questions**  
   List only the questions that materially affect the decision.

5. **Spec draft**  
   Include only if the user asked for a spec or approved a direction.

## Spec writing mode

If the user asks for a design doc or approves a direction and wants it written down, create a concise spec.

Default path:
`docs/design/YYYY-MM-DD-<topic>-design.md`

Use this structure:

```md
# <Title>

## Problem
What problem is being solved?

## Goals
- Goal 1
- Goal 2

## Non-goals
- Not doing X
- Not solving Y

## Constraints
- Technical constraints
- Product constraints
- Timeline or compatibility constraints

## Proposed approach
Describe the recommended design clearly and concretely.

## Alternatives considered
### Option A
Why it was considered, and why it was not chosen.

### Option B
Why it was considered, and why it was not chosen.

## Risks
- Risk 1
- Risk 2

## Open questions
- Question 1
- Question 2

## Next implementation steps
1. Step 1
2. Step 2
3. Step 3
```
