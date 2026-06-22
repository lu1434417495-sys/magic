using System;
using System.Collections.Generic;
using Godot;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class BattleTestFixture : IDisposable
{
    private BattleTestFixture(
        BattleRuntimeModule runtime,
        BattleState state,
        IReadOnlyList<BattleUnitState> allies,
        IReadOnlyList<BattleUnitState> enemies
    )
    {
        Runtime = runtime;
        State = state;
        Allies = allies;
        Enemies = enemies;
    }

    public BattleRuntimeModule Runtime { get; }
    public BattleState State { get; }
    public IReadOnlyList<BattleUnitState> Allies { get; }
    public IReadOnlyList<BattleUnitState> Enemies { get; }

    public static BattleTestFixture CreateFlatBattle(
        StringName battleId,
        Vector2I mapSize,
        IEnumerable<BattleUnitState> allies,
        IEnumerable<BattleUnitState> enemies
    )
    {
        BattleState state = BuildFlatState(battleId, mapSize);
        List<BattleUnitState> allyList = CopyUnits(allies);
        List<BattleUnitState> enemyList = CopyUnits(enemies);
        InstallUnits(state, allyList, enemyList);

        var runtime = new BattleRuntimeModule();
        runtime.setup();
        runtime.SetupStateForTests(state);
        return new BattleTestFixture(runtime, state, allyList, enemyList);
    }

    public static BattleTestFixture CreateFlatBattle(StringName battleId, Vector2I mapSize)
    {
        return CreateFlatBattle(
            battleId,
            mapSize,
            Array.Empty<BattleUnitState>(),
            Array.Empty<BattleUnitState>()
        );
    }

    public static BattleState BuildFlatState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        state.SetCellsFromDictionary(BuildFlatCells(mapSize), duplicateCells: false);
        return state;
    }

    public static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentAp = 1,
        int currentHp = 0
    )
    {
        int resolvedHp = currentHp > 0 ? currentHp : (factionId == new StringName("enemy") ? 30 : 100);
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_ap = currentAp,
            current_move_points = 2,
            current_hp = resolvedHp,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue("hp_max", resolvedHp);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    public static void InstallUnits(
        BattleState state,
        IReadOnlyList<BattleUnitState> allyUnits,
        IReadOnlyList<BattleUnitState> enemyUnits
    )
    {
        state.ClearUnits();
        state.ally_unit_ids = new GStringNameArray();
        state.enemy_unit_ids = new GStringNameArray();
        var gridService = new BattleGridService();

        foreach (BattleUnitState unit in allyUnits ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            gridService.PlaceUnit(state, unit, unit.coord, ignore_height: true);
            state.ally_unit_ids.Add(unit.unit_id);
        }
        foreach (BattleUnitState unit in enemyUnits ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            gridService.PlaceUnit(state, unit, unit.coord, ignore_height: true);
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id =
            state.ally_unit_ids.Count > 0 ? state.ally_unit_ids[0] : new StringName("");
    }

    public void Dispose()
    {
        DisposeBattleFixture(Runtime, State);
    }

    public static void ConfigureDamageResolverForTests(
        BattleRuntimeModule runtime,
        BattleDamageResolver damageResolver
    )
    {
        if (runtime == null)
        {
            damageResolver?.Dispose();
            return;
        }

        BattleDamageResolver previousResolver = runtime.GetDamageResolver();
        runtime.ConfigureDamageResolverForTests(damageResolver);
        if (!ReferenceEquals(previousResolver, damageResolver))
            DisposeDamageResolver(previousResolver);
    }

    public static void ConfigureHitResolverForTests(
        BattleRuntimeModule runtime,
        BattleHitResolver hitResolver
    )
    {
        if (runtime == null)
        {
            hitResolver?.Dispose();
            return;
        }

        BattleHitResolver previousResolver = runtime.GetHitResolver();
        runtime.ConfigureHitResolverForTests(hitResolver);
        if (!ReferenceEquals(previousResolver, hitResolver))
            DisposeHitResolver(previousResolver);
    }

    public static void DisposeRuntime(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.SetupStateForTests(null);
        runtime.Dispose();
    }

    public static void DisposeBattleFixture(
        BattleRuntimeModule runtime,
        BattleState state,
        params GodotObject[] ownedObjects
    )
    {
        if (runtime != null)
            runtime.SetupStateForTests(null);

        if (ownedObjects != null)
        {
            foreach (GodotObject ownedObject in ownedObjects)
                DisposeFixtureObject(ownedObject);
        }
        DisposeBattleState(state);

        DisposeRuntime(runtime);
    }

    public static void DisposeBattleState(BattleState state)
    {
        if (state == null)
            return;
        if (!GodotObject.IsInstanceValid(state))
        {
            GodotSharpCleanup.DisposeGodotObject(state);
            return;
        }

        var units = new List<BattleUnitState>();
        var cells = new List<BattleCellState>();
        foreach (BattleUnitState unit in state.Units())
            units.Add(unit);
        foreach (BattleCellState cell in state.Cells())
            cells.Add(cell);

        foreach (BattleUnitState unit in units)
            DisposeBattleUnit(unit);
        foreach (BattleCellState cell in cells)
            DisposeBattleCell(cell);
        DisposeBattleTimeline(state.timeline);
        GodotSharpCleanup.DisposeGodotObject(state.party_backpack_view);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(state.attack_disadvantage_tags);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(state.ally_unit_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(state.enemy_unit_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(state.log_entries);
        GodotCollectionDisposer.DisposeOwnedPayloadTree(state.report_entries);
        GodotCollectionDisposer.DisposeOwnedPayloadTree(state.promotion_queue);
        state.ClearRuntimeEdgeFaces();
        state.ClearBattleTopology();
        GodotSharpCleanup.DisposeGodotObject(state);
    }

    public static void DisposeFixtureObject(GodotObject ownedObject)
    {
        switch (ownedObject)
        {
            case null:
                return;
            case BattlePreview preview:
                DisposeBattlePreview(preview);
                return;
            case BattleCommand command:
                DisposeBattleCommand(command);
                return;
            case BattleEventBatch batch:
                GodotSharpCleanup.DisposeGodotObject(batch);
                return;
            case EquipmentState equipmentState:
                equipmentState.Dispose();
                return;
            case EquipmentInstanceState equipmentInstance:
                equipmentInstance.Dispose();
                return;
            case BattleUnitState unit:
                DisposeBattleUnit(unit);
                return;
            case BattleEffectiveTraitInstanceState effectiveTraitInstance:
                DisposeBattleEffectiveTraitInstance(effectiveTraitInstance);
                return;
            case TraitInstanceState traitInstance:
                traitInstance.Dispose();
                return;
            case TraitRollValueState rollValue:
                GodotSharpCleanup.DisposeGodotObject(rollValue);
                return;
            case BattleCellState cell:
                DisposeBattleCell(cell);
                return;
            case SkillDef skill:
                DisposeSkill(skill);
                return;
            case QuestDef quest:
                GodotSharpCleanup.DisposeGodotObject(quest);
                return;
            case TraitDef trait:
                DisposeTrait(trait);
                return;
            case RaceDef race:
                DisposeRace(race);
                return;
            case SubraceDef subrace:
                DisposeSubrace(subrace);
                return;
            case AgeProfileDef ageProfile:
                DisposeAgeProfile(ageProfile);
                return;
            case AgeStageRule ageStageRule:
                DisposeAgeStageRule(ageStageRule);
                return;
            case StageAdvancementModifier stageAdvancementModifier:
                GodotSharpCleanup.DisposeGodotObject(stageAdvancementModifier);
                return;
            case AscensionDef ascension:
                DisposeAscension(ascension);
                return;
            case AscensionStageDef ascensionStage:
                DisposeAscensionStage(ascensionStage);
                return;
            case BloodlineDef bloodline:
                DisposeBloodline(bloodline);
                return;
            case BloodlineStageDef bloodlineStage:
                DisposeBloodlineStage(bloodlineStage);
                return;
            case ProfessionDef profession:
                DisposeProfession(profession);
                return;
            case ItemDef item:
                DisposeItem(item);
                return;
            case EquipmentRequirement equipmentRequirement:
                GodotSharpCleanup.DisposeGodotObject(equipmentRequirement);
                return;
            case AttributeModifier attributeModifier:
                GodotSharpCleanup.DisposeGodotObject(attributeModifier);
                return;
            case TraitRollGroupDef traitRollGroup:
                DisposeTraitRollGroup(traitRollGroup);
                return;
            case TraitRollGroupEntryDef traitRollGroupEntry:
                GodotSharpCleanup.DisposeGodotObject(traitRollGroupEntry);
                return;
            case WeaponProfileDef weaponProfile:
                DisposeWeaponProfile(weaponProfile);
                return;
            case WeaponDamageDiceDef weaponDamageDice:
                GodotSharpCleanup.DisposeGodotObject(weaponDamageDice);
                return;
            case MeteorSwarmProfile meteorSwarmProfile:
                DisposeMeteorSwarmProfile(meteorSwarmProfile);
                return;
            case MeteorSwarmImpactComponent meteorSwarmImpactComponent:
                GodotSharpCleanup.DisposeGodotObject(meteorSwarmImpactComponent);
                return;
            case CombatSkillDef combatSkill:
                DisposeCombatSkill(combatSkill);
                return;
            case CombatCastVariantDef castVariant:
                DisposeCombatCastVariant(castVariant);
                return;
            case CombatEffectDef effect:
                GodotSharpCleanup.DisposeGodotObject(effect);
                return;
            case EnemyTemplateDef template:
                DisposeEnemyTemplate(template);
                return;
            case DropEntryDef dropEntry:
                GodotSharpCleanup.DisposeGodotObject(dropEntry);
                return;
            case EnemyAiBrainDef brain:
                DisposeEnemyAiBrain(brain);
                return;
            case EnemyAiStateDef aiState:
                DisposeEnemyAiState(aiState);
                return;
            case EnemyAiTransitionRuleDef transitionRule:
                DisposeEnemyAiTransitionRule(transitionRule);
                return;
            case EnemyAiTransitionConditionDef transitionCondition:
                GodotSharpCleanup.DisposeGodotObject(transitionCondition);
                return;
            case EnemyAiGenerationSlotDef generationSlot:
                GodotSharpCleanup.DisposeGodotObject(generationSlot);
                return;
            case EnemyAiAction action:
                DisposeEnemyAiAction(action);
                return;
            default:
                GodotSharpCleanup.DisposeGodotObject(ownedObject);
                return;
        }
    }

    public static void DisposeBattleAiScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
            return;
        DisposeBattlePreview(scoreInput.preview);
        GodotSharpCleanup.DisposeGodotObject(scoreInput.command);
        scoreInput.preview = null;
        scoreInput.command = null;
        scoreInput.skill_def = null;
    }

    public static void DisposeBattlePreview(BattlePreview preview)
    {
        if (preview == null)
            return;
        if (!GodotObject.IsInstanceValid(preview))
        {
            GodotSharpCleanup.DisposeGodotObject(preview);
            return;
        }
        GodotSharpCleanup.DisposeGodotObject(preview.hit_preview);
        GodotSharpCleanup.DisposeGodotObject(preview);
    }

    public static void DisposeBattleCommand(BattleCommand command)
    {
        if (command == null)
            return;
        if (GodotObject.IsInstanceValid(command))
        {
            command.equipment_instance?.Dispose();
            command.equipment_instance = null;
        }
        GodotSharpCleanup.DisposeGodotObject(command);
    }

    public static void DisposeItem(ItemDef item)
    {
        if (item == null)
            return;
        if (GodotObject.IsInstanceValid(item))
        {
            foreach (TraitRollGroupDef traitRollGroup in item.trait_roll_groups)
                DisposeTraitRollGroup(traitRollGroup);
            foreach (AttributeModifier attributeModifier in item.attribute_modifiers)
                GodotSharpCleanup.DisposeGodotObject(attributeModifier);
            DisposeFixtureObject(item.equip_requirement);
            DisposeFixtureObject(item.weapon_profile);
            item.trait_roll_groups?.Clear();
            item.attribute_modifiers?.Clear();
            item.equip_requirement = null;
            item.weapon_profile = null;
        }
        GodotSharpCleanup.DisposeGodotObject(item);
    }

    public static void DisposeTraitRollGroup(TraitRollGroupDef traitRollGroup)
    {
        if (traitRollGroup == null)
            return;
        if (GodotObject.IsInstanceValid(traitRollGroup))
        {
            foreach (TraitRollGroupEntryDef entry in traitRollGroup.entries)
                GodotSharpCleanup.DisposeGodotObject(entry);
            traitRollGroup.entries?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(traitRollGroup);
    }

    public static void DisposeWeaponProfile(WeaponProfileDef weaponProfile)
    {
        if (weaponProfile == null)
            return;
        if (GodotObject.IsInstanceValid(weaponProfile))
        {
            GodotSharpCleanup.DisposeGodotObject(weaponProfile.one_handed_dice);
            GodotSharpCleanup.DisposeGodotObject(weaponProfile.two_handed_dice);
            weaponProfile.one_handed_dice = null;
            weaponProfile.two_handed_dice = null;
        }
        GodotSharpCleanup.DisposeGodotObject(weaponProfile);
    }

    public static void DisposeMeteorSwarmProfile(MeteorSwarmProfile profile)
    {
        if (profile == null)
            return;
        if (GodotObject.IsInstanceValid(profile))
        {
            foreach (MeteorSwarmImpactComponent component in profile.impact_components)
                GodotSharpCleanup.DisposeGodotObject(component);
            profile.impact_components?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(profile);
    }

    public static void DisposeBattleUnit(BattleUnitState unit)
    {
        if (unit == null)
            return;
        if (!GodotObject.IsInstanceValid(unit))
        {
            GodotSharpCleanup.DisposeGodotObject(unit);
            return;
        }

        foreach (BattleStatusEffectState statusEffect in unit.GetStatusEffectsTyped())
            GodotSharpCleanup.DisposeGodotObject(statusEffect);
        foreach (BattleEffectiveTraitInstanceState traitInstance in unit.effective_trait_instances)
            DisposeBattleEffectiveTraitInstance(traitInstance);
        GodotSharpCleanup.DisposeGodotObject(unit.ai_blackboard);
        GodotSharpCleanup.DisposeGodotObject(unit.attribute_snapshot);
        GodotSharpCleanup.DisposeGodotObject(unit.equipment_view);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.occupied_coords);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.unlocked_combat_resource_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.known_active_skill_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.movement_tags);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.vision_tags);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.proficiency_tags);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.save_advantage_tags);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.effective_trait_instances);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(unit.effective_trait_ids);
        GodotSharpCleanup.DisposeGodotObject(unit);
    }

    public static void DisposeBattleEffectiveTraitInstance(
        BattleEffectiveTraitInstanceState traitInstance
    )
    {
        if (traitInstance == null)
            return;
        if (GodotObject.IsInstanceValid(traitInstance))
        {
            DisposeGodotObjectArray(traitInstance.roll_values);
            GodotCollectionDisposer.DisposeOwnedCollectionWrapper(traitInstance.roll_values);
        }
        GodotSharpCleanup.DisposeGodotObject(traitInstance);
    }

    private static void DisposeBattleTimeline(BattleTimelineState timeline)
    {
        if (timeline == null)
            return;
        if (GodotObject.IsInstanceValid(timeline))
        {
            GodotCollectionDisposer.DisposeOwnedCollectionWrapper(timeline.ready_unit_ids);
        }
        GodotSharpCleanup.DisposeGodotObject(timeline);
    }

    public static void DisposeBattleCell(BattleCellState cell)
    {
        if (cell == null)
            return;
        if (!GodotObject.IsInstanceValid(cell))
        {
            GC.SuppressFinalize(cell);
            return;
        }

        foreach (BattleTerrainEffectState timedEffect in cell.timed_terrain_effects)
            GodotSharpCleanup.DisposeGodotObject(timedEffect);
        GodotSharpCleanup.DisposeGodotObject(cell.edge_feature_east);
        GodotSharpCleanup.DisposeGodotObject(cell.edge_feature_south);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(cell.prop_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(cell.terrain_effect_ids);
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(cell.timed_terrain_effects);
        cell.edge_feature_east = null;
        cell.edge_feature_south = null;
        // Cell wrappers are also referenced by projection payload stores; those native
        // references can be released before this helper sees the C# wrapper.
        GC.SuppressFinalize(cell);
    }

    public static void DisposeBattleLayout(GDictionary layout)
    {
        if (layout == null)
            return;
        DisposeBattleCellsFromDictionary(layout, "cells");
        if (layout.TryReadValue("cell_columns", out Variant columnsValue))
        {
            try
            {
                if (columnsValue.VariantType == Variant.Type.Dictionary)
                {
                    GDictionary columns = columnsValue.AsGodotDictionary();
                    try
                    {
                        foreach (Variant columnValue in columns.Values)
                        {
                            try
                            {
                                if (columnValue.VariantType != Variant.Type.Array)
                                    continue;
                                Godot.Collections.Array column = columnValue.AsGodotArray();
                                try
                                {
                                    foreach (Variant cellValue in column)
                                    {
                                        try
                                        {
                                            if (cellValue.VariantType == Variant.Type.Object)
                                            {
                                                DisposeBattleCell(
                                                    cellValue.AsGodotObject()
                                                        as BattleCellState
                                                );
                                            }
                                        }
                                        finally
                                        {
                                            cellValue.Dispose();
                                        }
                                    }
                                }
                                finally
                                {
                                    GodotCollectionDisposer.DisposeWrapperOnly(column);
                                }
                            }
                            finally
                            {
                                columnValue.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        GodotCollectionDisposer.DisposeWrapperOnly(columns);
                    }
                }
            }
            finally
            {
                columnsValue.Dispose();
            }
        }
        GodotCollectionDisposer.DisposeOwnedCollectionWrapper(layout);
    }

    private static void DisposeBattleCellsFromDictionary(GDictionary owner, object key)
    {
        if (owner == null || !owner.TryReadValue(key, out Variant cellsValue))
            return;
        try
        {
            if (cellsValue.VariantType != Variant.Type.Dictionary)
                return;
            GDictionary cells = cellsValue.AsGodotDictionary();
            try
            {
                foreach (Variant cellValue in cells.Values)
                {
                    try
                    {
                        if (cellValue.VariantType == Variant.Type.Object)
                            DisposeBattleCell(cellValue.AsGodotObject() as BattleCellState);
                    }
                    finally
                    {
                        cellValue.Dispose();
                    }
                }
            }
            finally
            {
                GodotCollectionDisposer.DisposeWrapperOnly(cells);
            }
        }
        finally
        {
            cellsValue.Dispose();
        }
    }

    public static void DisposeSkill(SkillDef skill)
    {
        if (skill == null)
            return;
        if (GodotObject.IsInstanceValid(skill))
        {
            DisposeGodotObjectArray(skill.attribute_modifiers);
            DisposeCombatSkill(skill.combat_profile);
        }
        GodotSharpCleanup.DisposeGodotObject(skill);
    }

    public static void DisposeTrait(TraitDef trait)
    {
        if (trait == null)
            return;
        if (GodotObject.IsInstanceValid(trait))
        {
            DisposeGodotObjectArray(trait.attribute_modifiers);
            DisposeGodotObjectArray(trait.roll_value_schema);
            trait.attribute_modifiers?.Clear();
            trait.roll_value_schema?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(trait);
    }

    public static void DisposeAgeProfile(AgeProfileDef ageProfile)
    {
        if (ageProfile == null)
            return;
        if (GodotObject.IsInstanceValid(ageProfile))
        {
            DisposeGodotObjectArray(ageProfile.stage_rules);
            ageProfile.stage_rules?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(ageProfile);
    }

    public static void DisposeRace(RaceDef race)
    {
        if (race == null)
            return;
        if (GodotObject.IsInstanceValid(race))
        {
            DisposeGodotObjectArray(race.attribute_modifiers);
            DisposeGodotObjectArray(race.racial_granted_skills);
            race.attribute_modifiers?.Clear();
            race.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(race);
    }

    public static void DisposeSubrace(SubraceDef subrace)
    {
        if (subrace == null)
            return;
        if (GodotObject.IsInstanceValid(subrace))
        {
            DisposeGodotObjectArray(subrace.attribute_modifiers);
            DisposeGodotObjectArray(subrace.racial_granted_skills);
            subrace.attribute_modifiers?.Clear();
            subrace.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(subrace);
    }

    public static void DisposeAgeStageRule(AgeStageRule ageStageRule)
    {
        if (ageStageRule == null)
            return;
        if (GodotObject.IsInstanceValid(ageStageRule))
        {
            DisposeGodotObjectArray(ageStageRule.attribute_modifiers);
            ageStageRule.attribute_modifiers?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(ageStageRule);
    }

    public static void DisposeAscension(AscensionDef ascension)
    {
        if (ascension == null)
            return;
        if (GodotObject.IsInstanceValid(ascension))
        {
            DisposeGodotObjectArray(ascension.racial_granted_skills);
            ascension.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(ascension);
    }

    public static void DisposeAscensionStage(AscensionStageDef ascensionStage)
    {
        if (ascensionStage == null)
            return;
        if (GodotObject.IsInstanceValid(ascensionStage))
        {
            DisposeGodotObjectArray(ascensionStage.attribute_modifiers);
            DisposeGodotObjectArray(ascensionStage.racial_granted_skills);
            ascensionStage.attribute_modifiers?.Clear();
            ascensionStage.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(ascensionStage);
    }

    public static void DisposeBloodline(BloodlineDef bloodline)
    {
        if (bloodline == null)
            return;
        if (GodotObject.IsInstanceValid(bloodline))
        {
            DisposeGodotObjectArray(bloodline.attribute_modifiers);
            DisposeGodotObjectArray(bloodline.racial_granted_skills);
            bloodline.attribute_modifiers?.Clear();
            bloodline.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(bloodline);
    }

    public static void DisposeBloodlineStage(BloodlineStageDef bloodlineStage)
    {
        if (bloodlineStage == null)
            return;
        if (GodotObject.IsInstanceValid(bloodlineStage))
        {
            DisposeGodotObjectArray(bloodlineStage.attribute_modifiers);
            DisposeGodotObjectArray(bloodlineStage.racial_granted_skills);
            bloodlineStage.attribute_modifiers?.Clear();
            bloodlineStage.racial_granted_skills?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(bloodlineStage);
    }

    public static void DisposeProfession(ProfessionDef profession)
    {
        if (profession == null)
            return;
        if (GodotObject.IsInstanceValid(profession))
        {
            GodotSharpCleanup.DisposeGodotObject(profession.unlock_requirement);
            DisposeGodotObjectArray(profession.rank_requirements);
            DisposeGodotObjectArray(profession.granted_skills);
            DisposeGodotObjectArray(profession.attribute_modifiers);
            DisposeGodotObjectArray(profession.active_conditions);
            profession.unlock_requirement = null;
            profession.rank_requirements?.Clear();
            profession.granted_skills?.Clear();
            profession.attribute_modifiers?.Clear();
            profession.active_conditions?.Clear();
        }
        GodotSharpCleanup.DisposeGodotObject(profession);
    }

    public static void DisposeCombatSkill(CombatSkillDef combatSkill)
    {
        if (combatSkill == null)
            return;
        if (!GodotObject.IsInstanceValid(combatSkill))
        {
            GodotSharpCleanup.DisposeGodotObject(combatSkill);
            return;
        }

        DisposeEffectDefs(combatSkill.effect_defs);
        DisposeEffectDefs(combatSkill.passive_effect_defs);
        DisposeGodotObjectArray(combatSkill.cast_variants);
        GodotSharpCleanup.DisposeGodotObject(combatSkill);
    }

    public static void DisposeCombatCastVariant(CombatCastVariantDef castVariant)
    {
        if (castVariant == null)
            return;
        if (!GodotObject.IsInstanceValid(castVariant))
        {
            GodotSharpCleanup.DisposeGodotObject(castVariant);
            return;
        }

        DisposeEffectDefs(castVariant.effect_defs);
        GodotSharpCleanup.DisposeGodotObject(castVariant);
    }

    public static void DisposeEffectDefs(GCombatEffectArray effectDefs)
    {
        if (effectDefs == null)
            return;
        DisposeGodotObjectArray(effectDefs);
    }

    public static void DisposeEnemyAiBrain(EnemyAiBrainDef brain)
    {
        if (brain == null)
            return;
        if (!GodotObject.IsInstanceValid(brain))
        {
            GodotSharpCleanup.DisposeGodotObject(brain);
            return;
        }

        GodotSharpCleanup.DisposeGodotObject(brain.score_profile);
        DisposeGodotObjectArray(brain.states);
        DisposeGodotObjectArray(brain.transition_rules);
        GodotSharpCleanup.DisposeGodotObject(brain);
    }

    public static void DisposeEnemyTemplate(EnemyTemplateDef template)
    {
        if (template == null)
            return;
        if (!GodotObject.IsInstanceValid(template))
        {
            GodotSharpCleanup.DisposeGodotObject(template);
            return;
        }

        DisposeGodotObjectArray(template.drop_entries);
        template.drop_entries?.Clear();
        GodotSharpCleanup.DisposeGodotObject(template);
    }

    public static void DisposeEnemyAiState(EnemyAiStateDef aiState)
    {
        if (aiState == null)
            return;
        if (!GodotObject.IsInstanceValid(aiState))
        {
            GodotSharpCleanup.DisposeGodotObject(aiState);
            return;
        }

        DisposeGodotObjectArray(aiState.actions);
        DisposeGodotObjectArray(aiState.generation_slots);
        GodotSharpCleanup.DisposeGodotObject(aiState);
    }

    public static void DisposeEnemyAiTransitionRule(EnemyAiTransitionRuleDef rule)
    {
        if (rule == null)
            return;
        if (!GodotObject.IsInstanceValid(rule))
        {
            GodotSharpCleanup.DisposeGodotObject(rule);
            return;
        }

        DisposeGodotObjectArray(rule.conditions);
        GodotSharpCleanup.DisposeGodotObject(rule);
    }

    public static void DisposeEnemyAiAction(EnemyAiAction action)
    {
        GodotSharpCleanup.DisposeGodotObject(action);
    }

    public static void DisposeDamageResolver(BattleDamageResolver resolver)
    {
        if (resolver == null)
            return;
        resolver.Dispose();
    }

    public static void DisposeHitResolver(BattleHitResolver resolver)
    {
        resolver?.Dispose();
    }

    private static void DisposeGodotObjectArray<[MustBeVariant] T>(Godot.Collections.Array<T> values)
    {
        if (values == null)
            return;

        var objects = new List<GodotObject>();
        Godot.Collections.Array rawValues = (Godot.Collections.Array)values;
        foreach (Variant rawValue in rawValues)
        {
            if (rawValue.VariantType == Variant.Type.Object && rawValue.AsGodotObject() is GodotObject ownedObject)
                objects.Add(ownedObject);
        }

        foreach (GodotObject ownedObject in objects)
            DisposeFixtureObject(ownedObject);
    }

    private static GDictionary BuildFlatCells(Vector2I mapSize)
    {
        var cells = new GDictionary();
        for (int y = 0; y < Mathf.Max(mapSize.Y, 0); y++)
        {
            for (int x = 0; x < Mathf.Max(mapSize.X, 0); x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    base_terrain = "land",
                    base_height = 4,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                cells[coord] = cell;
            }
        }
        return cells;
    }

    private static List<BattleUnitState> CopyUnits(IEnumerable<BattleUnitState> units)
    {
        return units == null ? new List<BattleUnitState>() : new List<BattleUnitState>(units);
    }
}
