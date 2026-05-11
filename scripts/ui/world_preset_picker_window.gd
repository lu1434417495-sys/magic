## 文件说明：世界预设选择器窗口；继承 SelectableListWindow，仅覆盖条目展示与签名信号。
## 审查重点：保留对外的 preset_confirmed/cancelled 信号与 show_window 签名；条目字段命名遵循 preset_id/display_name/size_label。
## 备注：场景节点名需保持基类约定的 %List、%DetailLabel、%EmptyLabel、%ConfirmButton、%CancelButton、%FooterCancelButton。

class_name WorldPresetPickerWindow
extends SelectableListWindow

## 信号说明：当用户确认预设后发出的信号，供外层继续推进流程。
signal preset_confirmed(preset_id: StringName)
## 信号说明：当用户取消当前流程时发出的信号，供外层恢复默认界面状态或焦点。
signal cancelled


func _format_item_label(item: Dictionary) -> String:
	return "%s  |  %s" % [
		String(item.get("display_name", "未命名世界")),
		String(item.get("size_label", "")),
	]


func _format_detail_text(item: Dictionary) -> String:
	var preset_name := String(item.get("display_name", "世界"))
	var size_label := String(item.get("size_label", "未知尺寸"))
	return "\n".join(PackedStringArray([
		"世界类型：%s" % preset_name,
		"地图尺寸：%s" % size_label,
		"会创建一个全新的唯一存档。",
	]))


func _format_empty_detail() -> String:
	return "当前没有可用的世界预设。"


func _get_item_id(item: Dictionary) -> StringName:
	return StringName(String(item.get("preset_id", "")))


func _emit_confirmed_for_id(item_id: StringName) -> void:
	preset_confirmed.emit(item_id)


func _emit_cancelled() -> void:
	cancelled.emit()
