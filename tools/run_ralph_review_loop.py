#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import threading
import time
from datetime import datetime
from pathlib import Path
from typing import Any


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


def read_json_file(path: Path) -> dict[str, Any]:
    raw = path.read_text(encoding="utf-8")
    if not raw.strip():
        raise RuntimeError(f"JSON file is empty: {path}")
    return json.loads(raw)


def write_json_file(data: Any, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_utf8_text_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def run_git(args: list[str], repo_root: Path, check: bool = False) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        ["git", "-C", str(repo_root), *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    if check and result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or f"git {' '.join(args)} failed.")
    return result


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


def get_git_head_commit(repo_root: Path) -> str:
    result = run_git(["rev-parse", "HEAD"], repo_root)
    head = result.stdout.strip()
    if result.returncode != 0 or not head:
        raise RuntimeError("Failed to resolve HEAD commit.")
    return head


def resolve_commit_sha(repo_root: Path, commit_ref: str) -> str:
    result = run_git(["rev-parse", "--verify", f"{commit_ref}^{{commit}}"], repo_root)
    resolved = result.stdout.strip()
    if result.returncode != 0 or not resolved:
        raise RuntimeError(f"Failed to resolve commit: {commit_ref}")
    return resolved


def test_commit_is_ancestor_of(repo_root: Path, ancestor_commit: str, descendant_commit: str) -> bool:
    result = run_git(["merge-base", "--is-ancestor", ancestor_commit, descendant_commit], repo_root)
    return result.returncode == 0


def validate_state_directory_argument(state_directory_name: str, repo_root: Path) -> None:
    state_candidate = Path(state_directory_name)
    if state_candidate.suffix.lower() == ".json":
        raise RuntimeError(
            f"StateDirectory expects a directory, not a JSON file path: {state_directory_name}. "
            "Use the state directory itself, for example `-StateDirectory .ralph`."
        )

    resolved_state_candidate = resolve_absolute_path(state_directory_name, allow_missing=True, base_dir=repo_root)
    if resolved_state_candidate.exists() and resolved_state_candidate.is_file():
        raise RuntimeError(
            f"StateDirectory expects a directory, but resolved to a file: {resolved_state_candidate}."
        )


def get_state_paths(repo_root: Path, state_directory_name: str, checkpoint_file: str, reports_directory: str) -> dict[str, Path]:
    validate_state_directory_argument(state_directory_name, repo_root)
    state_root = resolve_absolute_path(state_directory_name, allow_missing=True, base_dir=repo_root)
    reports_root = state_root / reports_directory
    reports_root.mkdir(parents=True, exist_ok=True)
    return {
        "StateRoot": state_root,
        "Checkpoint": state_root / checkpoint_file,
        "ReportsRoot": reports_root,
        "Prd": state_root / "prd.json",
    }


def initialize_checkpoint(checkpoint_path: Path, head_commit: str, set_checkpoint_to_head: bool) -> None:
    if checkpoint_path.exists():
        return

    state = {
        "last_reviewed_commit": head_commit if set_checkpoint_to_head else "",
        "last_reviewed_at": None,
        "last_report": "",
        "pending_start_commit": "",
    }
    write_json_file(state, checkpoint_path)


def get_checkpoint_state(checkpoint_path: Path) -> dict[str, Any]:
    state = read_json_file(checkpoint_path)
    state.setdefault("last_reviewed_commit", "")
    state.setdefault("last_reviewed_at", None)
    state.setdefault("last_report", "")
    state.setdefault("pending_start_commit", "")
    return state


def set_checkpoint_start_commit(checkpoint_path: Path, commit_sha: str) -> None:
    state = {
        "last_reviewed_commit": "",
        "last_reviewed_at": None,
        "last_report": "",
        "pending_start_commit": commit_sha,
    }
    write_json_file(state, checkpoint_path)


def resolve_commit_prefix(configured_prefix: str) -> str:
    return configured_prefix.strip()


def resolve_effective_review_head(repo_root: Path, current_head: str, final_commit: str) -> tuple[str, bool]:
    if not final_commit.strip():
        return current_head, False
    if current_head == final_commit:
        return final_commit, True
    if test_commit_is_ancestor_of(repo_root, final_commit, current_head):
        return final_commit, True
    if test_commit_is_ancestor_of(repo_root, current_head, final_commit):
        return current_head, False
    raise RuntimeError(
        f"FinalCommit is not on the current branch history chain. "
        f"current_head={current_head} final_commit={final_commit}"
    )


def get_commit_range_lines(repo_root: Path, range_value: str, single_commit: bool = False) -> list[str]:
    args = ["-c", "i18n.logOutputEncoding=utf-8", "-C", str(repo_root), "log", "--format=%H\t%s", "--reverse"]
    if single_commit:
        args += ["-n", "1"]
    args.append(range_value)
    result = subprocess.run(args=["git", *args], capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise RuntimeError(f"Failed to read git log for range: {range_value}")
    return [line for line in result.stdout.splitlines() if line.strip()]


def get_first_matching_commit_line(commit_lines: list[str], prefix: str) -> str:
    if not prefix.strip():
        return commit_lines[0] if commit_lines else ""
    for line in commit_lines:
        parts = line.split("\t", 1)
        if len(parts) == 2 and parts[1].startswith(prefix):
            return line
    return ""


def get_matching_commit_lines(commit_lines: list[str], prefix: str) -> list[str]:
    if not prefix.strip():
        return list(commit_lines)
    matches: list[str] = []
    for line in commit_lines:
        parts = line.split("\t", 1)
        if len(parts) == 2 and parts[1].startswith(prefix):
            matches.append(line)
    return matches


def parse_commit_line(commit_line: str) -> dict[str, str]:
    parts = commit_line.split("\t", 1)
    if len(parts) != 2:
        raise RuntimeError(f"Invalid commit line: {commit_line}")
    return {"Sha": parts[0], "Subject": parts[1]}


def normalize_compact_text(text: str) -> str:
    if not text or not text.strip():
        return ""
    return re.sub(r"\s+", " ", text).strip()


def limit_text_length(text: str, max_length: int = 160) -> str:
    normalized = normalize_compact_text(text)
    if not normalized:
        return ""
    if len(normalized) <= max_length:
        return normalized
    return normalized[: max_length - 3].rstrip() + "..."


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


def build_commit_summary(commit_lines: list[str], max_lines: int = 12, max_subject_length: int = 140) -> str:
    summary_lines: list[str] = []
    total_count = 0
    for line in commit_lines:
        parts = line.split("\t", 1)
        if len(parts) != 2:
            continue
        total_count += 1
        if len(summary_lines) >= max_lines:
            continue
        short_sha = parts[0][:7]
        subject = limit_text_length(parts[1], max_subject_length)
        summary_lines.append(f"- {short_sha} {subject}")
    if total_count > len(summary_lines):
        summary_lines.append(f"- ... and {total_count - len(summary_lines)} more commits")
    return "\n".join(summary_lines)


def try_parse_datetime(value: str) -> datetime | None:
    if not value or not value.strip():
        return None
    try:
        return datetime.fromisoformat(value)
    except ValueError:
        return None


def get_completed_stories_since(prd_path: Path, since_timestamp: str) -> list[dict[str, Any]]:
    if not prd_path.exists():
        return []

    try:
        prd = read_json_file(prd_path)
    except Exception:
        return []

    since = try_parse_datetime(since_timestamp)
    completed_stories: list[dict[str, Any]] = []
    for story in list(prd.get("userStories", [])):
        if not story or str(story.get("status", "")) != "done":
            continue
        completed_at = try_parse_datetime(str(story.get("completedAt", "") or ""))
        if completed_at is None:
            continue
        if since is not None and completed_at <= since:
            continue
        completed_stories.append(
            {
                "Id": str(story.get("id", "") or ""),
                "Title": str(story.get("title", "") or ""),
                "CompletedAt": completed_at,
            }
        )

    completed_stories.sort(key=lambda item: item["CompletedAt"])
    return completed_stories


def format_story_summary(stories: list[dict[str, Any]], max_lines: int = 8, max_title_length: int = 120) -> str:
    if not stories:
        return ""

    lines: list[str] = []
    display_count = min(len(stories), max_lines)
    for index in range(display_count):
        story = stories[index]
        title = limit_text_length(str(story.get("Title", "") or ""), max_title_length)
        lines.append(f"- {story.get('Id', '')} {title}")
    if len(stories) > display_count:
        lines.append(f"- ... and {len(stories) - display_count} more stories")
    return "\n".join(lines)


def build_review_prompt(
    scope_label: str,
    commit_summary: str,
    prefix: str,
    single_commit: bool,
    structured_output: bool,
    story_summary: str,
) -> str:
    prefix_text = 'Review the whole scope.'
    if prefix.strip():
        prefix_text = f'Prefix "{prefix}" marks primary Ralph commits; still review the whole scope.'

    scope_text = f'Scope fixed: range "{scope_label}". Do not ask to choose scope.'
    if single_commit:
        scope_text = f'Scope fixed: commit "{scope_label}". Do not ask to choose scope.'

    lines = [
        'Use `$code-review`.',
        'Read `docs/design/project_context_units.md` first.',
        scope_text,
        prefix_text,
        'Use git diff/show directly; the commit list below is only a hint.',
        '',
        'Commits in scope:',
        commit_summary,
        '',
        'Check for runtime regressions, scene-script mismatches, world/battle/modal state bugs, save/load or dictionary-shape hazards, preview-vs-execution divergence, and missing headless regressions.',
        'Write the final review in Simplified Chinese. Keep file paths, commit hashes, commands, and code identifiers unchanged.',
        'Put the complete review in your final answer.',
    ]

    if story_summary.strip():
        lines.extend([
            'Recently completed stories:',
            story_summary,
            '',
        ])

    if structured_output:
        lines.extend([
            'Return exactly one JSON object matching the output schema.',
            'Use `Critical` / `Warning` / `Suggestion` severities. Every finding needs category, file, issue, and fix.',
            'If no issues are found, set `passed` to true and return an empty findings array.',
        ])
    else:
        lines.extend([
            'Follow the `code-review` skill output format.',
            'Report findings with severity, file:line, and concrete failure modes; include the required summary table.',
            'If no issues are found, say `Code review passed — no issues found.`',
        ])

    return "\n".join(lines)


def get_default_review_output_schema() -> str:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "additionalProperties": False,
        "required": [
            "passed",
            "findings",
            "what_was_done_well",
            "open_questions",
            "residual_risks",
            "summary",
        ],
        "properties": {
            "passed": {"type": "boolean"},
            "findings": {
                "type": "array",
                "items": {
                    "type": "object",
                    "additionalProperties": False,
                    "required": ["category", "severity", "file", "issue", "fix"],
                    "properties": {
                        "category": {"type": "string", "minLength": 1},
                        "severity": {"type": "string", "enum": ["Critical", "Warning", "Suggestion"]},
                        "file": {"type": "string", "minLength": 1},
                        "issue": {"type": "string", "minLength": 1},
                        "fix": {"type": "string", "minLength": 1},
                    },
                },
            },
            "what_was_done_well": {"type": "array", "items": {"type": "string"}},
            "open_questions": {"type": "array", "items": {"type": "string"}},
            "residual_risks": {"type": "array", "items": {"type": "string"}},
            "summary": {
                "type": "object",
                "additionalProperties": False,
                "required": ["totals", "categories"],
                "properties": {
                    "totals": {
                        "type": "object",
                        "additionalProperties": False,
                        "required": ["critical", "warning", "suggestion"],
                        "properties": {
                            "critical": {"type": "integer", "minimum": 0},
                            "warning": {"type": "integer", "minimum": 0},
                            "suggestion": {"type": "integer", "minimum": 0},
                        },
                    },
                    "categories": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "additionalProperties": False,
                            "required": ["name", "critical", "warning", "suggestion", "na"],
                            "properties": {
                                "name": {"type": "string", "minLength": 1},
                                "critical": {"type": "integer", "minimum": 0},
                                "warning": {"type": "integer", "minimum": 0},
                                "suggestion": {"type": "integer", "minimum": 0},
                                "na": {"type": "boolean"},
                            },
                        },
                    },
                },
            },
        },
    }
    return json.dumps(schema, ensure_ascii=False, indent=2)


