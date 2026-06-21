using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_barrier_store_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMalformedBarrierPayloadDoesNotReplaceLiveStore();
        }
        finally
        {
            GodotSharpCleanup.CollectPendingFinalizers();
        }
        Quit(_test.Finish("Battle barrier store typed regression"));
    }

    private void TestMalformedBarrierPayloadDoesNotReplaceLiveStore()
    {
        BattleState state = new();
        try
        {
            BattleBarrierInstanceState barrier = BuildBarrier("barrier_a", 42);
            state.LayeredBarrierStore.Put(barrier.BarrierInstanceId, barrier);

            _test.Eq(state.LayeredBarrierFieldCount, 1, "typed barrier store 应记录一个屏障。");

            var malformedPayload = new GDictionary { ["barrier_a"] = "not_a_barrier_dictionary" };
            state.ReplaceLayeredBarrierFieldsPayload(malformedPayload);

            _test.True(
                state.LayeredBarrierStore.TryGet("barrier_a", out BattleBarrierInstanceState stored),
                "非法字典 payload 不应清空 live typed barrier store。"
            );
            _test.Eq(stored.RemainingTu, 42, "非法 payload 不应改写已有屏障持续时间。");
            _test.Eq(stored.Layers.Count, 1, "非法 payload 不应丢失已有屏障层。");

            stored.RemainingTu = 1;
            _test.True(
                state.LayeredBarrierStore.TryGet("barrier_a", out BattleBarrierInstanceState storedAgain),
                "typed barrier store 应继续返回已有屏障。"
            );
            _test.Eq(storedAgain.RemainingTu, 42, "从 store 读出的屏障副本不应直接改写 live state。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private static BattleBarrierInstanceState BuildBarrier(StringName barrierId, int remainingTu)
    {
        var layer = new BattleBarrierLayerState
        {
            LayerId = "blue",
            DisplayName = "Blue",
            Order = 1,
            Broken = false,
        };
        layer.SetBlockedCategories(new[] { new StringName("magic") });

        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = barrierId,
            ProfileId = "typed_barrier",
            DisplayName = "Typed Barrier",
            SourceUnitId = "caster",
            SourceSkillId = "barrier_skill",
            AnchorCoord = new Vector2I(1, 1),
            RadiusCells = 1,
            AreaPattern = "diamond",
            RemainingTu = remainingTu,
            CreatedTu = 3,
            SaveDc = 15,
        };
        barrier.SetLayers(new[] { layer });
        return barrier;
    }
}
