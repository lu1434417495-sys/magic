# Windows export smoke

Run the real Windows Desktop export and all packaged-content cases from the
repository root:

```powershell
python tests/export/run_windows_export_smoke.py
```

The runner requires the Godot 4.6 Mono Windows export templates. It exports to
an isolated system temporary directory, starts the console-wrapper executable
from a different empty working directory, checks the packaged JSON probe and
the production typed engine-asset catalog, rebuilds the packaged world JSON
registry and verifies preset/root/mounted/shared stable-ID closure, verifies
three expected nonzero failure cases, and deletes the exported binaries when
the run ends.

The official release template does not expose editor-only `--script` or scene
override flags. The runner therefore writes an untracked `override.cfg` beside
the temporary executable. That file selects the smoke scene and test-only
no-op autoloads without changing the production project settings or lifecycle
owners. The scene asserts those temporary overrides before checking the pack.

Set `MAGIC_GODOT_BIN` only when `godot` is not on `PATH`.
