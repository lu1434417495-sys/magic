## Formats AiTraceRecorder.get_func_stats() output as cProfile-style top-N tables
## and CSV dumps.
class_name AiHotspotsFormatter
extends RefCounted


static func format_top_n(func_stats: Dictionary, sort_by: String = "self_usec", top_n: int = 20, name_filter: String = "") -> String:
	var entries: Array = []
	for name_variant in func_stats.keys():
		var name := String(name_variant)
		if not name_filter.is_empty() and name.find(name_filter) == -1:
			continue
		var s: Dictionary = func_stats[name_variant]
		entries.append({
			"name": name,
			"ncalls": int(s.get("ncalls", 0)),
			"self_usec": int(s.get("self_usec", 0)),
			"total_usec": int(s.get("total_usec", 0)),
			"max_usec": int(s.get("max_usec", 0)),
		})
	entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return int(a.get(sort_by, 0)) > int(b.get(sort_by, 0))
	)

	var lines: Array[String] = []
	lines.append("ncalls       tottime(ms)   percall(us)   cumtime(ms)   percall(us)   maxcall(us)   function")
	var displayed := 0
	for entry in entries:
		if displayed >= top_n:
			break
		displayed += 1
		var ncalls: int = entry["ncalls"]
		var self_usec: int = entry["self_usec"]
		var total_usec: int = entry["total_usec"]
		var max_usec: int = entry["max_usec"]
		var self_percall_us := 0.0
		var total_percall_us := 0.0
		if ncalls > 0:
			self_percall_us = float(self_usec) / float(ncalls)
			total_percall_us = float(total_usec) / float(ncalls)
		lines.append("%8d   %11.3f   %11.1f   %11.3f   %11.1f   %11d   %s" % [
			ncalls,
			self_usec / 1000.0,
			self_percall_us,
			total_usec / 1000.0,
			total_percall_us,
			max_usec,
			entry["name"],
		])
	if entries.size() > displayed:
		lines.append("... (%d more)" % (entries.size() - displayed))
	return "\n".join(lines)


static func format_header(scenario_id: String, ai_turns: int, total_self_usec: int, sort_by: String, godot_version: String, git_commit: String) -> String:
	return "=== AI Profile · %s · godot=%s · commit=%s ===\nai_turns=%d  total_self_usec=%.3f ms  sort=%s\n" % [
		scenario_id,
		godot_version,
		git_commit,
		ai_turns,
		total_self_usec / 1000.0,
		sort_by,
	]


static func write_csv(path: String, func_stats: Dictionary) -> bool:
	var dir_path := path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir_path):
		DirAccess.make_dir_recursive_absolute(dir_path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_line("function,ncalls,self_usec,total_usec,max_usec,self_per_call_usec,total_per_call_usec")
	var names: Array = func_stats.keys()
	names.sort_custom(func(a, b) -> bool:
		var sa: Dictionary = func_stats[a]
		var sb: Dictionary = func_stats[b]
		return int(sa.get("self_usec", 0)) > int(sb.get("self_usec", 0))
	)
	for name_variant in names:
		var s: Dictionary = func_stats[name_variant]
		var ncalls := int(s.get("ncalls", 0))
		var self_usec := int(s.get("self_usec", 0))
		var total_usec := int(s.get("total_usec", 0))
		var max_usec := int(s.get("max_usec", 0))
		var self_pc := 0.0
		var total_pc := 0.0
		if ncalls > 0:
			self_pc = float(self_usec) / float(ncalls)
			total_pc = float(total_usec) / float(ncalls)
		file.store_line("%s,%d,%d,%d,%d,%.2f,%.2f" % [
			String(name_variant), ncalls, self_usec, total_usec, max_usec, self_pc, total_pc,
		])
	file.close()
	return true


static func write_text_report(path: String, header: String, body: String) -> bool:
	var dir_path := path.get_base_dir()
	if not DirAccess.dir_exists_absolute(dir_path):
		DirAccess.make_dir_recursive_absolute(dir_path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return false
	file.store_string(header)
	file.store_string(body)
	file.close()
	return true


static func total_self_usec(func_stats: Dictionary) -> int:
	var sum := 0
	for s_variant in func_stats.values():
		var s: Dictionary = s_variant
		sum += int(s.get("self_usec", 0))
	return sum