def resolve_review_output_schema_path(state_root: Path, requested_path: str, enable_schema: bool) -> Path | None:
    if not enable_schema:
        return None
    if requested_path.strip():
        return resolve_absolute_path(requested_path)
    schema_path = resolve_absolute_path(str(state_root / "review_output.schema.json"), allow_missing=True)
    write_utf8_text_file(schema_path, get_default_review_output_schema())
    return schema_path


def test_markdown_review_report_completeness(report_path: Path) -> dict[str, Any]:
    if not report_path.exists():
        return {"IsValid": False, "Message": f"Review report file was not created: {report_path}"}

    content = report_path.read_text(encoding="utf-8")
    if not content.strip():
        return {"IsValid": False, "Message": f"Review report file is empty: {report_path}"}

    has_summary = re.search(r"(?m)^##\s+Summary\s*$", content) is not None
    has_summary_table = re.search(r"(?m)^\| Category\s+\|", content) is not None
    has_no_issues = "Code review passed — no issues found." in content
    has_findings = re.search(r"(?m)^### \[[^\]]+\] [—-] (Critical|Warning|Suggestion)\s*$", content) is not None
    has_file_field = "**File:**" in content
    has_issue_field = "**Issue:**" in content
    has_fix_field = "**Fix:**" in content

    if not has_summary:
        return {"IsValid": False, "Message": "Review report is missing the `## Summary` section."}
    if not has_summary_table:
        return {"IsValid": False, "Message": "Review report is missing the summary table header."}
    if not (has_no_issues or has_findings):
        return {"IsValid": False, "Message": "Review report is missing both findings and the explicit no-issues conclusion."}
    if has_findings and not (has_file_field and has_issue_field and has_fix_field):
        return {
            "IsValid": False,
            "Message": "Review report contains findings but is missing one of the required File/Issue/Fix fields.",
        }

    return {"IsValid": True, "Message": "Markdown review report passed completeness checks."}


