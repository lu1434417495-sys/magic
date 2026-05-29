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
        int oldStep = get_world_step(world_data);
        if (oldStep < 0)
        {
            return new GDictionary
            {
                ["old_step"] = -1,
                ["new_step"] = -1,
                ["old_day"] = -1,
                ["new_day"] = -1,
                ["changed"] = false,
                ["day_changed"] = false,
                ["days_elapsed"] = 0,
                ["error_code"] = "invalid_world_step",
            };
        }

        int oldDay = step_to_day(oldStep);
        int nextStep = oldStep + Mathf.Max(delta_steps, 0);
        int newDay = step_to_day(nextStep);
        if (world_data != null)
        {
            world_data["world_step"] = nextStep;
        }

        return new GDictionary
        {
            ["old_step"] = oldStep,
            ["new_step"] = nextStep,
            ["old_day"] = oldDay,
            ["new_day"] = newDay,
            ["changed"] = nextStep != oldStep,
            ["day_changed"] = newDay != oldDay,
            ["days_elapsed"] = newDay - oldDay,
        };
    }

    private static bool HasValidWorldStep(GDictionary worldData)
    {
        if (worldData == null || !worldData.ContainsKey("world_step"))
        {
            return false;
        }
        return GdInterop.HasInt(worldData, "world_step")
            && GdInterop.GetInt(worldData, "world_step") >= 0;
    }
}
