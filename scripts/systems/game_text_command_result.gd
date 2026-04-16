# Automation-oriented render payload for text command execution results.
class_name GameTextCommandResult
extends RefCounted


var command_text := ""
var ok := true
var skipped := false
var message := ""
var snapshot: Dictionary = {}
var human_log := ""
var snapshot_text := ""
var assertions: Array[Dictionary] = []


func render() -> String:
	var lines: PackedStringArray = []
	if skipped:
		lines.append("SKIP %s" % command_text)
	else:
		lines.append("%s %s" % ["OK" if ok else "ERR", command_text])
	if not message.is_empty():
		lines.append(message)
	if not assertions.is_empty():
		for assertion_variant in assertions:
			if assertion_variant is not Dictionary:
				continue
			var assertion: Dictionary = assertion_variant
			lines.append("ASSERT %s | actual=%s | expected=%s" % [
				String(assertion.get("summary", "")),
				String(assertion.get("actual", "")),
				String(assertion.get("expected", "")),
			])
	if not snapshot_text.is_empty():
		lines.append("")
		lines.append(snapshot_text)
	return "\n".join(lines)
