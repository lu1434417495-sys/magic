using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_barrier_geometry_contract_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestGeometryServiceIsPlainStaticTypedCSharp();
        TestFootprintTransitionDetectsLargeUnitBoundaryCrossing();
        TestFootprintTransitionDoesNotBlockInsideToInside();
        TestProjectedLineContract();
        TestCoordInsideBarrierContract();

        if (_failures.Count == 0)
        {
            GD.Print("Barrier geometry contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Barrier geometry contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestGeometryServiceIsPlainStaticTypedCSharp()
    {
        Type serviceType = typeof(BattleBarrierGeometryService);
        AssertTrue(
            serviceType.IsAbstract && serviceType.IsSealed,
            "BattleBarrierGeometryService 应是 plain static C# helper。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleBarrierGeometryService 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(serviceType, "GlobalClassAttribute"),
            "BattleBarrierGeometryService 不应注册 GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("classify_footprint_transition") == null,
            "BattleBarrierGeometryService 不应保留 GDScript Dictionary wrapper。"
        );
    }

    private void TestFootprintTransitionDetectsLargeUnitBoundaryCrossing()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        List<Vector2I> fromFootprint = Footprint(new Vector2I(5, 1), new Vector2I(2, 2));
        List<Vector2I> toFootprint = Footprint(new Vector2I(4, 1), new Vector2I(2, 2));

        BattleBarrierFootprintTransition transition =
            BattleBarrierGeometryService.ClassifyFootprintTransition(
                fromFootprint,
                toFootprint,
                barrierCoords
            );

        AssertTrue(
            transition.CrossesBoundary,
            "Large footprint crossing must trigger even when anchor-only checks would miss it."
        );
        AssertFalse(
            transition.FromInside,
            "The large unit source footprint starts outside the barrier."
        );
        AssertTrue(
            transition.ToInside,
            "The large unit destination footprint overlaps the barrier."
        );
    }

    private void TestFootprintTransitionDoesNotBlockInsideToInside()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        List<Vector2I> fromFootprint = Footprint(new Vector2I(2, 2), Vector2I.One);
        List<Vector2I> toFootprint = Footprint(new Vector2I(3, 2), Vector2I.One);

        BattleBarrierFootprintTransition transition =
            BattleBarrierGeometryService.ClassifyFootprintTransition(
                fromFootprint,
                toFootprint,
                barrierCoords
            );

        AssertFalse(
            transition.CrossesBoundary,
            "Inside-to-inside footprint transitions must not trigger barrier passage."
        );
        AssertTrue(transition.FromInside, "Source footprint must be classified inside.");
        AssertTrue(transition.ToInside, "Destination footprint must be classified inside.");
    }

    private void TestProjectedLineContract()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        AssertTrue(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 2),
                new Vector2I(-1, 2),
                barrierCoords
            ),
            "Outside-to-outside projected lines that pass through the barrier must be blocked."
        );
        AssertFalse(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 4),
                new Vector2I(6, 4),
                barrierCoords
            ),
            "Outside-to-outside projected lines that miss the barrier must not be blocked."
        );
        AssertFalse(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(2, 2),
                new Vector2I(3, 2),
                barrierCoords
            ),
            "Inside-to-inside projected lines must not be blocked by the boundary."
        );
    }

    private void TestCoordInsideBarrierContract()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        AssertTrue(
            BattleBarrierGeometryService.CoordInsideBarrier(new Vector2I(2, 2), barrierCoords),
            "Barrier center must be inside the barrier."
        );
        AssertFalse(
            BattleBarrierGeometryService.CoordInsideBarrier(new Vector2I(6, 6), barrierCoords),
            "Uncovered coord must be outside the barrier."
        );
    }

    private static List<Vector2I> Footprint(Vector2I anchor, Vector2I size)
    {
        var coords = new List<Vector2I>();
        Vector2I normalizedSize = new(Math.Max(size.X, 1), Math.Max(size.Y, 1));
        for (int y = 0; y < normalizedSize.Y; y++)
        {
            for (int x = 0; x < normalizedSize.X; x++)
            {
                coords.Add(anchor + new Vector2I(x, y));
            }
        }
        return coords;
    }

    private static List<Vector2I> DiamondArea(Vector2I center, int radius)
    {
        var coords = new List<Vector2I>();
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                Vector2I coord = new(x, y);
                if (Math.Abs(coord.X - center.X) + Math.Abs(coord.Y - center.Y) <= radius)
                {
                    coords.Add(coord);
                }
            }
        }
        return coords;
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }
}
