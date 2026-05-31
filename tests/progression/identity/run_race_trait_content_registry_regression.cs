using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_race_trait_content_registry_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialRaceTraitRegistryValidatesWithoutErrors();
        TestOfficialIdentityContentDomainValidatesWithoutErrors();

        if (_failures.Count == 0)
        {
            GD.Print("Race trait content registry regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Race trait content registry regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestOfficialRaceTraitRegistryValidatesWithoutErrors()
    {
        RaceTraitContentRegistry registry = new();
        AssertEmpty(
            ToList(registry.validate()),
            "Official race trait content should validate without errors."
        );
    }

    private void TestOfficialIdentityContentDomainValidatesWithoutErrors()
    {
        List<string> errors = new();
        AppendErrors(errors, new RaceContentRegistry().validate());
        AppendErrors(errors, new SubraceContentRegistry().validate());
        AppendErrors(errors, new RaceTraitContentRegistry().validate());
        AppendErrors(errors, new AgeContentRegistry().validate());
        AppendErrors(errors, new BloodlineContentRegistry().validate());
        AppendErrors(errors, new AscensionContentRegistry().validate());
        AppendErrors(errors, new StageAdvancementContentRegistry().validate());

        ProgressionContentRegistry progressionRegistry = new();
        GStringArray phase2Errors = new();
        progressionRegistry._append_identity_phase2_validation_errors(phase2Errors);
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
        _failures.Add($"{message} errors=[{string.Join(", ", errors)}]");
    }
}
