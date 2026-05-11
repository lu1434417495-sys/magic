extends RefCounted

var failures: Array[String] = []


func bind_failures(target_failures: Array[String]) -> void:
	failures = target_failures


func assert_true(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)


func assert_false(condition: bool, message: String) -> void:
	if condition:
		failures.append(message)


func assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func assert_ne(actual: Variant, unexpected: Variant, message: String) -> void:
	if actual == unexpected:
		failures.append("%s | unexpected=%s" % [message, str(unexpected)])


func fail(message: String) -> void:
	failures.append(message)


func has_failures() -> bool:
	return not failures.is_empty()


func failure_count() -> int:
	return failures.size()


func append_error(message: String) -> void:
	fail(message)


func append_errors(messages: Array) -> void:
	for message in messages:
		failures.append(String(message))


func finish(scene_tree: SceneTree, label: String) -> void:
	if failures.is_empty():
		print("%s: PASS" % label)
		scene_tree.quit(0)
		return

	for failure in failures:
		push_error(failure)
	print("%s: FAIL (%d)" % [label, failures.size()])
	scene_tree.quit(1)
