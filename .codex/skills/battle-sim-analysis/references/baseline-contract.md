# Baseline Contract

## Discover Current Baseline

Read the current simulation design document and inspect:

- canonical scenario resource
- benchmark runner
- formal roster builder
- default profiles and manual policy

The established mixed 6-vs-12 mirror is the reference baseline when those current owners still designate it. Treat its scenario, runner logic, roster composition, map contract, and default policy as read-only. Create a separate scenario or roster for variants.

## Invocation

Build first:

```powershell
dotnet build magic.csproj -nologo -clp:ErrorsOnly
```

Then run the current baseline command discovered from the benchmark runner or design document. Environment variables may adjust an invocation only when the runner explicitly supports them; they do not authorize editing the baseline fixture.

## Completion Checks

Before interpreting balance, report:

- process exit code
- requested versus completed runs
- battle-ended count
- timeout/invalid/stall/iteration-budget counts
- average iterations or duration
- faction outcomes
- report and compact-trace paths

Do not fold unfinished runs into normal win-rate denominators unless the current report contract explicitly defines that metric.

## Randomness

Read the current setup-seed and combat-random owners. A setup seed may control roster or placement without making hit, damage, save, or target randomness replayable. Compare aggregates unless current source proves a deterministic combat stream.

## Protection Rule

Never modify a canonical baseline to:

- make a test pass
- rebalance a matchup
- exercise a new mechanic
- improve tuning gradient

Add a named candidate scenario and compare it with the baseline.
