using Godot;

public partial class run_battle_sim_json_catalog_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(RunDeferred);

    private void RunDeferred()
    {
        TestResult result = Run();
        RequestTestExit(result);
    }

    private TestResult Run()
    {
        try
        {
            var profileRegistry = new BattleSimProfileContentRegistry();
            profileRegistry.Rebuild();
            var catalog = new BattleSimContentCatalog();
            catalog.Rebuild();
            _test.Eq(profileRegistry.GetValidationErrors().Count, 0, "BattleSim profile JSON registry 应严格导入全部生产数据。");
            _test.Eq(catalog.GetValidationErrors().Count, 0, "BattleSim scenario JSON catalog 应严格导入全部生产数据。");
            _test.Eq(profileRegistry.GetDefinitions().Count, 4, "BattleSim JSON registry 应发现全部 4 个 profile。");
            _test.Eq(catalog.GetScenarios().Count, 11, "BattleSim JSON catalog 应发现全部 11 个 scenario。");

            _test.True(profileRegistry.TryGetDefinition("mist_controller_aggressive", out BattleSimProfileDefinition profile), "profile 应可按 ID 获取。");
            if (profile is not null)
            {
                _test.Eq(profile.OverridePatches.Count, 6, "closed-kind override patches 应完整投影。");
                _test.Eq(profile.AiScoreProfile.MovementCostWeight, 8, "AI score profile 数值应保持 parity。");
                _test.Eq(profile.OverridePatches[2].TargetType, "action", "action patch kind 应保持 parity。");
                _test.Eq(profile.OverridePatches[2].TargetId.ToString(), "ranged_controller", "action patch brain_id 应投影为 TargetId。");
            }

            _test.True(catalog.TryGetScenario("ai_vs_ai_duel_example", out BattleSimScenarioDefinition scenario), "scenario 应可按 ID 获取。");
            if (scenario is not null)
            {
                _test.Eq(scenario.AllyUnits.Count, 1, "ally roster 应保持 parity。");
                _test.Eq(scenario.EnemyUnits.Count, 1, "enemy roster 应保持 parity。");
                _test.Eq(scenario.Seeds.Count, 2, "seed 列表应保持 parity。");
                BattleUnitState ally = scenario.AllyUnits[0].UnitDefinition.CreateRuntimeState();
                _test.Eq(ally.unit_id.ToString(), "player_vanguard_ai", "unit DTO 应投影为 runtime state。");
                _test.Eq(ally.GetAnchorCoord(), new Vector2I(0, 1), "unit coord 应保持 parity。");
                _test.Eq(
                    ally.GetWeaponProjectionReadViewTyped().Values.ItemId.ToString(),
                    "steel_longsword",
                    "weapon projection 应保持 parity。"
                );
            }
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        return _test.Finish("BattleSim JSON catalog regression");
    }
}
