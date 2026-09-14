# 人物信息展示

更新日期：2026-09-13

战斗棋盘的人物信息由 `GameRuntimeFacade` 打开，`GameRuntimeCharacterInfoBuilder` 构造 `GameRuntimeCharacterInfoContext`，`WorldMapSystem` 将其交给 `CharacterInfoWindow.ShowCharacter(...)`。每次打开重新构造展示快照；窗口自身不轮询或修改战斗状态。

敌我单位都展示完整属性与生效 trait，包括精确数值、效果和来源，供玩家进行战术判断；人物详情不按阵营隐藏这些信息。

## 内容与数据来源

- 基础概览保留当前生命、法力、行动等资源。
- 基础属性展示六维与当前调整值，直接读取 `BattleUnitState.attribute_snapshot`。调整值可能包含运行时叠加，因此不由界面根据六维重新计算。
- 战斗属性展示快照内存在的攻击、施法、射程、AC 分项、资源上限与行动节奏；缺失字段不伪造为零。护甲敏捷上限的 -1 展示为“不限”。
- 生效特性逐条读取 `GetEffectiveTraitsReadViewTyped().Instances`，以 trait 定义中的名称和描述显示效果，并保留实例等级、层数与来源。装备名称从战斗装备视图中的 source instance 解析；身份来源从种族、亚种、血脉与升华目录解析；角色 trait 的 source id 指向技能时显示技能名称。UI 不再次去重、选择最高值或恢复已失效实例。
- 身份段落继续展示种族、年龄、血脉、升华和身份说明，与真正的生效 trait 列表分开。
- 套装、装备悬停详情、状态、技能摘要和命运段落沿用原有投影。

世界 NPC 仍使用其世界数据中的基本资料；该数据未携带战斗属性，不能从 NPC 名称推造属性或 trait。

`CharacterAttributeDisplayText` 是人物详情和队伍属性页共用的属性名称映射。静态名称不改变属性的运行时 ID 或任何属性计算规则。

## 窗口与刷新

窗口沿用共享 Chronicle 主题，面板最大 900 × 760 逻辑像素，并随视口缩小。基础概览、基础属性和战斗属性采用双列；长内容位于正文滚动区，标题、关闭按钮、属性 / 特性跳转按钮和底部状态独立保留。世界 NPC 没有对应详情时不显示跳转按钮。重新打开或切换人物时清空旧节点并将滚动位置复位。关闭按钮、遮罩和 Escape 继续走原来的 `closed` 信号；尺寸调整使用本窗口的 `Resized` 信号，跳转按钮仅滚动正文，离树时解除连接。

`GameRuntimeCharacterInfoSectionLayout` 只控制 typed 窗口段落布局。自动化 plain snapshot 继续输出段落标题与条目，不含 Godot 节点或资源。本次展示不改变存档结构。

## 聚焦验证

`tests/world_map/ui/run_character_info_details_regression.cs` 覆盖六维调整值叠加、战斗属性、来源名称、多实例显示、移除后刷新、空特性、720p 窗口边界和滚动。非 headless 运行时，可通过 `MAGIC_CHARACTER_INFO_CAPTURE_DIR` 指定截图目录；截图为测试数据驱动的真实窗口渲染。

相邻回归包括 `run_character_info_identity_regression.cs`、`run_character_info_window_fate_regression.cs` 与 `tests/world_map/runtime/run_character_info_payload_schema_regression.cs`。
