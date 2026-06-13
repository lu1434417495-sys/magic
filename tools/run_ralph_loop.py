#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import threading
from datetime import datetime
from pathlib import Path
from typing import Any


DEFAULT_IMPLEMENTATION_SKILLS = [
    "godot-master",
    "algorithm-design",
]

DEFAULT_REVIEW_SKILLS = [
    "code-review",
]

DEFAULT_IMPLEMENTATION_SANDBOX_MODE = "workspace-write"
DEFAULT_REVIEW_SANDBOX_MODE = "danger-full-access"


def resolve_absolute_path(path: str, allow_missing: bool = False, base_dir: Path | None = None) -> Path:
    candidate = Path(path)
    if not candidate.is_absolute():
        candidate = (base_dir or Path.cwd()) / candidate

    resolved = candidate.resolve(strict=False)
    if not allow_missing and not resolved.exists():
        raise RuntimeError(f"Path does not exist: {path}")

    return resolved


def ensure_command_exists(name: str) -> None:
    if shutil.which(name) is None:
        raise RuntimeError(f"Required command is not available: {name}")


def get_preferred_powershell_command() -> str:
    for candidate in ("pwsh", "powershell"):
        if shutil.which(candidate) is not None:
            return candidate
    raise RuntimeError("Neither pwsh nor powershell is available.")


def get_resolved_command_path(name: str) -> str:
    resolved = shutil.which(name)
    if not resolved:
        raise RuntimeError(f"Unable to resolve command path: {name}")
    return resolved


def build_command_prefix(resolved_command_path: str) -> list[str]:
    if resolved_command_path.lower().endswith(".ps1"):
        return [
            get_preferred_powershell_command(),
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            resolved_command_path,
        ]
    return [resolved_command_path]


def build_script_command(script_path: Path) -> list[str]:
    suffix = script_path.suffix.lower()
    if suffix == ".ps1":
        return [
            get_preferred_powershell_command(),
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            str(script_path),
        ]
    if suffix == ".py":
        return [sys.executable, str(script_path)]
    return [str(script_path)]


def resolve_checks_path(state_root: Path) -> Path:
    for candidate_name in ("checks.py", "checks.ps1"):
        candidate = state_root / candidate_name
        if candidate.exists():
            return candidate
    return state_root / "checks.py"


def read_json_file(path: Path) -> dict[str, Any]:
    raw = path.read_text(encoding="utf-8")
    if not raw.strip():
        raise RuntimeError(f"JSON file is empty: {path}")
    return json.loads(raw)


