import json
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "tools" / "content_json_validation" / "Magic.ContentJsonValidation.Cli.csproj"
CLI_DLL = (
    ROOT
    / ".tmp"
    / "content_json_validation"
    / "bin"
    / "Debug"
    / "net8.0"
    / "Magic.ContentJsonValidation.Cli.dll"
)


def document(entries, templates=None):
    return {
        "schema": 1,
        "domain": "schema_fixture",
        "family": "fixture",
        "templates": templates or {},
        "entries": entries,
    }


def valid_entry(entry_id="valid", **overrides):
    entry = {
        "fixture_id": entry_id,
        "display_name": "Visible fixture",
        "mode": "Manual",
        "quality": "common",
        "tags": ["fixture"],
        "action": {"kind": "counter", "payload": {"amount": 2}},
    }
    entry.update(overrides)
    return entry


class ContentJsonValidationCliTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        completed = subprocess.run(
            ["dotnet", "build", str(PROJECT), "--nologo", "--verbosity", "quiet"],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
            timeout=120,
            check=False,
        )
        if completed.returncode != 0:
            raise AssertionError(
                "content JSON CLI build failed\n"
                + completed.stdout
                + "\n"
                + completed.stderr
            )

    def run_cli(self, input_path, output_format="json", domain="schema_fixture"):
        completed = subprocess.run(
            [
                "dotnet",
                str(CLI_DLL),
                "--domain",
                domain,
                "--input",
                str(input_path),
                "--format",
                output_format,
            ],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            errors="strict",
            capture_output=True,
            timeout=30,
            check=False,
        )
        self.assertNotIn("Godot Engine", completed.stdout + completed.stderr)
        return completed

    @staticmethod
    def write_json(path, payload):
        path.write_text(
            json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )

    def test_single_file_validates_without_starting_game_and_locks_json_success(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            input_path = Path(temporary_directory) / "single.json"
            self.write_json(input_path, document([valid_entry()]))

            completed = self.run_cli(input_path)

        self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)
        self.assertEqual(completed.stderr, "")
        self.assertEqual(
            completed.stdout,
            "{\n"
            '  "protocol": "magic.content_json.validation/v1",\n'
            '  "domain": "schema_fixture",\n'
            '  "success": true,\n'
            '  "validated_entry_count": 1,\n'
            '  "diagnostic_count": 0,\n'
            '  "diagnostics": []\n'
            "}\n",
        )

    def test_domain_directory_aggregates_entry_errors_and_exits_one_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            input_directory = Path(temporary_directory)
            strict_bad = valid_entry("strict_bad", unexpected=True)
            domain_bad = valid_entry("domain_bad", display_name="")
            self.write_json(input_directory / "a_strict.json", document([strict_bad]))
            self.write_json(input_directory / "b_domain.json", document([domain_bad]))
            self.write_json(input_directory / "c_good.json", document([valid_entry("good")]))
            (input_directory / "ignored.txt").write_text("not json", encoding="utf-8")

            completed = self.run_cli(input_directory)

        self.assertEqual(completed.returncode, 1, completed.stdout + completed.stderr)
        payload = json.loads(completed.stdout)
        self.assertFalse(payload["success"])
        self.assertEqual(payload["validated_entry_count"], 0)
        self.assertEqual(payload["diagnostic_count"], 2)
        actual = [
            (item["rule_id"], item["source_label"], item["json_pointer"])
            for item in payload["diagnostics"]
        ]
        self.assertEqual(
            actual,
            [
                (
                    "schema_fixture.dto.invalid_entry",
                    "a_strict.json#strict_bad",
                    "/entries/0/unexpected",
                ),
                (
                    "schema_fixture.display_name.required",
                    "b_domain.json#domain_bad",
                    "/entries/0/display_name",
                ),
            ],
        )

    def test_ndjson_is_deterministic_and_ends_with_machine_summary(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            input_path = Path(temporary_directory) / "bad.json"
            self.write_json(input_path, document([valid_entry("bad", display_name="")]))

            first = self.run_cli(input_path, "ndjson")
            second = self.run_cli(input_path, "ndjson")

        self.assertEqual(first.returncode, 1)
        self.assertEqual(second.returncode, 1)
        self.assertEqual(first.stdout, second.stdout)
        records = [json.loads(line) for line in first.stdout.splitlines()]
        self.assertEqual([record["type"] for record in records], ["diagnostic", "summary"])
        self.assertEqual(records[0]["source_label"], "bad.json#bad")
        self.assertEqual(records[0]["json_pointer"], "/entries/0/display_name")
        self.assertEqual(records[0]["rule_id"], "schema_fixture.display_name.required")
        self.assertFalse(records[-1]["success"])
        self.assertEqual(records[-1]["diagnostic_count"], 1)

    def test_unknown_domain_uses_v1_envelope_and_exit_two(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            input_path = Path(temporary_directory) / "valid.json"
            self.write_json(input_path, document([valid_entry()]))

            completed = self.run_cli(input_path, domain="missing_domain")

        self.assertEqual(completed.returncode, 2)
        payload = json.loads(completed.stdout)
        self.assertEqual(payload["protocol"], "magic.content_json.validation/v1")
        self.assertEqual(payload["domain"], "missing_domain")
        self.assertEqual(payload["diagnostics"][0]["rule_id"], "content.json.cli.unknown_domain")
        self.assertNotIn("stack", completed.stdout.lower())

    def test_godot_virtual_path_is_rejected_before_host_file_io(self):
        completed = self.run_cli("res://data/configs/json/schema_fixture")

        self.assertEqual(completed.returncode, 2)
        payload = json.loads(completed.stdout)
        self.assertEqual(
            payload["diagnostics"][0]["rule_id"],
            "content.json.cli.unsupported_virtual_path",
        )
        self.assertEqual(
            payload["diagnostics"][0]["source_label"],
            "content_json_validation_cli",
        )


if __name__ == "__main__":
    unittest.main()
