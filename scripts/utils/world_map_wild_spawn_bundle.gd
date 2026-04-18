## 文件说明：该脚本属于世界地图野外生成规则共享包相关的配置资源脚本，集中维护可复用的野怪放置规则。
## 审查重点：重点核对共享规则是否只承载世界放置语义，并确认敌人模板本体仍留在 enemies 内容域。
## 备注：后续如果新增主世界通用野外规则，优先追加到共享野怪包，而不是复制到各个 world_map_config.tres。

class_name WorldMapWildSpawnBundle
extends Resource

## 字段说明：在编辑器中暴露野外生成规则配置，便于主世界初始化时统一注入共享野怪模板。
@export var wild_monster_distribution: Array[Resource] = []
