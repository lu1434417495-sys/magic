# 战斗地图 UI 不足分析

更新日期：`2026-04-25`

## 状态

- 当前状态：`Active Discussion Record`
- 范围：针对 `scenes/ui/battle_map_panel.tscn` + `scripts/ui/battle_map_panel.gd` + `scripts/ui/battle_hud_adapter.gd` 三者当前实现所做的系统性盘点。
- 说明：本文件是设计讨论纪要，列出已观察到的问题与优化方向，不代表代码已完成改动。

## 背景

本轮改动前，战斗界面已经完成：

- 移除 3 个装饰性 `MarginContainer`（`TopBar/Margin`、`UnitCard/Margin`、`SkillPanel/Margin`），对应的内边距改由 `StyleBoxFlat.content_margin_*` 承担（`HUD_PANEL_CONTENT_MARGIN = 10`）。
- 运行时 `MapViewport.offset_right` 统一设为 `0.0`（战斗 / 非战斗都全宽），`RuntimeLogDock` 作为浮层在右上覆盖。
- 战斗态下 dock 纵向让出 HUD 带（`LOG_DOCK_BATTLE_TOP_MARGIN = 92`、`LOG_DOCK_BATTLE_BOTTOM_MARGIN = 184`），避免挡住 TopBar / BottomPanel。

在这个状态下，继续对战斗面板做信息架构和交互层面的盘点，形成以下清单。

## A. 结构性硬伤（数据产生了但没地方显示）

1. **行动队列 / Timeline 完全未渲染**
    - `battle_hud_adapter.gd:80, 126-191` 在 snapshot 里产出完整 `queue_entries`，包含 active/ready/enemy 标记、HP、AP、portrait。
    - 但 `battle_map_panel._apply_snapshot()`（`battle_map_panel.gd:592-604`）**没有消费这个字段**。
    - 结果：玩家无法看到“下一个谁行动”、“行动顺序”，这是 TRPG/SRPG 的基础信息。
2. **命中率分段 / 命运预览 / 确认就绪标志未体现**
    - snapshot 含 `selected_skill_hit_stage_rates`、`selected_skill_fate_preview_text`、`selected_skill_confirm_ready`、`selected_skill_auto_cast_ready` 等字段。
    - 目前这些信息只被塞进 `skill_subtitle_label.tooltip_text`，鼠标不悬停就看不见。
3. **~~脚本引用的节点一半不在场景里~~**（已修复）
    - 历史问题：`battle_map_panel.gd` 曾用 `get_node_or_null` 引用 `%ResolveBattleButton`、`%ResetMovementButton`、`%PrevVariantButton`、`%NextVariantButton`、`%ClearSkillButton`、`%CommandDock`、`%CommandSummaryLabel`、`%HintLabel`、`%LogLabel`、`%UnitDetailLabel` 等场景里并不存在的节点。
    - 处理方式：`get_node_or_null` 接口不再允许；所有 `@onready` 必须指向场景实际挂载的节点。对应的 `@onready` 声明、信号（`movement_reset_requested`、`resolve_requested`、`battle_skill_variant_cycle_requested`、`battle_skill_clear_requested`）、按钮连接和 emit 辅助方法已从 `battle_map_panel.gd` 中清除。
    - 连带清理：`game_runtime_facade._on_battle_skill_variant_cycle_requested` / `_on_battle_skill_clear_requested` 和 `battle_session_facade.on_battle_skill_variant_cycle_requested` / `on_battle_skill_clear_requested` 这 4 个孤儿 handler 已删除。业务逻辑仍由对等的 `command_battle_cycle_variant` / `command_battle_clear_skill`（`battle_session_facade.gd` 与 `game_runtime_facade.gd`）承担，世界系统键盘触发、文本命令入口、回归测试都继续走这条路径。
    - 仍然缺少的功能位：**确认结算 / 重置移动 / 切换技能变体 / 提示 / 战报回显** 等按钮与信息位，整个“指令区”仍为空，后续补 UI 时需要一并重新设计节点和信号。

## B. 信息架构 / 布局

1. **BottomPanel 信息过载**
    - 148px 高度里挤了 UnitCard（肖像 + 姓名 + 角色 + HP/MP/AP 三条进度条 + 三段数值）+ SkillPanel（标题 + 副标题 + 命运徽章行 + 5 列技能网格 + 地格标签）。
    - 所有信息靠小字号堆叠，留白缓冲不足。
2. **TileLabel 位置错乱**
    - “地格 (--, --) · 无 · 高度 0 · 占位 无” 这类悬停 / 选中地块的信息挤在 SkillPanel 最底（`battle_map_panel.tscn:220`）。
    - 它属于地图交互反馈，应靠近地图或鼠标，不应与技能网格混排。
3. **TopBar 利用率低**
    - 66px 高度只放标题（冗余） + 副标题 + BadgeColumn（TU + 模式）。
    - 大片空间空置，正是放 timeline、全局状态的最佳位置。
4. **没有敌我对比**
    - UnitCard 只反映焦点单位，选敌人时看不到攻方信息。
    - 缺少“目标预览卡”做对照，攻击预期全靠玩家脑补。
