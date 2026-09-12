#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class EquipmentClosureGenerationBattleSimScenarioFactory
{
    private static readonly Vector2I AllyCoord = new(1, 1);
    private static readonly Vector2I EnemyCoord = new(5, 1);

    internal static ItemDefinition? SelectBaselineItem(
        ItemDefinition candidate,
        ContentSnapshot processSnapshot
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(processSnapshot);
        StringName candidateSlot = FirstSlot(candidate);
        IEnumerable<ItemDefinition> compatible = processSnapshot.Items.Values.Where(item =>
            item != null
            && item.ItemId != candidate.ItemId
            && item.IsEquipment()
            && item.EquipmentTypeId == candidate.EquipmentTypeId
            && item.GetEquipmentSlotIdsTyped().Contains(candidateSlot)
            && (
                !candidate.IsWeapon()
                || item.WeaponProfile?.Family == candidate.WeaponProfile?.Family
            )
        );
        return compatible
            .OrderBy(PowerSurface)
            .ThenBy(item => item.ItemId.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    internal static BattleUnitState BuildProjectedAlly(
        string label,
        ItemDefinition? equippedItem,
        EquipmentClosureGenerationProjectedContent content,
        ContentSnapshot processSnapshot,
        EquipmentGenerationBattleSimFixtureDefinition fixture
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(processSnapshot);
        ArgumentNullException.ThrowIfNull(fixture);
        PartyState partyState = BuildPartyState("equipment_probe");
        PartyMemberState member = partyState.GetMemberState("equipment_probe");
        StringName entrySlot = "";
        if (equippedItem != null)
        {
            entrySlot = FirstSlot(equippedItem);
            if (entrySlot == "")
                throw new InvalidOperationException(
                    $"Equipment candidate {equippedItem.ItemId} has no entry slot."
                );
            bool equipped = member.equipment_state.SetEquippedEntry(
                entrySlot,
                equippedItem.ItemId,
                equippedItem.GetFinalOccupiedSlotIdsTyped(entrySlot),
                EquipmentInstanceState.CreateInstance(
                    equippedItem.ItemId,
                    new StringName($"equipment_generation::{label}")
                )
            );
            if (!equipped)
                throw new InvalidOperationException(
                    $"Equipment candidate {equippedItem.ItemId} could not be installed in {entrySlot}."
                );
        }

        using var characterManagement = new CharacterManagementModule();
        characterManagement.setup(
            partyState,
            processSnapshot.Skills,
            processSnapshot.Professions,
            processSnapshot.Achievements,
            content.CombinedItems,
            processSnapshot.Quests,
            content.CombinedTraits,
            null,
            new ProgressionIdentityCatalogData(),
            content.CombinedGearSets
        );
        using var projectionRuntime = new BattleRuntimeModule();
        projectionRuntime.setup(
            characterManagement,
            processSnapshot.Skills,
            item_defs: content.CombinedItems,
            trait_defs: content.CombinedTraits,
            equipment_ability_bindings: content.CombinedEquipmentAbilityBindings,
            basic_attack_skill_id: processSnapshot
                .GameplayConfiguration
                .BattleSkillRoles
                .BasicAttackSkillId
        );
        IReadOnlyList<BattleUnitState> units =
            projectionRuntime._unit_factory.BuildAllyUnits(partyState, null);
        if (units.Count != 1 || units[0] == null)
            throw new InvalidOperationException(
                "Equipment generation projection must produce exactly one ally unit."
            );
        BattleUnitState unit = units[0];
        if (
            equippedItem != null
            && unit.GetEquipmentView().GetEquippedItemId(entrySlot) != equippedItem.ItemId
        )
        {
            throw new InvalidOperationException(
                $"Projected unit did not retain equipment candidate {equippedItem.ItemId}."
            );
        }
        ConfigureCombatUnit(unit, label, "player", AllyCoord, fixture);
        return unit;
    }

    internal static BattleUnitState BuildControlEnemy(
        string label,
        EquipmentGenerationBattleSimFixtureDefinition fixture
    )
    {
        var unit = new BattleUnitState();
        ConfigureCombatUnit(unit, label, "hostile", EnemyCoord, fixture);
        return unit;
    }

    internal static BattleSimScenarioDefinition Create(
        ItemDefinition candidate,
        ItemDefinition? equippedItem,
        BattleUnitState ally,
        bool candidateArm,
        IReadOnlyList<int> seeds,
        EquipmentGenerationBattleSimFixtureDefinition fixture
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(ally);
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(fixture);
        string suffix = candidateArm ? "candidate" : "baseline";
        BattleUnitState enemy = BuildControlEnemy(
            $"equipment_generation_{candidate.ItemId}_{suffix}_enemy",
            fixture
        );
        return new BattleSimScenarioDefinition(
            scenarioId: new StringName(
                $"equipment_generation_{candidate.ItemId}_{suffix}"
            ),
            displayName: $"Generated equipment {candidate.ItemId} {suffix}",
            description:
                $"Standardized equipment duel; equipped_item={equippedItem?.ItemId.ToString() ?? "none"}.",
            mapSize: new Vector2I(7, 3),
            terrainProfileId: "",
            useFormalTerrainGeneration: false,
            worldCoord: Vector2I.Zero,
            allyUnits: new[]
            {
                BattleSimScenarioUnitEntry.FromProjectedState(
                    ally,
                    $"equipment_generation.{candidate.ItemId}.{suffix}.ally"
                ),
            },
            enemyUnits: new[]
            {
                BattleSimScenarioUnitEntry.FromProjectedState(
                    enemy,
                    $"equipment_generation.{candidate.ItemId}.{suffix}.enemy"
                ),
            },
            authoringAllyUnitCount: 1,
            authoringEnemyUnitCount: 1,
            cells: new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>(),
            timelineTicksPerStep: 1,
            tuPerTick: 5,
            maxIterations: 2000,
            manualPolicy: "wait",
            traceEnabled: false,
            seeds: seeds
        );
    }

    private static PartyState BuildPartyState(StringName memberId)
    {
        var partyState = new PartyState();
        partyState.SetMemberState(new PartyMemberState
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress(),
            equipment_state = new EquipmentState(),
        });
        partyState.active_member_ids.Add(memberId);
        partyState.leader_member_id = memberId;
        return partyState;
    }

    private static void ConfigureCombatUnit(
        BattleUnitState unit,
        string label,
        StringName factionId,
        Vector2I coord,
        EquipmentGenerationBattleSimFixtureDefinition fixture
    )
    {
        unit.unit_id = new StringName(label);
        unit.source_member_id = new StringName(label);
        unit.display_name = label;
        unit.faction_id = factionId;
        unit.ControlModeKind = BattleUnitControlMode.Ai;
        unit.ai_brain_id = fixture.MeleeBrainId;
        unit.ai_state_id = "engage";
        if (!unit.SetBodySizeCategory("medium"))
            throw new InvalidOperationException("Standard BattleSim body size is invalid.");
        unit.SetAnchorCoord(coord);
        AttributeSnapshot attributes = unit.attribute_snapshot;
        attributes.SetValue("strength", 16);
        attributes.SetValue("agility", 14);
        attributes.SetValue("constitution", 16);
        attributes.SetValue("perception", 14);
        attributes.SetValue("intelligence", 12);
        attributes.SetValue("willpower", 14);
        attributes.SetValue("hp_max", 180);
        attributes.SetValue("mp_max", 120);
        attributes.SetValue("stamina_max", 120);
        attributes.SetValue("aura_max", 120);
        attributes.SetValue("action_points", 2);
        attributes.SetValue("action_threshold", 40);
        attributes.SetValue("attack_bonus", 7);
        attributes.SetValue("base_attack_bonus", 4);
        attributes.SetValue("armor_class", 18);
        attributes.SetValue(AttributeContentRules.ArmorAcBonus, 6);
        attributes.SetValue(AttributeContentRules.ShieldAcBonus, 0);
        attributes.SetValue(AttributeContentRules.DodgeBonus, 2);
        attributes.SetValue(AttributeContentRules.DeflectionBonus, 0);
        unit.SetActionThresholdTyped(40);
        unit.UnlockCombatResource("mp");
        unit.UnlockCombatResource("stamina");
        unit.UnlockCombatResource("aura");
        unit.SetCombatResources(
            hp: 180,
            mp: 120,
            stamina: 120,
            aura: 120,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn
        );
        unit.SetKnownActiveSkillIds(new[] { fixture.BasicAttackSkillId });
        unit.SetKnownSkillLevelTyped(fixture.BasicAttackSkillId, 1, preserveZero: true);
    }

    private static int PowerSurface(ItemDefinition item) =>
        item.TraitIds.Count * 8
        + item.TraitRollGroups.Count * 8
        + item.AttributeModifiers.Count * 4
        + (item.WeaponProfile?.OneHandedDice?.DiceCount ?? 0)
            * (item.WeaponProfile?.OneHandedDice?.DiceSides ?? 0)
        + (item.WeaponProfile?.TwoHandedDice?.DiceCount ?? 0)
            * (item.WeaponProfile?.TwoHandedDice?.DiceSides ?? 0);

    private static StringName FirstSlot(ItemDefinition item)
    {
        List<StringName> slots = item.GetEquipmentSlotIdsTyped();
        return slots.Count > 0 ? slots[0] : new StringName("");
    }
}
