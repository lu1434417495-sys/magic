#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import os
import shlex
import shutil
import subprocess
import sys
import threading
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Any


DEFAULT_CLAUDE_TOOLS = ["Read", "Glob", "Grep"]


@dataclass
class DebateRuntime:
	codex_command_path: Path
	claude_command_path: Path
	codex_sandbox_mode: str
	codex_sandbox_mode_label: str
	claude_allowed_tools: list[str]
	claude_bare_mode_enabled: bool
	claude_bash_path: Path


@dataclass
class ProcessResult:
	exit_code: int
	stdout: str
	stderr: str
	duration_ms: int


def validate_range(name: str, value: int, minimum: int, maximum: int) -> int:
	if value < minimum or value > maximum:
		raise argparse.ArgumentTypeError(f"{name} must be between {minimum} and {maximum}.")
	return value


def rounds_value(value: str) -> int:
	return validate_range("Rounds", int(value), 1, 8)


def timeout_value(value: str) -> int:
	return validate_range("TimeoutSeconds", int(value), 30, 3600)


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Run a multi-round Codex/Claude debate and synthesize a final answer.")
	parser.add_argument("question", nargs="?", help="Inline debate question.")
	parser.add_argument("--question", "-Question", dest="question_option", help="Inline debate question.")
	parser.add_argument("--question-file", "-QuestionFile", help="Read the debate question from a file.")
	parser.add_argument("--rounds", "-Rounds", type=rounds_value, default=3)
	parser.add_argument("--working-directory", "-WorkingDirectory", default=".")
	parser.add_argument("--output-root", "-OutputRoot", default=".tmp/ai-debate")
	parser.add_argument("--codex-model", "-CodexModel", default="gpt-5.4")
	parser.add_argument("--codex-sandbox-mode", "-CodexSandboxMode", default="")
	parser.add_argument("--claude-model", "-ClaudeModel", default="sonnet")
	parser.add_argument("--codex-timeout-seconds", "-CodexTimeoutSeconds", type=timeout_value, default=600)
	parser.add_argument("--claude-timeout-seconds", "-ClaudeTimeoutSeconds", type=timeout_value, default=600)
	parser.add_argument("--tool-scope", "-ToolScope", choices=["auto", "workspace", "context"], default="auto")
	parser.add_argument("--claude-tools", "-ClaudeTools", nargs="+", default=DEFAULT_CLAUDE_TOOLS)
	parser.add_argument("--claude-git-bash-path", "-ClaudeGitBashPath", default="")
	parser.add_argument("--claude-bare-mode", "-ClaudeBareMode", action="store_true")
	parser.add_argument("--final-synthesizer", "-FinalSynthesizer", choices=["codex", "claude"], default="codex")
	parser.add_argument("--context-path", "-ContextPath", nargs="+", action="extend", default=[])
	return parser


def resolve_absolute_path(path: str | Path, allow_missing: bool = False, base_dir: Path | None = None) -> Path:
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


def read_text_file(path: Path) -> str:
	return path.read_text(encoding="utf-8").strip()


def write_text_file(path: Path, content: str) -> None:
	path.parent.mkdir(parents=True, exist_ok=True)
	path.write_text(content, encoding="utf-8")


def write_json_file(path: Path, content: Any) -> None:
	write_text_file(path, json.dumps(content, ensure_ascii=False, indent=2) + "\n")


def get_resolved_command_path(name: str) -> Path:
	resolved = shutil.which(name)
	if not resolved:
		raise RuntimeError(f"Unable to resolve command path: {name}")
	return Path(resolved).resolve(strict=False)


def get_preferred_powershell_command() -> str:
	for candidate in ("pwsh", "powershell"):
		resolved = shutil.which(candidate)
		if resolved:
			return resolved
	raise RuntimeError("A PowerShell executable is required to run an external .ps1 command shim.")


def build_command(command_path: Path, arguments: list[str]) -> list[str]:
	if command_path.suffix.lower() == ".ps1":
		return [
			get_preferred_powershell_command(),
			"-NoProfile",
			"-ExecutionPolicy",
			"Bypass",
			"-File",
			str(command_path),
			*arguments,
		]
	return [str(command_path), *arguments]