def write_json_file(data: Any, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def ensure_text_file(path: Path, default_content: str = "") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        path.write_text(default_content, encoding="utf-8")


def run_git(args: list[str], repo_root: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", "-C", str(repo_root), *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
    )


def get_repo_root(directory: str) -> Path:
    resolved_directory = resolve_absolute_path(directory)
    result = subprocess.run(
        ["git", "-C", str(resolved_directory), "rev-parse", "--show-toplevel"],
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    repo_root = result.stdout.strip()
    if result.returncode != 0 or not repo_root:
        raise RuntimeError(f"Failed to resolve git repository root from: {directory}")
    return Path(repo_root)


def get_run_timestamp() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def validate_state_directory_argument(state_directory_name: str, repo_root: Path) -> None:
    state_candidate = Path(state_directory_name)
    if state_candidate.suffix.lower() == ".json":
        raise RuntimeError(
            f"StateDirectory expects a directory, not a JSON file path: {state_directory_name}. "
            "Use -PrdPath for PRD files, for example `-PrdPath .ralph/prd.json`."
        )

    resolved_state_candidate = resolve_absolute_path(state_directory_name, allow_missing=True, base_dir=repo_root)
    if resolved_state_candidate.exists() and resolved_state_candidate.is_file():
        raise RuntimeError(
            f"StateDirectory expects a directory, but resolved to a file: {resolved_state_candidate}. "
            "Use -PrdPath for PRD files."
        )


def get_state_paths(repo_root: Path, state_directory_name: str, prd_path: str = "") -> dict[str, Path]:
    validate_state_directory_argument(state_directory_name, repo_root)
    state_root = resolve_absolute_path(state_directory_name, allow_missing=True, base_dir=repo_root)
    prd_file_path = state_root / "prd.json"
    if prd_path.strip():
        prd_candidate = Path(prd_path)
        if not prd_candidate.is_absolute():
            prd_candidate = repo_root / prd_candidate
        prd_file_path = resolve_absolute_path(str(prd_candidate), allow_missing=True)
        state_root = prd_file_path.parent

    runs_root = state_root / "runs"
    runs_root.mkdir(parents=True, exist_ok=True)
    reviews_root = state_root / "reviews"
    reviews_root.mkdir(parents=True, exist_ok=True)

    return {
        "StateRoot": state_root,
        "RunsRoot": runs_root,
        "ReviewsRoot": reviews_root,
        "Prd": prd_file_path,
        "Checks": resolve_checks_path(state_root),
        "OutputSchema": state_root / "output_schema.json",
        "ReviewOutputSchema": state_root / "review_output.schema.json",
    }


def assert_loop_files_exist(paths: dict[str, Path]) -> None:
    for required_path in (
        paths["Prd"],
        paths["Checks"],
        paths["OutputSchema"],
    ):
        if not required_path.exists():
            raise RuntimeError(f"Missing Ralph file: {required_path}")


def get_dirty_worktree_entries(repo_root: Path) -> list[str]:
    result = run_git(["status", "--porcelain"], repo_root)
    if result.returncode != 0:
        raise RuntimeError("Failed to read git worktree status.")
    return [line for line in result.stdout.splitlines() if line.strip()]


def get_current_branch_name(repo_root: Path) -> str:
    result = run_git(["branch", "--show-current"], repo_root)
    if result.returncode != 0:
        raise RuntimeError("Failed to read current branch name.")
    return result.stdout.strip()


def ensure_loop_branch(repo_root: Path, state: dict[str, Any]) -> None:
    branch_name = str(state.get("branchName", "") or "").strip()
    if not branch_name:
        return

    current_branch = get_current_branch_name(repo_root)
    if current_branch == branch_name:
        return

    branch_exists = run_git(["rev-parse", "--verify", f"refs/heads/{branch_name}"], repo_root).returncode == 0
    if branch_exists:
        result = run_git(["switch", branch_name], repo_root)
        if result.returncode != 0:
            raise RuntimeError(f"Failed to switch to existing branch: {branch_name}")
        return

    result = run_git(["switch", "-c", branch_name], repo_root)
    if result.returncode != 0:
        raise RuntimeError(f"Failed to create branch: {branch_name}")


def get_max_attempts_per_story(state: dict[str, Any]) -> int:
    return int(state.get("maxAttemptsPerStory", 3))


def get_commit_prefix(state: dict[str, Any]) -> str:
    value = str(state.get("commitPrefix", "") or "").strip()
    return value or "chore(ralph):"


def select_story(state: dict[str, Any], requested_story_id: str) -> dict[str, Any] | None:
    stories = list(state.get("userStories", []))
    if not requested_story_id.strip():
        for story in stories:
            if str(story.get("status", "")) == "open":
                return story
        return None

    for story in stories:
        if str(story.get("id", "")) != requested_story_id:
            continue
        if str(story.get("status", "")) in {"open", "in_progress"}:
            return story
    return None


def normalize_compact_text(text: str) -> str:
    if not text or not text.strip():
        return ""
    return re.sub(r"\s+", " ", text).strip()


def limit_text_length(text: str, max_length: int = 200) -> str:
    normalized = normalize_compact_text(text)
    if not normalized:
        return ""
    if len(normalized) <= max_length:
        return normalized
    return normalized[: max_length - 3].rstrip() + "..."


def format_compact_list(items: list[Any] | None = None, max_items: int = 5, max_item_length: int = 160) -> str:
    entries: list[str] = []
    for item in items or []:
        text = limit_text_length(str(item), max_item_length)
        if text:
            entries.append(text)

    if not entries:
        return ""

    lines = [f"- {entry}" for entry in entries[:max_items]]
    if len(entries) > max_items:
        lines.append(f"- ... and {len(entries) - max_items} more")
    return "\n".join(lines)


def format_story_context(story: dict[str, Any]) -> str:
    lines = [f"id: {story.get('id', '')}"]

    title = limit_text_length(str(story.get("title", "")), 180)
    if title:
        lines.append(f"title: {title}")

    acceptance = format_compact_list(list(story.get("acceptanceCriteria", [])), max_items=6, max_item_length=180)
    if acceptance:
        lines.append("acceptance:")
        lines.extend(acceptance.splitlines())

    notes = format_compact_list(list(story.get("notes", [])), max_items=4, max_item_length=180)
    if notes:
        lines.append("notes:")
        lines.extend(notes.splitlines())

    if "attemptCount" in story:
        lines.append(f"attempts: {int(story.get('attemptCount', 0))}")

    last_failure = limit_text_length(str(story.get("lastFailure", "")), 220)
    if last_failure:
        lines.append(f"previous failure: {last_failure}")

    last_learnings = limit_text_length(str(story.get("lastLearnings", "")), 220)
    if last_learnings:
        lines.append(f"previous learning: {last_learnings}")

    return "\n".join(lines)


def update_story_for_attempt(story: dict[str, Any], run_id: str) -> None:
    timestamp = get_run_timestamp()
    attempt_count = int(story.get("attemptCount", 0)) + 1
    story["status"] = "in_progress"
    story["attemptCount"] = attempt_count
    story["lastAttemptAt"] = timestamp
    story["lastRunId"] = run_id
    if not str(story.get("startedAt", "") or "").strip():
        story["startedAt"] = timestamp


def mark_story_done(story: dict[str, Any], checks_run: str, learnings: str) -> None:
    story["status"] = "done"
    story["completedAt"] = get_run_timestamp()
    story["lastFailure"] = ""
    story["lastChecksRun"] = checks_run
    story["lastLearnings"] = learnings


def mark_story_failed(story: dict[str, Any], max_attempts: int, failure_text: str, checks_run: str, learnings: str) -> None:
    attempt_count = int(story.get("attemptCount", 0))
    story["status"] = "blocked" if attempt_count >= max_attempts else "open"
    story["lastFailure"] = failure_text
    story["lastChecksRun"] = checks_run
    story["lastLearnings"] = learnings


def remove_done_stories_from_state(state: dict[str, Any]) -> dict[str, Any]:
    stories = state.get("userStories")
    if stories is None:
        raise RuntimeError("PRD state is missing userStories.")

    remaining_stories: list[dict[str, Any]] = []
    removed_story_ids: list[str] = []

    for story in list(stories):
        if str(story.get("status", "")) == "done":
            story_id = str(story.get("id", "") or "").strip()
            if story_id:
                removed_story_ids.append(story_id)
            continue
        remaining_stories.append(story)

    state["userStories"] = remaining_stories
    return {
        "State": state,
        "RemovedCount": len(removed_story_ids),
        "RemainingCount": len(remaining_stories),
        "RemovedStoryIds": removed_story_ids,
    }


def render_implementation_prompt(story: dict[str, Any], paths: dict[str, Path]) -> str:
    skill_list = ", ".join(f"${skill}" for skill in DEFAULT_IMPLEMENTATION_SKILLS)
    story_context = format_story_context(story)
    return f"""Ralph loop run. Work on exactly one story.

Do not edit {paths["Prd"]}; the outer loop owns story state.
Keep scope to the smallest complete slice. Prefer existing tests and project commands.
Use default skills: {skill_list}.

Story context:
{story_context}

Return JSON matching the schema:
- result: done | blocked
- changed: short implementation summary
- learnings: short takeaway summary
"""


def render_review_prompt(story: dict[str, Any], paths: dict[str, Path], run_id: str) -> str:
    skill_list = ", ".join(f"${skill}" for skill in DEFAULT_REVIEW_SKILLS)
    story_context = format_story_context(story)
    review_schema_path = paths["ReviewOutputSchema"]
    review_report_base = paths["ReviewsRoot"] / f"{run_id}.review"

    if review_schema_path.exists():
        report_instructions = (
            f"Write the complete review report to {review_report_base}.json.\n"
            f"The report must be exactly one JSON object matching {review_schema_path}."
        )
    else:
        report_instructions = (
            f"Write the complete review report to {review_report_base}.md in Markdown.\n"
            "Put findings first, ordered by severity, with concrete file paths and failure modes."
        )

    return f"""Ralph loop run. Review exactly one story.

Do not edit {paths["Prd"]}; the outer loop owns story state. Default to read-only code inspection unless the story explicitly asks for a fix.
Focus on findings first: bugs, regressions, missing tests, unsafe assumptions, schema drift, and state inconsistencies.
Use default skills: {skill_list}.
read docs/design/project_context_units.md before deep review.

Story context:
{story_context}

{report_instructions}

Return JSON matching the schema:
- result: done | blocked
- changed: report path plus a short findings summary
- learnings: short residual-risk or follow-up summary
"""


def render_prompt(story: dict[str, Any], paths: dict[str, Path], task_type: str, run_id: str) -> str:
    if task_type == "review":
        return render_review_prompt(story, paths, run_id)
    return render_implementation_prompt(story, paths)


def _extract_string(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    return str(value)


def _extract_text_from_value(value: Any) -> str:
    if value is None:
        return ""
    if isinstance(value, str):
        return value
    if isinstance(value, list):
        chunks = [_extract_text_from_value(item) for item in value]
        return "".join(chunk for chunk in chunks if chunk)
    if isinstance(value, dict):
        preferred_keys = ("text", "delta", "message", "content", "value", "output_text")
        chunks = [_extract_text_from_value(value.get(key)) for key in preferred_keys if key in value]
        return "".join(chunk for chunk in chunks if chunk)
    return ""


def _extract_event_message(event: dict[str, Any]) -> str:
    error = event.get("error")
    if isinstance(error, dict):
        message = _extract_string(error.get("message", "")).strip()
        if message:
            return message

    for key in ("message", "text", "delta", "content", "output_text"):
        message = _extract_text_from_value(event.get(key)).strip()
        if message:
            return message

    item = event.get("item")
    if isinstance(item, dict):
        for key in ("message", "text", "delta", "content", "output_text"):
            message = _extract_text_from_value(item.get(key)).strip()
            if message:
                return message

    return ""


def _extract_tool_name(event: dict[str, Any]) -> str:
    for candidate_key in ("tool_name", "name", "recipient_name"):
        candidate = _extract_string(event.get(candidate_key, "")).strip()
        if candidate:
            return candidate

    item = event.get("item")
    if isinstance(item, dict):
        for candidate_key in ("tool_name", "name", "recipient_name"):
            candidate = _extract_string(item.get(candidate_key, "")).strip()
            if candidate:
                return candidate

    call = event.get("call")
    if isinstance(call, dict):
        for candidate_key in ("tool_name", "name", "recipient_name"):
            candidate = _extract_string(call.get(candidate_key, "")).strip()
            if candidate:
                return candidate

    return ""


def _strip_matching_quotes(text: str) -> str:
    if len(text) >= 2 and text[0] == text[-1] and text[0] in {'"', "'"}:
        return text[1:-1]
    return text


def _extract_first_regex_group(pattern: str, text: str) -> str:
    match = re.search(pattern, text, flags=re.IGNORECASE)
    if match is None:
        return ""
    return _strip_matching_quotes(match.group(1).strip())


def _summarize_path_text(path_text: str) -> str:
    normalized = _strip_matching_quotes(path_text.strip())
    if not normalized:
        return ""

    normalized = normalized.replace("\\", "/")
    drive_match = re.match(r"^[A-Za-z]:/(.+)$", normalized)
    if drive_match:
        normalized = drive_match.group(1)

    home_match = re.match(r"^[A-Za-z]:/Users/[^/]+/(.+)$", normalized, flags=re.IGNORECASE)
    if home_match:
        normalized = home_match.group(1)

    repo_match = re.match(r"^.+?/magic/(.+)$", normalized, flags=re.IGNORECASE)
    if repo_match:
        normalized = repo_match.group(1)

    normalized = re.sub(r"/+", "/", normalized).strip("/")
    return normalized


def _summarize_shell_command(command: str) -> str:
    normalized = normalize_compact_text(command)
    if not normalized:
        return ""

    inner_command = _extract_first_regex_group(r"-Command\s+(.+)$", normalized)
    if inner_command:
        normalized = inner_command

    normalized = _strip_matching_quotes(normalized.strip())

    get_content_path = _extract_first_regex_group(r"Get-Content\s+-Path\s+(.+?)(?:\s*\|\s*Select-Object.*)?$", normalized)
    if get_content_path:
        return f"read {_summarize_path_text(get_content_path)}"

    rg_files_match = re.search(r'rg\s+--files\s+(.+?)\s*\|\s*rg\s+["' + "'" + r'](.+?)["' + "'" + r']', normalized, flags=re.IGNORECASE)
    if rg_files_match:
        scope = normalize_compact_text(rg_files_match.group(1))
        pattern = normalize_compact_text(rg_files_match.group(2))
        return f"list files matching {pattern} in {scope}"

    rg_search_match = re.search(r'rg\s+-n\s+["' + "'" + r'](.+?)["' + "'" + r']\s+(.+)$', normalized, flags=re.IGNORECASE)
    if rg_search_match:
        pattern = normalize_compact_text(rg_search_match.group(1))
        scope = normalize_compact_text(rg_search_match.group(2))
        return f"search {pattern} in {scope}"

    git_status_match = re.search(r"git\s+status\s+--short$", normalized, flags=re.IGNORECASE)
    if git_status_match:
        return "git status --short"

    select_object_match = re.search(r"Get-Content\s+-Path\s+(.+?)\s*\|\s*Select-Object\s+-Skip\s+(\d+)\s+-First\s+(\d+)$", normalized, flags=re.IGNORECASE)
    if select_object_match:
        path_text = _summarize_path_text(select_object_match.group(1))
        skip = select_object_match.group(2)
        first = select_object_match.group(3)
        return f"read {path_text} [{skip}:{first}]"

    return limit_text_length(normalized, 120)


def _extract_command_execution_summary(item: dict[str, Any]) -> str:
    command = _extract_string(item.get("command", "")).strip()
    command_summary = _summarize_shell_command(command)
    exit_code = item.get("exit_code")
    status = _extract_string(item.get("status", "")).strip()

    if status == "in_progress":
        return f"[codex] command started: {command_summary}" if command_summary else "[codex] command started"

    if status == "completed":
        if command_summary and exit_code is not None:
            return f"[codex] command completed ({exit_code}): {command_summary}"
        if command_summary:
            return f"[codex] command completed: {command_summary}"
        return "[codex] command completed"

    if status:
        if command_summary:
            return f"[codex] command {status}: {command_summary}"
        return f"[codex] command {status}"

    if command_summary:
        return f"[codex] command: {command_summary}"
    return ""


def _extract_todo_summary(item: dict[str, Any]) -> str:
    items = item.get("items", [])
    if not isinstance(items, list) or not items:
        return ""

    total = len(items)
    completed = 0
    for entry in items:
        if isinstance(entry, dict) and bool(entry.get("completed", False)):
            completed += 1
    return f"[codex] todo progress: {completed}/{total}"


def _extract_item_summary(event: dict[str, Any]) -> str:
    item = event.get("item")
    if not isinstance(item, dict):
        return ""

    item_type = _extract_string(item.get("type", "")).strip()
    event_type = _extract_string(event.get("type", "")).strip()

    if item_type == "command_execution":
        return _extract_command_execution_summary(item)

    if item_type == "todo_list":
        todo_summary = _extract_todo_summary(item)
        if todo_summary:
            return todo_summary

    if event_type == "item.started" and item_type:
        return f"[codex] item started: {item_type}"
    if event_type == "item.completed" and item_type:
        return f"[codex] item completed: {item_type}"
    if event_type == "item.updated" and item_type:
        return f"[codex] item updated: {item_type}"

    return ""


def render_codex_event(event: dict[str, Any]) -> dict[str, str] | None:
    event_type = _extract_string(event.get("type", "")).strip()
    message = _extract_event_message(event)

    if event_type == "thread.started":
        thread_id = _extract_string(event.get("thread_id", "")).strip()
        if thread_id:
            return {"kind": "line", "text": f"[codex] thread started: {thread_id}"}
        return {"kind": "line", "text": "[codex] thread started"}

    if event_type.endswith(".delta") or event_type.endswith(".chunk"):
        if message:
            return {"kind": "text", "text": message}
        return None

    if event_type in {"error", "turn.failed"}:
        summary = message or "Codex reported an error."
        return {"kind": "line", "text": f"[codex] {event_type}: {summary}"}

    if event_type == "turn.started":
        return {"kind": "line", "text": "[codex] turn started"}

    if event_type == "turn.completed":
        return {"kind": "line", "text": "[codex] turn completed"}

    if event_type.startswith("item."):
        item_summary = _extract_item_summary(event)
        if item_summary:
            return {"kind": "line", "text": item_summary}
        return None

    if "tool" in event_type or "call" in event_type:
        tool_name = _extract_tool_name(event)
        if message and tool_name:
            return {"kind": "line", "text": f"[codex] {event_type}: {tool_name} | {message}"}
        if tool_name:
            return {"kind": "line", "text": f"[codex] {event_type}: {tool_name}"}
        if message:
            return {"kind": "line", "text": f"[codex] {event_type}: {message}"}
        return {"kind": "line", "text": f"[codex] {event_type}"}

    if message and event_type in {"message", "message.completed", "response.completed"}:
        return {"kind": "line", "text": message}

    if message and not event_type:
        return {"kind": "line", "text": message}

    return None


def _ensure_console_newline(console_handle, render_state: dict[str, Any]) -> None:
    if render_state.get("text_stream_open", False):
        console_handle.write("\n")
        console_handle.flush()
        render_state["text_stream_open"] = False


def render_codex_event_line(raw_line: str, console_handle, render_state: dict[str, Any]) -> None:
    if not raw_line:
        return

    try:
        event = json.loads(raw_line)
    except json.JSONDecodeError:
        _ensure_console_newline(console_handle, render_state)
        console_handle.write(raw_line)
        console_handle.flush()
        return

    if not isinstance(event, dict):
        return

    rendered = render_codex_event(event)
    if rendered is None:
        return

    kind = rendered.get("kind", "")
    text = rendered.get("text", "")
    if not text:
        return

    if kind == "text":
        console_handle.write(text)
        console_handle.flush()
        render_state["text_stream_open"] = not text.endswith("\n")
        return

    _ensure_console_newline(console_handle, render_state)
    console_handle.write(text.rstrip() + "\n")
    console_handle.flush()


def invoke_codex_iteration(
    prompt_text: str,
    repo_root: Path,
    paths: dict[str, Path],
    run_id: str,
    model: str,
    sandbox_mode: str,
    persist_sessions: bool,
) -> dict[str, Any]:
    run_prefix = paths["RunsRoot"] / run_id
    prompt_path = Path(f"{run_prefix}.prompt.md")
    events_path = Path(f"{run_prefix}.events.jsonl")
    stderr_path = Path(f"{run_prefix}.stderr.log")
    final_path = Path(f"{run_prefix}.final.json")

    prompt_path.write_text(prompt_text, encoding="utf-8")

    args = [
        "--cd",
        str(repo_root),
        "--sandbox",
        sandbox_mode,
        "--json",
        "--output-schema",
        str(paths["OutputSchema"]),
        "-o",
        str(final_path),
    ]

    if model.strip():
        args = ["exec", "-m", model] + args
    else:
        args = ["exec"] + args

    if not persist_sessions:
        args.append("--ephemeral")

    args.append("-")

    for path in (events_path, stderr_path):
        if path.exists():
            path.unlink()

    codex_command_path = get_resolved_command_path("codex")
    command_prefix = build_command_prefix(codex_command_path)
    with (
        prompt_path.open("r", encoding="utf-8") as prompt_file,
        events_path.open("w", encoding="utf-8") as events_file,
        stderr_path.open("w", encoding="utf-8") as stderr_file,
    ):
        process = subprocess.Popen(
            [*command_prefix, *args],
            cwd=repo_root,
            stdin=prompt_file,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
        assert process.stdout is not None
        assert process.stderr is not None

        def stream_pipe(pipe, log_handle, console_handle, render_events: bool) -> None:
            render_state = {"text_stream_open": False}
            try:
                for line in iter(pipe.readline, ""):
                    log_handle.write(line)
                    log_handle.flush()
                    if render_events:
                        render_codex_event_line(line, console_handle, render_state)
                    else:
                        console_handle.write(line)
                        console_handle.flush()
            finally:
                if render_events:
                    _ensure_console_newline(console_handle, render_state)
                pipe.close()

        stdout_thread = threading.Thread(
            target=stream_pipe,
            args=(process.stdout, events_file, sys.stdout, True),
            daemon=True,
        )
        stderr_thread = threading.Thread(
            target=stream_pipe,
            args=(process.stderr, stderr_file, sys.stderr, False),
            daemon=True,
        )
        stdout_thread.start()
        stderr_thread.start()
        process.wait()
        stdout_thread.join()
        stderr_thread.join()

    return {
        "ExitCode": process.returncode,
        "PromptPath": prompt_path,
        "EventsPath": events_path,
        "StderrPath": stderr_path,
        "FinalPath": final_path,
    }


def read_codex_result(final_path: Path) -> dict[str, Any]:
    if not final_path.exists():
        raise RuntimeError(f"Codex final message file not found: {final_path}")

    raw = final_path.read_text(encoding="utf-8")
    if not raw.strip():
        raise RuntimeError(f"Codex final message file is empty: {final_path}")

    return json.loads(raw)


def normalize_codex_error_text(text: str) -> str:
    if not text or not text.strip():
        return ""

    candidate = text.strip()
    try:
        parsed = json.loads(candidate)
    except json.JSONDecodeError:
        return candidate

    if isinstance(parsed, dict):
        error = parsed.get("error")
        if isinstance(error, dict):
            nested_message = str(error.get("message", "") or "").strip()
            if nested_message:
                return normalize_codex_error_text(nested_message)

        message = str(parsed.get("message", "") or "").strip()
        if message:
            return normalize_codex_error_text(message)

    return candidate


def get_codex_failure_summary(events_path: Path, stderr_path: Path) -> str:
    messages: list[str] = []

    if events_path.exists():
        for line in events_path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue

            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue

            raw_message = ""
            if event.get("type") == "error":
                raw_message = str(event.get("message", "") or "")
            elif event.get("type") == "turn.failed":
                event_error = event.get("error", {})
                if isinstance(event_error, dict):
                    raw_message = str(event_error.get("message", "") or "")

            normalized = normalize_codex_error_text(raw_message)
            if normalized and normalized not in messages:
                messages.append(normalized)

    if stderr_path.exists():
        stderr_text = normalize_codex_error_text(stderr_path.read_text(encoding="utf-8"))
        if stderr_text and stderr_text not in messages:
            messages.append(stderr_text)

    if messages:
        return " | ".join(messages)
    return f"No structured Codex error details found. events: {events_path}"


def validate_structured_review_report(report_path: Path) -> str:
    if not report_path.exists():
        return f"Review report file was not created: {report_path}"
    content = report_path.read_text(encoding="utf-8")
    if not content.strip():
        return f"Review report file is empty: {report_path}"
    try:
        report = json.loads(content)
    except json.JSONDecodeError as exc:
        return f"Review report is not valid JSON. {exc}"
    if not isinstance(report, dict):
        return "Review report root is not a JSON object."
    for required_key in ("passed", "findings", "summary"):
        if required_key not in report:
            return f"Review report is missing required key: {required_key}"
    findings_value = report.get("findings")
    if not isinstance(findings_value, list):
        return "Review report findings must be an array."
    computed_totals = {"critical": 0, "warning": 0, "suggestion": 0}
    for finding in findings_value:
        if not isinstance(finding, dict):
            return "Review report contains a finding that is not an object."
        for required_field in ("category", "severity", "file", "issue", "fix"):
            if required_field not in finding or not str(finding[required_field]).strip():
                return f"Review report finding missing required field: {required_field}"
        severity = str(finding["severity"])
        if severity not in {"Critical", "Warning", "Suggestion"}:
            return f"Review report contains invalid severity value: {severity}"
        computed_totals[severity.lower()] += 1
    if bool(report["passed"]) and findings_value:
        return "Review report cannot set `passed=true` while findings are present."
    summary = report.get("summary")
    if not isinstance(summary, dict):
        return "Review report summary is missing or not an object."
    totals = summary.get("totals")
    if not isinstance(totals, dict):
        return "Review report summary.totals is missing or not an object."
    for key, expected in computed_totals.items():
        if key not in totals:
            return f"Review report summary.totals is missing key: {key}"
        if int(totals[key]) != expected:
            return f"Review report summary.totals.{key} does not match findings array."
    return ""


def invoke_checks(
    paths: dict[str, Path],
    repo_root: Path,
    story: dict[str, Any],
    run_id: str,
) -> dict[str, Any]:
    check_log_path = paths["RunsRoot"] / f"{run_id}.checks.log"
    with check_log_path.open("w", encoding="utf-8") as check_log:
        process = subprocess.run(
            [
                *build_script_command(paths["Checks"]),
                "-RepoRoot",
                str(repo_root),
                "-StoryId",
                str(story.get("id", "")),
            ],
            cwd=repo_root,
            stdout=check_log,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
        )

    return {
        "ExitCode": process.returncode,
        "LogPath": check_log_path,
    }


def commit_if_needed(repo_root: Path, message: str) -> bool:
    add_result = run_git(["add", "-A"], repo_root)
    if add_result.returncode != 0:
        raise RuntimeError("git add failed.")

    diff_result = run_git(["diff", "--cached", "--quiet"], repo_root)
    if diff_result.returncode == 0:
        return False
    if diff_result.returncode > 1:
        raise RuntimeError("git diff --cached --quiet failed.")

    commit_result = run_git(["commit", "-m", message], repo_root)
    if commit_result.returncode != 0:
        raise RuntimeError("git commit failed.")
    return True


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(allow_abbrev=False)
    # MaxIterations: 最多循环多少轮 story；例如 `python tools/run_ralph_loop.py -MaxIterations 5`
    parser.add_argument("-MaxIterations", dest="max_iterations", type=int, default=10)
    # WorkingDirectory: 从哪个目录解析 git 仓库根；例如 `python tools/run_ralph_loop.py -WorkingDirectory E:\game\magic`
    parser.add_argument("-WorkingDirectory", dest="working_directory", default=".")
    # StateDirectory: Ralph 状态目录；例如 `python tools/run_ralph_loop.py -StateDirectory .ralph`
    parser.add_argument("-StateDirectory", dest="state_directory", default=".ralph")
    # PrdPath: 显式指定 PRD 文件路径；例如 `python tools/run_ralph_loop.py -PrdPath .\tmp\custom_prd.json`
    parser.add_argument("-PrdPath", dest="prd_path", default="")
    # StoryId: 只跑某一个 story；例如 `python tools/run_ralph_loop.py -StoryId PVS_01A`
    parser.add_argument("-StoryId", dest="story_id", default="")
    # TaskType: 指定是实现 story 还是代码检视 story；例如 `python tools/run_ralph_loop.py -TaskType review`
    parser.add_argument(
        "-TaskType",
        dest="task_type",
        choices=["implementation", "review"],
        default="implementation",
    )
    # CodexModel: 覆盖默认 Codex 模型；例如 `python tools/run_ralph_loop.py -CodexModel gpt-5.4`
    parser.add_argument("-CodexModel", dest="codex_model", default="")
    # CodexSandboxMode: 传给 `codex exec --sandbox` 的模式；例如 `python tools/run_ralph_loop.py -CodexSandboxMode danger-full-access`
    parser.add_argument(
        "-CodexSandboxMode",
        dest="codex_sandbox_mode",
        choices=["read-only", "workspace-write", "danger-full-access"],
        default=DEFAULT_IMPLEMENTATION_SANDBOX_MODE,
    )
    # AllowDirtyWorktree: 允许在脏工作树上启动 loop；例如 `python tools/run_ralph_loop.py -AllowDirtyWorktree`
    parser.add_argument("-AllowDirtyWorktree", dest="allow_dirty_worktree", action="store_true")
    # NoCommit: story 完成后不自动 git commit；例如 `python tools/run_ralph_loop.py -NoCommit`
    parser.add_argument("-NoCommit", dest="no_commit", action="store_true")
    # SkipChecks: 跳过外层 `.ralph/checks.py`；例如 `python tools/run_ralph_loop.py -SkipChecks`
    parser.add_argument("-SkipChecks", dest="skip_checks", action="store_true")
    # PersistCodexSessions: 不附加 `--ephemeral`，保留 Codex 会话；例如 `python tools/run_ralph_loop.py -PersistCodexSessions`
    parser.add_argument("-PersistCodexSessions", dest="persist_codex_sessions", action="store_true")
    # ContinueAfterFailure: 某轮失败后继续找下一轮/重试；例如 `python tools/run_ralph_loop.py -ContinueAfterFailure`
    parser.add_argument("-ContinueAfterFailure", dest="continue_after_failure", action="store_true")
    # PruneDoneStories: 删除 PRD 中 `status = done` 的 story 并退出；例如 `python tools/run_ralph_loop.py -PruneDoneStories`
    parser.add_argument("-PruneDoneStories", dest="prune_done_stories", action="store_true")
    return parser


def main() -> int:
    parser = build_argument_parser()
    args = parser.parse_args()
    sandbox_mode_overridden = "-CodexSandboxMode" in sys.argv[1:]

    if args.max_iterations < 1 or args.max_iterations > 1000:
        raise RuntimeError("MaxIterations must be between 1 and 1000.")

    effective_codex_sandbox_mode = args.codex_sandbox_mode
    if args.task_type == "review" and not sandbox_mode_overridden:
        effective_codex_sandbox_mode = DEFAULT_REVIEW_SANDBOX_MODE

    ensure_command_exists("git")
    repo_root = get_repo_root(args.working_directory)
    paths = get_state_paths(repo_root, args.state_directory, args.prd_path)

    if args.prune_done_stories:
        if not paths["Prd"].exists():
            raise RuntimeError(f"Missing Ralph file: {paths['Prd']}")

        state = read_json_file(paths["Prd"])
        prune_result = remove_done_stories_from_state(state)
        write_json_file(prune_result["State"], paths["Prd"])

        story_word = "story" if int(prune_result["RemovedCount"]) == 1 else "stories"
        removed_summary = ""
        if prune_result["RemovedStoryIds"]:
            removed_summary = f" Removed: {', '.join(prune_result['RemovedStoryIds'])}."

        print(
            f"Pruned {prune_result['RemovedCount']} done {story_word} from {paths['Prd']}. "
            f"Remaining stories: {prune_result['RemainingCount']}.{removed_summary}"
        )
        return 0

    ensure_command_exists("codex")
    assert_loop_files_exist(paths)

    if not args.allow_dirty_worktree:
        dirty_entries = get_dirty_worktree_entries(repo_root)
        if dirty_entries:
            raise RuntimeError("Worktree must be clean before starting Ralph loop. Use -AllowDirtyWorktree to override.")

    state = read_json_file(paths["Prd"])
    max_attempts_per_story = get_max_attempts_per_story(state)
    commit_prefix = get_commit_prefix(state)

    for iteration in range(1, args.max_iterations + 1):
        state = read_json_file(paths["Prd"])
        story = select_story(state, args.story_id)

        if story is None:
            print("No eligible story found. Ralph loop finished.")
            return 0

        ensure_loop_branch(repo_root, state)

        run_id = f"{datetime.now().strftime('%Y%m%d-%H%M%S')}-{story.get('id', '')}"
        update_story_for_attempt(story, run_id)
        write_json_file(state, paths["Prd"])

        print(
            f"[{iteration}/{args.max_iterations}] task={args.task_type} "
            f"story={story.get('id', '')} title={story.get('title', '')}"
        )

        prompt_text = render_prompt(story, paths, args.task_type, run_id)
        codex_run = invoke_codex_iteration(
            prompt_text=prompt_text,
            repo_root=repo_root,
            paths=paths,
            run_id=run_id,
            model=args.codex_model,
            sandbox_mode=effective_codex_sandbox_mode,
            persist_sessions=args.persist_codex_sessions,
        )

        if int(codex_run["ExitCode"]) != 0:
            codex_error_summary = get_codex_failure_summary(codex_run["EventsPath"], codex_run["StderrPath"])
            failure_text = f"Codex exec failed: {codex_error_summary}"
            mark_story_failed(story, max_attempts_per_story, failure_text, "", "")
            write_json_file(state, paths["Prd"])
            print(f"WARNING: {failure_text}", file=sys.stderr)
            if not args.continue_after_failure:
                return 1
            continue

        codex_result = read_codex_result(codex_run["FinalPath"])
        changed_summary = str(codex_result.get("changed", "") or "")
        checks_summary = ""
        learnings_summary = str(codex_result.get("learnings", "") or "")
        result_state = str(codex_result.get("result", "") or "")

        if result_state != "done":
            if not changed_summary.strip():
                failure_text = "Codex returned blocked."
            else:
                failure_text = f"Codex returned blocked: {changed_summary}"
            mark_story_failed(story, max_attempts_per_story, failure_text, checks_summary, learnings_summary)
            write_json_file(state, paths["Prd"])
            print(f"WARNING: {failure_text}", file=sys.stderr)
            if not args.continue_after_failure:
                return 1
            continue

        if args.task_type == "review" and paths["ReviewOutputSchema"].exists():
            review_report_path = paths["ReviewsRoot"] / f"{run_id}.review.json"
            schema_error = validate_structured_review_report(review_report_path)
            if schema_error:
                failure_text = f"Review report schema validation failed: {schema_error}"
                mark_story_failed(story, max_attempts_per_story, failure_text, "", learnings_summary)
                write_json_file(state, paths["Prd"])
                print(f"WARNING: {failure_text}", file=sys.stderr)
                if not args.continue_after_failure:
                    return 1
                continue

        if not args.skip_checks:
            check_run = invoke_checks(paths, repo_root, story, run_id)
            if int(check_run["ExitCode"]) != 0:
                failure_text = f"Checks failed. log: {check_run['LogPath']}"
                mark_story_failed(story, max_attempts_per_story, failure_text, str(check_run["LogPath"]), learnings_summary)
                write_json_file(state, paths["Prd"])
                print(f"WARNING: {failure_text}", file=sys.stderr)
                if not args.continue_after_failure:
                    return 1
                continue
            checks_summary = checks_summary or str(check_run["LogPath"])
        else:
            checks_summary = checks_summary or "Skipped outer-loop checks."

        mark_story_done(story, checks_summary, learnings_summary)
        write_json_file(state, paths["Prd"])

        if not args.no_commit:
            commit_message = f"{commit_prefix} {story.get('id', '')} {story.get('title', '')}".strip()
            did_commit = commit_if_needed(repo_root, commit_message)
            if did_commit:
                print(f"Committed: {commit_message}")
            else:
                print("Story completed with no staged diff to commit.")

        if args.story_id.strip():
            print("Requested story completed.")
            return 0

    print(f"Reached max iterations: {args.max_iterations}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1)
