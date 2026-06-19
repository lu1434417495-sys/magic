using Godot;

public partial class run_trait_dictionary_boundary_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTraitFormalSourcesDoNotUseDictionaryBackedRuntimeData();
        TestTraitConfigsDoNotUseDictionaryParams();

        Quit(_test.Finish("Trait dictionary boundary regression"));
    }

    private void TestTraitFormalSourcesDoNotUseDictionaryBackedRuntimeData()
    {
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitDef.cs",
            "Godot.Collections.Dictionary @params"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitContentRegistry.cs",
            "traitDef.@params"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitContentRegistry.cs",
            "Dictionary<StringName"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitContentRegistry.cs",
            "IReadOnlyDictionary<StringName"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitTriggerContentRules.cs",
            "DISPATCH_TRIGGER_TYPES"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitTriggerContentRules.cs",
            "GetDispatchTriggerTypes"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitTriggerContentRules.cs",
            "Dictionary<"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitTriggerContentRules.cs",
            "IReadOnlyDictionary"
        );
        AssertFileDoesNotContain(
            "res://scripts/player/progression/TraitInstanceState.cs",
            "public Godot.Collections.Dictionary roll_values"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/progression/CharacterTraitService.cs",
            "Dictionary<StringName"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/inventory/EquipmentTraitRollService.cs",
            "Dictionary<StringName"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/progression/EffectiveTrait.cs",
            "Godot.Collections.Dictionary RollValues"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/progression/EffectiveTrait.cs",
            "Dictionary<StringName"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/progression/EffectiveTrait.cs",
            "ToBattlePayload("
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/battle/core/BattleUnitState.cs",
            "public GArray effective_trait_instances"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/battle/core/BattleUnitState.cs",
            "ParseEffectiveTraitPayload"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/battle/runtime/TraitTriggerHooks.cs",
            "GetAllEffectiveInstances(GArray"
        );
        AssertFileDoesNotContain(
            "res://scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs",
            "BattleEffectiveTraitProjection(GArray"
        );
    }

    private void TestTraitConfigsDoNotUseDictionaryParams()
    {
        using DirAccess dir = DirAccess.Open("res://data/configs/traits");
        _test.True(dir != null, "Trait config directory should exist.");
        if (dir == null)
            return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                AssertFileDoesNotContain($"res://data/configs/traits/{fileName}", "params = {");
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
    }

    private void AssertFileDoesNotContain(string path, string forbidden)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        _test.True(file != null, $"{path} should be readable.");
        if (file == null)
            return;

        string text = file.GetAsText();
        _test.False(
            text.Contains(forbidden),
            $"{path} should not contain dictionary-backed trait runtime pattern: {forbidden}"
        );
    }
}
