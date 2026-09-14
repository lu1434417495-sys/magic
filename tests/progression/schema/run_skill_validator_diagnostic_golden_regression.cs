using System;
using System.Collections.Generic;

public partial class run_skill_validator_diagnostic_golden_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            using SkillContentRegistry registry = new();
            IReadOnlyList<string> officialDiagnostics = registry.Validate();
            _test.Eq(
                officialDiagnostics.Count,
                0,
                "official JSON skill corpus remains validator-clean"
            );
            SkillValidatorDiagnosticGoldenHarness.AssertRuleHitSet();
        }
        catch (Exception exception)
        {
            _test.Fail($"Skill validator diagnostic golden crashed: {exception}");
        }
        RequestTestExit(_test.Finish("Skill validator diagnostic golden regression"));
    }
}
