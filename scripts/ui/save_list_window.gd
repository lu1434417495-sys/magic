## 文件说明：存档列表窗口；继承 SelectableListWindow，仅覆盖条目展示与签名信号。
## 审查重点：保留对外的 save_load_requested(String)/closed 信号与 show_window 签名；条目字段命名遵循 save_id/display_name/world_preset_name/world_size_cells/created_at_unix_time/updated_at_unix_time。
## 备注：场景节点名需保持基类约定的 %List、%DetailLabel、%EmptyLabel、%ConfirmButton、%CancelButton、%FooterCancelButton。

class_name SaveListWindow
extends SelectableListWindow

## 信号说明：当界面请求存档加载时发出的信号，具体处理由外层系统或控制器负责。
signal save_load_requested(save_id: String)
## 信号说明：当窗口或面板关闭时发出的信号，供外层恢复输入焦点、刷新数据或清理临时状态。
signal closed


func _format_item_label(item: Dictionary) -> String:
	return "%s  |  %s  |  %s" % [
		String(item.get("display_name", "")),
		String(item.get("world_preset_name", "世界")),
		_format_unix_time(int(item.get("updated_at_unix_time", 0))),
	]


func _format_detail_text(item: Dictionary) -> String:
	var world_size: Variant = item.get("world_size_cells", Vector2i.ZERO)
	var size_label := "%d x %d" % [world_size.x, world_size.y] if world_size is Vector2i else "未知尺寸"
	return "\n".join(PackedStringArray([
		"存档名：%s" % String(item.get("display_name", "")),
		"世界类型：%s" % String(item.get("world_preset_name", "世界")),
		"地图尺寸：%s" % size_label,
		"创建时间：%s" % _format_unix_time(int(item.get("created_at_unix_time", 0))),
		"最近保存：%s" % _format_unix_time(int(item.get("updated_at_unix_time", 0))),
	]))


func _format_empty_detail() -> String:
	return "当前没有可读取的存档。"


func _get_item_id(item: Dictionary) -> StringName:
	return StringName(String(item.get("save_id", "")))


func _emit_confirmed_for_id(item_id: StringName) -> void:
	save_load_requested.emit(String(item_id))


func _emit_cancelled() -> void:
	closed.emit()


func _format_unix_time(unix_time: int) -> String:
	if unix_time <= 0:
		return "未知"
	var datetime := Time.get_datetime_dict_from_unix_time(unix_time)
	return "%04d-%02d-%02d %02d:%02d:%02d" % [
		int(datetime.get("year", 1970)),
		int(datetime.get("month", 1)),
		int(datetime.get("day", 1)),
		int(datetime.get("hour", 0)),
		int(datetime.get("minute", 0)),
		int(datetime.get("second", 0)),
	]
