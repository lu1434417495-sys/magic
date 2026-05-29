using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiScoreContextAdapter : RefCounted, IBattleAiScoreContext
{
    public BattleState state { get; set; }
    public BattleUnitState unit_state { get; set; }
    public BattleGridService grid_service { get; set; }
    public GDictionary skill_defs { get; set; } = new();
    public Dictionary<string, object> score_projection_cache { get; set; } = new();

    private BattleAiScoreService _score_service;

    public void setup(
        BattleAiScoreService score_service,
        BattleState battle_state,
        BattleUnitState actor_unit_state,
        BattleGridService battle_grid_service,
        GDictionary raw_skill_defs,
        Dictionary<string, object> shared_score_projection_cache
    )
    {
        _score_service = null;
        state = null;
        unit_state = null;
        grid_service = null;
        skill_defs = new GDictionary();
        score_projection_cache = new Dictionary<string, object>();

        if (score_service == null)
        {
            Fail("BattleAiScoreContextAdapter requires BattleAiScoreService.");
            return;
        }
        if (battle_state == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null battle_state.");
            return;
        }
        if (actor_unit_state == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null actor_unit_state.");
            return;
        }
        if (battle_grid_service == null)
        {
            Fail("BattleAiScoreContextAdapter requires non-null battle_grid_service.");
            return;
        }

        StringName actorId = ProgressionDataUtils.to_string_name(actor_unit_state.unit_id);
        if (IsEmpty(actorId) || !battle_state.units.ContainsKey(actorId))
        {
            Fail(
                $"BattleAiScoreContextAdapter actor_unit_state.unit_id {actorId} not present in battle_state.units."
            );
            return;
        }

        _score_service = score_service;
        state = battle_state;
        unit_state = actor_unit_state;
        grid_service = battle_grid_service;
        skill_defs = raw_skill_defs ?? new GDictionary();
        score_projection_cache = shared_score_projection_cache ?? new Dictionary<string, object>();
    }

    public void setup(
        BattleAiScoreService score_service,
        BattleState battle_state,
        BattleUnitState actor_unit_state,
        BattleGridService battle_grid_service,
        GDictionary raw_skill_defs,
        GDictionary shared_score_projection_cache
    )
    {
        setup(
            score_service,
            battle_state,
            actor_unit_state,
            battle_grid_service,
            raw_skill_defs,
            DecodeScoreProjectionCache(shared_score_projection_cache)
        );
    }

    private static Dictionary<string, object> DecodeScoreProjectionCache(GDictionary cache)
    {
        var result = new Dictionary<string, object>();
        if (cache == null)
        {
            return result;
        }
        foreach (var keyValue in cache.Keys)
        {
            result[keyValue.ToString()] = cache[keyValue];
        }
        return result;
    }

    public BattleAiScoreInput build_action_score_input(
        BattleAiQueryService _query,
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata
    )
    {
        if (_score_service == null)
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

        BattleAiScoreInput scoreInput = _score_service.build_action_score_input(
            this,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            metadata
        );
        return ValidateScoreInput(scoreInput);
    }

    public BattleAiScoreInput build_skill_score_input(
        BattleAiQueryService _query,
        StringName skill_id,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs,
        GDictionary metadata
    )
    {
        if (_score_service == null)
        {
            Fail("BattleAiScoreContextAdapter missing score service.");
            return null;
        }

        SkillDef skillDef = ResolveSkillDef(skill_id);
        if (skillDef == null || skillDef.combat_profile == null)
        {
            Fail($"BattleAiScoreContextAdapter missing SkillDef for {skill_id}.");
            return null;
        }
        if (!ValidateCommandActorMatch(command))
        {
            return null;
        }
        if (!ValidateCommandSkillMatch(command, skill_id))
        {
            return null;
        }
        if (!ValidateCommandPreviewMetadata(command, preview, metadata))
        {
            return null;
        }

        BattleAiScoreInput scoreInput = _score_service.build_skill_score_input(
            this,
            skillDef,
            command,
            preview,
            effect_defs ?? new GArray(),
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
        if (command == null || unit_state == null)
        {
            return true;
        }
        StringName commandUnitId = ProgressionDataUtils.to_string_name(command.unit_id);
        if (IsEmpty(commandUnitId))
        {
            return true;
        }
        if (commandUnitId != ProgressionDataUtils.to_string_name(unit_state.unit_id))
        {
            Fail(
                $"BattleAiScoreContextAdapter command.unit_id {commandUnitId} does not match actor unit_id {unit_state.unit_id}."
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
        GDictionary metadata
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
            metadata ?? new GDictionary(),
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
        GDictionary metadata = scoreInput.runtime_action_metadata.Duplicate(true);
        metadata["skill_id"] = ProgressionDataUtils.to_string_name(skillId);
        scoreInput.runtime_action_metadata = metadata;
        scoreInput.skill_def = null;
    }

    private SkillDef ResolveSkillDef(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (
            skill_defs == null
            || IsEmpty(normalizedSkillId)
            || !skill_defs.ContainsKey(normalizedSkillId)
        )
        {
            return null;
        }
        return skill_defs[normalizedSkillId].AsGodotObject() as SkillDef;
    }

    private static bool Fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(
            message,
            new GDictionary { ["source"] = "BattleAiScoreContextAdapter" }
        );
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