5. **命运徽章埋在技能区**
    - FateBadgeRow 位于 SkillPanel 中部（`battle_map_panel.tscn:206`）。
    - 这是全局命运状态，应更突出（独立条或紧贴 TopBar）。
6. **没有 Buff / Debuff / 状态效果指示**
    - 选中单位的增益 / 减益、持续回合、异常状态都没有显示位置。

## C. 交互与反馈

1. **“战斗地图” header 冗余**
    - `battle_hud_adapter.gd:76` 里 `"header_title"` 固定为 “战斗地图”。
    - 玩家已经在战斗界面，这个标签不提供信息，应该换成遭遇名、章节名或关键元信息。
2. **`"TU --\nREADY 0"` 用 `\n` 硬拼**
    - `battle_map_panel.tscn:77-78` 把两个独立指标挤成一个 Label。
    - 字体粘连、对齐不可控，应拆成 HBox/VBox + 独立样式。
3. **`portrait://pending` 直接暴露给玩家**
    - `PortraitKeyLabel` 显示内部 portrait key（`battle_map_panel.tscn:135`、`battle_map_panel.gd:696`）。
    - 这是开发占位，玩家不该看到。
4. **HeaderSubtitle 信息过密**
    - `battle_hud_adapter.gd:108-110` 用 `"阶段 %s | 友军 %d | 敌军 %d | 当前 %s"` 把 4 个指标压成一行。
    - 扫读困难，应拆条 / 分格显示。
5. **技能快捷键提示缺失**
    - `_create_skill_slot` 动态渲染 hotkey_label，但场景里没有键绑定说明区。
    - 新玩家不知道哪个键对应哪个技能。
6. **没有伤害 / 治疗预览**
    - 选中目标时，目标 HP 条应显示“预扣血段”。
    - 当前 `hp_bar` 只是单色进度条，无分段反馈。
7. **模式切换无交互**
    - `ModeValueLabel`（文本“手动”）是只读 Label。
    - AI / 暂停 / 加速等运行时控制缺按钮入口。
8. **视口操作无视觉指示**
    - `_on_map_viewport_container_gui_input` 支持缩放 / 平移 / 中键拖拽。
    - 界面上没有当前缩放级别、没有“重置视角”按钮，新玩家难以察觉这些操作存在。

## D. 视觉 / 可读性

1. **顶部 22px 空白感**
    - `TopBar.offset_top = 12` + StyleBox `content_margin = 10`，首个 Label 离窗口顶 22px。
    - 叠加右侧日志浮层时整体显得“飘”。
2. **进度条太窄**
    - HP/MP/AP 三条都是 `custom_minimum_size = Vector2(0, 14)`（`battle_map_panel.tscn:155, 166, 177`）。
    - 14px 高度远看几乎是线，配色细节看不清。
3. **字号体系无规范**
    - 脚本里手动写死 `font_size` 24、22、20、15、14、13、12、11、10（`battle_map_panel.gd:602-628` 等处）。
    - 没有 theme 分层，风格调整要逐行改。
4. **色彩与地图冲突**
    - HUD 深红 / 铜金（`HUD_PANEL_BG = (0.16, 0.06, 0.03, 0.9)`）配偏深蓝的地图背景。
    - 焦点不突出，技能槽的 accent（命运 5 色 + 选中 / 禁用 / 空态）容易视觉混乱。
5. **面板阴影 + 圆角在短面板上臃肿**
    - 66px 高度的 TopBar 套 20px 圆角 + 10px 阴影，比例不协调。

## E. 与 RuntimeLogDock 协同

1. **右侧 412px 固定被 dock 独占**
    - 战斗态下 dock 纵向已避开 HUD 带，但 dock 本身不可折叠 / 不可拖动 / 不可调节透明度。
    - 战斗过程中玩家始终有 `412 × (viewport_height - 276)` 的视觉死角。
2. **面板内没有精简战报回显**
    - `%LogLabel` 引用为 null，战报只能看右侧 dock。
    - 想看“刚才那招打了多少伤害”必须视线大范围跳跃。

## 优先级建议

### 必做（修正硬伤）

- 把 `queue_entries` 落地成 TopBar 里的 timeline 条。 → A1
- 清理场景里没有对应节点的脚本引用，或按照用途补齐场景节点。 → A3
- 处理 `portrait://pending`、“战斗地图” 等玩家可见的占位 / 冗余文案。 → C1、C3
- 拆分 BottomPanel：UnitCard 左 / 技能面板中 / 目标预览卡右；TileLabel 改成地图 tooltip。 → B1、B2、B4

### 应做（提升体验）

- HP/MP/AP 条加高到 18–20px，HP 条加伤害预览分段。 → C6、D2
- TopBar 右端放“模式按钮 + 结算按钮”，给玩家可操作入口。 → A3、C7
- 命运徽章上移到 TopBar 下方独立条。 → B5
- 定义字号 theme，统一小 / 中 / 大 / 标题四档。 → D3

### 可选

- RuntimeLogDock 可折叠 / 可拖动 / 半透明切换。 → E1
- Buff / Debuff 条。 → B6
- 键位提示面板、战报回显条。 → C5、E2
