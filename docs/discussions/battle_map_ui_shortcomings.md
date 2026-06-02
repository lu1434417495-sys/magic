# 战斗地图 UI 不足分析

初次记录：`2026-04-25`
最近核对：`2026-05-16`

## 状态

- 当前状态：`Active Discussion Record`
- 范围：针对 `scenes/ui/battle_map_panel.tscn` + `scripts/ui/battle_map_panel.gd` + `scripts/systems/battle/presentation/battle_hud_adapter.gd` 三者当前实现所做的系统性盘点。
- 说明：本文件是设计讨论纪要，列出尚未解决的问题与优化方向；已经落地的清单已在 2026-05-13 的清理中移除。

## 背景

本轮改动前，战斗界面已经完成：

- 移除 3 个装饰性 `MarginContainer`（`TopBar/Margin`、`UnitCard/Margin`、`SkillPanel/Margin`），对应的内边距改由 `StyleBoxFlat.content_margin_*` 承担。
- 运行时 `MapViewport.offset_right` 统一设为 `0.0`（战斗 / 非战斗都全宽），`RuntimeLogDock` 作为浮层在右上覆盖。
- 战斗态下 dock 纵向让出 HUD 带，避免挡住 TopBar / BottomPanel。

2026-05-13 的核对中确认以下条目已落地，从清单中清除：
A1 行动队列渲染（`_rebuild_timeline_row` + 场景 `TimelineRow`）、A3 孤儿节点引用、B2 TileLabel 位置、B3 TopBar 空置（已被 timeline 占用）、C3 portrait://pending 暴露、C4 HeaderSubtitle 4 段并排、D1 顶部 22px 空白（TopBar offset_top 归零）、D3 字号体系（`BattleUiTheme.FONT_*`）、D4 配色（HUD 改为黑曜板方向 B，不再深红 / 铜金）、D5 短面板阴影 + 圆角（TOPBAR_RADIUS 改为 0）。

2026-05-14 的清理中再增：C2 `TU / READY` 单 Label 拼接（adapter `round_badge` 改为 `{tu_text, ready_text}` 结构化字典；场景 `RoundChip` 内换成 `HBoxContainer` + 两个独立 `TuLabel` / `ReadyLabel` 子节点；同步更新 `game_text_snapshot_renderer`、`run_battle_skill_protocol_regression`、`run_battle_ui_regression` 的 legacy fixture）。

2026-05-16 的核对中再增：A1 的 hover 浮层落地部分（`BattleHoverPreviewOverlay` 已在 `scripts/ui/battle_hover_preview_overlay.gd` 实现，`battle_hud_adapter.build_hover_preview()` 输出 `hit_stage_rates / fate_badges / damage_text / target_unit` 完整 payload，`battle_map_panel.update_hover_preview` 在鼠标移动时贴到目标格位上）。仅命中分段 / 命运预览的可见渲染入口已建好，`confirm_ready` / `auto_cast_ready` 仍只在 snapshot 中存在，未渲染为徽标——见下面 A1 残留项。

## A. 结构性硬伤（数据产生了但没地方显示）

1. **`confirm_ready` / `auto_cast_ready` 仍无渲染入口（A1 残留）**
    - hover 浮层已经接管 `hit_stage_rates` / `fate_badges` / `damage_text` 的渲染（见 2026-05-16 清理）。
    - 但 `selected_skill_confirm_ready` / `selected_skill_auto_cast_ready` 这两个字段在 `battle_map_panel.gd` 和 `battle_hover_preview_overlay.gd` 里都搜不到引用，snapshot 产出后没有任何 UI 位读取——技能确认是否就绪、是否会自动施法对玩家不可见。
    - 落地建议：要么在 hover 浮层加 confirm 徽标行；要么放到将来的指令区（A2），与"确认结算"按钮联动显示就绪态。
2. **指令区按钮 / 反馈位仍缺失**
    - 历史上引用过的 `%ResolveBattleButton`、`%ResetMovementButton`、`%PrevVariantButton`、`%NextVariantButton`、`%ClearSkillButton`、`%CommandDock`、`%CommandSummaryLabel`、`%HintLabel`、`%LogLabel`、`%UnitDetailLabel` 等节点都已经从脚本中清除（`get_node_or_null` 不再允许），但**对应的 UI 功能位也消失了**。
    - 业务命令仍走 `command_battle_cycle_variant` / `command_battle_clear_skill` 等通道（`battle_session_facade.gd` / `game_runtime_facade.gd`），世界系统键盘、文本命令、回归测试都可用；玩家在面板上没有可见的“确认结算 / 重置移动 / 切换变体 / 提示 / 战报回显”入口。
    - 后续补 UI 时需要重新设计节点和信号，避免再出现“snapshot 已经产出但没有渲染位”的情况。

## B. 信息架构 / 布局

