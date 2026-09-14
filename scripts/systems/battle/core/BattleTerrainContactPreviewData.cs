using Godot;

internal sealed record BattleTerrainContactPreviewData(
    StringName ContactMode,
    int SaveDc,
    StringName SaveAbility,
    int EffectiveTriggerCount,
    int DurationTu,
    bool RechecksFromInside,
    bool RequiresGroundContact
);
