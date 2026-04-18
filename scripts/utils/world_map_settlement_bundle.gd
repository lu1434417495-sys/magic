## 文件说明：该脚本属于世界地图据点/设施共享包相关的配置资源脚本，集中维护可复用的据点模板与设施模板。
## 审查重点：重点核对共享模板是否只承载据点与设施语义，并确认主世界预设不会再次内嵌同一批通用内容。
## 备注：后续如果新增主世界通用据点或设施模板，优先追加到共享据点包，而不是复制到各个 world_map_config.tres。

class_name WorldMapSettlementBundle
extends Resource

## 字段说明：在编辑器中暴露聚落资源库配置，便于主世界初始化时统一注入共享据点模板。
@export var settlement_library: Array[Resource] = []
## 字段说明：在编辑器中暴露设施资源库配置，便于主世界初始化时统一注入共享设施模板。
@export var facility_library: Array[Resource] = []