def quote_command(command: list[str]) -> str:
	if os.name == "nt":
		return subprocess.list2cmdline(command)
	return shlex.join(command)


def expand_template(template: str, values: dict[str, Any]) -> str:
	result = template
	for key, value in values.items():
		result = result.replace(f"{{{{{key}}}}}", str(value))
	return result


def get_section_text(content: str, fallback: str) -> str:
	if not content.strip():
		return fallback
	return content.strip()


def normalize_path_for_compare(path: Path) -> str:
	return os.path.normcase(os.path.abspath(str(path)))


def same_path(left: Path, right: Path) -> bool:
	return normalize_path_for_compare(left) == normalize_path_for_compare(right)


def unique_paths(paths: list[Path]) -> list[Path]:
	seen: set[str] = set()
	result: list[Path] = []
	for path in paths:
		key = normalize_path_for_compare(path)
		if key in seen:
			continue
		seen.add(key)
		result.append(path)
	return result


def get_tool_scope_paths(workspace_root: Path, additional_context_paths: list[Path], scope_mode: str) -> list[Path]:
	context_paths = unique_paths([path for path in additional_context_paths if str(path).strip()])
	if scope_mode == "context":
		if not context_paths:
			raise RuntimeError("ToolScope 'context' requires at least one ContextPath.")
		return context_paths
	if scope_mode == "workspace":
		return unique_paths([workspace_root, *context_paths])
	if context_paths:
		return context_paths
	return [workspace_root]


def get_tool_access_paths(workspace_root: Path, tool_scope_paths: list[Path]) -> list[Path]:
	access_paths: list[Path] = []
	for path in tool_scope_paths:
		access_path = path
		if path.is_file():
			access_path = path.parent
		if not str(access_path).strip():
			access_path = workspace_root
		access_paths.append(resolve_absolute_path(access_path))
	access_paths = unique_paths(access_paths)
	if not access_paths:
		return [workspace_root]
	return access_paths


def test_path_within_root(path: Path, root: Path) -> bool:
	resolved_root = resolve_absolute_path(root)
	candidate_path = resolve_absolute_path(path)
	if candidate_path.is_file():
		candidate_path = candidate_path.parent
	root_key = normalize_path_for_compare(resolved_root)
	candidate_key = normalize_path_for_compare(candidate_path)
	if candidate_key == root_key:
		return True
	root_prefix = root_key.rstrip("\\/") + os.sep
	return candidate_key.startswith(root_prefix)


def new_scoped_working_directory(output_directory: Path, tool_scope_paths: list[Path]) -> Path:
	scope_root = output_directory / "_tool-scope"
	scope_root.mkdir(parents=True, exist_ok=True)
	lines = ["Restricted tool workspace for AI debate subprocesses.", "", "Allowed repo paths:"]
	lines.extend(f"- {path}" for path in tool_scope_paths)
	write_text_file(scope_root / "README.md", "\n".join(lines))
	return scope_root


def get_tool_working_directory(workspace_root: Path, tool_scope_paths: list[Path], output_directory: Path) -> Path:
	if all(test_path_within_root(path, workspace_root) for path in tool_scope_paths):
		return workspace_root
	return new_scoped_working_directory(output_directory, tool_scope_paths)


def resolve_claude_bash_path(preferred_path: str = "") -> Path:
	candidates: list[str] = []
	if preferred_path.strip():
		candidates.append(preferred_path)
	if os.environ.get("CLAUDE_CODE_GIT_BASH_PATH", "").strip():
		candidates.append(os.environ["CLAUDE_CODE_GIT_BASH_PATH"])
	candidates.extend(
		[
			r"C:\Program Files\Git\bin\bash.exe",
			r"C:\Program Files\Git\usr\bin\bash.exe",
			r"D:\Git\bin\bash.exe",
			r"D:\Git\usr\bin\bash.exe",
			r"E:\Git\bin\bash.exe",
			r"E:\Git\usr\bin\bash.exe",
		]
	)
	bash_command = shutil.which("bash")
	if bash_command:
		candidates.append(bash_command)

	for candidate in candidates:
		if not candidate.strip():
			continue
		resolved = resolve_absolute_path(candidate, allow_missing=True)
		if resolved.is_file():
			return resolved
	raise RuntimeError("Claude requires CLAUDE_CODE_GIT_BASH_PATH, but no usable bash executable was found.")


