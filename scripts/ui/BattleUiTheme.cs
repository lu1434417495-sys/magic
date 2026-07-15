using Godot;

public static class BattleUiTheme
{
    private static readonly Color PanelBg = new(0.082f, 0.094f, 0.118f, 0.94f);
    private static readonly Color PanelBgAlt = new(0.102f, 0.114f, 0.141f, 0.92f);
    private static readonly Color PanelBgDeep = new(0.043f, 0.051f, 0.067f, 0.96f);
    private static readonly Color PanelEdge = new(0.353f, 0.392f, 0.439f, 1.0f);
    private static readonly Color PanelEdgeSoft = new(0.227f, 0.255f, 0.282f, 1.0f);
    private static readonly Color PanelEdgeGlow = new(0.439f, 0.706f, 1.0f, 1.0f);
    private static readonly Color PanelShadow = new(0.0f, 0.0f, 0.0f, 0.42f);

    private static readonly Color ChipBg = new(0.133f, 0.157f, 0.192f, 0.96f);
    private static readonly Color ChipEdge = new(0.227f, 0.255f, 0.282f, 1.0f);

    private static readonly Color TextPrimary = new(0.91f, 0.925f, 0.949f, 1.0f);
    private static readonly Color TextSecondary = new(0.612f, 0.639f, 0.682f, 1.0f);
    private static readonly Color TextMuted = new(0.42f, 0.447f, 0.502f, 1.0f);
    private static readonly Color TextAccent = new(1.0f, 0.847f, 0.42f, 1.0f);

    private static readonly Color FateCalm = new(0.212f, 0.784f, 0.698f, 1.0f);
    private static readonly Color FateGate = new(0.478f, 0.659f, 1.0f, 1.0f);
    private static readonly Color FateWarning = new(1.0f, 0.702f, 0.278f, 1.0f);
    private static readonly Color FateDanger = new(1.0f, 0.353f, 0.353f, 1.0f);
    private static readonly Color FateHighThreat = new(1.0f, 0.847f, 0.42f, 1.0f);
    private static readonly Color FateMercy = new(0.357f, 0.816f, 0.973f, 1.0f);

    private static readonly Color ResourceHp = new(0.482f, 0.82f, 0.353f, 1.0f);
    private static readonly Color ResourceStamina = new(0.961f, 0.769f, 0.353f, 1.0f);
    private static readonly Color ResourceMp = new(0.357f, 0.71f, 0.973f, 1.0f);
    private static readonly Color ResourceAura = new(0.847f, 0.482f, 0.847f, 1.0f);

    private static readonly Color ApDotFill = new(1.0f, 0.847f, 0.42f, 1.0f);
    private static readonly Color ApDotEmpty = new(1.0f, 0.847f, 0.42f, 0.18f);

    private static readonly Color TimelineAllyRing = new(0.439f, 0.706f, 1.0f, 1.0f);
    private static readonly Color TimelineEnemyRing = new(1.0f, 0.353f, 0.353f, 1.0f);
    private static readonly Color TimelineActiveRing = new(1.0f, 0.847f, 0.42f, 1.0f);

    public static Color PANEL_BG() => PanelBg;

    public static Color PANEL_BG_ALT() => PanelBgAlt;

    public static Color PANEL_BG_DEEP() => PanelBgDeep;

    public static Color PANEL_EDGE() => PanelEdge;

    public static Color PANEL_EDGE_SOFT() => PanelEdgeSoft;

    public static Color PANEL_EDGE_GLOW() => PanelEdgeGlow;

    public static Color PANEL_SHADOW() => PanelShadow;

    public static Color CHIP_BG() => ChipBg;

    public static Color CHIP_EDGE() => ChipEdge;

    public static Color TEXT_PRIMARY() => TextPrimary;

    public static Color TEXT_SECONDARY() => TextSecondary;

    public static Color TEXT_MUTED() => TextMuted;

    public static Color TEXT_ACCENT() => TextAccent;

    public static Color FATE_CALM() => FateCalm;

    public static Color FATE_GATE() => FateGate;

    public static Color FATE_WARNING() => FateWarning;

    public static Color FATE_DANGER() => FateDanger;

    public static Color FATE_HIGH_THREAT() => FateHighThreat;

    public static Color FATE_MERCY() => FateMercy;

    public static Color RESOURCE_HP() => ResourceHp;

    public static Color RESOURCE_STAMINA() => ResourceStamina;

    public static Color RESOURCE_MP() => ResourceMp;

    public static Color RESOURCE_AURA() => ResourceAura;

    public static Color AP_DOT_FILL() => ApDotFill;

    public static Color AP_DOT_EMPTY() => ApDotEmpty;

    public static int TOPBAR_HEIGHT() => 48;

    public static int TOPBAR_RADIUS() => 0;

    public static int PANEL_RADIUS_LARGE() => 8;

    public static int PANEL_RADIUS_MEDIUM() => 6;

    public static int PANEL_RADIUS_SMALL() => 4;

    public static int PANEL_RADIUS_TINY() => 3;

    public static int PANEL_BORDER() => 1;

    public static int PANEL_CONTENT_MARGIN() => 10;

    public static int SKILL_SLOT_SIZE() => 72;

    public static int SKILL_GLOW_BAND_HEIGHT() => 3;

    public static int PROGRESS_BAR_HEIGHT_PRIMARY() => 18;

    public static int PROGRESS_BAR_HEIGHT_SECONDARY() => 18;

    public static int TIMELINE_ENTRY_SIZE() => 26;

    public static int TIMELINE_HP_BAND_HEIGHT() => 3;

    public static int TIMELINE_SEPARATION() => 4;

    public static Color TIMELINE_ALLY_RING() => TimelineAllyRing;

    public static Color TIMELINE_ENEMY_RING() => TimelineEnemyRing;

    public static Color TIMELINE_ACTIVE_RING() => TimelineActiveRing;

    public static float TIMELINE_INACTIVE_ALPHA() => 0.5f;

    public static int FONT_TITLE() => 18;

    public static int FONT_HEADING() => 14;

    public static int FONT_BODY() => 12;

    public static int FONT_LABEL() => 11;

    public static int FONT_CAPTION() => 10;

    public static int FONT_GLYPH() => 26;

    public static Color FateColor(StringName tone)
    {
        if (tone == "calm")
            return FateCalm;
        if (tone == "warning")
            return FateWarning;
        if (tone == "danger")
            return FateDanger;
        if (tone == "high_threat")
            return FateHighThreat;
        if (tone == "mercy")
            return FateMercy;
        return FateGate;
    }
}
