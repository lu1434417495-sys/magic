using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_base_attack_bonus_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSingleClassFullBabTable();
        TestSingleClassThreeQuarterBabTable();
        TestSingleClassHalfBabTable();
        TestMultiClassAccumulatesNumeratorBeforeFloor();
        TestTotalRankCappedAtTwentyKeepsBabAtOrBelowTen();
        TestUnknownProgressionFallsBackToHalf();
        TestAttributeServiceWritesBaseAttackBonusForFullWarrior();
        TestAttributeServiceExcludesInactiveAndHiddenProfessions();
        TestAttributeServiceMultiClassMatchesStaticCalculation();
        TestAttributeServiceProtectedCustomStatSourceMapping();

        if (_failures.Count == 0)
        {
            GD.Print("Base attack bonus regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Base attack bonus regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSingleClassFullBabTable()
    {
        foreach ((int rank, int expected) in new[]
        {
            (1, 0), (2, 1), (3, 1), (4, 2), (5, 2),
            (6, 3), (7, 3), (8, 4), (9, 4), (10, 5),
            (11, 5), (12, 6), (13, 6), (14, 7), (15, 7),
            (16, 8), (17, 8), (18, 9), (19, 9), (20, 10),
        })
        {
            int actual = AttributeSnapshot.calculate_base_attack_bonus(
                Pairs((rank, AttributeSnapshot.BAB_PROGRESSION_FULL()))
            );
            AssertEq(actual, expected, $"Full BAB rank {rank} 应为 {expected}。");
        }
    }

    private void TestSingleClassThreeQuarterBabTable()
    {
        foreach ((int rank, int expected) in new[]
        {
            (1, 0), (3, 1), (5, 1), (6, 2), (8, 3),
            (10, 3), (11, 4), (14, 5), (18, 6), (20, 7),
        })
        {
            int actual = AttributeSnapshot.calculate_base_attack_bonus(
                Pairs((rank, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER()))
            );
            AssertEq(actual, expected, $"3/4 BAB rank {rank} 应为 {expected}。");
        }
    }

    private void TestSingleClassHalfBabTable()
    {
        foreach ((int rank, int expected) in new[]
        {
            (1, 0), (3, 0), (4, 1), (7, 1), (8, 2),
            (11, 2), (12, 3), (15, 3), (16, 4), (20, 5),
        })
        {
            int actual = AttributeSnapshot.calculate_base_attack_bonus(
                Pairs((rank, AttributeSnapshot.BAB_PROGRESSION_HALF()))
            );
            AssertEq(actual, expected, $"1/2 BAB rank {rank} 应为 {expected}。");
        }
    }

    private void TestMultiClassAccumulatesNumeratorBeforeFloor()
    {
        AssertEq(
            AttributeSnapshot.calculate_base_attack_bonus(
                Pairs(
                    (7, AttributeSnapshot.BAB_PROGRESSION_HALF()),
                    (5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER())
                )
            ),
            3,
            "法师 7 + 牧师 5 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
        );

        AssertEq(
            AttributeSnapshot.calculate_base_attack_bonus(
                Pairs(
                    (1, AttributeSnapshot.BAB_PROGRESSION_FULL()),
                    (1, AttributeSnapshot.BAB_PROGRESSION_HALF()),
                    (1, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER())
                )
            ),
            1,
            "战士 1 + 法师 1 + 牧师 1 应得 BAB 1（per-prof floor 会全归 0）。"
        );

        AssertEq(
            AttributeSnapshot.calculate_base_attack_bonus(
                Pairs(
                    (3, AttributeSnapshot.BAB_PROGRESSION_FULL()),
                    (3, AttributeSnapshot.BAB_PROGRESSION_HALF()),
                    (3, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER())
                )
            ),
            3,
            "战士 3 + 法师 3 + 牧师 3 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
        );
    }

    private void TestTotalRankCappedAtTwentyKeepsBabAtOrBelowTen()
    {
        GArray[] distributions =
        {
            Pairs((20, AttributeSnapshot.BAB_PROGRESSION_FULL())),
            Pairs((10, AttributeSnapshot.BAB_PROGRESSION_FULL()), (10, AttributeSnapshot.BAB_PROGRESSION_FULL())),
            Pairs((15, AttributeSnapshot.BAB_PROGRESSION_FULL()), (5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER())),
            Pairs((10, AttributeSnapshot.BAB_PROGRESSION_FULL()), (10, AttributeSnapshot.BAB_PROGRESSION_HALF())),
            Pairs(
                (5, AttributeSnapshot.BAB_PROGRESSION_FULL()),
                (5, AttributeSnapshot.BAB_PROGRESSION_FULL()),
                (5, AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER()),
                (5, AttributeSnapshot.BAB_PROGRESSION_HALF())
            ),
        };

        foreach (GArray pairs in distributions)
        {
            int bab = AttributeSnapshot.calculate_base_attack_bonus(pairs);
            AssertTrue(bab <= 10, $"总 rank <= 20 时 BAB 不应超 +10，得到 {bab}，分布 {Variant.From(pairs)}。");
        }
        AssertEq(
            AttributeSnapshot.calculate_base_attack_bonus(
                Pairs((20, AttributeSnapshot.BAB_PROGRESSION_FULL()))
            ),
            10,
            "纯 Full BAB rank 20 应为 +10。"
        );
    }

    private void TestUnknownProgressionFallsBackToHalf()
    {
        int unknownBab = AttributeSnapshot.calculate_base_attack_bonus(
            Pairs((10, new StringName("unknown_value")))
        );
        int halfBab = AttributeSnapshot.calculate_base_attack_bonus(
            Pairs((10, AttributeSnapshot.BAB_PROGRESSION_HALF()))
        );
        AssertEq(unknownBab, halfBab, "未知 BAB progression 应安全回退到 half。");
    }

    private void TestAttributeServiceWritesBaseAttackBonusForFullWarrior()
    {
        ProfessionDef warrior = MakeProfession("warrior", AttributeSnapshot.BAB_PROGRESSION_FULL());
        UnitProgress progress = MakeProgress("hero");
        progress.set_profession_progress(MakeProfessionProgress("warrior", 5, true, false));

        AttributeSnapshot snapshot = BuildSnapshot(progress, new[] { warrior });
        AssertEq(snapshot.get_value(AttributeService.BASE_ATTACK_BONUS_ID()), 2, "战士 rank 5 在 snapshot 中应写入 BAB 2。");
    }

    private void TestAttributeServiceExcludesInactiveAndHiddenProfessions()
    {
        ProfessionDef warrior = MakeProfession("warrior", AttributeSnapshot.BAB_PROGRESSION_FULL());
        ProfessionDef mage = MakeProfession("mage", AttributeSnapshot.BAB_PROGRESSION_HALF());

        UnitProgress inactiveProgress = MakeProgress("inactive_hero");
        inactiveProgress.set_profession_progress(MakeProfessionProgress("warrior", 10, false, false));
        inactiveProgress.set_profession_progress(MakeProfessionProgress("mage", 4, true, false));
        AttributeSnapshot inactiveSnapshot = BuildSnapshot(inactiveProgress, new[] { warrior, mage });
        AssertEq(
            inactiveSnapshot.get_value(AttributeService.BASE_ATTACK_BONUS_ID()),
            1,
            "未激活的战士 rank 10 不应贡献 BAB；仅法师 rank 4 (1/2) = 1。"
        );

        UnitProgress hiddenProgress = MakeProgress("hidden_hero");
        hiddenProgress.set_profession_progress(MakeProfessionProgress("warrior", 10, true, true));
        hiddenProgress.set_profession_progress(MakeProfessionProgress("mage", 4, true, false));
        AttributeSnapshot hiddenSnapshot = BuildSnapshot(hiddenProgress, new[] { warrior, mage });
        AssertEq(
            hiddenSnapshot.get_value(AttributeService.BASE_ATTACK_BONUS_ID()),
            1,
            "被隐藏的战士不应贡献 BAB；仅法师 rank 4 (1/2) = 1。"
        );
    }

    private void TestAttributeServiceMultiClassMatchesStaticCalculation()
    {
        ProfessionDef warrior = MakeProfession("warrior", AttributeSnapshot.BAB_PROGRESSION_FULL());
        ProfessionDef mage = MakeProfession("mage", AttributeSnapshot.BAB_PROGRESSION_HALF());
        ProfessionDef priest = MakeProfession("priest", AttributeSnapshot.BAB_PROGRESSION_THREE_QUARTER());

        UnitProgress progress = MakeProgress("multi_hero");
        progress.set_profession_progress(MakeProfessionProgress("warrior", 3, true, false));
        progress.set_profession_progress(MakeProfessionProgress("mage", 3, true, false));
        progress.set_profession_progress(MakeProfessionProgress("priest", 3, true, false));

        AttributeSnapshot snapshot = BuildSnapshot(progress, new[] { warrior, mage, priest });
        AssertEq(
            snapshot.get_value(AttributeService.BASE_ATTACK_BONUS_ID()),
            3,
            "战士 3 + 法师 3 + 牧师 3 在 service 应得 BAB 3，与静态算法一致。"
        );
    }

    private void TestAttributeServiceProtectedCustomStatSourceMapping()
    {
        UnitProgress progress = MakeProgress("hidden_luck_source_mapping");
        AttributeService service = new();
        service.setup(progress);
        StringName hiddenLuck = UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH();

        AssertTrue(
            !service.apply_permanent_attribute_change(hiddenLuck, 1, new GDictionary()),
            "protected custom stat 不应接受空 source_context。"
        );
        AssertEq(progress.unit_base_attributes.get_attribute_value(hiddenLuck), 0, "空 source_context 不应改写 hidden luck。");

        AssertTrue(
            !service.apply_permanent_attribute_change(
                hiddenLuck,
                1,
                new GDictionary
                {
                    ["source_type"] = AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT_ID(),
                    [AttributeService.PROTECTED_CUSTOM_STAT_WRITE_FLAG_ID()] = 1,
                }
            ),
            "protected custom stat 不应接受非 bool 的 story_script 写入 flag。"
        );
        AssertEq(progress.unit_base_attributes.get_attribute_value(hiddenLuck), 0, "非 bool flag 不应改写 hidden luck。");

        AssertTrue(
            service.apply_permanent_attribute_change(
                hiddenLuck,
                1,
                new GDictionary
                {
                    ["source_type"] = AttributeService.PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT_ID(),
                    ["source_id"] = "story_event",
                    [AttributeService.PROTECTED_CUSTOM_STAT_WRITE_FLAG_ID()] = true,
                }
            ),
            "story_script + 明确 bool flag 应允许改写 protected custom stat。"
        );
        AssertEq(progress.unit_base_attributes.get_attribute_value(hiddenLuck), 1, "bool flag 应改写 hidden luck。");
    }

    private static GArray Pairs(params (int Rank, StringName Progression)[] pairs)
    {
        GArray result = new();
        foreach ((int rank, StringName progression) in pairs)
        {
            result.Add(new GArray { rank, progression });
        }
        return result;
    }

    private static AttributeSnapshot BuildSnapshot(UnitProgress progress, IEnumerable<ProfessionDef> professionDefs)
    {
        AttributeService service = new();
        GDictionary indexedProfessionDefs = new();
        foreach (ProfessionDef professionDef in professionDefs)
        {
            indexedProfessionDefs[professionDef.profession_id] = professionDef;
        }
        service.setup(progress, new GDictionary(), indexedProfessionDefs);
        return service.get_snapshot();
    }

    private static ProfessionDef MakeProfession(StringName professionId, StringName progression)
    {
        return new ProfessionDef
        {
            profession_id = professionId,
            display_name = professionId.ToString(),
            description = "Fixture profession.",
            max_rank = 20,
            bab_progression = progression,
        };
    }

    private static UnitProfessionProgress MakeProfessionProgress(
        StringName professionId,
        int rank,
        bool isActive,
        bool isHidden
    )
    {
        return new UnitProfessionProgress
        {
            profession_id = professionId,
            rank = rank,
            is_active = isActive,
            is_hidden = isHidden,
        };
    }

    private static UnitProgress MakeProgress(StringName unitId)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
        };
        foreach (StringName attributeId in new[]
        {
            UnitBaseAttributes.STRENGTH(),
            UnitBaseAttributes.AGILITY(),
            UnitBaseAttributes.CONSTITUTION(),
            UnitBaseAttributes.PERCEPTION(),
            UnitBaseAttributes.INTELLIGENCE(),
            UnitBaseAttributes.WILLPOWER(),
        })
        {
            progress.unit_base_attributes.set_attribute_value(attributeId, 10);
        }
        return progress;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
