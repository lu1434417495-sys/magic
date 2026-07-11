from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
MIGRATOR = REPOSITORY_ROOT / "tools" / "migrate_test_exit_calls.py"


class MigrateTestExitCallsTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory()
        self.workspace = Path(self._temporary_directory.name)
        self.tests_root = self.workspace / "tests"
        self.tests_root.mkdir()
        self.manifest = self.workspace / "manifest.txt"

    def tearDown(self) -> None:
        self._temporary_directory.cleanup()

    def write_runner(self, relative_path: str, source: str) -> Path:
        path = self.tests_root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(source, encoding="utf-8", newline="\n")
        return path

    def run_migrator(self, mode: str, manifest: Path | None = None) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(MIGRATOR),
                mode,
                "--root",
                "tests",
                "--manifest",
                str(manifest or self.manifest),
            ],
            cwd=self.workspace,
            check=False,
            capture_output=True,
            text=True,
        )

    def test_check_reports_direct_finish_without_modifying_source(self) -> None:
        original = """using Godot;

public partial class DirectRunner : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Quit(_test.Finish(\"Direct regression\"));
    }
}
"""
        runner = self.write_runner("direct.cs", original)

        result = self.run_migrator("--check")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(runner.read_text(encoding="utf-8"), original)
        self.assertEqual(self.manifest.read_text(encoding="utf-8"), "tests/direct.cs\n")

    def test_apply_migrates_finish_with_explicit_exit_code(self) -> None:
        runner = self.write_runner(
            "explicit.cs",
            """using Godot;

public partial class ExplicitRunner : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Quit(_test.Finish(\"Explicit regression\", 1));
    }
}
""",
        )

        result = self.run_migrator("--apply")

        self.assertEqual(result.returncode, 0, result.stderr)
        migrated = runner.read_text(encoding="utf-8")
        self.assertIn("class ExplicitRunner : LifecycleTestSceneTree", migrated)
        self.assertIn(
            'RequestTestExit(_test.Finish("Explicit regression", 1));',
            migrated,
        )
        self.assertNotIn("Quit(", migrated)

    def test_apply_migrates_stored_finish_result_followed_by_quit(self) -> None:
        runner = self.write_runner(
            "stored.cs",
            """using Godot;

public partial class StoredRunner : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = _test.Finish(\"Stored regression\");
        Quit(exitCode);
    }
}
""",
        )

        result = self.run_migrator("--apply")

        self.assertEqual(result.returncode, 0, result.stderr)
        migrated = runner.read_text(encoding="utf-8")
        self.assertIn('TestResult exitCode = _test.Finish("Stored regression");', migrated)
        self.assertIn("RequestTestExit(exitCode);", migrated)

    def test_apply_preserves_failure_fallback_for_deferred_stored_result(self) -> None:
        runner = self.write_runner(
            "deferred.cs",
            """using Godot;

public partial class DeferredRunner : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = 1;
        try
        {
            exitCode = _test.Finish(\"Deferred regression\");
        }
        finally
        {
            Quit(exitCode);
        }
    }
}
""",
        )

        result = self.run_migrator("--apply")

        self.assertEqual(result.returncode, 0, result.stderr)
        migrated = runner.read_text(encoding="utf-8")
        self.assertIn("TestResult exitCode = null;", migrated)
        self.assertIn(
            'RequestTestExit(exitCode ?? _test.Finish("Deferred regression", 1));',
            migrated,
        )

    def test_apply_routes_direct_quit_one_through_unique_test_label(self) -> None:
        runner = self.write_runner(
            "failure.cs",
            """using Godot;

public partial class FailureRunner : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            Quit(_test.Finish(\"Failure regression\"));
        }
        catch
        {
            Quit(1);
        }
    }
}
""",
        )

        result = self.run_migrator("--apply")

        self.assertEqual(result.returncode, 0, result.stderr)
        migrated = runner.read_text(encoding="utf-8")
        self.assertIn(
            'RequestTestExit(_test.Finish("Failure regression", 1));',
            migrated,
        )
        self.assertEqual(migrated.count("RequestTestExit("), 2)

    def test_manifest_is_sorted_exact_and_repeatable(self) -> None:
        source_template = """using Godot;
public partial class {name} : SceneTree
{{
    private readonly TestHarness _test = new();
    public override void _Initialize() => Quit(_test.Finish(\"{name}\"));
}}
"""
        self.write_runner("zeta.cs", source_template.format(name="Zeta"))
        self.write_runner("nested/alpha.cs", source_template.format(name="Alpha"))

        first = self.run_migrator("--check")
        first_manifest = self.manifest.read_bytes()
        second = self.run_migrator("--check")

        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(self.manifest.read_bytes(), first_manifest)
        self.assertEqual(
            first_manifest,
            b"tests/nested/alpha.cs\ntests/zeta.cs\n",
        )

    def test_shared_lifecycle_base_is_not_a_migration_candidate(self) -> None:
        base = self.write_runner(
            "shared/LifecycleTestSceneTree.cs",
            """using Godot;
public abstract partial class LifecycleTestSceneTree : SceneTree
{
    private protected void RequestTestExit(TestResult result) { }
}
""",
        )

        result = self.run_migrator("--check")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            base.read_text(encoding="utf-8"),
            """using Godot;
public abstract partial class LifecycleTestSceneTree : SceneTree
{
    private protected void RequestTestExit(TestResult result) { }
}
""",
        )
        self.assertEqual(self.manifest.read_bytes(), b"")

    def test_apply_is_idempotent_and_post_apply_check_is_empty(self) -> None:
        runner = self.write_runner(
            "idempotent.cs",
            """using Godot;
public partial class IdempotentRunner : SceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => Quit(_test.Finish(\"Idempotent\"));
}
""",
        )

        first = self.run_migrator("--apply")
        migrated = runner.read_bytes()
        second = self.run_migrator("--apply")
        post_check_manifest = self.workspace / "post-check.txt"
        checked = self.run_migrator("--check", post_check_manifest)

        self.assertEqual(first.returncode, 0, first.stderr)
        self.assertEqual(second.returncode, 0, second.stderr)
        self.assertEqual(checked.returncode, 0, checked.stderr)
        self.assertEqual(runner.read_bytes(), migrated)
        self.assertEqual(post_check_manifest.read_bytes(), b"")

    def test_unknown_shape_fails_atomically_without_manifest(self) -> None:
        known = self.write_runner(
            "known.cs",
            """using Godot;
public partial class KnownRunner : SceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => Quit(_test.Finish(\"Known\"));
}
""",
        )
        unknown = self.write_runner(
            "unknown.cs",
            """using Godot;
public partial class UnknownRunner : SceneTree
{
    public override void _Initialize() => Quit(ResolveExitCode());
    private int ResolveExitCode() => 1;
}
""",
        )
        known_before = known.read_bytes()
        unknown_before = unknown.read_bytes()

        result = self.run_migrator("--apply")

        self.assertEqual(result.returncode, 2)
        self.assertIn("unknown Quit argument shape", result.stderr)
        self.assertEqual(known.read_bytes(), known_before)
        self.assertEqual(unknown.read_bytes(), unknown_before)
        self.assertFalse(self.manifest.exists())

    def test_stored_result_with_an_unrelated_assignment_is_rejected(self) -> None:
        runner = self.write_runner(
            "unknown_stored.cs",
            """using Godot;
public partial class UnknownStoredRunner : SceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize()
    {
        int exitCode = 0;
        exitCode = _test.Finish(\"Unknown stored\");
        Quit(exitCode);
    }
}
""",
        )
        before = runner.read_bytes()

        result = self.run_migrator("--check")

        self.assertEqual(result.returncode, 2)
        self.assertIn("stored Quit value is not a recognized Finish result", result.stderr)
        self.assertEqual(runner.read_bytes(), before)
        self.assertFalse(self.manifest.exists())


if __name__ == "__main__":
    unittest.main()
