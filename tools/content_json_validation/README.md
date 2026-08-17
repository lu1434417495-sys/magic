# Content JSON offline validation CLI

This standalone .NET tool validates authoring files without loading Godot, starting the game, or
building `ContentSnapshot`. It runs the registered domain's existing pipeline exactly once:

```text
document load -> file-local template merge -> strict typed DTO parse -> domain-local validation
```

Build and run from the repository root:

```powershell
dotnet build tools/content_json_validation/Magic.ContentJsonValidation.Cli.csproj
dotnet .tmp/content_json_validation/bin/Debug/net8.0/Magic.ContentJsonValidation.Cli.dll --domain schema_fixture --input data/configs/json/schema_fixture --format json
```

`--input` accepts one host-filesystem `.json` file or a directory whose direct `.json` children
form the selected domain. `res://` and `user://` are intentionally rejected because this process
does not host Godot. `--format` accepts `json` or `ndjson`.

Exit codes are stable: `0` means valid, `1` means content diagnostics were produced, and `2` means
the CLI/domain/input boundary failed. Every outcome uses protocol
`magic.content_json.validation/v1`; no environment-dependent stack trace is written to stdout.

Future migrated domains register one `ContentJsonOfflineValidationDomain<TDto,TImport>` in
`ContentJsonOfflineValidationCatalog`. The registration must supply an explicit source-generated
`JsonTypeInfo<TDto>` parse delegate, its typed import normalizer, and its domain-local validator.
The CLI does not reflectively discover DTOs, perform cross-domain checks, or build a runtime
snapshot.
