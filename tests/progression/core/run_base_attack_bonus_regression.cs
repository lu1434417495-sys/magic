using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_base_attack_bonus_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

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
        TestUnknownProgressionContributesNoBab();
        TestAttributeServiceWritesBaseAttackBonusForFullWarrior();
        TestAttributeServiceExcludesInactiveAndHiddenProfessions();
        TestAttributeServiceMultiClassMatchesStaticCalculation();
        TestAttributeServiceProtectedCustomStatSourceMapping();

        RequestTestExit(_test.Finish("Base attack bonus regression"));
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
            int actual = AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs((rank, ProfessionBaseAttackProgression.Full))
            );
            _test.Eq(actual, expected, $"Full BAB rank {rank} 应为 {expected}。");
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
            int actual = AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs((rank, ProfessionBaseAttackProgression.ThreeQuarter))
            );
            _test.Eq(actual, expected, $"3/4 BAB rank {rank} 应为 {expected}。");
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
            int actual = AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs((rank, ProfessionBaseAttackProgression.Half))
            );
            _test.Eq(actual, expected, $"1/2 BAB rank {rank} 应为 {expected}。");
        }
    }

    private void TestMultiClassAccumulatesNumeratorBeforeFloor()
    {
        _test.Eq(
            AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs(
                    (7, ProfessionBaseAttackProgression.Half),
                    (5, ProfessionBaseAttackProgression.ThreeQuarter)
                )
            ),
            3,
            "法师 7 + 牧师 5 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
        );

        _test.Eq(
            AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs(
                    (1, ProfessionBaseAttackProgression.Full),
                    (1, ProfessionBaseAttackProgression.Half),
                    (1, ProfessionBaseAttackProgression.ThreeQuarter)
                )
            ),
            1,
            "战士 1 + 法师 1 + 牧师 1 应得 BAB 1（per-prof floor 会全归 0）。"
        );

        _test.Eq(
            AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs(
                    (3, ProfessionBaseAttackProgression.Full),
                    (3, ProfessionBaseAttackProgression.Half),
                    (3, ProfessionBaseAttackProgression.ThreeQuarter)
                )
            ),
            3,
            "战士 3 + 法师 3 + 牧师 3 应得 BAB 3（per-prof floor 会丢精度变成 2）。"
        );
    }

    private void TestTotalRankCappedAtTwentyKeepsBabAtOrBelowTen()
    {
        List<AttributeSnapshot.BaseAttackProgressionPair>[] distributions =
        {
            Pairs((20, ProfessionBaseAttackProgression.Full)),
            Pairs((10, ProfessionBaseAttackProgression.Full), (10, ProfessionBaseAttackProgression.Full)),
            Pairs((15, ProfessionBaseAttackProgression.Full), (5, ProfessionBaseAttackProgression.ThreeQuarter)),
            Pairs((10, ProfessionBaseAttackProgression.Full), (10, ProfessionBaseAttackProgression.Half)),
            Pairs(
                (5, ProfessionBaseAttackProgression.Full),
                (5, ProfessionBaseAttackProgression.Full),
                (5, ProfessionBaseAttackProgression.ThreeQuarter),
                (5, ProfessionBaseAttackProgression.Half)
            ),
        };

        foreach (List<AttributeSnapshot.BaseAttackProgressionPair> pairs in distributions)
        {
            int bab = AttributeSnapshot.CalculateBaseAttackBonus(pairs);
            _test.True(bab <= 10, $"总 rank <= 20 时 BAB 不应超 +10，得到 {bab}。");
        }
        _test.Eq(
            AttributeSnapshot.CalculateBaseAttackBonus(
                Pairs((20, ProfessionBaseAttackProgression.Full))
            ),
            10,
            "纯 Full BAB rank 20 应为 +10。"
        );
    }

    private void TestUnknownProgressionContributesNoBab()
    {
        int unknownBab = AttributeSnapshot.CalculateBaseAttackBonus(
            Pairs((10, ProfessionBaseAttackProgression.Unknown))
        );
        _test.Eq(unknownBab, 0, "未知 BAB progression 不应回退到 half。");
    }

    private void TestAttributeServiceWritesBaseAttackBonusForFullWarrior()
    {
        ProfessionDef warrior = MakeProfession("warrior", ProfessionBaseAttackProgression.Full);
        UnitProgress progress = MakeProgress("hero");
        progress.SetProfessionProgress(MakeProfessionProgress("warrior", 5, true, false));

        AttributeSnapshot snapshot = BuildSnapshot(progress, new[] { warrior });
        _test.Eq(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus)), 2, "战士 rank 5 在 snapshot 中应写入 BAB 2。");
    }

    private void TestAttributeServiceExcludesInactiveAndHiddenProfessions()
    {
        ProfessionDef warrior = MakeProfession("warrior", ProfessionBaseAttackProgression.Full);
        ProfessionDef mage = MakeProfession("mage", ProfessionBaseAttackProgression.Half);

        UnitProgress inactiveProgress = MakeProgress("inactive_hero");
        inactiveProgress.SetProfessionProgress(MakeProfessionProgress("warrior", 10, false, false));
        inactiveProgress.SetProfessionProgress(MakeProfessionProgress("mage", 4, true, false));
        AttributeSnapshot inactiveSnapshot = BuildSnapshot(inactiveProgress, new[] { warrior, mage });
        _test.Eq(
            inactiveSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus)),
            1,
            "未激活的战士 rank 10 不应贡献 BAB；仅法师 rank 4 (1/2) = 1。"
        );

        UnitProgress hiddenProgress = MakeProgress("hidden_hero");
        hiddenProgress.SetProfessionProgress(MakeProfessionProgress("warrior", 10, true, true));
        hiddenProgress.SetProfessionProgress(MakeProfessionProgress("mage", 4, true, false));
        AttributeSnapshot hiddenSnapshot = BuildSnapshot(hiddenProgress, new[] { warrior, mage });
        _test.Eq(
            hiddenSnapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus)),
            1,
            "被隐藏的战士不应贡献 BAB；仅法师 rank 4 (1/2) = 1。"
        );
    }

    private void TestAttributeServiceMultiClassMatchesStaticCalculation()
    {
        ProfessionDef warrior = MakeProfession("warrior", ProfessionBaseAttackProgression.Full);
        ProfessionDef mage = MakeProfession("mage", ProfessionBaseAttackProgression.Half);
        ProfessionDef priest = MakeProfession("priest", ProfessionBaseAttackProgression.ThreeQuarter);

        UnitProgress progress = MakeProgress("multi_hero");
        progress.SetProfessionProgress(MakeProfessionProgress("warrior", 3, true, false));
        progress.SetProfessionProgress(MakeProfessionProgress("mage", 3, true, false));
        progress.SetProfessionProgress(MakeProfessionProgress("priest", 3, true, false));

        AttributeSnapshot snapshot = BuildSnapshot(progress, new[] { warrior, mage, priest });
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus)),
            3,
            "战士 3 + 法师 3 + 牧师 3 在 service 应得 BAB 3，与静态算法一致。"
        );
    }

    private void TestAttributeServiceProtectedCustomStatSourceMapping()
    {
        UnitProgress progress = MakeProgress("hidden_luck_source_mapping");
        AttributeService service = new();
        service.Setup(progress);
        StringName hiddenLuck = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth);

        _test.True(
            !service.ApplyPermanentAttributeChange(
                hiddenLuck,
                1,
                AttributePermanentChangeSource.None
            ),
            "protected custom stat 不应接受 Unknown source。"
        );
        _test.Eq(progress.unit_base_attributes.GetAttributeValue(hiddenLuck), 0, "Unknown source 不应改写 hidden luck。");

        _test.True(
            !service.ApplyPermanentAttributeChange(
                hiddenLuck,
                1,
                new AttributePermanentChangeSource(
                    AttributePermanentChangeSourceKind.StoryScript,
                    "story_event",
                    false
                )
            ),
            "protected custom stat 不应接受未授权的 story_script source。"
        );
        _test.Eq(progress.unit_base_attributes.GetAttributeValue(hiddenLuck), 0, "未授权 story_script 不应改写 hidden luck。");

        _test.True(
            service.ApplyPermanentAttributeChange(
                hiddenLuck,
                1,
                new AttributePermanentChangeSource(
                    AttributePermanentChangeSourceKind.StoryScript,
                    "story_event",
                    true
                )
            ),
            "story_script + 明确授权应允许改写 protected custom stat。"
        );
        _test.Eq(progress.unit_base_attributes.GetAttributeValue(hiddenLuck), 1, "显式授权应改写 hidden luck。");
    }

    private static List<AttributeSnapshot.BaseAttackProgressionPair> Pairs(
        params (int Rank, ProfessionBaseAttackProgression Progression)[] pairs
    )
    {
        List<AttributeSnapshot.BaseAttackProgressionPair> result = new();
        foreach ((int rank, ProfessionBaseAttackProgression progression) in pairs)
        {
            result.Add(new AttributeSnapshot.BaseAttackProgressionPair(rank, progression));
        }
        return result;
    }

    private static AttributeSnapshot BuildSnapshot(UnitProgress progress, IEnumerable<ProfessionDef> professionDefs)
    {
        AttributeService service = new();
        Dictionary<StringName, ProfessionDef> indexedProfessionDefs = new();
        foreach (ProfessionDef professionDef in professionDefs)
        {
            indexedProfessionDefs[professionDef.profession_id] = professionDef;
        }
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                profession_defs = indexedProfessionDefs,
            }
        );
        return service.GetSnapshot();
    }

    private static ProfessionDef MakeProfession(
        StringName professionId,
        ProfessionBaseAttackProgression progression
    )
    {
        return new ProfessionDef
        {
            profession_id = professionId,
            display_name = professionId.ToString(),
            description = "Fixture profession.",
            max_rank = 20,
            BabProgressionKind = progression,
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
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength),
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution),
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception),
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence),
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower),
        })
        {
            progress.unit_base_attributes.SetAttributeValue(attributeId, 10);
        }
        return progress;
    }


}