def initialize_claude_environment(preferred_bash_path: str = "") -> Path:
	bash_path = resolve_claude_bash_path(preferred_bash_path)
	os.environ["CLAUDE_CODE_GIT_BASH_PATH"] = str(bash_path)
	return bash_path


def get_claude_auth_status(claude_command_path: Path, working_directory: Path) -> dict[str, Any]:
	command = build_command(claude_command_path, ["auth", "status"])
	result = subprocess.run(command, cwd=working_directory, capture_output=True, text=True, encoding="utf-8", errors="replace")
	output = result.stdout.strip()
	if not output:
		raise RuntimeError("Claude auth status returned no output.")
	try:
		return json.loads(output)
	except json.JSONDecodeError as exc:
		raise RuntimeError("Claude auth status returned invalid JSON.") from exc


def append_log_line(log_path: Path, line: str, lock: threading.Lock) -> None:
	with lock:
		with log_path.open("a", encoding="utf-8") as handle:
			handle.write(line + "\n")
		print(line)


def kill_process_tree(process: subprocess.Popen[str]) -> None:
	if os.name == "nt":
		subprocess.run(
			["taskkill", "/F", "/T", "/PID", str(process.pid)],
			stdout=subprocess.DEVNULL,
			stderr=subprocess.DEVNULL,
			check=False,
		)
	else:
		process.kill()


def invoke_logged_process(
	command: list[str],
	working_directory: Path,
	log_path: Path,
	timeout_seconds: int,
	header_lines: list[str] | None = None,
	stdin_text: str | None = None,
) -> ProcessResult:
	header = list(header_lines or [])
	header.extend(
		[
			f"started_at: {datetime.now().astimezone().isoformat()}",
			f"working_directory: {working_directory}",
			f"timeout_seconds: {timeout_seconds}",
			f"command: {quote_command(command)}",
			"",
		]
	)
	write_text_file(log_path, "\n".join(header))

	stdout_chunks: list[str] = []
	stderr_chunks: list[str] = []
	log_lock = threading.Lock()
	start = time.monotonic()
	process: subprocess.Popen[str] | None = None
	timed_out = False

	def reader_thread(stream: Any, prefix: str, chunks: list[str]) -> None:
		try:
			for line in iter(stream.readline, ""):
				chunks.append(line)
				trimmed = line.rstrip("\r\n")
				if trimmed:
					append_log_line(log_path, f"[{prefix}] {trimmed}", log_lock)
		finally:
			stream.close()

	try:
		process = subprocess.Popen(
			command,
			cwd=working_directory,
			stdin=subprocess.PIPE if stdin_text is not None else None,
			stdout=subprocess.PIPE,
			stderr=subprocess.PIPE,
			text=True,
			encoding="utf-8",
			errors="replace",
		)
		assert process.stdout is not None
		assert process.stderr is not None
		stdout_reader = threading.Thread(target=reader_thread, args=(process.stdout, "stdout", stdout_chunks), daemon=True)
		stderr_reader = threading.Thread(target=reader_thread, args=(process.stderr, "stderr", stderr_chunks), daemon=True)
		stdout_reader.start()
		stderr_reader.start()

		if stdin_text is not None:
			assert process.stdin is not None
			process.stdin.write(stdin_text)
			process.stdin.close()

		try:
			process.wait(timeout=timeout_seconds)
		except subprocess.TimeoutExpired:
			timed_out = True
			append_log_line(log_path, f"[system] Process timed out after {timeout_seconds} seconds. Killing process tree...", log_lock)
			kill_process_tree(process)
			process.wait()

		stdout_reader.join(timeout=5)
		stderr_reader.join(timeout=5)
	except Exception as exc:
		with log_path.open("a", encoding="utf-8") as handle:
			handle.write(f"[system] Process launch failed: {exc}\n")
		raise
	finally:
		duration_ms = int((time.monotonic() - start) * 1000)
		if process is not None:
			with log_path.open("a", encoding="utf-8") as handle:
				handle.write(f"[system] Exit code: {process.returncode}\n")
				handle.write(f"[system] Duration ms: {duration_ms}\n")
		else:
			with log_path.open("a", encoding="utf-8") as handle:
				handle.write("[system] Exit code: (process failed to start)\n")
				handle.write(f"[system] Duration ms: {duration_ms}\n")

	if timed_out:
		raise RuntimeError(f"Command timed out after {timeout_seconds} seconds. See {log_path}")

	return ProcessResult(
		exit_code=process.returncode if process is not None else -1,
		stdout="".join(stdout_chunks),
		stderr="".join(stderr_chunks),
		duration_ms=duration_ms,
	)


