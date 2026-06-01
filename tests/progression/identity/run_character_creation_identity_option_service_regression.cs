using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_character_creation_identity_option_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();

        ProgressionContentRegistry registry = MakeFixtureRegistry();
        TestCollectSubracesUsesOnlyBidirectionalEdges(registry);
        TestCollectSubracesFiltersMissingAndOrdersLegalCandidates(registry);
        TestChooseSubraceHandlesDefaultAndStaleSelection(registry);
        TestRaceWithoutLegalSubraceHasNoFallback(registry);
        TestStringKeyContentSourceIsSupported(registry);
        TestCollectCreationRacesRequiresLegalPair(registry);
        TestIsValidCreationPairRejectsParentOnlyAndWrongParent(registry);
        registry.Dispose();

        if (_failures.Count == 0)
        {
            GD.Print("Character creation identity option service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Character creation identity option service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(CharacterCreationIdentityOptionService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "CharacterCreationIdentityOptionService 应是普通 C# static helper，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "CharacterCreationIdentityOptionService 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void TestCollectSubracesUsesOnlyBidirectionalEdges(
        ProgressionContentRegistry registry
    )
    {
        IReadOnlyList<StringName> ids =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(registry, "human");

        AssertTrue(ContainsId(ids, "common_human"), "合法 common_human 应进入 human 建卡候选。");
        AssertTrue(ContainsId(ids, "noble_human"), "合法 noble_human 应进入 human 建卡候选。");
        AssertFalse(
            ContainsId(ids, "parent_only_human"),
            "parent_race_id 指向 human 但未被 race.subrace_ids 列出的 subrace 不得进入候选。"
        );
        AssertFalse(
            ContainsId(ids, "wrong_parent"),
            "race.subrace_ids 列出但 parent_race_id 不匹配的 subrace 不得进入候选。"
        );
    }

    private void TestCollectSubracesFiltersMissingAndOrdersLegalCandidates(
        ProgressionContentRegistry registry
    )
    {
        IReadOnlyList<StringName> ids =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(registry, "human");
        AssertIdsEq(
            ids,
            new[] { new StringName("common_human"), new StringName("noble_human") },
            "候选应过滤 missing/wrong-parent，并保持稳定字典序。"
        );
    }

    private void TestChooseSubraceHandlesDefaultAndStaleSelection(
        ProgressionContentRegistry registry
    )
    {
        AssertEq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(registry, "human", ""),
            new StringName("common_human"),
            "合法 default_subrace_id 应优先成为选择。"
        );
        AssertEq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                registry,
                "human",
                "noble_human"
            ),
            new StringName("noble_human"),
            "当前选择仍合法时应保留。"
        );
        AssertEq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                registry,
                "human",
                "parent_only_human"
            ),
            new StringName("common_human"),
            "stale parent-only subrace 必须被纠正为合法候选。"
        );
        AssertEq(
            CharacterCreationIdentityOptionService.ChooseSubraceId(
                registry,
                "invalid_default_race",
                ""
            ),
            new StringName("valid_for_invalid_default"),
            "default_subrace_id 非法时应选择第一个合法候选。"
        );
    }

    private void TestRaceWithoutLegalSubraceHasNoFallback(ProgressionContentRegistry registry)
    {
        IReadOnlyList<StringName> ids =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(
                registry,
                "orphan_race"
            );
        StringName choice = CharacterCreationIdentityOptionService.ChooseSubraceId(
            registry,
            "orphan_race",
            ""
        );

        AssertEq(ids.Count, 0, "无合法 subrace 的 race 应返回空候选，不扫描 parent_race fallback。");
        AssertEq(choice, new StringName(""), "无合法 subrace 的 race 不应产生默认选择。");
    }

    private void TestStringKeyContentSourceIsSupported(ProgressionContentRegistry registry)
    {
        IReadOnlyList<StringName> ids =
            CharacterCreationIdentityOptionService.CollectSubraceIdsForRace(
                registry,
                "string_key_race"
            );
        StringName raceChoice = CharacterCreationIdentityOptionService.ChooseRaceId(
            registry,
            "",
            "string_key_race"
        );
        StringName subraceChoice = CharacterCreationIdentityOptionService.ChooseSubraceId(
            registry,
            "string_key_race",
            ""
        );

        AssertIdsEq(
            ids,
            new[] { new StringName("string_key_subrace") },
            "String 字典 key 的 race/subrace 也应被候选服务解析为 StringName。"
        );
        AssertEq(
            raceChoice,
            new StringName("string_key_race"),
            "String key race 可作为合法默认 race。"
        );
        AssertEq(
            subraceChoice,
            new StringName("string_key_subrace"),
            "String key subrace 可作为合法默认 subrace。"
        );
    }

    private void TestCollectCreationRacesRequiresLegalPair(ProgressionContentRegistry registry)
    {
        IReadOnlyList<StringName> ids =
            CharacterCreationIdentityOptionService.CollectCreationRaceIds(registry);

        AssertTrue(ContainsId(ids, "human"), "human 有合法 subrace，应进入 race 候选。");
        AssertTrue(
            ContainsId(ids, "invalid_default_race"),
            "default 非法但存在合法 subrace 的 race 仍应进入候选。"
        );
        AssertTrue(
            ContainsId(ids, "string_key_race"),
            "String key race 有合法 pair，应进入 race 候选。"
        );
        AssertFalse(
            ContainsId(ids, "orphan_race"),
            "无合法 subrace 的 race 不应进入建卡 race 候选。"
        );
    }

    private void TestIsValidCreationPairRejectsParentOnlyAndWrongParent(
        ProgressionContentRegistry registry
    )
    {
        AssertTrue(
            CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                registry,
                "human",
                "common_human"
            ),
            "合法 race/subrace pair 应通过。"
        );
        AssertFalse(
            CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                registry,
                "human",
                "parent_only_human"
            ),
            "parent-only subrace 不得作为合法 pair。"
        );
        AssertFalse(
            CharacterCreationIdentityOptionService.IsValidCreationRaceSubracePair(
                registry,
                "human",
                "wrong_parent"
            ),
            "parent mismatch subrace 不得作为合法 pair。"
        );
    }

    private static ProgressionContentRegistry MakeFixtureRegistry()
    {
        ProgressionContentRegistry registry = new();
        RaceDef human = MakeRace(
            "human",
            "common_human",
            new[] { "common_human", "noble_human", "wrong_parent", "missing_subrace" }
        );
        RaceDef orphanRace = MakeRace("orphan_race", "", Array.Empty<string>());
        RaceDef invalidDefaultRace = MakeRace(
            "invalid_default_race",
            "parent_only_invalid_default",
            new[] { "valid_for_invalid_default" }
        );
        RaceDef stringKeyRace = MakeRace(
            "string_key_race",
            "string_key_subrace",
            new[] { "string_key_subrace" }
        );

        registry._race_defs = new GDictionary
        {
            [human.race_id] = human,
            [orphanRace.race_id] = orphanRace,
            [invalidDefaultRace.race_id] = invalidDefaultRace,
            ["string_key_race"] = stringKeyRace,
        };
        registry._subrace_defs = new GDictionary
        {
            ["common_human"] = MakeSubrace("common_human", "human"),
            ["noble_human"] = MakeSubrace("noble_human", "human"),
            ["parent_only_human"] = MakeSubrace("parent_only_human", "human"),
            ["wrong_parent"] = MakeSubrace("wrong_parent", "elf"),
            ["parent_only_invalid_default"] = MakeSubrace(
                "parent_only_invalid_default",
                "invalid_default_race"
            ),
            ["valid_for_invalid_default"] = MakeSubrace(
                "valid_for_invalid_default",
                "invalid_default_race"
            ),
            ["string_key_subrace"] = MakeSubrace("string_key_subrace", "string_key_race"),
        };
        return registry;
    }

    private static RaceDef MakeRace(
        StringName raceId,
        StringName defaultSubraceId,
        IEnumerable<string> subraceIds
    )
    {
        RaceDef race = new()
        {
            race_id = raceId,
            display_name = raceId.ToString(),
            age_profile_id = "human_age_profile",
            default_subrace_id = defaultSubraceId,
            body_size_category = "medium",
        };
        foreach (string subraceId in subraceIds)
            race.subrace_ids.Add(subraceId);
        return race;
    }

    private static SubraceDef MakeSubrace(StringName subraceId, StringName parentRaceId) =>
        new()
        {
            subrace_id = subraceId,
            parent_race_id = parentRaceId,
            display_name = subraceId.ToString(),
        };

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }

    private void AssertIdsEq(
        IReadOnlyList<StringName> actual,
        IReadOnlyList<StringName> expected,
        string message
    )
    {
        if (actual.Count != expected.Count)
        {
            _failures.Add($"{message} | actual={FormatIds(actual)} expected={FormatIds(expected)}");
            return;
        }

        for (int index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _failures.Add(
                    $"{message} | actual={FormatIds(actual)} expected={FormatIds(expected)}"
                );
                return;
            }
        }
    }

    private static bool ContainsId(IEnumerable<StringName> ids, StringName targetId)
    {
        foreach (StringName id in ids)
        {
            if (id == targetId)
                return true;
        }
        return false;
    }

    private static string FormatIds(IEnumerable<StringName> ids)
    {
        List<string> values = new();
        foreach (StringName id in ids)
            values.Add(id.ToString());
        return $"[{string.Join(", ", values)}]";
    }
}
