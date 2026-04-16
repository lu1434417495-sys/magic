## 文件说明：该脚本属于挂载子地图配置相关的配置资源脚本，集中维护子地图标识、配置路径和返回提示等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与世界运行时切图链之间的对应关系。
## 备注：后续如果扩展多层子地图，需要同步检查 map stack、持久化和返回提示文案。

class_name MountedSubmapConfig
extends Resource

## 字段说明：在编辑器中暴露子地图唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var submap_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：记录生成配置路径，供运行时懒加载子地图世界数据时直接使用。
@export_file("*.tres") var generation_config_path: String = ""
## 字段说明：用于界面展示的返回提示文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var return_hint_text: String = "点击任意地点返回原位置。"