def test_structured_review_report_completeness(report_path: Path) -> dict[str, Any]:
    if not report_path.exists():
        return {"IsValid": False, "Message": f"Structured review report file was not created: {report_path}"}

    content = report_path.read_text(encoding="utf-8")
    if not content.strip():
        return {"IsValid": False, "Message": f"Structured review report file is empty: {report_path}"}

    try:
        report = json.loads(content)
    except json.JSONDecodeError as exc:
        return {"IsValid": False, "Message": f"Structured review report is not valid JSON. {exc}"}

    for required_key in ("passed", "findings", "what_was_done_well", "open_questions", "residual_risks", "summary"):
        if required_key not in report:
            return {"IsValid": False, "Message": f"Structured review report is missing required key: {required_key}"}

    findings = list(report["findings"])
    for finding in findings:
        if not isinstance(finding, dict):
            return {"IsValid": False, "Message": "Structured review report contains a finding that is not an object."}
        for required_key in ("category", "severity", "file", "issue", "fix"):
            if required_key not in finding or not str(finding[required_key]).strip():
                return {"IsValid": False, "Message": f"Structured review report contains a finding missing required field: {required_key}"}
        if str(finding["severity"]) not in {"Critical", "Warning", "Suggestion"}:
            return {"IsValid": False, "Message": f"Structured review report contains an invalid severity value: {finding['severity']}"}

    if bool(report["passed"]) and findings:
        return {"IsValid": False, "Message": "Structured review report cannot set `passed=true` while findings are present."}

    summary = report["summary"]
    if not isinstance(summary, dict):
        return {"IsValid": False, "Message": "Structured review report summary is missing or not an object."}

    totals = summary.get("totals")
    if not isinstance(totals, dict):
        return {"IsValid": False, "Message": "Structured review report summary.totals is missing or not an object."}

    computed_totals = {"critical": 0, "warning": 0, "suggestion": 0}
    for finding in findings:
        severity = str(finding["severity"])
        if severity == "Critical":
            computed_totals["critical"] += 1
        elif severity == "Warning":
            computed_totals["warning"] += 1
        elif severity == "Suggestion":
            computed_totals["suggestion"] += 1

    for key in ("critical", "warning", "suggestion"):
        if key not in totals:
            return {"IsValid": False, "Message": f"Structured review report summary.totals is missing key: {key}"}
        if int(totals[key]) != int(computed_totals[key]):
            return {"IsValid": False, "Message": f"Structured review report summary.totals.{key} does not match the findings array."}

    return {"IsValid": True, "Message": "Structured review report passed completeness checks."}


