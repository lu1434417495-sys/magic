using Godot;

[GlobalClass]
public partial class BattleSpecialProfileGate : RefCounted
{
    private const string PLAYER_BLOCK_MESSAGE = "该禁咒配置未通过校验，暂时无法施放。";

    private Godot.Collections.Dictionary _registry_snapshot = new();

    public void setup(Godot.Collections.Dictionary registrySnapshot)
    {
        _registry_snapshot =
            registrySnapshot?.Duplicate(true) ?? new Godot.Collections.Dictionary();
    }

    public BattleSpecialProfileGateResult preflight_skill(SkillDef skillDef, BattleState battleState) =>
        _evaluate_skill(skillDef, battleState);

    public BattleSpecialProfileGateResult preview_skill(
        SkillDef skillDef,
        BattleCommand command,
        BattleUnitState activeUnit,
        BattleState battleState
    ) => _evaluate_skill(skillDef, battleState, command, activeUnit);

    public BattleSpecialProfileGateResult can_execute_skill(
        SkillDef skillDef,
        BattleCommand command,
        BattleUnitState activeUnit,
        BattleState battleState
    ) => _evaluate_skill(skillDef, battleState, command, activeUnit);

    private BattleSpecialProfileGateResult _evaluate_skill(
        SkillDef skillDef,
        BattleState battleState,
        BattleCommand command = null,
        BattleUnitState activeUnit = null
    )
    {
        var result = new BattleSpecialProfileGateResult();
        if (skillDef == null || skillDef.combat_profile == null)
            return _block(
                result,
                "",
                "",
                "missing_skill",
                "Missing skill definition.",
                new Godot.Collections.Dictionary()
            );

        var combatProfile = skillDef.combat_profile as CombatSkillDef;
        if (combatProfile == null)
            return _block(
                result,
                "",
                skillDef.skill_id,
                "missing_combat_profile",
                "Missing combat skill profile.",
                new Godot.Collections.Dictionary()
            );
        result.skill_id = skillDef.skill_id;
        result.profile_id = combatProfile.special_resolution_profile_id;

        if (result.profile_id == "")
        {
            result.allowed = true;
            return result;
        }
        if (!_registry_snapshot.ContainsKey("ok") || !_registry_snapshot["ok"].AsBool())
        {
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "content_invalid",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary
                {
                    {
                        "errors",
                        _registry_snapshot.GetValueOrDefault(
                            "errors",
                            new Godot.Collections.Array()
                        )
                    },
                }
            );
        }

        Godot.Collections.Dictionary profileIdBySkillId = ReadDictionary(
            _registry_snapshot,
            "profile_id_by_skill_id"
        );
        if (profileIdBySkillId.Count == 0)
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "missing_profile_index",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary()
            );

        var skillKey = (string)result.skill_id;
        if (
            !profileIdBySkillId.ContainsKey(skillKey)
            || profileIdBySkillId[skillKey].AsString() != (string)result.profile_id
        )
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "skill_not_owned",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary()
            );

        Godot.Collections.Dictionary profiles = ReadDictionary(_registry_snapshot, "profiles");
        if (!profiles.ContainsKey((string)result.profile_id))
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "profile_missing",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary()
            );

        Godot.Collections.Dictionary profileSnapshot = profiles[(string)result.profile_id]
            .AsGodotDictionary();
        if (profileSnapshot.Count == 0)
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "profile_invalid",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary()
            );

        if (
            !profileSnapshot.ContainsKey("runtime_resolver_id")
            || profileSnapshot["runtime_resolver_id"].AsString() != (string)result.profile_id
        )
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "resolver_mismatch",
                PLAYER_BLOCK_MESSAGE,
                profileSnapshot
            );

        if (battleState == null)
            return _block(
                result,
                result.profile_id,
                result.skill_id,
                "missing_battle_state",
                PLAYER_BLOCK_MESSAGE,
                new Godot.Collections.Dictionary()
            );

        if (command == null && activeUnit == null)
        {
            result.allowed = true;
            return result;
        }
        result.allowed = true;
        return result;
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

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary source,
        string key
    )
    {
        if (source == null)
            return new Godot.Collections.Dictionary();
        if (source.ContainsKey(key))
            return source[key].AsGodotDictionary();
        var stringNameKey = new StringName(key);
        if (source.ContainsKey(stringNameKey))
            return source[stringNameKey].AsGodotDictionary();
        return new Godot.Collections.Dictionary();
    }
}
