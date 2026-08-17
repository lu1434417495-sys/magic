from __future__ import annotations

from contextlib import contextmanager
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
from typing import Iterator


PRESET_NAME = "Windows Desktop Smoke"
SMOKE_SCENE_PATH = "res://tests/export/windows_export_smoke.tscn"
NOOP_AUTOLOAD_PATH = (
    "res://tests/export/windows_export_smoke_noop_autoload.cs"
)
CASES: tuple[tuple[str, int, str], ...] = (
    ("success", 0, "WINDOWS_EXPORT_SMOKE_PASS case=success"),
    ("missing_file", 1, "WINDOWS_EXPORT_SMOKE_FAIL case=missing_file"),
    ("missing_entry", 1, "WINDOWS_EXPORT_SMOKE_FAIL case=missing_entry"),
    ("type_mismatch", 1, "WINDOWS_EXPORT_SMOKE_FAIL case=type_mismatch"),
)
EXPORT_TIMEOUT_SECONDS = 600
CASE_TIMEOUT_SECONDS = 120
MAX_STREAM_OUTPUT_CHARS = 8_000
TEMP_PREFIX = "magic-windows-export-smoke-"
CLEANUP_ATTEMPTS = 10
CLEANUP_RETRY_SECONDS = 0.5


def main() -> int:
    if os.name != "nt":
        print("Windows export smoke requires a Windows host.", file=sys.stderr)
        return 2

    repo_root = Path(__file__).resolve().parents[2]
    godot_bin = os.environ.get("MAGIC_GODOT_BIN") or shutil.which("godot")
    if not godot_bin:
        print(
            "Godot was not found; set MAGIC_GODOT_BIN to the Godot 4.6 Mono editor.",
            file=sys.stderr,
        )
        return 2

    with _temporary_directory() as temp_root:
        artifact_dir = temp_root / "artifacts"
        run_dir = temp_root / "isolated-run"
        appdata_dir = temp_root / "appdata"
        local_appdata_dir = temp_root / "local-appdata"
        for directory in (
            artifact_dir,
            run_dir,
            appdata_dir,
            local_appdata_dir,
        ):
            directory.mkdir()

        exported_exe = artifact_dir / "magic_export_smoke.exe"
        try:
            export = _run(
                [
                    godot_bin,
                    "--headless",
                    "--path",
                    str(repo_root),
                    "--export-release",
                    PRESET_NAME,
                    str(exported_exe),
                ],
                cwd=repo_root,
                timeout_seconds=EXPORT_TIMEOUT_SECONDS,
            )
        except subprocess.TimeoutExpired as error:
            _show_timeout("export", error, EXPORT_TIMEOUT_SECONDS)
            return 1
        _show("export", export)
        if export.returncode != 0:
            print("Windows export failed.", file=sys.stderr)
            return 1
        if _find_scanned_architecture_build_output(export):
            print(
                "Windows export scanned excluded tools/architecture build output.",
                file=sys.stderr,
            )
            return 1
        print("[export] scanned_architecture_build_outputs=False")

        console_wrapper = exported_exe.with_name(
            f"{exported_exe.stem}.console{exported_exe.suffix}"
        )
        exported_pck = exported_exe.with_suffix(".pck")
        launch_exe = console_wrapper if console_wrapper.is_file() else exported_exe
        override_path = artifact_dir / "override.cfg"
        _write_startup_override(override_path)
        print(
            f"[artifact] executable={exported_exe} exists={exported_exe.is_file()}"
        )
        print(
            f"[artifact] console_wrapper={console_wrapper} "
            f"exists={console_wrapper.is_file()}"
        )
        print(f"[artifact] pck={exported_pck} exists={exported_pck.is_file()}")
        print(f"[artifact] launch={launch_exe}")
        print(f"[artifact] project_settings_override={override_path}")
        print(f"[run] isolated_cwd={run_dir}")
        if not exported_exe.is_file() or not launch_exe.is_file():
            print("Windows export did not produce a runnable executable.", file=sys.stderr)
            return 1

        run_env = os.environ.copy()
        run_env["APPDATA"] = str(appdata_dir)
        run_env["LOCALAPPDATA"] = str(local_appdata_dir)
        for case_name, expected_code, expected_marker in CASES:
            try:
                result = _run(
                    [
                        str(launch_exe),
                        "--headless",
                        "--log-file",
                        str(temp_root / f"{case_name}.log"),
                        "--",
                        f"--case={case_name}",
                    ],
                    cwd=run_dir,
                    env=run_env,
                    timeout_seconds=CASE_TIMEOUT_SECONDS,
                )
            except subprocess.TimeoutExpired as error:
                _show_timeout(case_name, error, CASE_TIMEOUT_SECONDS)
                return 1
            _show(case_name, result)
            combined = result.stdout + result.stderr
            if result.returncode != expected_code or expected_marker not in combined:
                print(
                    f"Smoke case {case_name} returned {result.returncode}; "
                    f"expected {expected_code} with marker {expected_marker!r}.",
                    file=sys.stderr,
                )
                return 1

        print("Windows export asset catalog smoke cases: PASS; cleaning temporary export")

    print("Windows export asset catalog smoke: PASS")
    return 0


