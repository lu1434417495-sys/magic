using System.Collections.Generic;
using Godot;

internal sealed class BattleAiScoreContextAdapter : IBattleAiScoreContext
{
    private BattleState _state;
    private BattleUnitState _unitState;
    private BattleGridService _gridService;
    private BattleAiScoreService _scoreService;
    private Godot.Collections.Dictionary _skillDefsProjection = new();
    private Dictionary<StringName, SkillDef> _skillDefsById = new();

    BattleState IBattleAiScoreContext.state => _state;

    BattleUnitState IBattleAiScoreContext.unit_state => _unitState;

    BattleGridService IBattleAiScoreContext.grid_service => _gridService;

    Godot.Collections.Dictionary IBattleAiScoreContext.skill_defs => _skillDefsProjection;

    internal void Setup(
        BattleAiScoreService scoreService,
        BattleState battleState,
        BattleUnitState actorUnitState,
        BattleGridService battleGridService,
        Godot.Collections.Dictionary rawSkillDefs
    )
    {
        _scoreService = null;
        _state = null;
        _unitState = null;
        _gridService = null;
        _skillDefsProjection = new Godot.Collections.Dictionary();
        _skillDefsById = new Dictionary<StringName, SkillDef>();

        if (scoreService == null)
        {
            Fail("BattleAiScoreContextAdapter requires BattleAiScoreService.");
            return;
        }
        if (battleState == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null battle_state.");
            return;
        }
        if (actorUnitState == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null actor_unit_state.");
            return;
        }
        if (battleGridService == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null battle_grid_service.");
            return;
        }

        StringName actorId = ProgressionDataUtils.to_string_name(actorUnitState.unit_id);
        if (IsEmpty(actorId) || !battleState.units.ContainsKey(actorId))
        {
            Fail(
                $"BattleAiScoreContextAdapter actor_unit_state.unit_id {actorId} not present in battle_state.units."
            );
            return;
        }

        _scoreService = scoreService;
        _state = battleState;
        _unitState = actorUnitState;
        _gridService = battleGridService;
        _skillDefsProjection = rawSkillDefs ?? new Godot.Collections.Dictionary();
        _skillDefsById = BuildSkillDefIndex(_skillDefsProjection);
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        BattleAiQueryService _query,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Dictionary metadata
    )
    {
        if (_scoreService == null)
        {
            Fail("BattleAiScoreContextAdapter missing score service.");
            return null;
        }
        if (!ValidateCommandActorMatch(command))
        {
            return null;
        }
        if (!ValidateCommandPreviewMetadata(command, preview, metadata))
        {
            return null;
        }

        BattleAiScoreInput scoreInput = _scoreService.BuildActionScoreInput(
            this,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
        return ValidateScoreInput(scoreInput);
    }

    internal BattleAiScoreInput BuildSkillScoreInput(
        BattleAiQueryService _query,
        StringName skillId,
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Array effectDefs,
        Godot.Collections.Dictionary metadata
    )
    {
        if (_scoreService == null)
        {
            Fail("BattleAiScoreContextAdapter missing score service.");
            return null;
        }

        SkillDef skillDef = ResolveSkillDef(skillId);
        if (skillDef == null || skillDef.combat_profile == null)
        {
            Fail($"BattleAiScoreContextAdapter missing SkillDef for {skillId}.");
            return null;
        }
        if (!ValidateCommandActorMatch(command))
        {
            return null;
        }
        if (!ValidateCommandSkillMatch(command, skillId))
        {
            return null;
        }
        if (!ValidateCommandPreviewMetadata(command, preview, metadata))
        {
            return null;
        }

        BattleAiScoreInput scoreInput = _scoreService.BuildSkillScoreInput(
            this,
            skillDef,
            command,
            preview,
            effectDefs ?? new Godot.Collections.Array(),
            metadata
        );
        if (scoreInput != null)
        {
            StripRuntimeSkillResource(scoreInput, skillDef.skill_id);
        }
        return ValidateScoreInput(scoreInput);
    }

    private bool ValidateCommandActorMatch(BattleCommand command)
    {
        if (command == null || _unitState == null)
        {
            return true;
        }
        StringName commandUnitId = ProgressionDataUtils.to_string_name(command.unit_id);
        if (IsEmpty(commandUnitId))
        {
            return true;
        }
        if (commandUnitId != ProgressionDataUtils.to_string_name(_unitState.unit_id))
        {
            Fail(
                $"BattleAiScoreContextAdapter command.unit_id {commandUnitId} does not match actor unit_id {_unitState.unit_id}."
            );
            return false;
        }
        return true;
    }

    private static bool ValidateCommandSkillMatch(BattleCommand command, StringName expectedSkillId)
    {
        if (command == null)
        {
            return true;
        }
        StringName commandSkillId = ProgressionDataUtils.to_string_name(command.skill_id);
        if (IsEmpty(commandSkillId))
        {
            return true;
        }
        if (commandSkillId != ProgressionDataUtils.to_string_name(expectedSkillId))
        {
            Fail(
                $"BattleAiScoreContextAdapter command.skill_id {commandSkillId} does not match expected skill_id {expectedSkillId}."
            );
            return false;
        }
        return true;
    }

    private static bool ValidateCommandPreviewMetadata(
        BattleCommand command,
        BattlePreview preview,
        Godot.Collections.Dictionary metadata
    )
    {
        if (!BattleAiPayloadGuard.CommandIsValueObject(command))
        {
            return false;
        }
        if (!BattleAiPayloadGuard.PreviewHasNoLiveState(preview))
        {
            return false;
        }
        return BattleAiPayloadGuard.ValidateNoForbiddenObject(
            metadata ?? new Godot.Collections.Dictionary(),
            "score_adapter.metadata"
        );
    }

    private static BattleAiScoreInput ValidateScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            Fail("BattleAiScoreContextAdapter score service returned invalid score input.");
            return null;
        }
        return BattleAiPayloadGuard.ScoreInputHasNoLiveState(scoreInput) ? scoreInput : null;
    }

    private static void StripRuntimeSkillResource(BattleAiScoreInput scoreInput, StringName skillId)
    {
        if (scoreInput == null)
        {
            return;
        }
        Godot.Collections.Dictionary metadata = scoreInput.runtime_action_metadata.Duplicate(true);
        metadata["skill_id"] = ProgressionDataUtils.to_string_name(skillId);
        scoreInput.runtime_action_metadata = metadata;
        scoreInput.skill_def = null;
    }

    private SkillDef ResolveSkillDef(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (IsEmpty(normalizedSkillId))
        {
            return null;
        }
        return _skillDefsById.TryGetValue(normalizedSkillId, out SkillDef skillDef)
            ? skillDef
            : null;
    }

    private static Dictionary<StringName, SkillDef> BuildSkillDefIndex(
        Godot.Collections.Dictionary skillDefs
    )
    {
        var result = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
        {
            return result;
        }
        foreach (var rawKey in skillDefs.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawKey);
            if (IsEmpty(skillId))
            {
                continue;
            }
            SkillDef skillDef = skillDefs[rawKey].As<SkillDef>();
            if (skillDef == null)
            {
                continue;
            }
            result[skillId] = skillDef;
        }
        return result;
    }

    private static bool Fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new Godot.Collections.Dictionary { ["source"] = "BattleAiScoreContextAdapter" }
        );
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
