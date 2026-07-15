using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_sim_definition_patch_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            RunPatchValueIsolationContract();
            RunDefinitionPatchContract();
            RunMageTunerActionPatchContract();
            RunRunnerOverrideFailFastContract();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Battle sim definition patch regression"));
    }

    private void RunPatchValueIsolationContract()
    {
        var nested = new List<object> { 1 };
        var source = new Dictionary<string, object> { ["nested"] = nested };
        var patch = new BattleSimOverridePatchDefinition(
            "probe",
            "",
            "",
            "",
            "value",
            source
        );

        nested[0] = 9;
        var first = (Dictionary<string, object>)patch.Value;
        _test.Eq(
            ((List<object>)first["nested"])[0],
            1,
            "override patch definition must detach its plain value from source mutation"
        );
        ((List<object>)first["nested"])[0] = 7;
        var second = (Dictionary<string, object>)patch.Value;
        _test.Eq(
            ((List<object>)second["nested"])[0],
            1,
            "override patch definition must not expose its stored plain value by alias"
        );

        using var objectCarrier = new Resource();
        bool rejected = false;
        try
        {
            _ = new BattleSimOverridePatchDefinition(
                "probe",
                "",
                "",
                "",
                "value",
                objectCarrier
            );
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        _test.True(rejected, "override patch definition must reject Godot Object carriers");
    }

    private void RunDefinitionPatchContract()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        SkillDefinition sourceSkill = snapshot.Skills["archer_pinning_shot"];
        EnemyAiBrainDefinition sourceBrain = snapshot.EnemyBrains["ranged_suppressor"];
        BattleAiScoreProfileDefinition sourceScore = BattleAiScoreProfileDefinition.Default;
        var profile = new BattleSimProfileDefinition(
            "typed_patch_probe",
            "Typed Patch Probe",
            "",
            sourceScore,
            new BattleSimOverridePatchDefinition[]
            {
                new(
                    "skill",
                    "archer_pinning_shot",
                    "",
                    "",
                    "combat_profile.stamina_cost",
                    999
                ),
                new(
                    "brain",
                    "ranged_suppressor",
                    "",
                    "",
                    "transition_rules.0.conditions.0.basis_points",
                    6000
                ),
                new(
                    "action",
                    "ranged_suppressor",
                    "pressure",
                    "harrier_keep_range",
                    "desired_min_distance",
                    5
                ),
                new("ai_score_profile", "", "", "", "movement_cost_weight", 7),
                new("faction_ai_score_profile", "player", "", "", "damage_weight", 12),
            }
        );

        BattleSimOverrideApplyResult result = new BattleSimOverrideApplier().ApplyProfileTyped(
            snapshot.Skills,
            snapshot.EnemyBrains,
            profile
        );

        _test.Eq(result.Errors.Count, 0, $"typed patches should apply: {string.Join(" | ", result.Errors)}");
        _test.Eq(
            result.SkillDefinitions["archer_pinning_shot"].CombatProfile.StaminaCost,
            999,
            "skill patch should update the immutable combat profile copy"
        );
        _test.False(
            ReferenceEquals(sourceSkill, result.SkillDefinitions["archer_pinning_shot"]),
            "skill patch should return a new definition"
        );
        _test.False(
            ReferenceEquals(sourceBrain, result.EnemyAiBrains["ranged_suppressor"]),
            "brain patch should return a new recursive definition"
        );
        _test.Eq(
            result.EnemyAiBrains["ranged_suppressor"]
                .TransitionRules[0]
                .Conditions[0]
                .BasisPoints,
            6000,
            "brain transition condition should be patched"
        );
        MoveToRangeActionDefinition patchedAction = FindAction<MoveToRangeActionDefinition>(
            result.EnemyAiBrains["ranged_suppressor"],
            "pressure",
            "harrier_keep_range"
        );
        _test.Eq(
            patchedAction?.DesiredMinDistance ?? -1,
            5,
            "action patch should update the matching typed action"
        );
        _test.Eq(
            result.AiScoreProfile.MovementCostWeight,
            7,
            "global score patch should update the typed score definition"
        );
        _test.Eq(
            result.FactionAiScoreProfiles["player"].DamageWeight,
            12,
            "faction score patch should derive from the patched baseline"
        );
        _test.Eq(
            sourceScore.MovementCostWeight,
            BattleAiScoreProfileDefinition.Default.MovementCostWeight,
            "source score definition should remain unchanged"
        );
    }

    private void RunMageTunerActionPatchContract()
    {
        EnemyAiBrainDefinition source = GameSessionTestFactory
            .GetProcessSnapshot()
            .EnemyBrains["mage_controller"];
        BattleSimProfileDefinition profile = new(
            "mage_tuner_action_patch_probe",
            "Mage Tuner Action Patch Probe",
            "",
            BattleAiScoreProfileDefinition.Default,
            new BattleSimOverridePatchDefinition[]
            {
                new(
                    "action",
                    source.BrainId,
                    "",
                    "mage_blink_escape",
                    "min_survival_margin_gain_to_escape",
                    -7
                ),
                new(
                    "action",
                    source.BrainId,
                    "",
                    "mage_blink_escape",
                    "minimum_safe_distance",
                    6
                ),
                new(
                    "action",
                    source.BrainId,
                    "",
                    "mage_blink_escape",
                    "desired_max_distance_bonus",
                    5
                ),
                new(
                    "action",
                    source.BrainId,
                    "",
                    "mage_survival_position",
                    "min_survival_margin_gain_to_escape",
                    9
                ),
            }
        );
        BattleSimOverrideApplyResult result = new BattleSimOverrideApplier().ApplyProfileTyped(
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition> { [source.BrainId] = source },
            profile
        );
        _test.Eq(
            result.Errors.Count,
            0,
            $"mage tuner action patches should apply: {string.Join(" | ", result.Errors)}"
        );
        EnemyAiBrainDefinition patched = result.EnemyAiBrains[source.BrainId];

        foreach (StringName stateId in new[] { new StringName("pressure"), new StringName("retreat") })
        {
            UseGroundRepositionSkillActionDefinition blink =
                FindAction<UseGroundRepositionSkillActionDefinition>(
                    patched,
                    stateId,
                    "mage_blink_escape"
                );
            _test.Eq(blink?.MinSurvivalMarginGainToEscape ?? int.MinValue, -7, $"{stateId} blink gain");
            _test.Eq(blink?.MinimumSafeDistance ?? -1, 6, $"{stateId} blink safe distance");
            _test.Eq(blink?.DesiredMaxDistanceBonus ?? -1, 5, $"{stateId} blink distance bonus");
        }

        foreach (
            StringName stateId in new[]
            {
                new StringName("engage"),
                new StringName("pressure"),
                new StringName("retreat"),
            }
        )
        {
            MoveToAdvantagePositionActionDefinition survival =
                FindAction<MoveToAdvantagePositionActionDefinition>(
                    patched,
                    stateId,
                    "mage_survival_position"
                );
            _test.Eq(
                survival?.MinSurvivalMarginGainToEscape ?? int.MinValue,
                9,
                $"{stateId} survival gain"
            );
        }

        _test.Eq(
            FindAction<UseGroundRepositionSkillActionDefinition>(
                source,
                "pressure",
                "mage_blink_escape"
            )?.MinimumSafeDistance ?? -1,
            4,
            "source mage brain must remain unchanged"
        );
    }

    private void RunRunnerOverrideFailFastContract()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        var invalidProfile = new BattleSimProfileDefinition(
            "runner_invalid_override_probe",
            "Runner Invalid Override Probe",
            "",
            BattleAiScoreProfileDefinition.Default,
            new[]
            {
                new BattleSimOverridePatchDefinition(
                    "ai_score_profile",
                    "",
                    "",
                    "",
                    "unsupported_runner_scalar",
                    1
                ),
            }
        );
        using var contentProvider = new BattleSimContentProvider(snapshot);
        var runner = new BattleSimRunner(contentProvider);
        BattleSimScenarioReport partialReport = null;
        InvalidOperationException failure = null;
        try
        {
            partialReport = runner.RunScenario(
                null,
                new[] { invalidProfile }
            );
        }
        catch (InvalidOperationException exception)
        {
            failure = exception;
        }

        _test.True(failure != null, "runner must fail fast when override application has errors");
        _test.True(
            failure?.Message.Contains("runner_invalid_override_probe", StringComparison.Ordinal)
                == true,
            "runner failure must include the profile id"
        );
        _test.True(
            failure?.Message.Contains("unsupported_runner_scalar", StringComparison.Ordinal)
                == true,
            "runner failure must include the override errors"
        );
        _test.True(
            partialReport == null,
            "runner must not return a partial report after override validation fails"
        );
    }

    private static TAction FindAction<TAction>(
        EnemyAiBrainDefinition brain,
        StringName stateId,
        StringName actionId
    )
        where TAction : EnemyAiActionDefinition
    {
        if (brain == null || !brain.TryGetState(stateId, out EnemyAiStateDefinition state))
            return null;
        foreach (EnemyAiActionDefinition action in state.Actions)
        {
            if (action.ActionId == actionId)
                return action as TAction;
        }
        return null;
    }
}
