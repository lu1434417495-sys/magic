# Content JSON offline validation CLI

This standalone .NET tool validates authoring files without loading Godot, starting the game, or
building `ContentSnapshot`. It runs the registered domain's existing pipeline exactly once:

```text
document load -> file-local template merge -> strict typed DTO parse -> domain-local validation
```

Build and run from the repository root:

```powershell
dotnet build tools/content_json_validation/Magic.ContentJsonValidation.Cli.csproj
dotnet .tmp/content_json_validation/bin/Debug/net8.0/Magic.ContentJsonValidation.Cli.dll --domain items --input data/configs/json/items --format json
dotnet .tmp/content_json_validation/bin/Debug/net8.0/Magic.ContentJsonValidation.Cli.dll --domain equipment_abilities --input path/to/generated/equipment_abilities --format ndjson
```

`--input` accepts one host-filesystem `.json` file or a directory whose direct `.json` children
form the selected domain. `res://` and `user://` are intentionally rejected because this process
does not host Godot. `--format` accepts `json` or `ndjson`.

Exit codes are stable: `0` means valid, `1` means content diagnostics were produced, and `2` means
the CLI/domain/input boundary failed. Every outcome uses protocol
`magic.content_json.validation/v1`; no environment-dependent stack trace is written to stdout.

The catalog contains 30 production domains (plus the test-only schema fixture): skills, inventory
and progression, enemy/encounter, barrier and battle-special profiles, quest/contingency,
BattleSim, and world content. Each registration links the same
strict DTO parser, plain import mapper, nullability policy, and domain-local validator used by the
runtime authoring boundary. The CLI does not reflectively discover DTOs, perform cross-domain
checks, resolve engine assets, run BattleSim, or build a runtime snapshot.

All registered production domains are JSON direct-load domains. Their offline success proves
schema and domain-local validity only; publication and generated-content admission must still run
the repository's cross-domain and simulation gates.
