## 文件说明：该脚本属于世界地图据点名称池相关的配置资源脚本，集中维护主世界默认据点实例展示名。
## 审查重点：重点核对名称数据是否全部来自配置资源，并确认运行时读取后会去重和忽略空行。
## 备注：后续如果扩充默认名称池，优先编辑对应 `.tres` 资源，不要把名称写回代码。

class_name WorldMapSettlementNamePool
extends Resource

## 字段说明：以字符串数组方式承载据点展示名，便于运行时直接读取并维持稳定序列。
@export var settlement_display_names: Array[String] = []


func build_unique_display_names() -> Array[String]:
	var unique_names: Array[String] = []
	var seen_names: Dictionary = {}
	for raw_name in settlement_display_names:
		var normalized_name := String(raw_name).strip_edges()
		if normalized_name.is_empty() or seen_names.has(normalized_name):
			continue
		seen_names[normalized_name] = true
		unique_names.append(normalized_name)
	return unique_names
