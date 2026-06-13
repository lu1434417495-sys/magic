using System;
using System.Reflection;
using Godot;

// Narrow guard for the ground-AoE setup scan prefilter in
// BattleAiMoveToRangeCandidateEvaluator. The scan now iterates the cast-range bounding box
// instead of the whole map. This regression proves the box is a safe superset: every map cell
// that could pass the per-cell distance check (distance-to-footprint <= castRange) still lies
// inside the box, so no usable AoE center is silently dropped.
public partial class run_battle_ai_aoe_setup_scan_bounds_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        MethodInfo boundsMethod = FindStaticMethod("ComputeGroundAoeScanBounds");
        MethodInfo distanceMethod = FindStaticMethod("DistanceFromAnchorFootprintToCoord");
        if (boundsMethod == null || distanceMethod == null)
        {
            _test.Fail(
                "BattleAiMoveToRangeCandidateEvaluator must expose ComputeGroundAoeScanBounds and "
                    + "DistanceFromAnchorFootprintToCoord for the scan-bounds invariant guard."
            );
            return Report();
        }

        var mapSize = new Vector2I(30, 18);
        Vector2I[] footprints = { new(1, 1), new(2, 1), new(1, 3), new(2, 2) };
        Vector2I[] anchors =
        {
            new(0, 0),
            new(29, 17),
            new(15, 9),
            new(1, 16),
            new(28, 1),
        };
        int[] castRanges = { 0, 1, 2, 4, 7, 40 };

        foreach (Vector2I footprint in footprints)
        {
            foreach (Vector2I anchor in anchors)
            {
                foreach (int castRange in castRanges)
                {
                    AssertBoundsAreSafeSuperset(
                        boundsMethod,
                        distanceMethod,
                        anchor,
                        footprint,
                        castRange,
                        mapSize
                    );
                }
            }
        }

        return Report();
    }

    private void AssertBoundsAreSafeSuperset(
        MethodInfo boundsMethod,
        MethodInfo distanceMethod,
        Vector2I anchor,
        Vector2I footprint,
        int castRange,
        Vector2I mapSize
    )
    {
        object rawBounds = boundsMethod.Invoke(
            null,
            new object[] { anchor, footprint, castRange, mapSize }
        );
        if (rawBounds is not ValueTuple<int, int, int, int> bounds)
        {
            _test.Fail($"scan bounds must be a 4-int tuple. anchor={anchor} range={castRange}");
            return;
        }
        int minX = bounds.Item1;
        int maxX = bounds.Item2;
        int minY = bounds.Item3;
        int maxY = bounds.Item4;

        // Box must stay within the map so it can be used directly as iteration limits.
        if (minX < 0 || minY < 0 || maxX > mapSize.X - 1 || maxY > mapSize.Y - 1)
        {
            _test.Fail(
                $"scan box must be clamped to the map. box=({minX},{maxX},{minY},{maxY}) map={mapSize}"
            );
        }

        for (int y = 0; y < mapSize.Y; y += 1)
        {
            for (int x = 0; x < mapSize.X; x += 1)
            {
                var center = new Vector2I(x, y);
                int distance = (int)
                    distanceMethod.Invoke(null, new object[] { anchor, footprint, center });
                bool withinRange = distance >= 0 && distance <= castRange;
                if (!withinRange)
                {
                    continue;
                }
                bool insideBox = x >= minX && x <= maxX && y >= minY && y <= maxY;
                if (!insideBox)
                {
                    _test.Fail(
                        $"in-range cell dropped by scan box. cell={center} distance={distance} "
                            + $"range={castRange} anchor={anchor} footprint={footprint} "
                            + $"box=({minX},{maxX},{minY},{maxY})"
                    );
                }
            }
        }
    }

    private static MethodInfo FindStaticMethod(string methodName)
    {
        foreach (
            MethodInfo method in typeof(BattleAiMoveToRangeCandidateEvaluator).GetMethods(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
            )
        )
        {
            if (method.Name == methodName)
            {
                return method;
            }
        }
        return null;
    }

    private int Report() => _test.Finish("AoE setup scan-bounds regression");
}
