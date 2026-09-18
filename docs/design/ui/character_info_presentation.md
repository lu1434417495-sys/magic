# 人物信息展示

更新日期：2026-09-15

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

## 大地图队伍人物档案

大地图“队伍”按钮和 `P` 键打开 `PartyManagementWindow`。左侧保留上阵、替补和编成操作，右侧固定八页：概览、属性、种族、职业、技能、装备、特性、成就。

- 概览展示成员身份、当前资源、等级和成就摘要；标题持续显示所选成员姓名。
- 属性页与战斗详情共用六维调整值及资源 / 战斗属性投影，读取当前战外装备视图计算的属性快照，并保留命运数值。
- 种族页展示种族、亚种、年龄阶段、生理年龄、星界记忆、血脉及升华阶段，补充身份说明、种族技能、豁免能力、抗性、熟练、视觉和属性修正；末尾单独列出当前实际生效的身份 trait。
- 职业页保留可见职业、阶位、激活情况、属性修正和核心 / 授予技能，并展示晋升历史中的成长与资格技能。
- 技能页逐项展示全部已学技能，不按数量截断，保留等级、熟练度、来源、当前效果及配置中的升级预览。
- 装备页读取每个锚点槽位的 `EquipmentInstanceState`，显示品质、当前 / 最大耐久、随机词条等级 / 层数 / 掷值，以及物品属性修正、护甲敏捷上限、固定 trait 和原有套装摘要；多槽装备只显示一次，空槽和占用关系仍单独说明。
- 特性页读取 `CharacterManagementModule.BuildEffectiveTraitProjectionForEquipmentView(...)` 的只读实例。`CharacterTraitDisplayText` 与战斗人物详情共用效果、来源、等级 / 层数和掷值文字，不重新应用叠层策略或恢复已移除 trait。
- 成就页只展示所选成员 `UnitProgress` 中 `is_unlocked = true` 的已获得成就，从注入的 `AchievementDefinition` 读取名称、说明和奖励内容，按取得时间降序排列并显示本地取得时间；没有时间的记录不伪造日期。未激活成就、进行中条目、未开始条目、进度和总完成度均不出现在该页；没有已获得成就时只显示“尚未获得成就”。窗口不根据阈值推断解锁，也不在浏览时创建进度或发放奖励。概览继续保留简短成就摘要。

`WorldMapSystem` 注入只读身份目录和已有内容索引。页签独立滚动，禁止横向滚动以保证正文换行；换人保留页签并重置滚动位置，刷新后反映卸装和特性移除，重新打开返回概览。窗口继续使用原有编成、仓库、晋升、触发术和关闭信号，无新增信号或存档字段，无需编辑器手工接线。

## 聚焦验证

### 队伍人物档案

`tests/world_map/ui/run_party_character_details_regression.cs` 检查八页、正式种族资料、不同来源的 trait、装备实例数值、完整技能和成就列表、晋升历史、成就解锁与奖励展示、切换 / 移除刷新和视口边界。设置 `MAGIC_PARTY_DETAILS_CAPTURE_DIR` 可启用原生大地图场景、真实页签点击、滚轮和关闭验证；该模式要求 `MAGIC_E2E_ISOLATED_USER_DATA=1` 和经过路径检查的绝对 `MAGIC_E2E_USER_DATA_ROOT`，避免读写玩家存档。截图覆盖 1280 × 720 和 3840 × 2160（1920 × 1080 逻辑 UI）。这属于场景集成验证，不替代从登录开始的完整应用 E2E。

### 战斗人物详情

`tests/world_map/ui/run_character_info_details_regression.cs` 覆盖六维调整值叠加、战斗属性、来源名称、多实例显示、移除后刷新、空特性、720p 窗口边界和滚动。非 headless 运行时，可通过 `MAGIC_CHARACTER_INFO_CAPTURE_DIR` 指定截图目录；截图为测试数据驱动的真实窗口渲染。

相邻回归包括 `run_character_info_identity_regression.cs`、`run_character_info_window_fate_regression.cs` 与 `tests/world_map/runtime/run_character_info_payload_schema_regression.cs`。
