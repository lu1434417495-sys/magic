#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class BattleSimContentDefinitionProjector
{
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName MpMax = "mp_max";
    private static readonly StringName StaminaMax = "stamina_max";
    private static readonly StringName AuraMax = "aura_max";
    private static readonly StringName ActionPoints = "action_points";
    private static readonly StringName ActionThreshold = "action_threshold";
    private static readonly StringName AttackBonus = "attack_bonus";
    private static readonly StringName ArmorClass = "armor_class";
    private static readonly StringName SpellProficiencyBonus = "spell_proficiency_bonus";
    private static readonly StringName[] BaseAttributeIds =
    {
        "strength", "agility", "constitution", "perception", "intelligence", "willpower",
    };
    private static readonly StringName[] AcComponentAttributeIds =
    {
        AttributeContentRules.ArmorAcBonus,
        AttributeContentRules.ShieldAcBonus,
        AttributeContentRules.DodgeBonus,
        AttributeContentRules.DeflectionBonus,
    };
    private const int InitialHpBase = 14;

    internal static BattleSimScenarioDefinition ProjectScenario(
        BattleSimScenarioImportModel source,
        string sourceLabel
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        var allies = new List<BattleSimScenarioUnitEntry>(source.AllyUnits.Count);
        for (int index = 0; index < source.AllyUnits.Count; index++)
        {
            allies.Add(new BattleSimScenarioUnitEntry(
                ProjectUnitDefinition(
                    source.AllyUnits[index],
                    "player",
                    "manual",
                    $"{sourceLabel}.ally_units[{index}]"
                )
            ));
        }
        var enemies = new List<BattleSimScenarioUnitEntry>(source.EnemyUnits.Count);
        for (int index = 0; index < source.EnemyUnits.Count; index++)
        {
            enemies.Add(new BattleSimScenarioUnitEntry(
                ProjectUnitDefinition(
                    source.EnemyUnits[index],
                    "hostile",
                    "ai",
                    $"{sourceLabel}.enemy_units[{index}]"
                )
            ));
        }
        IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> cells =
            source.UseFormalTerrainGeneration
                ? new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>()
                : BuildCells(source, sourceLabel);
        IReadOnlyList<int> seeds = source.Seeds.Count == 0 ? new[] { 101 } : source.Seeds;
        return new BattleSimScenarioDefinition(
            source.ScenarioId,
            source.DisplayName,
            source.Description,
            new Vector2I(source.MapSize.X, source.MapSize.Y),
            source.TerrainProfileId,
            source.UseFormalTerrainGeneration,
            new Vector2I(source.WorldCoord.X, source.WorldCoord.Y),
            allies,
            enemies,
            source.AllyUnits.Count,
            source.EnemyUnits.Count,
            cells,
            source.TimelineTicksPerStep,
            source.TuPerTick,
            source.MaxIterations,
            source.ManualPolicy,
            source.TraceEnabled,
            seeds
        );
    }

    internal static BattleSimUnitDefinition ProjectUnitDefinition(
        BattleSimUnitImportModel source,
        StringName defaultFactionId,
        StringName defaultControlMode,
        string sourceLabel
    ) => BattleSimUnitDefinition.FromProjectedState(
        ProjectUnitState(source, defaultFactionId, defaultControlMode),
        sourceLabel
    );

    private static BattleUnitState ProjectUnitState(
        BattleSimUnitImportModel source,
        StringName defaultFactionId,
        StringName defaultControlMode
    )
    {
        StringName configuredControlMode = source.ControlMode;
        StringName resolvedDefaultControl = IsEmpty(defaultControlMode)
            ? BattleTypedNames.ControlModeManual
            : defaultControlMode;
        var state = new BattleUnitState
        {
            unit_id = source.UnitId,
            source_member_id = source.SourceMemberId,
            display_name = string.IsNullOrEmpty(source.DisplayName) ? source.UnitId : source.DisplayName,
            faction_id = string.IsNullOrWhiteSpace(source.FactionId) ? defaultFactionId : new StringName(source.FactionId),
            ControlModeKind = BattleTypedNames.ToControlMode(IsEmpty(configuredControlMode) ? resolvedDefaultControl : configuredControlMode),
            ai_brain_id = source.AiBrainId,
            ai_state_id = source.AiStateId,
        };
        if (!state.SetBodySizeCategory(source.BodySizeCategory))
            throw new InvalidOperationException($"BattleSim unit '{source.UnitId}' has invalid body_size_category '{source.BodySizeCategory}'.");
        state.SetAnchorCoord(new Vector2I(source.Coord.X, source.Coord.Y));
        ApplyAttributeDefaults(state, source);
        ApplyAttributeOverrides(state, source);
        state.SetCombatResources(
            Math.Clamp(source.CurrentHp, 0, Math.Max(GetSnapshotValue(state.attribute_snapshot, HpMax), 1)),
            Math.Clamp(source.CurrentMp, 0, Math.Max(GetSnapshotValue(state.attribute_snapshot, MpMax), 0)),
            Math.Clamp(source.CurrentStamina, 0, Math.Max(GetSnapshotValue(state.attribute_snapshot, StaminaMax), 0)),
            Math.Clamp(source.CurrentAura, 0, Math.Max(GetSnapshotValue(state.attribute_snapshot, AuraMax), 0)),
            Math.Clamp(source.CurrentAp, 0, Math.Max(GetSnapshotValue(state.attribute_snapshot, ActionPoints), 1)),
            Math.Clamp(source.CurrentMovePoints, 0, BattleUnitState.DefaultMovePointsPerTurn)
        );
        int threshold = GetSnapshotValue(state.attribute_snapshot, ActionThreshold);
        state.SetActionThresholdTyped(threshold > 0 ? threshold : Math.Max(source.ActionThreshold, 1));
        var skillIds = new List<StringName>();
        foreach (string raw in source.SkillIds)
            if (!string.IsNullOrWhiteSpace(raw)) skillIds.Add(raw);
        state.SetKnownActiveSkillIds(skillIds);
        foreach (StringName skillId in skillIds)
        {
            int level = source.SkillLevelMap.TryGetValue(skillId.ToString(), out int configured) ? configured : 1;
            state.SetKnownSkillLevelTyped(skillId, level, preserveZero: true);
        }
        foreach (string tag in source.MovementTags)
            if (!string.IsNullOrWhiteSpace(tag)) state.AddMovementTagTyped(tag);
        foreach (string resourceId in source.UnlockedCombatResourceIds)
            if (!string.IsNullOrWhiteSpace(resourceId)) state.UnlockCombatResource(resourceId);
        if (source.WeaponProjection is not null)
            state.ApplyWeaponProjectionTyped(ProjectWeapon(source.WeaponProjection));
        return state;
    }

    private static void ApplyAttributeDefaults(BattleUnitState state, BattleSimUnitImportModel source)
    {
        if (source.BaseAttributes.Count > 0)
        {
            state.attribute_snapshot = BuildFormalAttributeSnapshot(source);
            return;
        }
        SetSnapshotValue(state.attribute_snapshot, HpMax, Math.Max(source.CurrentHp, 1));
        SetSnapshotValue(state.attribute_snapshot, MpMax, Math.Max(source.CurrentMp, 0));
        SetSnapshotValue(state.attribute_snapshot, StaminaMax, Math.Max(source.CurrentStamina, 0));
        SetSnapshotValue(state.attribute_snapshot, AuraMax, Math.Max(source.CurrentAura, 0));
        SetSnapshotValue(state.attribute_snapshot, ActionPoints, Math.Max(source.CurrentAp, 1));
        SetSnapshotValue(state.attribute_snapshot, ActionThreshold, Math.Max(source.ActionThreshold, 1));
        SetSnapshotValue(state.attribute_snapshot, AttackBonus, GetOverride(source, "attack_bonus", 4));
        SetSnapshotValue(state.attribute_snapshot, AttributeContentRules.ArmorAcBonus, GetOverride(source, AttributeContentRules.ArmorAcBonus, 0));
        SetSnapshotValue(state.attribute_snapshot, AttributeContentRules.ShieldAcBonus, GetOverride(source, AttributeContentRules.ShieldAcBonus, 0));
        SetSnapshotValue(state.attribute_snapshot, AttributeContentRules.DodgeBonus, GetOverride(source, AttributeContentRules.DodgeBonus, 0));
        SetSnapshotValue(state.attribute_snapshot, AttributeContentRules.DeflectionBonus, GetOverride(source, AttributeContentRules.DeflectionBonus, 0));
        SetSnapshotValue(state.attribute_snapshot, SpellProficiencyBonus, GetOverride(source, "spell_proficiency_bonus", 2));
    }

    private static AttributeSnapshot BuildFormalAttributeSnapshot(BattleSimUnitImportModel source)
    {
        var progress = new UnitProgress();
        int constitution = GetBase(source, "constitution", 10);
        foreach (StringName id in BaseAttributeIds)
            progress.unit_base_attributes?.SetAttributeValue(id, GetBase(source, id, 10));
        foreach (StringName id in AcComponentAttributeIds)
            if (HasOverride(source, id)) progress.unit_base_attributes?.SetAttributeValue(id, Math.Max(GetOverride(source, id, 0), 0));
        progress.unit_base_attributes?.SetAttributeValue(
            HpMax,
            HasOverride(source, HpMax)
                ? GetOverride(source, HpMax, source.CurrentHp)
                : Math.Max(1, InitialHpBase + AttributeSnapshot.CalculateScoreModifier(constitution) * 2)
        );
        if (HasOverride(source, ActionThreshold))
            progress.unit_base_attributes?.SetAttributeValue(ActionThreshold, GetOverride(source, ActionThreshold, source.ActionThreshold));
        var service = new AttributeService();
        service.Setup(progress);
        return service.GetSnapshot();
    }

    private static void ApplyAttributeOverrides(BattleUnitState state, BattleSimUnitImportModel source)
    {
        foreach ((string key, int value) in source.AttributeOverrides)
        {
            StringName id = key;
            if (id == ArmorClass || (source.BaseAttributes.Count > 0 && (id == HpMax || id == ActionThreshold))) continue;
            SetSnapshotValue(state.attribute_snapshot, id, value);
        }
    }

    private static IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> BuildCells(
        BattleSimScenarioImportModel source,
        string sourceLabel
    )
    {
        var cells = new Dictionary<Vector2I, BattleCellState>();
        for (int y = 0; y < source.MapSize.Y; y++)
        for (int x = 0; x < source.MapSize.X; x++)
        {
            var cell = new BattleCellState();
            cell.SetCoord(new Vector2I(x, y));
            cell.SetTerrain("land");
            cell.SetBaseHeight(4);
            cell.SetHeightOffset(0);
            cells[cell.coord] = cell;
        }
        foreach (BattleSimCellOverrideImportModel value in source.CellOverrides)
        {
            Vector2I coord = new(value.Coord.X, value.Coord.Y);
            if (!cells.TryGetValue(coord, out BattleCellState? cell))
            {
                cell = new BattleCellState();
                cell.SetCoord(coord);
            }
            if (value.BaseTerrain is not null) cell.SetTerrain(value.BaseTerrain);
            if (value.BaseHeight.HasValue) cell.SetBaseHeight(value.BaseHeight.Value);
            if (value.HeightOffset.HasValue) cell.SetHeightOffset(value.HeightOffset.Value);
            if (value.FlowDirection is not null) cell.flow_direction = new Vector2I(value.FlowDirection.X, value.FlowDirection.Y);
            if (value.TerrainEffectIds is not null)
            {
                cell.terrain_effect_ids.Clear();
                foreach (string id in value.TerrainEffectIds) cell.terrain_effect_ids.Add(id);
            }
            if (value.PropIds is not null)
            {
                cell.prop_ids.Clear();
                foreach (string id in value.PropIds) cell.prop_ids.Add(id);
            }
            cell.RecalculateRuntimeValues();
            cells[coord] = cell;
        }
        var snapshots = new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>();
        foreach ((Vector2I coord, BattleCellState cell) in cells)
        {
            snapshots[coord] = ContentValueNormalizer.NormalizeDictionary(
                RuntimePlainPayload.CloneDictionary(cell.BuildSnapshotPlain()),
                $"{sourceLabel}.cells[{coord}]"
            );
        }
        return snapshots;
    }

    private static WeaponProjection ProjectWeapon(BattleSimWeaponProjectionImportModel source) => new()
    {
        weapon_profile_kind = source.WeaponProfileKind,
        weapon_item_id = source.WeaponItemId,
        weapon_profile_type_id = source.WeaponProfileTypeId,
        weapon_range_type = source.WeaponRangeType,
        weapon_family = source.WeaponFamily,
        weapon_current_grip = source.WeaponCurrentGrip,
        weapon_attack_range = Math.Max(source.WeaponAttackRange, 0),
        weapon_one_handed_dice = new WeaponDice { dice_count = source.OneHandedDiceCount, dice_sides = source.OneHandedDiceSides },
        weapon_two_handed_dice = new WeaponDice { dice_count = source.TwoHandedDiceCount, dice_sides = source.TwoHandedDiceSides },
        weapon_is_versatile = source.WeaponIsVersatile,
        weapon_uses_two_hands = source.WeaponUsesTwoHands,
        weapon_physical_damage_tag = source.WeaponPhysicalDamageTag,
    };

    private static int GetBase(BattleSimUnitImportModel source, StringName id, int fallback) =>
        source.BaseAttributes.TryGetValue(id.ToString(), out int value) ? value : fallback;
    private static bool HasOverride(BattleSimUnitImportModel source, StringName id) =>
        source.AttributeOverrides.ContainsKey(id.ToString());
    private static int GetOverride(BattleSimUnitImportModel source, StringName id, int fallback) =>
        source.AttributeOverrides.TryGetValue(id.ToString(), out int value) ? value : fallback;
    private static int GetSnapshotValue(AttributeSnapshot snapshot, StringName id) => snapshot?.GetValue(id) ?? 0;
    private static void SetSnapshotValue(AttributeSnapshot snapshot, StringName id, int value) => snapshot?.SetValue(id, value);
    private static bool IsEmpty(StringName value) => value == null || value.ToString().Length == 0;
}
