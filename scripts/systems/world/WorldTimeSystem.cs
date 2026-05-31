using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldTimeSystem : RefCounted
{
    public const int STEPS_PER_DAY = 15;

    public int get_world_step(GDictionary world_data)
    {
        return HasValidWorldStep(world_data) ? world_data["world_step"].AsInt32() : -1;
    }

    public int get_world_day(GDictionary world_data)
    {
        int step = get_world_step(world_data);
        return step < 0 ? -1 : step / STEPS_PER_DAY;
    }

    public static int step_to_day(int world_step)
    {
        return world_step < 0 ? -1 : world_step / STEPS_PER_DAY;
    }

    public GDictionary advance(GDictionary world_data, int delta_steps)
    {
        return AdvanceWorldData(world_data, delta_steps).ToDictionary();
    }

    internal WorldTimeAdvanceResult AdvanceWorldData(GDictionary worldData, int deltaSteps)
    {
        WorldTimeAdvanceResult result = AdvanceWorldStep(get_world_step(worldData), deltaSteps);
        if (result.IsValid && worldData != null)
        {
            worldData["world_step"] = result.new_step;
        }
        return result;
    }

    internal static WorldTimeAdvanceResult AdvanceWorldStep(int oldStep, int deltaSteps)
    {
        if (oldStep < 0)
        {
            return WorldTimeAdvanceResult.Invalid("invalid_world_step");
        }

        int oldDay = step_to_day(oldStep);
        int nextStep = oldStep + Mathf.Max(deltaSteps, 0);
        int newDay = step_to_day(nextStep);
        return new WorldTimeAdvanceResult(
            oldStep,
            nextStep,
            oldDay,
            newDay,
            nextStep != oldStep,
            newDay != oldDay,
            newDay - oldDay
        );
    }

    private static bool HasValidWorldStep(GDictionary worldData)
    {
        if (worldData == null || !worldData.ContainsKey("world_step"))
        {
            return false;
        }
        return worldData["world_step"].VariantType == Variant.Type.Int
            && worldData["world_step"].AsInt32() >= 0;
    }
}

internal sealed class WorldTimeAdvanceResult
{
    public readonly int old_step;
    public readonly int new_step;
    public readonly int old_day;
    public readonly int new_day;
    public readonly bool changed;
    public readonly bool day_changed;
    public readonly int days_elapsed;
    public readonly string error_code;

    public bool IsValid => string.IsNullOrEmpty(error_code);

    public WorldTimeAdvanceResult(
        int oldStep,
        int newStep,
        int oldDay,
        int newDay,
        bool changed,
        bool dayChanged,
        int daysElapsed,
        string errorCode = ""
    )
    {
        old_step = oldStep;
        new_step = newStep;
        old_day = oldDay;
        new_day = newDay;
        this.changed = changed;
        day_changed = dayChanged;
        days_elapsed = daysElapsed;
        error_code = errorCode ?? "";
    }

    public static WorldTimeAdvanceResult Invalid(string errorCode)
    {
        return new WorldTimeAdvanceResult(
            -1,
            -1,
            -1,
            -1,
            false,
            false,
            0,
            errorCode
        );
    }

    public GDictionary ToDictionary()
    {
        var result = new GDictionary
        {
            ["old_step"] = old_step,
            ["new_step"] = new_step,
            ["old_day"] = old_day,
            ["new_day"] = new_day,
            ["changed"] = changed,
            ["day_changed"] = day_changed,
            ["days_elapsed"] = days_elapsed,
        };
        if (!IsValid)
        {
            result["error_code"] = error_code;
        }
        return result;
    }
}
