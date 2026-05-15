## 文件说明：该脚本集中维护任务来源 provider_interaction_id 的静态白名单。
## 审查重点：新增任务板/悬赏等任务来源时，需要同步确认内容校验与据点运行时路由具备明确语义。
## 备注：这里是 Quest 内容校验和据点任务板运行时共享的唯一 provider 白名单来源。

class_name QuestProviderContentRules
extends RefCounted

const PROVIDER_CONTRACT_BOARD: StringName = &"service_contract_board"
const PROVIDER_BOUNTY_REGISTRY: StringName = &"service_bounty_registry"

const SUPPORTED_PROVIDER_IDS := {
	PROVIDER_CONTRACT_BOARD: true,
	PROVIDER_BOUNTY_REGISTRY: true,
}


static func normalize_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		var text := (value as String).strip_edges()
		if text.is_empty():
			return &""
		return StringName(text)
	return &""


static func is_supported_provider_id(value: Variant) -> bool:
	return SUPPORTED_PROVIDER_IDS.has(normalize_string_name(value))


static func supported_provider_ids() -> Dictionary:
	return SUPPORTED_PROVIDER_IDS.duplicate()


static func supported_provider_label() -> String:
	var labels: Array[String] = []
	for provider_id in SUPPORTED_PROVIDER_IDS.keys():
		labels.append(String(provider_id))
	labels.sort()
	return ", ".join(labels)
