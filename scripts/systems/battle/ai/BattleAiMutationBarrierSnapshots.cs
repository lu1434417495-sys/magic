using System;
using System.Collections.Generic;
using Godot;

internal sealed class LayeredBarrierFieldsSnapshot
{
    private readonly Dictionary<StringName, BarrierInstanceSnapshot> _barriers = new();

    public static LayeredBarrierFieldsSnapshot Capture(
        IEnumerable<KeyValuePair<StringName, BattleBarrierInstanceState>> source
    )
    {
        LayeredBarrierFieldsSnapshot result = new();
        if (source == null)
        {
            return result;
        }
        foreach (KeyValuePair<StringName, BattleBarrierInstanceState> entry in source)
        {
            result._barriers[entry.Key] = entry.Value == null
                ? null
                : BarrierInstanceSnapshot.Capture(entry.Value);
        }
        return result;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        foreach (KeyValuePair<StringName, BarrierInstanceSnapshot> entry in _barriers)
        {
            result.Set(
                BattleAiMutationSnapshotModel.StableKey(entry.Key),
                entry.Value == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(entry.Value.ToStableMap())
            );
        }
        return result;
    }
}

internal sealed class BarrierInstanceSnapshot
{
    private StringName _barrierInstanceId = "";
    private StringName _profileId = "";
    private string _displayName = "";
    private StringName _sourceUnitId = "";
    private StringName _sourceSkillId = "";
    private BarrierAnchorMode _anchorMode = BarrierAnchorMode.Fixed;
    private Vector2I _anchorCoord = Vector2I.Zero;
    private int _radiusCells;
    private StringName _areaPattern = "diamond";
    private int _remainingTu;
    private int _createdTu;
    private int _saveDc;
    private bool _catchAllProjectedEffects;
    private List<BarrierLayerSnapshot> _layers = new();

    public static BarrierInstanceSnapshot Capture(BattleBarrierInstanceState barrier)
    {
        BarrierInstanceSnapshot snapshot = new();
        if (barrier == null)
        {
            return snapshot;
        }
        snapshot._barrierInstanceId = barrier.BarrierInstanceId;
        snapshot._profileId = barrier.ProfileId;
        snapshot._displayName = barrier.DisplayName;
        snapshot._sourceUnitId = barrier.SourceUnitId;
        snapshot._sourceSkillId = barrier.SourceSkillId;
        snapshot._anchorMode = barrier.AnchorMode;
        snapshot._anchorCoord = barrier.AnchorCoord;
        snapshot._radiusCells = barrier.RadiusCells;
        snapshot._areaPattern = barrier.AreaPattern;
        snapshot._remainingTu = barrier.RemainingTu;
        snapshot._createdTu = barrier.CreatedTu;
        snapshot._saveDc = barrier.SaveDc;
        snapshot._catchAllProjectedEffects = barrier.CatchAllProjectedEffects;
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            snapshot._layers.Add(
                layer == null ? null : BarrierLayerSnapshot.Capture(layer)
            );
        }
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("barrier_instance_id", BattleAiMutationStableProjection.StableNullableStringName(_barrierInstanceId));
        result.Set("profile_id", BattleAiMutationStableProjection.StableNullableStringName(_profileId));
        result.Set("display_name", BattleAiMutationStableProjection.StableNullableText(_displayName));
        result.Set("source_unit_id", BattleAiMutationStableProjection.StableNullableStringName(_sourceUnitId));
        result.Set("source_skill_id", BattleAiMutationStableProjection.StableNullableStringName(_sourceSkillId));
        result.Set("anchor_mode", StableValue.FromInteger((int)_anchorMode));
        result.Set("anchor_coord", StableValue.FromVector2I(_anchorCoord));
        result.Set("radius_cells", StableValue.FromInteger(_radiusCells));
        result.Set("area_pattern", BattleAiMutationStableProjection.StableNullableStringName(_areaPattern));
        result.Set("remaining_tu", StableValue.FromInteger(_remainingTu));
        result.Set("created_tu", StableValue.FromInteger(_createdTu));
        result.Set("save_dc", StableValue.FromInteger(_saveDc));
        result.Set(
            "catch_all_projected_effects",
            StableValue.FromBool(_catchAllProjectedEffects)
        );
        List<StableValue> layers = new();
        foreach (BarrierLayerSnapshot layer in _layers)
        {
            layers.Add(
                layer == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(layer.ToStableMap())
            );
        }
        result.Set("layers", StableValue.FromArray(layers));
        return result;
    }
}

internal sealed class BarrierLayerSnapshot
{
    private StringName _layerId = "";
    private string _displayName = "";
    private int _order;
    private bool _broken;
    private List<StringName> _blockedCategories = new();
    private List<StringName> _breakerSkillIds = new();
    private List<BarrierOutcomeSnapshot> _passageOutcomes = new();
    private bool _hasSaveRollOverride;
    private int _saveRollOverride;

