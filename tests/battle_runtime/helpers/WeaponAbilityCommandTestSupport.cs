using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class WeaponAbilityCommandTestSupport
{
    internal static readonly StringName BasicAttackSkillId = "basic_attack";

    internal static void PrimeBasicAttack(BattleUnitState unit, int skillLevel = 1)
    {
        if (unit == null)
            return;

        unit.AddKnownActiveSkill(BasicAttackSkillId);
        unit.SetKnownSkillLevelTyped(BasicAttackSkillId, Math.Max(skillLevel, 1));
        unit.SetCombatResources(
            Math.Max(unit.GetCurrentHp(), unit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 30),
            Math.Max(unit.GetCurrentMp(), 0),
            Math.Max(unit.GetCurrentStamina(), 30),
            Math.Max(unit.GetCurrentAura(), 0),
            Math.Max(unit.GetCurrentAp(), 2),
            Math.Max(unit.GetCurrentMovePoints(), 2)
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.HP_MAX,
            Math.Max(unit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? unit.GetCurrentHp(), unit.GetCurrentHp())
        );
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, Math.Max(unit.GetCurrentStamina(), 30));
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
    }

    internal static void PrimeActionResources(BattleUnitState unit, int ap = 2)
    {
        if (unit == null)
            return;

        unit.SetCombatResources(
            Math.Max(unit.GetCurrentHp(), unit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? 30),
            Math.Max(unit.GetCurrentMp(), 10),
            Math.Max(unit.GetCurrentStamina(), 30),
            Math.Max(unit.GetCurrentAura(), 0),
            Math.Max(unit.GetCurrentAp(), ap),
            Math.Max(unit.GetCurrentMovePoints(), 2)
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.HP_MAX,
            Math.Max(unit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? unit.GetCurrentHp(), unit.GetCurrentHp())
        );
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, Math.Max(unit.GetCurrentStamina(), 30));
    }

    internal static BattleCommand BuildBasicAttackCommand(
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = attacker?.unit_id ?? new StringName(""),
            skill_entry_id = BattleSkillEntryIds.KnownSkill(BasicAttackSkillId),
            skill_id = BasicAttackSkillId,
            target_unit_id = target?.unit_id ?? new StringName(""),
            target_coord = target?.GetAnchorCoord() ?? new Vector2I(-1, -1),
        };
        if (target != null)
            command.AddTargetUnitId(target.unit_id);
        return command;
    }

    internal static BattleCommand BuildUnitSkillCommand(
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = user?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = skillId,
            target_unit_id = target?.unit_id ?? new StringName(""),
            target_coord = target?.GetAnchorCoord() ?? new Vector2I(-1, -1),
        };
        if (target != null)
            command.AddTargetUnitId(target.unit_id);
        return command;
    }

    internal static BattleState BuildFlatState(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target,
        int worldStep = 0,
        int currentTu = 0,
        Vector2I mapSize = default
    )
    {
        Vector2I resolvedMapSize = mapSize == default ? new Vector2I(6, 6) : mapSize;
        var state = new BattleState
        {
            battle_id = battleId,
            map_size = resolvedMapSize,
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.timeline.current_tu = Math.Max(currentTu, 0);
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(
                new GDictionary { ["world_step"] = worldStep }
            )
        );
        AddPlainCells(state);
        if (attacker != null)
        {
            state.SetUnit(attacker);
            SetUnitOccupants(state, attacker);
            state.ally_unit_ids.Add(attacker.unit_id);
            state.active_unit_id = attacker.unit_id;
        }
        if (target != null)
        {
            state.SetUnit(target);
            SetUnitOccupants(state, target);
            state.enemy_unit_ids.Add(target.unit_id);
        }
        return state;
    }

    internal static BattleEventBatch IssueBasicAttack(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int worldStep = 0,
        int currentTu = 0,
        bool previewCommand = true
    )
    {
        PrimeBasicAttack(attacker);
        BattleState state = BuildFlatState(battleId, attacker, target, worldStep, currentTu);
        runtime.SetupStateForTests(state);
        BattleCommand command = BuildBasicAttackCommand(attacker, target);
        if (previewCommand)
        {
            BattlePreview preview = runtime.PreviewCommand(command);
            if (preview?.allowed != true)
            {
                throw new InvalidOperationException(
                    $"basic_attack preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
                );
            }
        }
        return runtime.IssueCommand(command);
    }

    internal static BattleEventBatch IssueUnitSkill(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        StringName battleId,
        int worldStep = 0,
        int currentTu = 0,
        bool previewCommand = true
    )
    {
        PrimeActionResources(user);
        BattleState state = BuildFlatState(battleId, user, target, worldStep, currentTu);
        runtime.SetupStateForTests(state);
        BattleCommand command = BuildUnitSkillCommand(user, target, entry, skillId);
        if (previewCommand)
        {
            BattlePreview preview = runtime.PreviewCommand(command);
            if (preview?.allowed != true)
            {
                throw new InvalidOperationException(
                    $"unit skill preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
                );
            }
        }
        return runtime.IssueCommand(command);
    }

    private static void AddPlainCells(BattleState state)
    {
        if (state == null)
            return;
        for (int x = 0; x < state.map_size.X; x++)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            {
                BattleCellState cell = new();
                cell.SetCoord(new Vector2I(x, y));
                state.SetCell(cell);
            }
        }
    }

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        foreach (Vector2I coord in unit.GetOccupiedCoordsReadViewTyped())
        {
            BattleCellState cell = state.GetCell(coord);
            cell?.SetOccupant(unit.unit_id);
        }
    }
}
