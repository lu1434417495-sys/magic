# Lifecycle-Safe Test Fixtures

## Choose The Smallest Owner Graph

- Prefer plain definitions or CLR builders when the behavior does not require authored Resource semantics.
- Use `TestResourceOwnership` for pathless or explicitly test-owned authored Resource wrappers.
- Use one scoped `TestContentResourceLoader` with `CacheMode.IgnoreDeep` for path-backed `.tres` graphs.
- Keep the loader alive through projection and every assertion that still borrows the graph.
- Use the project-specific shared projection helper when one exists, such as `TestSkillDefinitionProjection`.

Do not repeatedly call default-cache `GD.Load(...Reuse)` for the same graph inside a lifecycle-sensitive runner.

## Runner Lifecycle

1. Derive the runner from `LifecycleTestSceneTree`.
2. Use `RunAfterProcessStartup(...)` when autoloads, process content, async waits, or Resources are involved.
3. Capture assertions in `TestHarness`.
4. Close explicit fixture/runtime owners in `finally`.
5. Call `TestHarness.Finish(...)` to freeze the result.
6. Submit it through `RequestTestExit(...)`.
7. Let `TestExitCoordinator` and `ApplicationLifetimeCoordinator` perform the canonical shutdown.

Ordinary runners must not call local `SceneTree.Quit`, forced GC, or finalizer drains.

## Async Safety

For `async void` callbacks:

- wrap the whole awaited body in `try/catch/finally`
- record unexpected exceptions
- request exit from `finally`

Otherwise Godot can log the exception while the outer process waits until timeout.

## Diagnosing CI-Only Failures

Separate:

- business assertions
- process exit code
- shutdown report
- GodotSharp fatal/finalizer markers
- platform and runner profile

A functional PASS followed by a finalizer crash is a lifecycle failure. A Windows focused PASS does not disprove an Ubuntu shutdown failure.

When a large resource graph only increases finalizer noise, reduce it to the smallest authored fixture that preserves the ownership contract.
