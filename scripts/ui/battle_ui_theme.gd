## 文件说明：战斗 UI 主题常量集合，方向 B（黑曜板 + 命运辉光）的全部配色 / 尺寸。
## 审查重点：调色仅在此处变更，避免 battle_map_panel.gd 内散落硬写颜色；新增分组时保持命名前缀一致（PANEL_/TEXT_/FATE_/RESOURCE_/SKILL_）。
## 备注：所有 Color 数值采用 Godot 0–1 浮点。颜色注释里的 hex 仅供查阅，不参与计算。

class_name BattleUiTheme
extends RefCounted

# Panel surfaces ----------------------------------------------------
const PANEL_BG := Color(0.082, 0.094, 0.118, 0.94)        # #15181E
const PANEL_BG_ALT := Color(0.102, 0.114, 0.141, 0.92)    # #1A1D24
const PANEL_BG_DEEP := Color(0.043, 0.051, 0.067, 0.96)   # #0B0D11
const PANEL_EDGE := Color(0.353, 0.392, 0.439, 1.0)       # #5A6470
const PANEL_EDGE_SOFT := Color(0.227, 0.255, 0.282, 1.0)  # #3A4148
const PANEL_EDGE_GLOW := Color(0.439, 0.706, 1.0, 1.0)    # #70B4FF (cool edge highlight)
const PANEL_SHADOW := Color(0.0, 0.0, 0.0, 0.42)

# Chip surfaces -----------------------------------------------------
const CHIP_BG := Color(0.133, 0.157, 0.192, 0.96)         # #222831
const CHIP_EDGE := Color(0.227, 0.255, 0.282, 1.0)        # #3A4148

# Typography --------------------------------------------------------
const TEXT_PRIMARY := Color(0.91, 0.925, 0.949, 1.0)      # #E8ECF2
const TEXT_SECONDARY := Color(0.612, 0.639, 0.682, 1.0)   # #9CA3AE
const TEXT_MUTED := Color(0.42, 0.447, 0.502, 1.0)        # #6B7280
const TEXT_ACCENT := Color(1.0, 0.847, 0.42, 1.0)         # #FFD86B (gold, used for selection / mastery)

# Fate accents (used for skill slot bottom glow band + fate badges) -
const FATE_CALM := Color(0.212, 0.784, 0.698, 1.0)        # #36C8B2
const FATE_GATE := Color(0.478, 0.659, 1.0, 1.0)          # #7AA8FF
const FATE_WARNING := Color(1.0, 0.702, 0.278, 1.0)       # #FFB347
const FATE_DANGER := Color(1.0, 0.353, 0.353, 1.0)        # #FF5A5A
const FATE_HIGH_THREAT := Color(1.0, 0.847, 0.42, 1.0)    # #FFD86B
const FATE_MERCY := Color(0.357, 0.816, 0.973, 1.0)       # #5BD0F8

# Resource bar fills (HP/Stamina/MP/Aura) ---------------------------
const RESOURCE_HP := Color(0.482, 0.82, 0.353, 1.0)       # #7BD15A
const RESOURCE_STAMINA := Color(0.961, 0.769, 0.353, 1.0) # #F5C45A
const RESOURCE_MP := Color(0.357, 0.71, 0.973, 1.0)       # #5BB5F8
const RESOURCE_AURA := Color(0.847, 0.482, 0.847, 1.0)    # #D87BD8

# AP dot pips -------------------------------------------------------
const AP_DOT_FILL := Color(1.0, 0.847, 0.42, 1.0)         # #FFD86B
const AP_DOT_EMPTY := Color(1.0, 0.847, 0.42, 0.18)

# Geometry ----------------------------------------------------------
const TOPBAR_HEIGHT := 48
const TOPBAR_RADIUS := 0
const PANEL_RADIUS_LARGE := 8
const PANEL_RADIUS_MEDIUM := 6
const PANEL_RADIUS_SMALL := 4
const PANEL_RADIUS_TINY := 3
const PANEL_BORDER := 1
const PANEL_CONTENT_MARGIN := 10
const SKILL_SLOT_SIZE := 72
const SKILL_GLOW_BAND_HEIGHT := 3
const PROGRESS_BAR_HEIGHT_PRIMARY := 18
const PROGRESS_BAR_HEIGHT_SECONDARY := 14
const TIMELINE_ENTRY_SIZE := 26       # 迷你头像方块尺寸
const TIMELINE_HP_BAND_HEIGHT := 3    # 头像下方 HP 带高度
const TIMELINE_SEPARATION := 4        # entry 间距
const TIMELINE_ALLY_RING := Color(0.439, 0.706, 1.0, 1.0)    # #70B4FF 我方蓝环
const TIMELINE_ENEMY_RING := Color(1.0, 0.353, 0.353, 1.0)   # #FF5A5A 敌方红环
const TIMELINE_ACTIVE_RING := Color(1.0, 0.847, 0.42, 1.0)   # #FFD86B 当前回合金环
const TIMELINE_INACTIVE_ALPHA := 0.5  # 未就绪单位整块透明度

# Font sizes --------------------------------------------------------
const FONT_TITLE := 18
const FONT_HEADING := 14
const FONT_BODY := 12
const FONT_LABEL := 11
const FONT_CAPTION := 10
const FONT_GLYPH := 26


static func fate_color(tone: StringName) -> Color:
	match tone:
		&"calm":
			return FATE_CALM
		&"warning":
			return FATE_WARNING
		&"danger":
			return FATE_DANGER
		&"high_threat":
			return FATE_HIGH_THREAT
		&"mercy":
			return FATE_MERCY
		_:
			return FATE_GATE