@contextmanager
def _temporary_directory() -> Iterator[Path]:
    system_temp = Path(tempfile.gettempdir()).resolve()
    temp_root = Path(
        tempfile.mkdtemp(prefix=TEMP_PREFIX, dir=system_temp)
    ).resolve()
    if temp_root.parent != system_temp or not temp_root.name.startswith(TEMP_PREFIX):
        raise RuntimeError(f"Refusing unsafe smoke temp directory: {temp_root}")

    try:
        yield temp_root
    finally:
        for attempt in range(1, CLEANUP_ATTEMPTS + 1):
            try:
                shutil.rmtree(temp_root)
                break
            except FileNotFoundError:
                break
            except PermissionError:
                if attempt == CLEANUP_ATTEMPTS:
                    raise
                print(
                    f"[cleanup] retry={attempt} locked_temp={temp_root}",
                    file=sys.stderr,
                )
                time.sleep(CLEANUP_RETRY_SECONDS)


def _run(
    command: list[str],
    *,
    cwd: Path,
    env: dict[str, str] | None = None,
    timeout_seconds: int,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        cwd=cwd,
        env=env,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=timeout_seconds,
    )


def _write_startup_override(path: Path) -> None:
    path.write_text(
        "[application]\n"
        f'run/main_scene="{SMOKE_SCENE_PATH}"\n'
        "\n"
        "[autoload]\n"
        f'ApplicationLifetimeCoordinator="*{NOOP_AUTOLOAD_PATH}"\n'
        f'GameSession="*{NOOP_AUTOLOAD_PATH}"\n',
        encoding="utf-8",
        newline="\n",
    )


def _find_scanned_architecture_build_output(
    result: subprocess.CompletedProcess[str],
) -> bool:
    normalized = (result.stdout + result.stderr).replace("\\", "/").lower()
    return any(
        "res://tools/architecture/" in line
        and ("/bin/" in line or "/obj/" in line)
        for line in normalized.splitlines()
    )


def _show(label: str, result: subprocess.CompletedProcess[str]) -> None:
    print(f"[{label}] exit={result.returncode}")
    if result.stdout:
        _show_stream(result.stdout, file=sys.stdout)
    if result.stderr:
        _show_stream(result.stderr, file=sys.stderr)


def _show_timeout(
    label: str,
    error: subprocess.TimeoutExpired,
    timeout_seconds: int,
) -> None:
    print(f"[{label}] timed out after {timeout_seconds} seconds", file=sys.stderr)
    if error.stdout:
        _show_stream(_as_text(error.stdout), file=sys.stdout)
    if error.stderr:
        _show_stream(_as_text(error.stderr), file=sys.stderr)


def _as_text(output: str | bytes) -> str:
    if isinstance(output, bytes):
        return output.decode("utf-8", errors="replace")
    return output


def _show_stream(output: str, *, file: object) -> None:
    if len(output) > MAX_STREAM_OUTPUT_CHARS:
        omitted = len(output) - MAX_STREAM_OUTPUT_CHARS
        print(f"... omitted {omitted} leading characters ...", file=file)
        output = output[-MAX_STREAM_OUTPUT_CHARS:]
    print(output, end="" if output.endswith("\n") else "\n", file=file)


if __name__ == "__main__":
    raise SystemExit(main())
