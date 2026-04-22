#!/usr/bin/env python3

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


CHECK_COMMANDS: tuple[tuple[str, list[str]], ...] = (
    (
        "battle_runtime_smoke",
        ["godot", "--headless", "--script", "tests/battle_runtime/run_battle_runtime_smoke.gd"],
    ),
    (
        "progression_tests",
        ["godot", "--headless", "--script", "tests/progression/run_progression_tests.gd"],
    ),
    (
        "party_warehouse_regression",
        ["godot", "--headless", "--script", "tests/warehouse/run_party_warehouse_regression.gd"],
    ),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("-RepoRoot", dest="repo_root", default=".")
    parser.add_argument("-StoryId", dest="story_id", default="")
    return parser.parse_args()


def resolve_command_path(name: str) -> str:
    resolved = shutil.which(name)
    if resolved is None:
        raise RuntimeError(f"Required command is not available: {name}")
    return resolved


def run_checked_command(name: str, command_path: str, arguments: list[str], repo_root: Path) -> None:
    full_command = [command_path, *arguments[1:]]
    print(f"[checks] {name}: {' '.join(full_command)}", flush=True)
    result = subprocess.run(full_command, cwd=repo_root)
    if result.returncode != 0:
        raise RuntimeError(f"Command failed with exit code {result.returncode}: {' '.join(arguments)}")


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    godot_command = resolve_command_path("godot")

    print(f"Running Ralph checks for story: {args.story_id}", flush=True)
    for check_name, command in CHECK_COMMANDS:
        run_checked_command(check_name, godot_command, command, repo_root)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exc:  # pragma: no cover - command-line failure path
        print(str(exc), file=sys.stderr)
        sys.exit(1)