    public static BarrierLayerSnapshot Capture(BattleBarrierLayerState layer)
    {
        BarrierLayerSnapshot snapshot = new();
        if (layer == null)
        {
            return snapshot;
        }
        snapshot._layerId = layer.LayerId;
        snapshot._displayName = layer.DisplayName;
        snapshot._order = layer.Order;
        snapshot._broken = layer.Broken;
        snapshot._blockedCategories = BattleAiMutationStableProjection.StringNameArrayToList(layer.BlockedCategories);
        snapshot._breakerSkillIds = BattleAiMutationStableProjection.StringNameArrayToList(layer.BreakerSkillIds);
        snapshot._hasSaveRollOverride = layer.HasSaveRollOverride;
        snapshot._saveRollOverride = layer.SaveRollOverride;
        foreach (BattleBarrierOutcomeState outcome in layer.GetPassageOutcomesTyped())
        {
            snapshot._passageOutcomes.Add(
                outcome == null ? null : BarrierOutcomeSnapshot.Capture(outcome)
            );
        }
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("layer_id", BattleAiMutationStableProjection.StableNullableStringName(_layerId));
        result.Set("display_name", BattleAiMutationStableProjection.StableNullableText(_displayName));
        result.Set("order", StableValue.FromInteger(_order));
        result.Set("broken", StableValue.FromBool(_broken));
        result.Set("blocked_categories", StableValue.FromArray(BattleAiMutationStableProjection.StableStringNameList(_blockedCategories)));
        result.Set("breaker_skill_ids", StableValue.FromArray(BattleAiMutationStableProjection.StableStringNameList(_breakerSkillIds)));
        List<StableValue> outcomes = new();
        foreach (BarrierOutcomeSnapshot outcome in _passageOutcomes)
        {
            outcomes.Add(
                outcome == null
                    ? StableValue.Nil()
                    : StableValue.FromMap(outcome.ToStableMap())
            );
        }
        result.Set("passage_outcomes", StableValue.FromArray(outcomes));
        result.Set("has_save_roll_override", StableValue.FromBool(_hasSaveRollOverride));
        result.Set("save_roll_override", StableValue.FromInteger(_saveRollOverride));
        return result;
    }
}

internal sealed class BarrierOutcomeSnapshot
{
    private StringName _outcomeType = "";
    private int _amount;
    private StringName _damageTag = "";
    private bool _halfOnSuccess;
    private int _successAmount;
    private StringName _successDamageTag = "";
    private int _fatalDamage = 99999;
    private StringName _statusId = "";
    private StringName _saveAbility = "";
    private StringName _saveTag = "";
    private int _saveDc;

    public static BarrierOutcomeSnapshot Capture(BattleBarrierOutcomeState outcome)
    {
        BarrierOutcomeSnapshot snapshot = new();
        if (outcome == null)
        {
            return snapshot;
        }
        snapshot._outcomeType = outcome.OutcomeType;
        snapshot._amount = outcome.Amount;
        snapshot._damageTag = outcome.DamageTag;
        snapshot._halfOnSuccess = outcome.HalfOnSuccess;
        snapshot._successAmount = outcome.SuccessAmount;
        snapshot._successDamageTag = outcome.SuccessDamageTag;
        snapshot._fatalDamage = outcome.FatalDamage;
        snapshot._statusId = outcome.StatusId;
        snapshot._saveAbility = outcome.SaveAbility;
        snapshot._saveTag = outcome.SaveTag;
        snapshot._saveDc = outcome.SaveDc;
        return snapshot;
    }

    public StableMap ToStableMap()
    {
        StableMap result = new();
        result.Set("outcome_type", BattleAiMutationStableProjection.StableNullableStringName(_outcomeType));
        result.Set("amount", StableValue.FromInteger(_amount));
        result.Set("damage_tag", BattleAiMutationStableProjection.StableNullableStringName(_damageTag));
        result.Set("half_on_success", StableValue.FromBool(_halfOnSuccess));
        result.Set("success_amount", StableValue.FromInteger(_successAmount));
        result.Set("success_damage_tag", BattleAiMutationStableProjection.StableNullableStringName(_successDamageTag));
        result.Set("fatal_damage", StableValue.FromInteger(_fatalDamage));
        result.Set("status_id", BattleAiMutationStableProjection.StableNullableStringName(_statusId));
        result.Set("save_ability", BattleAiMutationStableProjection.StableNullableStringName(_saveAbility));
        result.Set("save_tag", BattleAiMutationStableProjection.StableNullableStringName(_saveTag));
        result.Set("save_dc", StableValue.FromInteger(_saveDc));
        return result;
    }
}