def test_review_report_completeness(report_path: Path, structured_output: bool) -> dict[str, Any]:
    if structured_output:
        return test_structured_review_report_completeness(report_path)
    return test_markdown_review_report_completeness(report_path)


def build_codex_review_command(model: str, output_schema_path: Path | None, persist_sessions: bool, repo_root: Path, sandbox_mode: str, report_path: Path) -> list[str]:
    arguments = [
        "exec",
        "--cd",
        str(repo_root),
        "--sandbox",
        sandbox_mode,
        "--json",
        "-o",
        str(report_path),
    ]
    if model.strip():
        arguments += ["-m", model]
    if output_schema_path is not None:
        arguments += ["--output-schema", str(output_schema_path)]
    if not persist_sessions:
        arguments += ["--ephemeral"]
    arguments += ["-"]

    codex_command_path = get_resolved_command_path("codex")
    if codex_command_path.lower().endswith(".ps1"):
        powershell_command = get_preferred_powershell_command()
        return [
            powershell_command,
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            codex_command_path,
            *arguments,
        ]
    return [codex_command_path, *arguments]


def invoke_codex_review(
    repo_root: Path,
    prompt_text: str,
    report_path: Path,
    sandbox_mode: str,
    model: str,
    persist_sessions: bool,
    output_schema_path: Path | None,
) -> int:
    command = build_codex_review_command(
        model=model,
        output_schema_path=output_schema_path,
        persist_sessions=persist_sessions,
        repo_root=repo_root,
        sandbox_mode=sandbox_mode,
        report_path=report_path,
    )
    events_path = Path(f"{report_path}.events.jsonl")
    stderr_path = Path(f"{report_path}.stderr.log")
    for path in (events_path, stderr_path):
        if path.exists():
            path.unlink()

    with (
        events_path.open("w", encoding="utf-8") as events_file,
        stderr_path.open("w", encoding="utf-8") as stderr_file,
    ):
        process = subprocess.Popen(
            command,
            cwd=repo_root,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
        assert process.stdin is not None
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
        process.stdin.write(prompt_text)
        process.stdin.close()
        process.wait()
        stdout_thread.join()
        stderr_thread.join()

    return process.returncode


def update_checkpoint_after_review(checkpoint_path: Path, reviewed_commit: str, report_path: Path) -> None:
    state = {
        "last_reviewed_commit": reviewed_commit,
        "last_reviewed_at": datetime.now().astimezone().isoformat(timespec="seconds"),
        "last_report": str(report_path),
        "pending_start_commit": "",
    }
    write_json_file(state, checkpoint_path)


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(allow_abbrev=False)
    # WorkingDirectory: 从哪个目录解析 git 仓库根；例如 `python tools/run_ralph_review_loop.py -WorkingDirectory E:\game\magic`
    parser.add_argument("-WorkingDirectory", dest="working_directory", default=".")
    # StateDirectory: review loop 的状态目录；例如 `python tools/run_ralph_review_loop.py -StateDirectory .ralph`
    parser.add_argument("-StateDirectory", dest="state_directory", default=".ralph")
    # CheckpointFileName: checkpoint 文件名；例如 `python tools/run_ralph_review_loop.py -CheckpointFileName review_state.json`
    parser.add_argument("-CheckpointFileName", dest="checkpoint_file_name", default="review_state.json")
    # ReportsDirectoryName: review 报告目录名；例如 `python tools/run_ralph_review_loop.py -ReportsDirectoryName reviews`
    parser.add_argument("-ReportsDirectoryName", dest="reports_directory_name", default="reviews")
    # StartCommit: 从指定 commit 开始建立 review 起点；例如 `python tools/run_ralph_review_loop.py -StartCommit abc1234`
    parser.add_argument("-StartCommit", dest="start_commit", default="")
    # FinalCommit: 指定最终 review 边界；到达该提交时，即使未达到批量阈值，也会补一次剩余范围 review。
    parser.add_argument("-FinalCommit", dest="final_commit", default="")
    # CommitPrefix: 只把带此前缀的提交视为 Ralph 主提交；不传时不做前缀过滤。
    parser.add_argument("-CommitPrefix", dest="commit_prefix", default="")
    # CodexModel: 覆盖默认 Codex 模型；例如 `python tools/run_ralph_review_loop.py -CodexModel gpt-5.4`
    parser.add_argument("-CodexModel", dest="codex_model", default="")
    # CodexSandboxMode: 传给 `codex exec --sandbox` 的模式；例如 `python tools/run_ralph_review_loop.py -CodexSandboxMode read-only`
    parser.add_argument(
        "-CodexSandboxMode",
        dest="codex_sandbox_mode",
        choices=["read-only", "workspace-write", "danger-full-access"],
        default="read-only",
    )
    # UseOutputSchema: 要求 review 输出匹配 JSON schema；例如 `python tools/run_ralph_review_loop.py -UseOutputSchema`
    parser.add_argument("-UseOutputSchema", dest="use_output_schema", action="store_true")
    # ReviewOutputSchemaPath: 显式指定 review output schema 文件；例如 `python tools/run_ralph_review_loop.py -UseOutputSchema -ReviewOutputSchemaPath .\custom_review.schema.json`
    parser.add_argument("-ReviewOutputSchemaPath", dest="review_output_schema_path", default="")
    # ReviewEveryNCommits: 每累计多少个匹配提交触发一次范围 review；例如 `python tools/run_ralph_review_loop.py -ReviewEveryNCommits 5`
    parser.add_argument("-ReviewEveryNCommits", dest="review_every_n_commits", type=int, default=0)
    # ReviewOnStoryCompletion: 检测到 `.ralph/prd.json` 有新完成 story 时触发范围 review；例如 `python tools/run_ralph_review_loop.py -ReviewOnStoryCompletion`
    parser.add_argument("-ReviewOnStoryCompletion", dest="review_on_story_completion", action="store_true")
    # PollSeconds: 每次轮询之间休眠多少秒；例如 `python tools/run_ralph_review_loop.py -PollSeconds 300`
    parser.add_argument("-PollSeconds", dest="poll_seconds", type=int, default=600)
    # PersistCodexSessions: 不附加 `--ephemeral`，保留 Codex 会话；例如 `python tools/run_ralph_review_loop.py -PersistCodexSessions`
    parser.add_argument("-PersistCodexSessions", dest="persist_codex_sessions", action="store_true")
    # BootstrapReview: 首次启动时不要把 checkpoint 直接对齐到当前 HEAD；例如 `python tools/run_ralph_review_loop.py -BootstrapReview`
    parser.add_argument("-BootstrapReview", dest="bootstrap_review", action="store_true")
    return parser


def main() -> int:
    parser = build_argument_parser()
    args = parser.parse_args()

    if args.review_every_n_commits < 0 or args.review_every_n_commits > 1000:
        raise RuntimeError("ReviewEveryNCommits must be between 0 and 1000.")
    if args.poll_seconds < 60 or args.poll_seconds > 86400:
        raise RuntimeError("PollSeconds must be between 60 and 86400.")

    ensure_command_exists("git")
    ensure_command_exists("codex")

    repo_root = get_repo_root(args.working_directory)
    paths = get_state_paths(
        repo_root=repo_root,
        state_directory_name=args.state_directory,
        checkpoint_file=args.checkpoint_file_name,
        reports_directory=args.reports_directory_name,
    )
    resolved_output_schema_path = resolve_review_output_schema_path(
        state_root=paths["StateRoot"],
        requested_path=args.review_output_schema_path,
        enable_schema=args.use_output_schema,
    )

    head_at_start = get_git_head_commit(repo_root)
    initialize_checkpoint(paths["Checkpoint"], head_at_start, not args.bootstrap_review)

    resolved_start_commit = ""
    if args.start_commit.strip():
        resolved_start_commit = resolve_commit_sha(repo_root, args.start_commit)
        if not test_commit_is_ancestor_of(repo_root, resolved_start_commit, head_at_start):
            raise RuntimeError(f"StartCommit must be reachable from the current HEAD: {resolved_start_commit}")
        set_checkpoint_start_commit(paths["Checkpoint"], resolved_start_commit)
        print(f"Start commit override set to: {resolved_start_commit}")

    resolved_final_commit = ""
    if args.final_commit.strip():
        resolved_final_commit = resolve_commit_sha(repo_root, args.final_commit)
        resolve_effective_review_head(repo_root, head_at_start, resolved_final_commit)
        if resolved_start_commit and not test_commit_is_ancestor_of(repo_root, resolved_start_commit, resolved_final_commit):
            raise RuntimeError(
                f"FinalCommit must not be earlier than StartCommit. "
                f"start_commit={resolved_start_commit} final_commit={resolved_final_commit}"
            )

    print(f"Review loop watching repo: {repo_root}")
    print(f"Checkpoint file: {paths['Checkpoint']}")
    print(f"Reports directory: {paths['ReportsRoot']}")
    if resolved_final_commit:
        print(f"Final commit boundary: {resolved_final_commit}")
    if args.commit_prefix.strip():
        print(f'Commit prefix filter: "{args.commit_prefix.strip()}"')
    else:
        print("Commit prefix filter: disabled")
    if args.review_every_n_commits > 0:
        print(f"Commit batch trigger: every {args.review_every_n_commits} matching commits")
    if args.review_on_story_completion:
        print("Story trigger: enabled for completed stories in .ralph/prd.json")

    while True:
        state = get_checkpoint_state(paths["Checkpoint"])
        last_reviewed_commit = str(state.get("last_reviewed_commit", "") or "")
        last_reviewed_at = str(state.get("last_reviewed_at", "") or "")
        pending_start_commit = str(state.get("pending_start_commit", "") or "")
        current_head = get_git_head_commit(repo_root)
        review_head, final_commit_reached = resolve_effective_review_head(repo_root, current_head, resolved_final_commit)
        commit_prefix_to_use = resolve_commit_prefix(args.commit_prefix)

        target_commit_line = ""
        scope_label = ""
        single_commit = True
        target_commit: dict[str, str] | None = None
        target_commit_lines: list[str] = []
        completed_stories: list[dict[str, Any]] = []

        if resolved_final_commit and last_reviewed_commit.strip():
            if last_reviewed_commit == resolved_final_commit:
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Final commit {resolved_final_commit[:7]} already reviewed. Exiting."
                )
                return 0
            if test_commit_is_ancestor_of(repo_root, resolved_final_commit, last_reviewed_commit):
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Checkpoint {last_reviewed_commit[:7]} is already beyond final commit {resolved_final_commit[:7]}. Exiting."
                )
                return 0

        if pending_start_commit.strip():
            pending_lines = get_commit_range_lines(repo_root, pending_start_commit, single_commit=True)
            if not pending_lines:
                raise RuntimeError(f"Pending start commit could not be loaded: {pending_start_commit}")
            target_commit_line = pending_lines[0]
            target_commit_lines = [target_commit_line]
        elif not last_reviewed_commit.strip():
            head_lines = get_commit_range_lines(repo_root, review_head, single_commit=True)
            if not head_lines:
                raise RuntimeError(f"Review head commit could not be loaded: {review_head}")
            target_commit_line = head_lines[0]
            target_commit_lines = [target_commit_line]
        elif last_reviewed_commit == review_head:
            if resolved_final_commit and final_commit_reached:
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Reached final commit {resolved_final_commit[:7]} with no remaining commits. Exiting."
                )
                return 0
            print(f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] No new commits. Sleeping {args.poll_seconds} seconds.")
            time.sleep(args.poll_seconds)
            continue
        else:
            range_value = f"{last_reviewed_commit}..{review_head}"
            commit_lines = get_commit_range_lines(repo_root, range_value)
            matching_commit_lines = get_matching_commit_lines(commit_lines, commit_prefix_to_use)
            if not matching_commit_lines and not (final_commit_reached and resolved_final_commit and commit_lines):
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"No new commits matching prefix '{commit_prefix_to_use}'. Sleeping {args.poll_seconds} seconds."
                )
                time.sleep(args.poll_seconds)
                continue

            should_batch_review = False
            trigger_reasons: list[str] = []
            if args.review_every_n_commits > 0 and len(matching_commit_lines) >= args.review_every_n_commits:
                should_batch_review = True
                trigger_reasons.append(f"matching commits: {len(matching_commit_lines)}")
            if final_commit_reached and resolved_final_commit and commit_lines:
                should_batch_review = True
                trigger_reasons.append(f"reached final commit: {resolved_final_commit[:7]}")
            if args.review_on_story_completion:
                completed_stories = get_completed_stories_since(paths["Prd"], last_reviewed_at)
                if completed_stories:
                    should_batch_review = True
                    trigger_reasons.append(f"completed stories: {len(completed_stories)}")

            if should_batch_review:
                single_commit = False
                scope_label = range_value
                target_commit_lines = list(commit_lines)
                target_commit = {"Sha": review_head, "Subject": "range review"}
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Triggering range review for {range_value} ({', '.join(trigger_reasons)})."
                )
            else:
                target_commit_line = get_first_matching_commit_line(commit_lines, commit_prefix_to_use)
                if not target_commit_line:
                    print(
                        f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                        f"No eligible commit selected. Sleeping {args.poll_seconds} seconds."
                    )
                    time.sleep(args.poll_seconds)
                    continue
                target_commit_lines = [target_commit_line]

        if single_commit:
            target_commit = parse_commit_line(target_commit_line)
            scope_label = target_commit["Sha"]
        assert target_commit is not None

        commit_summary = build_commit_summary(target_commit_lines)
        story_summary = format_story_summary(completed_stories)
        prompt = build_review_prompt(
            scope_label=scope_label,
            commit_summary=commit_summary,
            prefix=commit_prefix_to_use,
            single_commit=single_commit,
            structured_output=args.use_output_schema,
            story_summary=story_summary,
        )

        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        report_extension = "json" if args.use_output_schema else "md"
        report_stem = target_commit["Sha"][:7]
        if not single_commit:
            report_stem = f"{last_reviewed_commit[:7]}-to-{review_head[:7]}"
        report_path = paths["ReportsRoot"] / f"{timestamp}-{report_stem}.{report_extension}"

        if single_commit:
            print(f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] Reviewing commit {target_commit['Sha'][:7]} {target_commit['Subject']}")
        else:
            print(f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] Reviewing commit range {scope_label}")

        exit_code = invoke_codex_review(
            repo_root=repo_root,
            prompt_text=prompt,
            report_path=report_path,
            sandbox_mode=args.codex_sandbox_mode,
            model=args.codex_model,
            persist_sessions=args.persist_codex_sessions,
            output_schema_path=resolved_output_schema_path,
        )

        if exit_code == 0:
            validation = test_review_report_completeness(report_path, args.use_output_schema)
            if bool(validation.get("IsValid", False)):
                reviewed_commit = target_commit["Sha"] if single_commit else review_head
                update_checkpoint_after_review(paths["Checkpoint"], reviewed_commit, report_path)
                print(f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] Review saved to {report_path}")
                if resolved_final_commit and reviewed_commit == resolved_final_commit:
                    print(
                        f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                        f"Reached final commit {resolved_final_commit[:7]} after successful review. Exiting."
                    )
                    return 0
            else:
                validation_log_path = Path(f"{report_path}.validation.log")
                write_utf8_text_file(validation_log_path, str(validation.get("Message", "")))
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Review output failed validation: {validation.get('Message', '')}"
                )
                print(
                    f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                    f"Validation log saved to {validation_log_path}. Checkpoint not advanced."
                )
        else:
            print(
                f"[{datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}] "
                f"Codex review failed with exit code {exit_code}. Checkpoint not advanced."
            )

        time.sleep(args.poll_seconds)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1)
