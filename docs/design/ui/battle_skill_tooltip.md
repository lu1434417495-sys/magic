# 战斗技能悬停详情

更新日期：2026-09-13

技能栏的 `BattleSkillSlotButton` 拥有悬停延时和关闭时机，`BattleSkillTooltipPanel` 拥有固定尺寸内容与边框。鼠标停在技能图标或短名上 0.3 秒后弹出详情；鼠标可以移入框内阅读和滚动，离开图标与详情框 0.18 秒后关闭。技能禁用时仍可查看费用、效果和不可用原因，Escape 关闭详情。

`BattleMapPanel.SkillGrid` 将技能图标等比铺满 72 像素槽位边框内区域，不再单独显示快捷键数字行或额外图标留白；快捷键仍显示在悬停详情中。剩余冷却以描边文字叠在右上角，底部保留颜色细线，选中状态由边框表示，禁用状态继续使用灰度图标。按钮悬停与按下只叠加半透明反馈，不遮住图标；空图标仍显示技能短名。

图框统一按当前 viewport 约四分之一面积计算，保持 0.9 的宽高比和屏幕边距，同一视口中所有技能使用相同尺寸，不区分小窗口与全屏规格。文字字号保持原样（正文 18），只沿用游戏全局 UI 缩放。标题、消耗、熟练度与可用性固定排版，等级说明位于独立 `ScrollContainer`；短说明保留留白，超长说明通过滚轮或拖动滚动条阅读。多种资源消耗换行，不挤占相邻射程和冷却列。`BattleSkillTooltipFrame` 以 Godot 绘制深色底板、双层金属边、切角纹样与中轴菱形装饰，不修改战斗背景或新增技能图标映射。

悬停内容挂在槽位自己拥有的 `CanvasLayer`（layer 19，低于战中 modal），不拦截框外输入；坐标始终使用当前 viewport 的逻辑坐标，并夹取在可见范围内。技能栏重建、按钮隐藏、viewport 尺寸变化或离开战斗时关闭并释放对应节点，不保留全局 popup。没有新增编辑器信号接线。

## 数据边界

`BattleHudAdapter.BuildSkillSlots` 将当前 `BattleAvailableSkillEntry.SkillLevel`、定义简介和 `BattleHudSkillTooltipSnapshot` 一起写入 detached 技能槽快照。详情的费用来自同一槽位费用计算入口，射程来自 `BattleRangeService`，基础冷却和吟唱时间来自有效战斗定义。按目标槽计费的技能标注“单目标消耗”；剩余冷却单独位于当前可用性行。

当前等级效果复用 `SkillLevelDescriptionFormatter.BuildLevelDescriptionTyped`。HUD 以 plain CLR context 覆盖当前射程、费用、冷却及体质 / 意志调整值，不创建 Godot Dictionary 或在窗口里计算技能规则。原有 Godot Dictionary 入口继续只承担同步边界归一化。

窗口展示技能图标（空 asset ID 则使用短名）、名称、等级、快捷键，随后展示消耗 / 射程 / 冷却、当前等级效果、熟练度和可用性。战斗悬停不显示普通 `Description`；完整说明继续在 `PartyManagementWindow` 人物详情的技能页展示。图标仍只使用正式 `IconId`，不按 skill id 推造或替换资产。

熟练度通过已有 `IBattleHudContext.GetPartyMemberState` 读取来源队员的 `UnitSkillProgress`，投影成不可变 `BattleHudSkillMasterySnapshot`。当前值来自 `current_mastery`，升级门槛使用 `SkillDefinition.GetMasteryRequiredForLevel`，当前等级上限使用 `SkillEffectiveMaxLevelRules`。战斗奖励写入同一角色成长 owner；HUD 刷新时重新投影，UI 不保存 live progression。达到当前上限时显示上限并隐藏进度条；无已学成长记录的单位显示“暂无成长进度”。装备授予等级高于已学等级时，熟练度标题单独标明已学等级，避免混用等级和升级门槛。

`BattleMapPanel.SkillGrid` 的缓存签名包含技能条目、等级、简介、当前效果、数值、熟练度快照和禁用原因。即使图标或费用未改变，等级、射程或熟练度变化也会重建详情。底层 PanelContainer 不再提供另一份简略 tooltip。

新增 `tooltip` 字段属于 HUD 的 outward snapshot，不是存档字段；技能数据、伤害、消耗与冷却规则没有改动。

## 验证与展示

`tests/battle_runtime/presentation/run_battle_skill_tooltip_regression.cs` 使用正式 catalog 的 3 级 `mage_frost_bolt`（霜击术）和 1 级 `mage_phantasmal_kill`（怪影杀戮），经过真实 HUD adapter 和技能网格，再通过鼠标运动事件触发悬停。额外的超长文本只在布局 fixture 中构造，不写入技能内容。

验证覆盖当前等级数值、普通描述不出现在悬停中、弹窗出现和离开关闭、仅熟练度变化时刷新且旧快照保持不变、当前等级上限与无成长进度状态、4 级射程更新、禁用图标保留详情，以及长短说明同尺寸、鼠标移入后保留、真实滚轮到达正文末尾、滚动时熟练度不移位和隐藏战斗后清理。显示模式验证经正式 `DisplaySettingsService.ApplySettings` 设置物理分辨率与逻辑 UI 尺寸，检查图框占比、字号不变及窗口调整后重新布局。非 headless 运行时设置 `MAGIC_SKILL_TOOLTIP_CAPTURE_DIR` 可保存截图；`MAGIC_SKILL_TOOLTIP_CAPTURE_SCALE=3` 选择 3840×2160，搭配 `MAGIC_SKILL_TOOLTIP_CAPTURE_FULLSCREEN=1` 进入全屏。只应用设置，不写入用户显示配置。展示角色及其当前熟练度为测试 fixture，技能定义、等级数值和升级门槛来自正式内容。

相邻验证包括 battle HUD typed projection、battle map panel schema、skill level description typed、level description template 与 skill description consistency 回归。编辑器无需新增 signal 接线；窗口使用原有鼠标悬停与技能选择通道。
