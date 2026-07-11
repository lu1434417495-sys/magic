using Godot;

public partial class run_battle_seed_source_regression : LifecycleTestSceneTree
{
    private const int LifecycleSoakSeed = 0x5A17_2026;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFacadeDelegatesToInjectedSource();
        TestFixedSourceReturnsConfiguredSeed();

        RequestTestExit(_test.Finish("Battle seed source regression"));
    }

    private void TestFacadeDelegatesToInjectedSource()
    {
        EncounterAnchorData encounterAnchor = BuildEncounterAnchor();
        RecordingBattleSeedSource seedSource = new(1729);
        using GameRuntimeFacade facade = new(seedSource);

        _test.Eq(
            facade._build_battle_seed(encounterAnchor),
            1729,
            "GameRuntimeFacade 应返回注入 source 生成的战斗 seed。"
        );
        _test.Eq(seedSource.CallCount, 1, "每次构建战斗 seed 应只调用 source 一次。");
        _test.True(
            ReferenceEquals(seedSource.LastEncounterAnchor, encounterAnchor),
            "战斗 seed source 应收到原始 encounter anchor。"
        );

        _test.Eq(
            facade._build_battle_seed(null),
            0,
            "空 encounter anchor 应保持原有的零 seed 行为。"
        );
        _test.Eq(seedSource.CallCount, 1, "空 encounter anchor 不应调用 seed source。");
    }

    private void TestFixedSourceReturnsConfiguredSeed()
    {
        EncounterAnchorData encounterAnchor = BuildEncounterAnchor();
        FixedBattleSeedSource seedSource = new(LifecycleSoakSeed);
        using GameRuntimeFacade facade = new(seedSource);

        _test.Eq(
            facade._build_battle_seed(encounterAnchor),
            LifecycleSoakSeed,
            "FixedBattleSeedSource 应保留 lifecycle soak 配置的 seed。"
        );
    }

    private static EncounterAnchorData BuildEncounterAnchor() =>
        new()
        {
            entity_id = "battle_seed_source_test",
            display_name = "Battle Seed Source Test",
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            enemy_roster_template_id = "wolf_pack",
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
        };

    private sealed class RecordingBattleSeedSource : IBattleSeedSource
    {
        private readonly int _seed;

        internal RecordingBattleSeedSource(int seed)
        {
            _seed = seed;
        }

        internal int CallCount { get; private set; }
        internal EncounterAnchorData LastEncounterAnchor { get; private set; }

        public int NextSeed(EncounterAnchorData encounterAnchor)
        {
            CallCount++;
            LastEncounterAnchor = encounterAnchor;
            return _seed;
        }
    }
}
