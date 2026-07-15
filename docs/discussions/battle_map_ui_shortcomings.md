# 战斗地图 UI 不足分析

初次记录：`2026-04-25`
最近核对：`2026-07-16`

## 状态

- 当前状态：`Active Discussion Record`
- 范围：针对 `scenes/ui/battle_map_panel.tscn` + `scripts/ui/BattleMapPanel.cs` + `scripts/systems/battle/presentation/BattleHudAdapter.cs` + `scripts/ui/BattleHoverPreviewOverlay.cs` 当前实现所做的系统性盘点。
- 说明：本文件是设计讨论纪要，列出尚未解决的问题与优化方向；已经落地的清单已在 2026-05-13 的清理中移除。
- 代码迁移提示：UI 层早期为 `.gd` 脚本，已整体迁移到 C#（`scripts/ui/` 下不再有 `.gd`）。本文件 2026-06-17 已把所有路径 / 行号 / 类名重新对齐到 C# 实现；方法名多数沿用 snake_case（如 `_create_skill_slot`、`_rebuild_timeline_row`），类名 / 文件名为 PascalCase。

## 背景

本轮改动前，战斗界面已经完成：

- 移除 3 个装饰性 `MarginContainer`（`TopBar/Margin`、`UnitCard/Margin`、`SkillPanel/Margin`），对应的内边距改由 `StyleBoxFlat.content_margin_*` 承担。
- 运行时 `MapViewport.offset_right` 统一设为 `0.0`（战斗 / 非战斗都全宽），`RuntimeLogDock` 作为浮层在右上覆盖。
- 战斗态下 dock 纵向让出 HUD 带，避免挡住 TopBar / BottomPanel。

2026-05-13 的核对中确认以下条目已落地，从清单中清除：
A1 行动队列渲染（`_rebuild_timeline_row` + 场景 `TimelineRow`）、A3 孤儿节点引用、B2 TileLabel 位置、B3 TopBar 空置（已被 timeline 占用）、C3 portrait://pending 暴露、C4 HeaderSubtitle 4 段并排、D1 顶部 22px 空白（TopBar offset_top 归零）、D3 字号体系（`BattleUiTheme.FONT_*`）、D4 配色（HUD 改为黑曜板方向 B，不再深红 / 铜金）、D5 短面板阴影 + 圆角（TOPBAR_RADIUS 改为 0）。

2026-05-14 的清理中再增：C2 `TU / READY` 单 Label 拼接（adapter `round_badge` 改为 `{tu_text, ready_text}` 结构化字典；场景 `RoundChip`（`tscn:86`）内 `RoundLayout` 为 `HBoxContainer` + 两个独立 `TuLabel`（`tscn:97`）/ `ReadyLabel`（`tscn:103`）子节点；快照渲染由 `scripts/utils/GameTextSnapshotRenderer.cs` 承担，回归现由 `tests/battle_runtime/runtime/run_battle_map_panel_schema_regression.cs` 覆盖 `round_badge.tu_text` / `ready_text`）。
> 注（2026-06-17）：原纪要列出的 `run_battle_skill_protocol_regression` / `run_battle_ui_regression` 两个 runner 在 C# 化后已不存在，相关 RoundChip 断言现归口到 `run_battle_map_panel_schema_regression.cs`。

2026-05-16 的核对中再增：A1 的 hover 浮层落地部分（`BattleHoverPreviewOverlay` 已在 `scripts/ui/BattleHoverPreviewOverlay.cs` 实现，`BattleHudAdapter.BuildHoverPreview()`（`BattleHudAdapter.cs:146`）输出 `hit_stage_rates / fate_badges / damage_text / target_unit` 完整 payload，`BattleMapPanel.update_hover_preview` 在鼠标移动时贴到目标格位上）。仅命中分段 / 命运预览的可见渲染入口已建好，`confirm_ready` / `auto_cast_ready` 仍只在 snapshot 中存在，未渲染为徽标——见下面 A1 残留项。

2026-06-17 的核对中再增：全文路径 / 行号 / 类名从 `.gd` 同步到 C# 实现；A1 残留与 A2 缺失两项硬伤已在 C# 代码中复核仍然成立（见各条更新后的引用），其余条目坐标已刷新，结论未变。

2026-07-16 的核对中清除以下已落地条目：

- **A1 残留 + A2 指令区**：已由 2026-06 下旬的指令区重建落地（`BattleMapPanel._create_command_dock` 代码内建节点；`confirm_ready` 挂结算按钮高亮 `_set_resolve_highlight`；hint/log/variant 信息位齐备）。回归：`run_battle_command_dock_snapshot_regression.cs`（数据层）+ `run_battle_map_panel_schema_regression.cs`（面板渲染）。C4 ModeButton 决定不做——战中没有切换 `control_mode` 的运行时命令通道，`ModeValueLabel` 保持只读（详见该决定的会话纪要）。
- **C3 伤害预扣段**：`BattleHoverPreviewOverlay` 的目标 HP 条改为双层叠放（`TargetHpStack`：下层预警色按当前 HP 填充，上层 HP 色按"受击后剩余"填充，露出的色带即预计伤害段），HP 文本追加"→ 受击后 X~Y"。回归：`run_battle_hover_hp_predict_regression.cs`。注：预扣段落在 hover 浮层（钉在地图左侧的事实目标预览卡）而非 UnitCard——UnitCard 显示的是焦点单位而非受击目标。
- **D1 副资源条偏窄**：Stamina/MP/Aura 由 14px 升到 18px（`battle_map_panel.tscn` + `BattleUiTheme.PROGRESS_BAR_HEIGHT_SECONDARY`），hover 浮层 HP 条同步升到 18px。
- **C1 header 开发占位**：缺 `encounter_display_name` 时的回落文案由开发占位"战斗地图"改为通用的"遭遇战"（`BattleHudAdapter` + `BattleMapPanel` 占位态 + 场景默认文本三处同步）。
- **B4 Buff / Debuff 指示位**（同日第二批）：`BattleHudAdapter.BuildStatusEffectSnapshots` 把 `BattleUnitState.GetStatusEffectsTyped()` 投影为 `status_effects`（label 取 `display_label` → 语义表 → status_id 三级回落，减益判定走 `BattleStatusSemanticTable.IsHarmfulStatusEntry`，含 override 通道），挂在 `focus_unit` 与 hover `target_unit` 两个快照上。渲染：UnitCard InfoColumn 代码内建 `StatusBadgeRow`（减益红 / 增益青，文本 `标签×层数 剩余TU`）+ hover 浮层 `TargetStatusRow`；headless 文本快照新增 `hud_status` 行（无状态时不输出，保持既有快照文本不变）。回归：`run_battle_status_badge_regression.cs` + schema / typed projection 基线更新。

