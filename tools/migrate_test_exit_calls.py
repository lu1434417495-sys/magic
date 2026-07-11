#!/usr/bin/env python3
"""Migrate C# SceneTree test exits to the shared lifecycle shutdown adapter.

The migration is intentionally conservative. It first analyzes every C# file under
the requested root, and it writes neither sources nor the manifest if any Quit call
has an unknown shape.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence


class MigrationError(RuntimeError):
    """Raised when a source file contains an exit shape we cannot prove safe."""


@dataclass(frozen=True)
class _Call:
    start: int
    open_paren: int
    close_paren: int
    argument: str


@dataclass(frozen=True)
class _Edit:
    start: int
    end: int
    replacement: str


@dataclass(frozen=True)
class _Migration:
    path: Path
    source: str
    migrated: str


@dataclass(frozen=True)
class _BlockMethod:
    return_type_start: int
    return_type_end: int
    open_brace: int
    close_brace: int


_IDENTIFIER_RE = re.compile(r"[A-Za-z_]\w*\Z")
_DIRECT_SCENE_TREE_RE = re.compile(r"(?P<prefix>:\s*)SceneTree\b")
_LIFECYCLE_SCENE_TREE_RE = re.compile(r":\s*LifecycleTestSceneTree\b")
_LIFECYCLE_BASE_DECLARATION_RE = re.compile(
    r"\babstract\s+partial\s+class\s+LifecycleTestSceneTree\s*:\s*SceneTree\b"
)


def _code_view(source: str) -> str:
    """Return source with comments and literals blanked while preserving offsets."""

    chars = list(source)
    code = [True] * len(source)
    index = 0
    length = len(source)

    def blank(start: int, end: int) -> None:
        for offset in range(start, end):
            if source[offset] not in "\r\n":
                code[offset] = False

    while index < length:
        if source.startswith("//", index):
            end = source.find("\n", index + 2)
            if end < 0:
                end = length
            blank(index, end)
            index = end
            continue

        if source.startswith("/*", index):
            end = source.find("*/", index + 2)
            if end < 0:
                end = length
            else:
                end += 2
            blank(index, end)
            index = end
            continue

        raw_match = re.match(r'\$?"{3,}', source[index:])
        if raw_match:
            delimiter = '"' * (len(raw_match.group(0)) - (1 if raw_match.group(0).startswith("$") else 0))
            end = source.find(delimiter, index + len(raw_match.group(0)))
            end = length if end < 0 else end + len(delimiter)
            blank(index, end)
            index = end
            continue

        string_prefix = None
        for prefix in ("$@\"", "@$\"", "@\"", "$\"", "\""):
            if source.startswith(prefix, index):
                string_prefix = prefix
                break
        if string_prefix is not None:
            verbatim = "@" in string_prefix
            cursor = index + len(string_prefix)
            while cursor < length:
                if verbatim and source.startswith('""', cursor):
                    cursor += 2
                    continue
                if source[cursor] == '"':
                    cursor += 1
                    break
                if not verbatim and source[cursor] == "\\":
                    cursor += 2
                    continue
                cursor += 1
            blank(index, min(cursor, length))
            index = min(cursor, length)
            continue

        if source[index] == "'":
            cursor = index + 1
            while cursor < length:
                if source[cursor] == "\\":
                    cursor += 2
                    continue
                if source[cursor] == "'":
                    cursor += 1
                    break
                cursor += 1
            blank(index, min(cursor, length))
            index = min(cursor, length)
            continue

        index += 1

    return "".join(
        character if code[offset] else (character if character in "\r\n" else " ")
        for offset, character in enumerate(chars)
    )


def _matching_paren(code: str, open_paren: int) -> int:
    depth = 0
    for index in range(open_paren, len(code)):
        character = code[index]
        if character == "(":
            depth += 1
        elif character == ")":
            depth -= 1
            if depth == 0:
                return index
    raise MigrationError("unclosed Quit call")


def _matching_brace(code: str, open_brace: int) -> int:
    depth = 0
    for index in range(open_brace, len(code)):
        character = code[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return index
    raise MigrationError("unclosed method body")


def _quit_calls(source: str, code: str) -> list[_Call]:
    calls: list[_Call] = []
    for match in re.finditer(r"\bQuit\s*\(", code):
        prefix = code[: match.start()].rstrip()
        if prefix.endswith("."):
            line = source.count("\n", 0, match.start()) + 1
            raise MigrationError(f"line {line}: qualified Quit call is not supported")

        open_paren = code.find("(", match.start(), match.end())
        close_paren = _matching_paren(code, open_paren)
        cursor = close_paren + 1
        while cursor < len(code) and code[cursor].isspace():
            cursor += 1
        if cursor >= len(code) or code[cursor] != ";":
            line = source.count("\n", 0, match.start()) + 1
            raise MigrationError(f"line {line}: Quit call must be a statement")

        calls.append(
            _Call(
                start=match.start(),
                open_paren=open_paren,
                close_paren=close_paren,
                argument=source[open_paren + 1 : close_paren].strip(),
            )
        )
    return calls


def _exact_finish_arguments(expression: str) -> str | None:
    stripped = expression.strip()
    match = re.match(r"_test\s*\.\s*Finish\s*\(", stripped)
    if match is None:
        return None

    code = _code_view(stripped)
    open_paren = code.find("(", match.start(), match.end())
    close_paren = _matching_paren(code, open_paren)
    if code[close_paren + 1 :].strip():
        return None
    return stripped[open_paren + 1 : close_paren]


def _split_arguments(arguments: str) -> list[str]:
    code = _code_view(arguments)
    parts: list[str] = []
    start = 0
    paren = bracket = brace = 0
    for index, character in enumerate(code):
        if character == "(":
            paren += 1
        elif character == ")":
            paren -= 1
        elif character == "[":
            bracket += 1
        elif character == "]":
            bracket -= 1
        elif character == "{":
            brace += 1
        elif character == "}":
            brace -= 1
        elif character == "," and paren == bracket == brace == 0:
            parts.append(arguments[start:index].strip())
            start = index + 1
    parts.append(arguments[start:].strip())
    return parts


def _finish_labels(source: str, code: str) -> list[str]:
    labels: set[str] = set()
    for match in re.finditer(r"_test\s*\.\s*Finish\s*\(", code):
        open_paren = code.find("(", match.start(), match.end())
        close_paren = _matching_paren(code, open_paren)
        arguments = _split_arguments(source[open_paren + 1 : close_paren])
        if arguments and _is_string_literal(arguments[0]):
            labels.add(arguments[0])
    return sorted(labels)


def _is_string_literal(expression: str) -> bool:
    stripped = expression.strip()
    return stripped.startswith(('"', '@"', '$"', '$@"', '@$"')) and stripped.endswith('"')


def _stored_finish_edits(
    source: str,
    code: str,
    call: _Call,
    identifier: str,
    labels: Sequence[str],
) -> tuple[list[_Edit], str] | None:
    before_call = code[: call.start]
    assignment_pattern = re.compile(
        rf"\b{re.escape(identifier)}\s*=\s*_test\s*\.\s*Finish\s*\("
    )
    if assignment_pattern.search(before_call) is None:
        return None

    declaration_pattern = re.compile(
        rf"\b(?P<type>int|var|TestResult)\s+{re.escape(identifier)}\b"
        rf"(?P<initializer>\s*=\s*[^;]+)?\s*;"
    )
    declarations = list(declaration_pattern.finditer(before_call))
    if len(declarations) != 1:
        return None

    declaration = declarations[0]
    declared_type = declaration.group("type")
    initializer = declaration.group("initializer")
    edits: list[_Edit] = []
    replacement_argument = identifier

    assignments = list(
        re.finditer(
            rf"(?<![=])\b{re.escape(identifier)}\s*=(?!=)\s*(?P<value>[^;]+);",
            before_call,
        )
    )
    if not assignments:
        return None
    for assignment in assignments:
        value = assignment.group("value").strip()
        is_declaration_initializer = (
            declaration.start() <= assignment.start() < declaration.end()
        )
        if _exact_finish_arguments(value) is not None:
            continue
        if is_declaration_initializer and value == "1":
            continue
        return None

    if initializer is None:
        if declared_type == "int":
            edits.append(_Edit(declaration.start("type"), declaration.end("type"), "TestResult"))
    else:
        initializer_expression = initializer.split("=", 1)[1].strip()
        if _exact_finish_arguments(initializer_expression) is not None:
            if declared_type == "int":
                edits.append(_Edit(declaration.start("type"), declaration.end("type"), "TestResult"))
        elif initializer_expression == "1" and len(labels) == 1:
            replacement = f"TestResult {identifier} = null;"
            edits.append(_Edit(declaration.start(), declaration.end(), replacement))
            replacement_argument = f"{identifier} ?? _test.Finish({labels[0]}, 1)"
        else:
            return None

    return edits, replacement_argument


def _private_instance_int_block_method(code: str, name: str) -> _BlockMethod | None:
    pattern = re.compile(
        rf"\bprivate\s+(?P<return_type>int)\s+{re.escape(name)}\s*\(\s*\)\s*\{{"
    )
    matches = list(pattern.finditer(code))
    if len(matches) != 1:
        return None

    match = matches[0]
    open_brace = code.find("{", match.start(), match.end())
    return _BlockMethod(
        return_type_start=match.start("return_type"),
        return_type_end=match.end("return_type"),
        open_brace=open_brace,
        close_brace=_matching_brace(code, open_brace),
    )


def _method_return_expressions(
    source: str,
    code: str,
    method: _BlockMethod,
) -> list[str] | None:
    expressions: list[str] = []
    body_start = method.open_brace + 1
    body_code = code[body_start : method.close_brace]
    for match in re.finditer(r"\breturn\b", body_code):
        expression_start = body_start + match.end()
        semicolon = code.find(";", expression_start, method.close_brace)
        if semicolon < 0:
            return None
        expressions.append(source[expression_start:semicolon].strip())
    return expressions


def _exact_parameterless_call(expression: str, name: str) -> bool:
    return re.fullmatch(
        rf"{re.escape(name)}\s*\(\s*\)",
        expression.strip(),
    ) is not None


def _finish_helper_return_type_edit(
    source: str,
    code: str,
) -> _Edit | None:
    pattern = re.compile(
        r"\bprivate\s+(?P<return_type>int)\s+Finish\s*\(\s*\)\s*=>"
    )
    matches = list(pattern.finditer(code))
    if len(matches) != 1:
        return None

    match = matches[0]
    semicolon = code.find(";", match.end())
    if semicolon < 0:
        return None
    expression = source[match.end() : semicolon].strip()
    finish_arguments = _exact_finish_arguments(expression)
    if finish_arguments is None:
        return None

    arguments = _split_arguments(finish_arguments)
    if len(arguments) != 1 or not _is_string_literal(arguments[0]):
        return None
    if _finish_labels(source, code) != [arguments[0]]:
        return None

    return _Edit(
        match.start("return_type"),
        match.end("return_type"),
        "TestResult",
    )


def _run_forwarding_edits(
    source: str,
    code: str,
    call: _Call,
    identifier: str,
) -> tuple[list[_Edit], str] | None:
    if identifier != "exitCode":
        return None

    declaration_pattern = re.compile(
        rf"\b(?P<type>int)\s+{re.escape(identifier)}\s*=\s*Run\s*\(\s*\)\s*;"
    )
    declarations = list(declaration_pattern.finditer(code[: call.start]))
    if len(declarations) != 1:
        return None

    declaration = declarations[0]
    if code[declaration.end() : call.start].strip():
        return None

    run_method = _private_instance_int_block_method(code, "Run")
    if run_method is None:
        return None
    return_expressions = _method_return_expressions(source, code, run_method)
    if not return_expressions:
        return None

    edits = [
        _Edit(declaration.start("type"), declaration.end("type"), "TestResult"),
        _Edit(run_method.return_type_start, run_method.return_type_end, "TestResult"),
    ]
    if all(
        _exact_finish_arguments(expression) is not None
        for expression in return_expressions
    ):
        return edits, identifier

    if all(
        _exact_parameterless_call(expression, "Finish")
        for expression in return_expressions
    ):
        helper_edit = _finish_helper_return_type_edit(source, code)
        if helper_edit is not None:
            edits.append(helper_edit)
            return edits, identifier

    return None


def _apply_edits(source: str, edits: Iterable[_Edit]) -> str:
    ordered = sorted(edits, key=lambda edit: (edit.start, edit.end))
    for previous, current in zip(ordered, ordered[1:]):
        if current.start < previous.end:
            if current == previous:
                continue
            raise MigrationError("overlapping migration edits")

    deduplicated: list[_Edit] = []
    for edit in ordered:
        if not deduplicated or edit != deduplicated[-1]:
            deduplicated.append(edit)

    migrated = source
    for edit in reversed(deduplicated):
        migrated = migrated[: edit.start] + edit.replacement + migrated[edit.end :]
    return migrated


def transform_source(source: str, display_path: str = "<memory>") -> str:
    """Return migrated source or raise MigrationError for an unknown exit shape."""

    code = _code_view(source)
    calls = _quit_calls(source, code)
    direct_bases = list(_DIRECT_SCENE_TREE_RE.finditer(code))
    lifecycle_base = _LIFECYCLE_SCENE_TREE_RE.search(code) is not None

    if not calls:
        if direct_bases:
            if (
                len(direct_bases) == 1
                and _LIFECYCLE_BASE_DECLARATION_RE.search(code) is not None
            ):
                return source
            raise MigrationError(
                f"{display_path}: direct SceneTree runner has no Quit call to migrate"
            )
        return source

    if len(direct_bases) > 1:
        raise MigrationError(f"{display_path}: multiple direct SceneTree bases are ambiguous")
    if not direct_bases and not lifecycle_base:
        raise MigrationError(f"{display_path}: Quit call is not owned by a SceneTree runner")

    labels = _finish_labels(source, code)
    edits: list[_Edit] = []
    for call in calls:
        finish_arguments = _exact_finish_arguments(call.argument)
        if finish_arguments is not None:
            replacement_argument = call.argument
        elif call.argument == "1":
            if len(labels) != 1:
                line = source.count("\n", 0, call.start) + 1
                raise MigrationError(
                    f"{display_path}:{line}: Quit(1) needs exactly one TestHarness label"
                )
            replacement_argument = f"_test.Finish({labels[0]}, 1)"
        elif _IDENTIFIER_RE.fullmatch(call.argument):
            stored = _stored_finish_edits(
                source,
                code,
                call,
                call.argument,
                labels,
            )
            if stored is None:
                stored = _run_forwarding_edits(
                    source,
                    code,
                    call,
                    call.argument,
                )
            if stored is None:
                line = source.count("\n", 0, call.start) + 1
                raise MigrationError(
                    f"{display_path}:{line}: stored Quit value is not a recognized Finish result"
                )
            stored_edits, replacement_argument = stored
            edits.extend(stored_edits)
        else:
            line = source.count("\n", 0, call.start) + 1
            raise MigrationError(
                f"{display_path}:{line}: unknown Quit argument shape: {call.argument}"
            )

        edits.append(
            _Edit(
                call.start,
                call.close_paren + 1,
                f"RequestTestExit({replacement_argument})",
            )
        )

    if direct_bases:
        base = direct_bases[0]
        scene_tree_start = base.end() - len("SceneTree")
        edits.append(_Edit(scene_tree_start, base.end(), "LifecycleTestSceneTree"))

    return _apply_edits(source, edits)


def _read_source(path: Path) -> str:
    with path.open("r", encoding="utf-8", newline="") as handle:
        return handle.read()


def _write_source(path: Path, source: str) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        handle.write(source)


def _manifest_entry(root_argument: Path, root: Path, path: Path) -> str:
    relative = path.relative_to(root)
    if root_argument.is_absolute():
        return relative.as_posix()
    return (root_argument / relative).as_posix()


def collect_migrations(root_argument: Path) -> list[_Migration]:
    root = root_argument.resolve()
    if not root.is_dir():
        raise MigrationError(f"migration root does not exist: {root_argument}")

    migrations: list[_Migration] = []
    failures: list[str] = []
    paths = sorted(root.rglob("*.cs"), key=lambda path: path.relative_to(root).as_posix())
    for path in paths:
        source = _read_source(path)
        display_path = _manifest_entry(root_argument, root, path)
        try:
            migrated = transform_source(source, display_path)
        except MigrationError as error:
            failures.append(str(error))
            continue
        if migrated != source:
            migrations.append(_Migration(path, source, migrated))

    if failures:
        details = "\n".join(f"- {failure}" for failure in failures)
        raise MigrationError(f"unknown test-exit shapes:\n{details}")
    return migrations


def _write_manifest(
    manifest: Path,
    root_argument: Path,
    root: Path,
    migrations: Sequence[_Migration],
) -> None:
    entries = sorted(
        _manifest_entry(root_argument, root, migration.path) for migration in migrations
    )
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(
        "\n".join(entries) + ("\n" if entries else ""),
        encoding="utf-8",
        newline="\n",
    )


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true", help="validate and write the manifest")
    mode.add_argument("--apply", action="store_true", help="validate, write the manifest, and migrate")
    parser.add_argument("--root", type=Path, required=True, help="root containing C# tests")
    parser.add_argument("--manifest", type=Path, required=True, help="output manifest path")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        migrations = collect_migrations(arguments.root)
        root = arguments.root.resolve()
        _write_manifest(arguments.manifest, arguments.root, root, migrations)
        if arguments.apply:
            for migration in migrations:
                _write_source(migration.path, migration.migrated)
    except (MigrationError, OSError) as error:
        print(error, file=sys.stderr)
        return 2

    verb = "migrated" if arguments.apply else "validated"
    print(f"{verb} {len(migrations)} C# test runner(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
