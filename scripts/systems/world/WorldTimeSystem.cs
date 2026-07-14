using System;

public sealed class WorldTimeSystem
{
    private const int StepsPerDay = 15;
    private const int DaysPerMonth = 30;

    public static int StepToDay(int world_step)
    {
        return world_step < 0 ? -1 : world_step / StepsPerDay;
    }

    public static int StepToMonth(int world_step)
    {
        return world_step < 0 ? -1 : world_step / (StepsPerDay * DaysPerMonth);
    }

    public static int StepToDayStep(int world_step)
    {
        return world_step < 0 ? -1 : world_step % StepsPerDay;
    }

    public static bool IsNightStep(int world_step)
    {
        int dayStep = StepToDayStep(world_step);
        return dayStep == 12 || dayStep == 13 || dayStep == 14 || dayStep == 0 || dayStep == 1;
    }

    internal static WorldTimeAdvanceResult AdvanceWorldStep(int oldStep, int deltaSteps)
    {
        if (oldStep < 0)
        {
            return WorldTimeAdvanceResult.Invalid("invalid_world_step");
        }

        int oldDay = StepToDay(oldStep);
        int nextStep = oldStep + Math.Max(deltaSteps, 0);
        int newDay = StepToDay(nextStep);
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

}
