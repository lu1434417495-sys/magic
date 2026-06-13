using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_race_trait_content_registry_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialRaceTraitRegistryValidatesWithoutErrors();
        TestOfficialIdentityContentDomainValidatesWithoutErrors();

        Quit(_test.Finish("Race trait content registry regression"));
    }

    private void TestOfficialRaceTraitRegistryValidatesWithoutErrors()
    {
        RaceTraitContentRegistry registry = new();
        AssertEmpty(
            ToList(registry.Validate()),
            "Official race trait content should validate without errors."
        );
    }

    private void TestOfficialIdentityContentDomainValidatesWithoutErrors()
    {
        List<string> errors = new();
        AppendErrors(errors, new RaceContentRegistry().Validate());
        AppendErrors(errors, new SubraceContentRegistry().Validate());
        AppendErrors(errors, new RaceTraitContentRegistry().Validate());
        AppendErrors(errors, new AgeContentRegistry().Validate());
        AppendErrors(errors, new BloodlineContentRegistry().Validate());
        AppendErrors(errors, new AscensionContentRegistry().Validate());
        AppendErrors(errors, new StageAdvancementContentRegistry().Validate());

        ProgressionContentRegistry progressionRegistry = new();
        GStringArray phase2Errors = new();
        progressionRegistry.AppendIdentityPhase2ValidationErrors(phase2Errors);
        AppendErrors(errors, phase2Errors);

        AssertEmpty(errors, "Official identity content should validate without errors.");
    }

    private static void AppendErrors(List<string> target, IEnumerable<string> errors)
    {
        foreach (string error in errors)
        {
            if (!target.Contains(error))
            {
                target.Add(error);
            }
        }
    }

    private static List<string> ToList(IEnumerable<string> values)
    {
        List<string> result = new();
        foreach (string value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private void AssertEmpty(List<string> errors, string message)
    {
        if (errors.Count == 0)
        {
            return;
        }
        _test.Fail($"{message} errors=[{string.Join(", ", errors)}]");
    }
}