def invoke_codex_round(
	runtime: DebateRuntime,
	prompt_text: str,
	round_label: str,
	output_directory: Path,
	model: str,
	tool_working_directory: Path,
	tool_access_paths: list[Path],
	timeout_seconds: int,
) -> str:
	prompt_path = output_directory / f"{round_label}-codex-prompt.md"
	message_path = output_directory / f"{round_label}-codex.txt"
	log_path = output_directory / f"{round_label}-codex-cli.log"
	write_text_file(prompt_path, prompt_text)

	arguments = [
		"-a",
		"never",
		"exec",
		"--ephemeral",
		"--color",
		"never",
		"--skip-git-repo-check",
		"-C",
		str(tool_working_directory),
		"-m",
		model,
		"-o",
		str(message_path),
	]
	if runtime.codex_sandbox_mode.strip():
		arguments.extend(["-s", runtime.codex_sandbox_mode])
	for path in tool_access_paths:
		if not same_path(path, tool_working_directory):
			arguments.extend(["--add-dir", str(path)])
	arguments.append("-")

	header_lines = [
		"provider: codex",
		f"model: {model}",
		f"sandbox: {runtime.codex_sandbox_mode_label}",
		f"tool_working_directory: {tool_working_directory}",
		"tool_access_paths:",
		*(f"- {path}" for path in tool_access_paths),
	]
	result = invoke_logged_process(
		build_command(runtime.codex_command_path, arguments),
		tool_working_directory,
		log_path,
		timeout_seconds,
		header_lines,
		stdin_text=prompt_text,
	)
	if result.exit_code != 0:
		raise RuntimeError(f"Codex failed during {round_label}. See {log_path}")
	if not message_path.exists():
		raise RuntimeError(f"Codex completed without writing the final message during {round_label}. See {log_path}")
	return read_text_file(message_path)


def invoke_claude_round(
	runtime: DebateRuntime,
	prompt_text: str,
	round_label: str,
	output_directory: Path,
	model: str,
	tool_working_directory: Path,
	tool_access_paths: list[Path],
	timeout_seconds: int,
) -> str:
	prompt_path = output_directory / f"{round_label}-claude-prompt.md"
	json_path = output_directory / f"{round_label}-claude.json"
	message_path = output_directory / f"{round_label}-claude.txt"
	log_path = output_directory / f"{round_label}-claude-cli.log"
	write_text_file(prompt_path, prompt_text)

	arguments = [
		"-p",
		"--disable-slash-commands",
		"--output-format",
		"json",
		"--no-session-persistence",
		"--permission-mode",
		"dontAsk",
		"--tools",
		",".join(runtime.claude_allowed_tools),
		"--model",
		model,
	]
	if runtime.claude_bare_mode_enabled:
		arguments.append("--bare")
	for path in tool_access_paths:
		arguments.extend(["--add-dir", str(path)])

	header_lines = [
		"provider: claude",
		f"model: {model}",
		f"allowed_tools: {', '.join(runtime.claude_allowed_tools)}",
		f"claude_bare_mode: {runtime.claude_bare_mode_enabled}",
		f"claude_git_bash_path: {runtime.claude_bash_path}",
		f"tool_working_directory: {tool_working_directory}",
		"tool_access_paths:",
		*(f"- {path}" for path in tool_access_paths),
	]
	result = invoke_logged_process(
		build_command(runtime.claude_command_path, arguments),
		tool_working_directory,
		log_path,
		timeout_seconds,
		header_lines,
		stdin_text=prompt_text,
	)
	json_text = result.stdout.strip()
	write_text_file(json_path, json_text)
	if result.exit_code != 0:
		raise RuntimeError(f"Claude failed during {round_label}. See {log_path}")

	try:
		payload = json.loads(json_text)
	except json.JSONDecodeError as exc:
		raise RuntimeError(f"Claude returned invalid JSON during {round_label}. See {log_path}") from exc

	if payload.get("is_error"):
		error_message = str(payload.get("result") or "Claude returned an error payload.")
		raise RuntimeError(f"Claude returned an error during {round_label}: {error_message}. See {log_path}")

	message = str(payload.get("result", ""))
	write_text_file(message_path, message)
	return message.strip()


