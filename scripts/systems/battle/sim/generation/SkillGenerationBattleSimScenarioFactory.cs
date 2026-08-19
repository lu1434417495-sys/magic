#nullable enable

// Tooling-owned standardized fixture builder for the generated-skill gate.

using System;
using System.Collections.Generic;
using Godot;

internal static class SkillGenerationBattleSimScenarioFactory
{
    private static readonly StringName BasicAttackSkillId = "basic_attack";
    private static readonly StringName ArcaneMissileSkillId = "mage_arcane_missile";
    private static readonly StringName FireballSkillId = "mage_fireball";
    private static readonly StringName FrostBoltSkillId = "mage_frost_bolt";
    private static readonly StringName MeleeBrainId = "melee_aggressor";
    private static readonly StringName MageBrainId = "mage_controller";
    private static readonly StringName RangedArcherBrainId = "ranged_archer";
    private static readonly Vector2I AllyCoord = new(1, 1);
    private static readonly Vector2I EnemyCoord = new(5, 1);

    internal static BattleSimScenarioDefinition Create(
        SkillDefinition candidate,
        bool includeCandidate,
        IReadOnlyList<int> seeds
    )
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(seeds);
        StringName candidateId = candidate.SkillId;
        StringName benchmarkSkillId = ResolveBenchmarkSkillId(candidate);
        StringName allyBrainId = ResolveAllyBrain(candidate);
        StringName allyStateId = allyBrainId == MeleeBrainId ? "engage" : "pressure";
        ResourceCapacities resourceCapacities = ResolveResourceCapacities(candidate);
        BattleUnitState ally = BuildUnit(
            unitId: "generated_probe",
            factionId: "player",
            coord: AllyCoord,
            brainId: allyBrainId,
            stateId: allyStateId,
            skills: includeCandidate
                ? DistinctSkills(BasicAttackSkillId, candidateId)
                : DistinctSkills(BasicAttackSkillId, benchmarkSkillId),
            candidate: candidate,
            resourceCapacities: resourceCapacities
        );
        BattleUnitState enemy = BuildUnit(
            unitId: "generated_control",
            factionId: "hostile",
            coord: EnemyCoord,
            brainId: MeleeBrainId,
            stateId: "engage",
            skills: new[] { BasicAttackSkillId },
            candidate: null,
            resourceCapacities: ResourceCapacities.Standard
        );
        string suffix = includeCandidate ? "candidate" : "baseline";
        return new BattleSimScenarioDefinition(
            scenarioId: new StringName($"skill_generation_{candidateId}_{suffix}"),
            displayName: $"Generated skill {candidateId} {suffix}",
            description:
                $"Standardized generated-skill duel against same-surface benchmark {benchmarkSkillId}; same seeds and unit stats are used for both arms.",
            mapSize: new Vector2I(7, 3),
            terrainProfileId: "",
            useFormalTerrainGeneration: false,
            worldCoord: Vector2I.Zero,
            allyUnits: new[]
            {
                BattleSimScenarioUnitEntry.FromProjectedState(
                    ally,
                    $"skill_generation.{candidateId}.{suffix}.ally"
                ),
            },
            enemyUnits: new[]
            {
                BattleSimScenarioUnitEntry.FromProjectedState(
                    enemy,
                    $"skill_generation.{candidateId}.{suffix}.enemy"
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

    internal static bool IsSupportedCandidate(SkillDefinition candidate)
    {
        CombatSkillDefinition? combat = candidate?.CombatProfile;
        if (
            candidate?.SkillTypeKind != SkillTypeKind.Active
            || combat == null
            || combat.TargetFilterKind != BattleTargetFilter.Enemy
            || combat.TargetModeKind is not (BattleTargetMode.Unit or BattleTargetMode.Ground)
        )
        {
            return false;
        }
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (
                effect?.EffectKind
                    is BattleEffectKind.Damage
                    or BattleEffectKind.ChainDamage
                    or BattleEffectKind.Execute
                    or BattleEffectKind.GradedSaveExecute
            )
            {
                return true;
            }
        }
        foreach (CombatCastVariantDefinition variant in combat.CastVariants)
        {
            foreach (CombatEffectDefinition effect in variant.EffectDefinitions)
            {
                if (
                    effect?.EffectKind
                        is BattleEffectKind.Damage
                        or BattleEffectKind.ChainDamage
                        or BattleEffectKind.Execute
                        or BattleEffectKind.GradedSaveExecute
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        StringName brainId,
        StringName stateId,
        IReadOnlyList<StringName> skills,
        SkillDefinition? candidate,
        ResourceCapacities resourceCapacities
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            ControlModeKind = BattleUnitControlMode.Ai,
            ai_brain_id = brainId,
            ai_state_id = stateId,
        };
        if (!unit.SetBodySizeCategory("medium"))
            throw new InvalidOperationException("Standard BattleSim body size is invalid.");
        unit.SetAnchorCoord(coord);
        ConfigureAttributes(unit.attribute_snapshot, resourceCapacities);
        unit.SetActionThresholdTyped(40);
        unit.UnlockCombatResource("mp");
        unit.UnlockCombatResource("aura");
        unit.SetCombatResources(
            hp: 160,
            mp: resourceCapacities.Mp,
            stamina: resourceCapacities.Stamina,
            aura: resourceCapacities.Aura,
            ap: resourceCapacities.Ap,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn
        );
        unit.SetKnownActiveSkillIds(skills);
        foreach (StringName skillId in skills)
            unit.SetKnownSkillLevelTyped(skillId, 1, preserveZero: true);
        unit.ApplyWeaponProjectionTyped(BuildWeaponProjection(candidate));
        return unit;
    }

    private static void ConfigureAttributes(
        AttributeSnapshot snapshot,
        ResourceCapacities resourceCapacities
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.SetValue("strength", 14);
        snapshot.SetValue("agility", 14);
        snapshot.SetValue("constitution", 14);
        snapshot.SetValue("perception", 14);
        snapshot.SetValue("intelligence", 14);
        snapshot.SetValue("willpower", 14);
        snapshot.SetValue("hp_max", 160);
        snapshot.SetValue("mp_max", resourceCapacities.Mp);
        snapshot.SetValue("stamina_max", resourceCapacities.Stamina);
        snapshot.SetValue("aura_max", resourceCapacities.Aura);
        snapshot.SetValue("action_points", resourceCapacities.Ap);
        snapshot.SetValue("action_threshold", 40);
        snapshot.SetValue("attack_bonus", 7);
        snapshot.SetValue("spell_proficiency_bonus", 5);
        snapshot.SetValue("armor_class", 18);
        snapshot.SetValue(AttributeContentRules.ArmorAcBonus, 6);
        snapshot.SetValue(AttributeContentRules.ShieldAcBonus, 0);
        snapshot.SetValue(AttributeContentRules.DodgeBonus, 2);
        snapshot.SetValue(AttributeContentRules.DeflectionBonus, 0);
    }

    private static WeaponProjection BuildWeaponProjection(SkillDefinition? candidate)
    {
        CombatSkillDefinition? combat = candidate?.CombatProfile;
        StringName family = combat?.RequiredWeaponFamilies.Count > 0
            ? combat.RequiredWeaponFamilies[0]
            : "sword";
        bool ranged = family.ToString()
            is "bow" or "crossbow" or "sling" or "firearm";
        bool twoHanded = ranged || combat?.RequiresHeavyWeapon == true;
        return new WeaponProjection
        {
            weapon_profile_kind = "equipped",
            weapon_item_id = "skill_generation_standard_weapon",
            weapon_instance_id = "skill_generation_standard_weapon_instance",
            weapon_profile_type_id = combat?.RequiredWeaponTypeIds.Count > 0
                ? combat.RequiredWeaponTypeIds[0]
                : family,
            weapon_range_type = ranged ? "ranged" : "melee",
            weapon_family = family,
            weapon_current_grip = twoHanded ? "two_handed" : "one_handed",
            weapon_attack_range = ranged ? 5 : 1,
            weapon_one_handed_dice = new WeaponDice
            {
                dice_count = twoHanded ? 0 : 1,
                dice_sides = twoHanded ? 0 : 8,
            },
            weapon_two_handed_dice = new WeaponDice
            {
                dice_count = 1,
                dice_sides = ranged ? 8 : 10,
            },
            weapon_is_versatile = !ranged,
            weapon_uses_two_hands = twoHanded,
            weapon_is_heavy = combat?.RequiresHeavyWeapon == true,
            weapon_physical_damage_tag = ranged
                ? "physical_pierce"
                : "physical_slash",
        };
    }

    private static StringName ResolveAllyBrain(SkillDefinition candidate)
    {
        CombatSkillDefinition combat = candidate.CombatProfile;
        if (combat.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit)
            return RangedArcherBrainId;
        return combat.TargetModeKind == BattleTargetMode.Ground
            || combat.RangeValue > 1
            || combat.ProjectileKind == (StringName)"magical"
            ? MageBrainId
            : MeleeBrainId;
    }

    private static StringName ResolveBenchmarkSkillId(SkillDefinition candidate)
    {
        CombatSkillDefinition combat = candidate.CombatProfile;
        if (combat.TargetModeKind == BattleTargetMode.Ground)
            return FireballSkillId;
        if (combat.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit)
            return ArcaneMissileSkillId;
        return ResolveAllyBrain(candidate) == MageBrainId
            ? FrostBoltSkillId
            : BasicAttackSkillId;
    }

    private static ResourceCapacities ResolveResourceCapacities(
        SkillDefinition candidate
    )
    {
        CombatSkillDefinition combat = candidate.CombatProfile;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(1);
        int targetSlots = combat.TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit
            ? Math.Max(combat.MaxTargetCount, 1)
            : 1;
        return new ResourceCapacities(
            Ap: Math.Clamp(Math.Max(costs.ApCost, 2), 2, 8),
            Mp: ResolveResourceCapacity(
                costs.MpCost,
                combat.GetEffectiveMpCostPerTargetSlot(1),
                targetSlots
            ),
            Stamina: ResolveResourceCapacity(
                costs.StaminaCost,
                combat.GetEffectiveStaminaCostPerTargetSlot(1),
                targetSlots
            ),
            Aura: ResolveResourceCapacity(costs.AuraCost, 0, 1)
        );
    }

    private static int ResolveResourceCapacity(
        int baseCost,
        int perTargetSlotCost,
        int targetSlots
    )
    {
        long oneCastCost = Math.Max(baseCost, 0)
            + (long)Math.Max(perTargetSlotCost, 0) * Math.Max(targetSlots, 1);
        return (int)Math.Clamp(oneCastCost * 3L, 120L, 10000L);
    }

    private static IReadOnlyList<StringName> DistinctSkills(
        StringName first,
        StringName second
    ) => first == second ? new[] { first } : new[] { first, second };

    private readonly record struct ResourceCapacities(
        int Ap,
        int Mp,
        int Stamina,
        int Aura
    )
    {
        internal static ResourceCapacities Standard { get; } =
            new(2, 120, 120, 120);
    }
}
