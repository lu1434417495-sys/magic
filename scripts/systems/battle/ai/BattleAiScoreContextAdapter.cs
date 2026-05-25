using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleAiScoreContextAdapter : RefCounted
{
    public BattleState state { get; set; }
    public BattleUnitState unit_state { get; set; }
    public BattleGridService grid_service { get; set; }
    public GDictionary skill_defs { get; set; } = new();
    public GDictionary score_projection_cache { get; set; } = new();

    private GodotObject _score_service;

    public void setup(
        GodotObject score_service,
        BattleState battle_state,
        BattleUnitState actor_unit_state,
        BattleGridService battle_grid_service,
        GDictionary raw_skill_defs,
        GDictionary shared_score_projection_cache)
    {
        _score_service = null;
        state = null;
        unit_state = null;
        grid_service = null;
        skill_defs = new GDictionary();
        score_projection_cache = new GDictionary();

        if (!IsScoreService(score_service))
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
            Fail($"BattleAiScoreContextAdapter actor_unit_state.unit_id {actorId} not present in battle_state.units.");
            return;
        }

        _score_service = score_service;
        state = battle_state;
        unit_state = actor_unit_state;
        grid_service = battle_grid_service;
        skill_defs = raw_skill_defs ?? new GDictionary();
        score_projection_cache = shared_score_projection_cache ?? new GDictionary();
    }

    public BattleAiScoreInput build_action_score_input(
        GodotObject _query,
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata)
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

        GDictionary privateMetadata = BuildPrivateScoreMetadata(metadata);
        GodotObject scoreInput = _score_service.Call(
            "build_action_score_input",
            this,
            action_kind,
            action_label,
            score_bucket_id,
            command,
            preview,
            privateMetadata).AsGodotObject();
        return ValidateScoreInput(scoreInput);
    }

    public BattleAiScoreInput build_skill_score_input(
        GodotObject _query,
        StringName skill_id,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs,
        GDictionary metadata)
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

        GDictionary privateMetadata = BuildPrivateScoreMetadata(metadata);
        GodotObject scoreInputObject = _score_service.Call(
            "build_skill_score_input",
            this,
            skillDef,
            command,
            preview,
            effect_defs ?? new GArray(),
            privateMetadata).AsGodotObject();
        var scoreInput = scoreInputObject as BattleAiScoreInput;
        if (scoreInput != null)
        {
            StripRuntimeSkillResource(scoreInput, skillDef.skill_id);
        }
        return ValidateScoreInput(scoreInput);
    }

    private GDictionary BuildPrivateScoreMetadata(GDictionary metadata)
    {
        GDictionary copied = metadata != null ? metadata.Duplicate(true) : new GDictionary();
        StringName focusTargetUnitId = ProgressionDataUtils.to_string_name(
            copied.ContainsKey("focus_target_unit_id") ? copied["focus_target_unit_id"] : new StringName(""));
        if (!IsEmpty(focusTargetUnitId) && state != null && state.units.ContainsKey(focusTargetUnitId))
        {
            BattleUnitState targetUnit = state.units[focusTargetUnitId].AsGodotObject() as BattleUnitState;
            if (targetUnit != null)
            {
                copied["position_target_unit"] = targetUnit;
            }
        }
        return copied;
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
            Fail($"BattleAiScoreContextAdapter command.unit_id {commandUnitId} does not match actor unit_id {unit_state.unit_id}.");
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
            Fail($"BattleAiScoreContextAdapter command.skill_id {commandSkillId} does not match expected skill_id {expectedSkillId}.");
            return false;
        }
        return true;
    }

    private static bool ValidateCommandPreviewMetadata(BattleCommand command, BattlePreview preview, GDictionary metadata)
    {
        if (!BattleAiPayloadGuard.CommandIsValueObject(command))
        {
            return false;
        }
        if (!BattleAiPayloadGuard.PreviewHasNoLiveState(preview))
        {
            return false;
        }
        return BattleAiPayloadGuard.ValidateNoForbiddenObject(metadata ?? new GDictionary(), "score_adapter.metadata");
    }

    private static BattleAiScoreInput ValidateScoreInput(GodotObject scoreInputObject)
    {
        var scoreInput = scoreInputObject as BattleAiScoreInput;
        if (scoreInput == null)
        {
            Fail("BattleAiScoreContextAdapter score service returned invalid score input.");
            return null;
        }
        if (scoreInput.command != null && !BattleAiPayloadGuard.CommandIsValueObject(scoreInput.command))
        {
            Fail("BattleAiScoreContextAdapter score_input.command is not a value object.");
            return null;
        }
        if (scoreInput.preview != null && !BattleAiPayloadGuard.PreviewHasNoLiveState(scoreInput.preview))
        {
            Fail("BattleAiScoreContextAdapter score_input.preview contains live state.");
            return null;
        }
        if (scoreInput.skill_def != null)
        {
            Fail("BattleAiScoreContextAdapter score_input.skill_def must be stripped before returning.");
            return null;
        }
        return BattleAiPayloadGuard.ValidateNoForbiddenObject(scoreInput.to_dict(), "score_adapter.score_input")
            ? scoreInput
            : null;
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
        if (skill_defs == null || IsEmpty(normalizedSkillId) || !skill_defs.ContainsKey(normalizedSkillId))
        {
            return null;
        }
        return skill_defs[normalizedSkillId].AsGodotObject() as SkillDef;
    }

    private static bool IsScoreService(GodotObject scoreService)
    {
        return scoreService != null
            && scoreService.HasMethod("build_action_score_input")
            && scoreService.HasMethod("build_skill_score_input");
    }

    private static bool Fail(string message)
    {
        return BattleAiPayloadGuard.FailLoud(message, new GDictionary { ["source"] = "BattleAiScoreContextAdapter" });
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
