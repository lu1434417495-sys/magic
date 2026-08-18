import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
TOOL_SCRIPT = "res://scripts/tools/run_skill_tres_to_json_converter.cs"
EXPECTED_FILES = [
    f"mage_prismatic_{color}_ward.json"
    for color in ("red", "orange", "yellow", "green", "blue", "indigo", "violet")
]
SUCCESS_PATTERN = re.compile(
    r"^WROTE skill tres-to-json: entries=7 output_dir=(.+) "
    r"mode=(temporary|output-dir|in-place)$"
)


class SkillTresToJsonConverterCliTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        godot = os.environ.get("MAGIC_GODOT_BIN") or shutil.which("godot")
        if not godot:
            raise AssertionError(
                "Godot was not found; set MAGIC_GODOT_BIN to the Godot 4.6 Mono editor."
            )
        cls.godot_command = [godot]
        if os.name == "nt" and Path(godot).suffix.lower() in {".cmd", ".bat"}:
            cls.godot_command = ["cmd", "/d", "/c", godot]

        completed = subprocess.run(
            ["dotnet", "build", "magic.csproj", "--nologo", "--verbosity", "quiet"],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
            timeout=180,
            check=False,
        )
        if completed.returncode != 0:
            raise AssertionError(
                "skill tres-to-JSON converter build failed\n"
                + completed.stdout
                + "\n"
                + completed.stderr
            )

    def run_converter(self, *arguments):
        return subprocess.run(
            self.godot_command
            + [
                "--headless",
                "--path",
                str(ROOT),
                "--script",
                TOOL_SCRIPT,
                "--",
                *arguments,
            ],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
            timeout=180,
            check=False,
        )

    @staticmethod
    def success_line(completed):
        matches = [
            line
            for line in completed.stdout.splitlines()
            if line.startswith("WROTE skill tres-to-json:")
        ]
        if len(matches) != 1:
            raise AssertionError(
                f"expected one converter success line, got {matches!r}\n"
                + completed.stdout
                + "\n"
                + completed.stderr
            )
        return matches[0]

    def assert_output_directory(self, output_directory):
        self.assertTrue(output_directory.is_dir())
        self.assertEqual(
            sorted(path.name for path in output_directory.iterdir()),
            sorted(EXPECTED_FILES),
        )
        for file_name in EXPECTED_FILES:
            data = (output_directory / file_name).read_bytes()
            self.assertTrue(data.endswith(b"\n"), file_name)
            self.assertFalse(data.startswith(b"\xef\xbb\xbf"), file_name)
            self.assertNotIn(b"\r", data, file_name)

    def test_default_mode_uses_persistent_system_temp_outside_project(self):
        outputs = []
        try:
            for _ in range(2):
                completed = self.run_converter()

                self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
                self.assertEqual(completed.stderr, "")
                match = SUCCESS_PATTERN.fullmatch(self.success_line(completed))
                self.assertIsNotNone(match)
                self.assertEqual(match.group(2), "temporary")
                output_directory = Path(match.group(1)).resolve()
                self.assertFalse(output_directory.is_relative_to(ROOT.resolve()))
                self.assert_output_directory(output_directory)
                outputs.append(output_directory)
            self.assertNotEqual(outputs[0], outputs[1])
        finally:
            for output_directory in outputs:
                shutil.rmtree(output_directory, ignore_errors=True)

    def test_explicit_host_output_directory_is_reported_and_published(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            output_directory = Path(temporary_directory) / "review-output"
            completed = self.run_converter(f"--output-dir={output_directory}")

            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
            self.assertEqual(completed.stderr, "")
            expected_line = (
                "WROTE skill tres-to-json: entries=7 "
                f"output_dir={output_directory.resolve()} mode=output-dir"
            )
            self.assertEqual(self.success_line(completed), expected_line)
            self.assert_output_directory(output_directory)

    def test_explicit_output_rejects_real_symlink_ancestor(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            real_directory = root / "real"
            link_directory = root / "link"
            real_directory.mkdir()
            try:
                link_directory.symlink_to(real_directory, target_is_directory=True)
            except (NotImplementedError, OSError) as exception:
                self.skipTest(f"directory symlink is unavailable: {exception}")

            output_directory = link_directory / "review-output"
            completed = self.run_converter(f"--output-dir={output_directory}")

            self.assertEqual(completed.returncode, 2, completed.stdout + completed.stderr)
            self.assertNotIn("WROTE skill tres-to-json:", completed.stdout)
            self.assertIn("ERROR:", completed.stderr)
            self.assertIn("reparse-point ancestor", completed.stderr)
            self.assertFalse((real_directory / "review-output").exists())

    def test_invalid_arguments_exit_two_without_publishing(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            output = Path(temporary_directory) / "unused"
            cases = {
                "unknown": ["--domain=skills"],
                "empty": ["--output-dir="],
                "duplicate output": [
                    f"--output-dir={output}",
                    f"--output-dir={output}",
                ],
                "duplicate in-place": ["--in-place", "--in-place"],
                "mutually exclusive": [f"--output-dir={output}", "--in-place"],
                "Godot URI": ["--output-dir=res://outside"],
                "project child": [f"--output-dir={ROOT / '.tmp' / 'converter'}"],
                "project root": [f"--output-dir={ROOT}"],
                "project ancestor": [f"--output-dir={ROOT.parent}"],
            }
            for label, arguments in cases.items():
                with self.subTest(label=label):
                    completed = self.run_converter(*arguments)
                    self.assertEqual(
                        completed.returncode,
                        2,
                        completed.stdout + completed.stderr,
                    )
                    self.assertNotIn("WROTE skill tres-to-json:", completed.stdout)
                    self.assertIn("ERROR:", completed.stderr)
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