1. **BottomPanel 信息过载**
    - 110px 高度（`offset_top = -126`、`offset_bottom = -16`）里挤了 UnitCard（肖像 + 姓名 + 角色 + HP/Stamina/MP/Aura 四条进度条 + 各段数值 + AP 圆点行 + 战中背包按钮）和 SkillPanel（副标题 + 命运徽章行 + 6 列技能网格）。
    - 所有信息靠小字号堆叠，留白缓冲不足。
2. **没有敌我对比**
    - UnitCard 只反映焦点单位，选敌人时看不到攻方信息。
    - 缺少“目标预览卡”做对照，攻击预期全靠玩家脑补。
3. **命运徽章埋在技能区**
    - FateBadgeRow 位于 SkillHeader 内（`battle_map_panel.tscn:275`）。
    - 这是全局命运状态，应更突出（独立条或紧贴 TopBar）。
4. **没有 Buff / Debuff / 状态效果指示**
    - 选中单位的增益 / 减益、持续回合、异常状态都没有显示位置。

## C. 交互与反馈

1. **header 标题在缺 encounter_display_name 时回落到“战斗地图”**
    - `battle_hud_adapter.gd:145` 已经改成“若 encounter 名非空则用 encounter 名，否则 fallback 到‘战斗地图’”。
    - 但在 fixture / 早期 reveal / 缺 callback 的场景下仍会显示开发占位“战斗地图”，应明确：要么保证 encounter name 始终可解析，要么改成更通用的回落（如关卡 / 章节名）。
2. **技能快捷键提示只在槽位上**
    - `_create_skill_slot` 给每个技能槽渲染了 hotkey_label（`battle_map_panel.gd:1719-1724`），玩家能看到“1/2/3…”这类字符。
    - 但场景里没有专门的键位说明面板 / 帮助提示位，缺少“按 Esc 取消选中”“Tab 切换变体”这类全局键位提示。
3. **没有伤害 / 治疗预览**
    - 选中目标时，目标 HP 条应显示“预扣血段”。
    - 当前 `hp_bar` 只是单色进度条，无分段反馈；脚本里也搜索不到 `damage_preview` 相关逻辑。
4. **模式切换无交互**
    - `ModeValueLabel`（文本“手动”）仍是只读 Label。
    - AI / 暂停 / 加速等运行时控制缺按钮入口。
5. **视口操作无视觉指示**
    - `_on_map_viewport_container_gui_input` 支持缩放 / 平移 / 中键拖拽。
    - 界面上没有当前缩放级别、没有“重置视角”按钮，新玩家难以察觉这些操作存在。

## D. 视觉 / 可读性

1. **副资源进度条仍偏窄**
    - HP 已经升到 18px（`BattleUiTheme.PROGRESS_BAR_HEIGHT_PRIMARY`），但 Stamina/MP/Aura 三条还保持 14px（`PROGRESS_BAR_HEIGHT_SECONDARY`）。
    - 14px 在小分辨率下细节仍偏弱；伤害预览段（C4）落地时需要顺手评估是否一并加高。

## E. 与 RuntimeLogDock 协同

1. **右侧 dock 仍占固定区域**
    - 已支持折叠按钮（`COLLAPSE_BUTTON_TEXT_*`）和 3 档透明度（`OPACITY_LEVELS = [1.0, 0.6, 0.2]`），战斗中可以收起 / 减少干扰。
    - 仍不支持拖动 / 自定义停靠位，展开态下 `412 × (viewport_height - 276)` 的视觉死角依旧存在。
2. **面板内没有精简战报回显**
    - BattleMapPanel 内不存在 `LogLabel` / `log_output` 类节点，战报只能看右侧 dock。
    - 想看“刚才那招打了多少伤害”必须视线大范围跳跃。

## 优先级建议

### 必做（修正硬伤）

- 把 `selected_skill_confirm_ready` / `selected_skill_auto_cast_ready` 渲染到可见位置（hover 浮层加一行确认徽标，或挂到指令区按钮的就绪态）。命中分段 + 命运摘要已由 hover 浮层接管。 → A1 残留
- 重建“指令区”：技能确认 / 重置移动 / 切换变体 / 战报回显按钮 + 信息位，与现有 facade 命令通道挂钩。 → A2、C2、C4、E2
- 处理 fixture / 早期 reveal 路径下 header 仍会显示开发占位“战斗地图”的情况。 → C1
- 拆分 BottomPanel：UnitCard 左 / 技能面板中 / 目标预览卡右。 → B1、B2

### 应做（提升体验）

- Stamina/MP/Aura 进度条升到 18px，并为 HP 条增加伤害预览分段。 → C3、D1
- TopBar 右端放“模式按钮 + 结算按钮”，给玩家可操作入口。 → C4
- 命运徽章上移到 TopBar 下方独立条。 → B3

### 可选

- RuntimeLogDock 增加拖动 / 自定义停靠（折叠 + 透明度已完成）。 → E1
- Buff / Debuff 条。 → B4
- 全局键位说明面板（已在技能槽上显示快捷键）。 → C2