def get_question_text(args: argparse.Namespace) -> str:
	question_values = [value for value in [args.question, args.question_option] if value]
	if args.question_file and question_values:
		raise RuntimeError("Use either an inline question or --question-file, not both.")
	if len(question_values) > 1:
		raise RuntimeError("Specify the inline question only once.")
	if args.question_file:
		question_text = read_text_file(resolve_absolute_path(args.question_file))
	elif question_values:
		question_text = question_values[0].strip()
	else:
		raise RuntimeError("Question cannot be empty.")
	if not question_text.strip():
		raise RuntimeError("Question cannot be empty.")
	return question_text.strip()


def run(args: argparse.Namespace) -> int:
	if not args.claude_tools:
		raise RuntimeError("ClaudeTools cannot be empty.")

	ensure_command_exists("codex")
	ensure_command_exists("claude")

	codex_sandbox_mode = args.codex_sandbox_mode.strip()
	codex_sandbox_mode_label = codex_sandbox_mode if codex_sandbox_mode else "(config/default)"
	runtime = DebateRuntime(
		codex_command_path=get_resolved_command_path("codex"),
		claude_command_path=get_resolved_command_path("claude"),
		codex_sandbox_mode=codex_sandbox_mode,
		codex_sandbox_mode_label=codex_sandbox_mode_label,
		claude_allowed_tools=list(args.claude_tools),
		claude_bare_mode_enabled=bool(args.claude_bare_mode),
		claude_bash_path=initialize_claude_environment(args.claude_git_bash_path),
	)

	workspace_root = resolve_absolute_path(args.working_directory)
	output_root_path = resolve_absolute_path(args.output_root, allow_missing=True)
	context_paths = [resolve_absolute_path(path) for path in args.context_path]

	script_root = Path(__file__).resolve().parent
	repo_root = script_root.parent
	round_template_path = repo_root / "prompts" / "ai_debate_round_prompt.md"
	final_template_path = repo_root / "prompts" / "ai_debate_final_prompt.md"
	round_template = read_text_file(round_template_path)
	final_template = read_text_file(final_template_path)

	question_text = get_question_text(args)
	run_directory = output_root_path / datetime.now().strftime("%Y%m%d-%H%M%S")
	run_directory.mkdir(parents=True, exist_ok=True)

	tool_scope_paths = get_tool_scope_paths(workspace_root, context_paths, args.tool_scope)
	tool_access_paths = get_tool_access_paths(workspace_root, tool_scope_paths)
	tool_working_directory = get_tool_working_directory(workspace_root, tool_scope_paths, run_directory)
	claude_auth_status = get_claude_auth_status(runtime.claude_command_path, workspace_root)

	metadata = {
		"question": question_text,
		"rounds": args.rounds,
		"final_synthesizer": args.final_synthesizer,
		"working_directory": str(workspace_root),
		"context_paths": [str(path) for path in context_paths],
		"codex_model": args.codex_model,
		"codex_sandbox_mode": runtime.codex_sandbox_mode_label,
		"claude_model": args.claude_model,
		"codex_timeout_seconds": args.codex_timeout_seconds,
		"claude_timeout_seconds": args.claude_timeout_seconds,
		"tool_scope": args.tool_scope,
		"tool_scope_paths": [str(path) for path in tool_scope_paths],
		"tool_access_paths": [str(path) for path in tool_access_paths],
		"tool_working_directory": str(tool_working_directory),
		"claude_tools": runtime.claude_allowed_tools,
		"claude_bare_mode": runtime.claude_bare_mode_enabled,
		"claude_git_bash_path": str(runtime.claude_bash_path),
		"claude_logged_in": bool(claude_auth_status.get("loggedIn")),
		"claude_auth_method": str(claude_auth_status.get("authMethod", "")),
		"started_at": datetime.now().astimezone().isoformat(),
	}
	write_json_file(run_directory / "run.json", metadata)
	write_text_file(run_directory / "question.md", question_text)

	context_summary = ", ".join(str(path) for path in context_paths) if context_paths else "(none)"
	codex_previous = ""
	claude_previous = ""
	round_summaries: list[str] = []

	for round_index in range(1, args.rounds + 1):
		round_label = f"round-{round_index:02d}"
		print(f"[{round_index}/{args.rounds}] Codex debating...")
		codex_prompt = expand_template(
			round_template,
			{
				"SELF_NAME": "Codex",
				"OPPONENT_NAME": "Claude",
				"WORKDIR": workspace_root,
				"ROUND_INDEX": round_index,
				"ROUND_COUNT": args.rounds,
				"CONTEXT_PATHS": context_summary,
				"QUESTION": question_text,
				"SELF_POSITION": get_section_text(codex_previous, "No prior position yet."),
				"OPPONENT_POSITION": get_section_text(claude_previous, "No counterpart position yet."),
			},
		)
		codex_response = invoke_codex_round(
			runtime,
			codex_prompt,
			round_label,
			run_directory,
			args.codex_model,
			tool_working_directory,
			tool_access_paths,
			args.codex_timeout_seconds,
		)
		codex_previous = codex_response

		print(f"[{round_index}/{args.rounds}] Claude debating...")
		claude_prompt = expand_template(
			round_template,
			{
				"SELF_NAME": "Claude",
				"OPPONENT_NAME": "Codex",
				"WORKDIR": workspace_root,
				"ROUND_INDEX": round_index,
				"ROUND_COUNT": args.rounds,
				"CONTEXT_PATHS": context_summary,
				"QUESTION": question_text,
				"SELF_POSITION": get_section_text(claude_previous, "No prior position yet."),
				"OPPONENT_POSITION": codex_response,
			},
		)
		claude_response = invoke_claude_round(
			runtime,
			claude_prompt,
			round_label,
			run_directory,
			args.claude_model,
			tool_working_directory,
			tool_access_paths,
			args.claude_timeout_seconds,
		)
		claude_previous = claude_response

		round_summaries.append(
			f"""
## Round {round_index}

### Codex
{codex_response}

### Claude
{claude_response}
""".strip()
		)

	transcript = "\n\n".join(round_summaries)
	write_text_file(run_directory / "transcript.md", transcript)

	final_prompt = expand_template(
		final_template,
		{
			"SYNTHESIZER_NAME": "Codex" if args.final_synthesizer == "codex" else "Claude",
			"WORKDIR": workspace_root,
			"QUESTION": question_text,
			"TRANSCRIPT": transcript,
		},
	)
	print(f"[final] {args.final_synthesizer.title()} synthesizing...")

	if args.final_synthesizer == "codex":
		final_text = invoke_codex_round(
			runtime,
			final_prompt,
			"final",
			run_directory,
			args.codex_model,
			tool_working_directory,
			tool_access_paths,
			args.codex_timeout_seconds,
		)
	else:
		final_text = invoke_claude_round(
			runtime,
			final_prompt,
			"final",
			run_directory,
			args.claude_model,
			tool_working_directory,
			tool_access_paths,
			args.claude_timeout_seconds,
		)

	summary = f"""
# AI Debate Summary

## Question
{question_text}

## Final Answer
{final_text}

## Transcript
{transcript}
""".strip()
	write_text_file(run_directory / "summary.md", summary)
	print(f"Debate complete. Output: {run_directory}")
	return 0


def main() -> int:
	args = build_parser().parse_args()
	try:
		return run(args)
	except Exception as exc:
		print(str(exc), file=sys.stderr)
		return 1


if __name__ == "__main__":
	raise SystemExit(main())