## A. 结构性硬伤（数据产生了但没地方显示）

（本节两项已于 2026-07-16 核对确认全部落地并清除，见上方核对增量。）

## B. 信息架构 / 布局

1. **BottomPanel 信息过载**
    - 110px 高度（`offset_top = -126`、`offset_bottom = -16`）里挤了 UnitCard（肖像 + 姓名 + 角色 + HP/Stamina/MP/Aura 四条进度条 + 各段数值 + AP 圆点行 + 战中背包按钮）和 SkillPanel（副标题 + 命运徽章行 + 6 列技能网格）。
    - 所有信息靠小字号堆叠，留白缓冲不足。
2. **没有敌我对比**
    - UnitCard 只反映焦点单位，选敌人时看不到攻方信息。
    - 缺少“目标预览卡”做对照，攻击预期全靠玩家脑补。
3. **命运徽章埋在技能区**
    - FateBadgeRow 位于 SkillHeader 内（`battle_map_panel.tscn:287`，SkillHeader 在 `tscn:275`）。
    - 这是全局命运状态，应更突出（独立条或紧贴 TopBar）。
4. ~~没有 Buff / Debuff / 状态效果指示~~（2026-07-16 已落地 UnitCard `StatusBadgeRow` + hover `TargetStatusRow`，见核对增量。）

## C. 交互与反馈

1. ~~header 标题回落到“战斗地图”~~（2026-07-16 已改为通用回落“遭遇战”，见核对增量。）
2. **技能快捷键提示只在槽位上**
    - `_create_skill_slot` 给每个技能槽渲染了 hotkey label（`BattleMapPanel.cs:2278-2285`），玩家能看到“1/2/3…”这类字符。
    - 但场景里没有专门的键位说明面板 / 帮助提示位，缺少“按 Esc 取消选中”“Tab 切换变体”这类全局键位提示。
3. ~~没有伤害 / 治疗预览~~（2026-07-16 已在 hover 浮层目标 HP 条落地预扣血段，见核对增量。治疗预览仍未做。）
4. **模式切换无交互**
    - `ModeValueLabel`（文本“手动”）仍是只读 Label。
    - AI / 暂停 / 加速等运行时控制缺按钮入口。
    - 2026-06-27 决定：ModeButton **不做**——战中缺少切换 `control_mode` 的运行时命令通道，做它需要新增 `CommandBattleToggleControlMode`（涉 AI 回合 / mutation guard），不属于纯渲染层补齐。
5. **视口操作无视觉指示**
    - `_on_map_viewport_container_gui_input` 支持缩放 / 平移 / 中键拖拽。
    - 界面上没有当前缩放级别、没有“重置视角”按钮，新玩家难以察觉这些操作存在。

## D. 视觉 / 可读性

（D1 副资源条已于 2026-07-16 升到 18px 并清除，见核对增量。）

## E. 与 RuntimeLogDock 协同

1. **右侧 dock 仍占固定区域**
    - 已支持折叠按钮（`RuntimeLogDock.cs` 的 `CollapseButtonTextExpanded` / `CollapseButtonTextCollapsed`）和 3 档透明度（`OpacityLevels = { 1.0f, 0.6f, 0.2f }`），战斗中可以收起 / 减少干扰。
    - 仍不支持拖动 / 自定义停靠位，展开态下 `LockedPanelWidth = 412 × (viewport_height - 276)`（`RuntimeLogDock.cs:20`）的视觉死角依旧存在。
2. **面板内没有精简战报回显**
    - BattleMapPanel 内不存在 `LogLabel` / `log_output` 类节点，战报只能看右侧 dock。
    - 想看“刚才那招打了多少伤害”必须视线大范围跳跃。

## 优先级建议

（2026-07-16 更新：原“必做”清单中 A1/A2/C1 已落地，C3/D1 应做项已落地，均见核对增量。）

### 必做（修正硬伤）

- 拆分 BottomPanel：UnitCard 左 / 技能面板中 / 目标预览卡右。hover 浮层已承担“目标预览卡”大部分职责（钉在地图左侧、含预扣血段），本条降级为“评估是否仍需要 BottomPanel 内的常驻目标卡”。 → B1、B2

### 应做（提升体验）

- 命运徽章上移到 TopBar 下方独立条。 → B3
- 治疗预览（预扣血段的反向：预回血段）。 → C3 残留

### 可选

- RuntimeLogDock 增加拖动 / 自定义停靠（折叠 + 透明度已完成）。 → E1
- 全局键位说明面板。 → C2
- 视口缩放级别指示 / 重置视角按钮。 → C5
- ModeButton：需先新增 `CommandBattleToggleControlMode` 运行时命令，2026-06-27 决定暂不做。 → C4
