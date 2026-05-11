## 文件说明：封装持久化文件的原子写入、替换和清理流程。
## 审查重点：重点核对错误码传播、临时文件/备份文件清理顺序以及调用方错误上报是否保持一致。
## 备注：该服务不持有会话状态；会话层通过 error_sink 注入日志上报。

class_name FileIOCoordinator
extends RefCounted


static func write_compressed_variant_atomically(
	virtual_path: String,
	payload: Variant,
	compression_mode: int,
	error_event_prefix: String,
	label: String,
	error_sink: Callable = Callable()
) -> int:
	var temp_path := "%s.tmp" % virtual_path
	var cleanup_temp_error := remove_file_if_exists(temp_path)
	if cleanup_temp_error != OK:
		return cleanup_temp_error

	var file := FileAccess.open_compressed(temp_path, FileAccess.WRITE, compression_mode)
	if file == null:
		var open_error := FileAccess.get_open_error()
		_push_error(error_sink, "%s.open_failed" % error_event_prefix, "Failed to open %s file %s. Error: %s" % [label, temp_path, open_error], {
			"path": temp_path,
			"open_error": open_error,
		})
		return open_error

	file.store_var(payload, false)
	var write_error := file.get_error()
	file.close()
	if write_error != OK:
		remove_file_if_exists(temp_path)
		_push_error(error_sink, "%s.write_failed" % error_event_prefix, "Failed to write %s file %s. Error: %s" % [label, temp_path, write_error], {
			"path": temp_path,
			"write_error": write_error,
		})
		return write_error

	return replace_file_atomically(temp_path, virtual_path, error_event_prefix, label, error_sink)


static func replace_file_atomically(
	source_path: String,
	target_path: String,
	error_event_prefix: String,
	label: String,
	error_sink: Callable = Callable()
) -> int:
	var backup_path := "%s.bak" % target_path
	var cleanup_backup_error := remove_file_if_exists(backup_path)
	if cleanup_backup_error != OK:
		remove_file_if_exists(source_path)
		return cleanup_backup_error

	var had_existing_target := FileAccess.file_exists(target_path)
	if had_existing_target:
		var backup_error := rename_file(target_path, backup_path)
		if backup_error != OK:
			remove_file_if_exists(source_path)
			_push_error(error_sink, "%s.backup_failed" % error_event_prefix, "Failed to prepare existing %s file %s for replacement. Error: %s" % [label, target_path, backup_error], {
				"target_path": target_path,
				"backup_path": backup_path,
				"backup_error": backup_error,
			})
			return backup_error

	var replace_error := rename_file(source_path, target_path)
	if replace_error != OK:
		remove_file_if_exists(source_path)
		if had_existing_target:
			rename_file(backup_path, target_path)
		_push_error(error_sink, "%s.replace_failed" % error_event_prefix, "Failed to replace %s file %s. Error: %s" % [label, target_path, replace_error], {
			"source_path": source_path,
			"target_path": target_path,
			"replace_error": replace_error,
		})
		return replace_error

	if had_existing_target:
		var remove_backup_error := remove_file_if_exists(backup_path)
		if remove_backup_error != OK:
			push_warning("FileIOCoordinator: replaced %s file but failed to remove backup %s. Error: %s" % [label, backup_path, remove_backup_error])
	return OK


static func recover_replace_target(
	target_path: String,
	compression_mode: int,
	error_event_prefix: String,
	label: String,
	error_sink: Callable = Callable()
) -> int:
	var temp_path := "%s.tmp" % target_path
	remove_file_if_exists(temp_path)

	var backup_path := "%s.bak" % target_path
	if FileAccess.file_exists(target_path):
		if FileAccess.file_exists(backup_path):
			var cleanup_backup_error := remove_file_if_exists(backup_path)
			if cleanup_backup_error != OK:
				push_warning("FileIOCoordinator: found valid %s target but failed to remove stale backup %s. Error: %s" % [label, backup_path, cleanup_backup_error])
		return OK

	if not FileAccess.file_exists(backup_path):
		return ERR_DOES_NOT_EXIST

	if not is_compressed_variant_file_readable(backup_path, compression_mode):
		_push_error(error_sink, "%s.backup_invalid" % error_event_prefix, "Failed to recover %s file %s because backup %s is invalid." % [label, target_path, backup_path], {
			"target_path": target_path,
			"backup_path": backup_path,
		})
		return ERR_INVALID_DATA

	var restore_error := rename_file(backup_path, target_path)
	if restore_error != OK:
		_push_error(error_sink, "%s.backup_restore_failed" % error_event_prefix, "Failed to restore %s file %s from backup %s. Error: %s" % [label, target_path, backup_path, restore_error], {
			"target_path": target_path,
			"backup_path": backup_path,
			"restore_error": restore_error,
		})
		return restore_error
	return OK


static func is_compressed_variant_file_readable(
	virtual_path: String,
	compression_mode: int
) -> bool:
	if not FileAccess.file_exists(virtual_path):
		return false
	var file := FileAccess.open_compressed(virtual_path, FileAccess.READ, compression_mode)
	if file == null:
		return false
	if int(file.get_length()) < 8:
		file.close()
		return false
	var _payload = file.get_var(false)
	var read_error := file.get_error()
	file.close()
	return read_error == OK


static func rename_file(from_virtual_path: String, to_virtual_path: String) -> int:
	return DirAccess.rename_absolute(
		ProjectSettings.globalize_path(from_virtual_path),
		ProjectSettings.globalize_path(to_virtual_path)
	)


static func remove_file_if_exists(virtual_path: String) -> int:
	if not FileAccess.file_exists(virtual_path):
		return OK
	return DirAccess.remove_absolute(ProjectSettings.globalize_path(virtual_path))


static func remove_directory_recursive(virtual_path: String, error_sink: Callable = Callable()) -> int:
	var absolute_path := ProjectSettings.globalize_path(virtual_path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		return OK

	var dir := DirAccess.open(virtual_path)
	if dir == null:
		var open_error := DirAccess.get_open_error()
		_push_error(error_sink, "session.cleanup.open_directory_failed", "Failed to open directory %s for cleanup. Error: %s" % [virtual_path, open_error], {
			"virtual_path": virtual_path,
			"open_error": open_error,
		})
		return open_error

	var list_error := dir.list_dir_begin()
	if list_error != OK:
		_push_error(error_sink, "session.cleanup.list_directory_failed", "Failed to list directory %s for cleanup. Error: %s" % [virtual_path, list_error], {
			"virtual_path": virtual_path,
			"list_error": list_error,
		})
		return list_error
	while true:
		var name := dir.get_next()
		if name.is_empty():
			break
		if name == "." or name == "..":
			continue

		var child_virtual_path := "%s/%s" % [virtual_path, name]
		if dir.current_is_dir():
			var nested_error := remove_directory_recursive(child_virtual_path, error_sink)
			if nested_error != OK:
				dir.list_dir_end()
				return nested_error
			continue

		var remove_file_error := DirAccess.remove_absolute(ProjectSettings.globalize_path(child_virtual_path))
		if remove_file_error != OK:
			dir.list_dir_end()
			return remove_file_error

	dir.list_dir_end()
	return DirAccess.remove_absolute(absolute_path)


static func _push_error(error_sink: Callable, event_id: String, message: String, context: Dictionary = {}) -> void:
	if error_sink.is_valid():
		error_sink.call(event_id, message, context)
