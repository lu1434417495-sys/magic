using Godot;

[GlobalClass]
public partial class BattleSpecialProfileGate : RefCounted
{
    private const string PLAYER_BLOCK_MESSAGE = "该禁咒配置未通过校验，暂时无法施放。";

    private Godot.Collections.Dictionary _registry_snapshot = new();

    public void setup(Godot.Collections.Dictionary registrySnapshot)
    {
        _registry_snapshot = registrySnapshot?.Duplicate(true) ?? new Godot.Collections.Dictionary();
    }

    public BattleSpecialProfileGateResult preflight_skill(GodotObject skillDef, GodotObject battleState)
        => _evaluate_skill(skillDef, battleState);

    public BattleSpecialProfileGateResult preview_skill(GodotObject skillDef, BattleCommand command, BattleUnitState activeUnit, GodotObject battleState)
        => _evaluate_skill(skillDef, battleState, command, activeUnit);

    public BattleSpecialProfileGateResult can_execute_skill(GodotObject skillDef, BattleCommand command, BattleUnitState activeUnit, GodotObject battleState)
        => _evaluate_skill(skillDef, battleState, command, activeUnit);

    private BattleSpecialProfileGateResult _evaluate_skill(GodotObject skillDef, GodotObject battleState, BattleCommand command = null, BattleUnitState activeUnit = null)
    {
        var result = new BattleSpecialProfileGateResult();
        if (skillDef == null || skillDef.Get("combat_profile").AsGodotObject() == null)
            return _block(result, "", "", "missing_skill", "Missing skill definition.", new Godot.Collections.Dictionary());

        var combatProfile = skillDef.Get("combat_profile").AsGodotObject();
        result.skill_id = skillDef.Get("skill_id").AsStringName();
        result.profile_id = combatProfile.Get("special_resolution_profile_id").AsStringName();

        if (result.profile_id == "")
        {
            result.allowed = true;
            return result;
        }
        if (!_registry_snapshot.ContainsKey("ok") || !(bool)_registry_snapshot["ok"])
        {
            return _block(result, result.profile_id, result.skill_id, "content_invalid", PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary { { "errors", _dict_get(_registry_snapshot, "errors", new Godot.Collections.Array()) } });
        }

        var profileIdBySkillId = _dict_get(_registry_snapshot, "profile_id_by_skill_id", new Godot.Collections.Dictionary());
        if (profileIdBySkillId.VariantType != Variant.Type.Dictionary)
            return _block(result, result.profile_id, result.skill_id, "missing_profile_index", PLAYER_BLOCK_MESSAGE, new Godot.Collections.Dictionary());

        var skillKey = (string)result.skill_id;
        if (_dict_get(profileIdBySkillId.AsGodotDictionary(), skillKey, "").AsString() != (string)result.profile_id)
            return _block(result, result.profile_id, result.skill_id, "skill_not_owned", PLAYER_BLOCK_MESSAGE, new Godot.Collections.Dictionary());

        var profiles = _dict_get(_registry_snapshot, "profiles", new Godot.Collections.Dictionary());
        if (profiles.VariantType != Variant.Type.Dictionary || !profiles.AsGodotDictionary().ContainsKey((string)result.profile_id))
            return _block(result, result.profile_id, result.skill_id, "profile_missing", PLAYER_BLOCK_MESSAGE, new Godot.Collections.Dictionary());

        var profileSnapshot = _dict_get(profiles.AsGodotDictionary(), (string)result.profile_id, new Godot.Collections.Dictionary());
        if (profileSnapshot.VariantType != Variant.Type.Dictionary)
            return _block(result, result.profile_id, result.skill_id, "profile_invalid", PLAYER_BLOCK_MESSAGE, new Godot.Collections.Dictionary());

        if (_dict_get(profileSnapshot.AsGodotDictionary(), "runtime_resolver_id", "").AsString() != (string)result.profile_id)
            return _block(result, result.profile_id, result.skill_id, "resolver_mismatch", PLAYER_BLOCK_MESSAGE, profileSnapshot.AsGodotDictionary());

        if (battleState == null)
            return _block(result, result.profile_id, result.skill_id, "missing_battle_state", PLAYER_BLOCK_MESSAGE, new Godot.Collections.Dictionary());

        if (command == null && activeUnit == null)
        {
            result.allowed = true;
            return result;
        }
        result.allowed = true;
        return result;
    }

    private static Variant _dict_get(Godot.Collections.Dictionary values, Variant key, Variant defaultValue)
    {
        if (values != null && values.ContainsKey(key))
            return values[key];
        return defaultValue;
    }

    private static BattleSpecialProfileGateResult _block(
        BattleSpecialProfileGateResult result,
        StringName profileId,
        StringName skillId,
        StringName blockCode,
        string playerMessage,
        Godot.Collections.Dictionary debugDetails
    )
    {
        result.allowed = false;
        result.profile_id = profileId;
        result.skill_id = skillId;
        result.block_code = blockCode;
        result.player_message = playerMessage;
        result.debug_details = debugDetails.Duplicate(true);
        return result;
    }
}
