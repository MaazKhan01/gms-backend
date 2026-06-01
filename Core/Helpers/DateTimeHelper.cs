namespace Core.Helpers;

public static class DateTimeHelper
{
    /// <summary>
    /// Converts a day of week integer to a readable day name.
    /// </summary>
    /// <param name="dayOfWeek">The day of week (1 = Monday, 2 = Tuesday, ..., 7 = Sunday)</param>
    /// <returns>The name of the day</returns>
    public static string GetDayName(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            1 => "Monday",
            2 => "Tuesday",
            3 => "Wednesday",
            4 => "Thursday",
            5 => "Friday",
            6 => "Saturday",
            7 => "Sunday",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Validates if the day of week value is valid (1-7).
    /// </summary>
    /// <param name="dayOfWeek">The day of week to validate (1 = Monday, ..., 7 = Sunday)</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidDayOfWeek(int dayOfWeek)
    {
        return dayOfWeek >= 1 && dayOfWeek <= 7;
    }

    /// <summary>
    /// Gets our internal day of week integer (1=Monday, ..., 7=Sunday) from System.DayOfWeek.
    /// </summary>
    /// <param name="dayOfWeek">The System.DayOfWeek enum value</param>
    /// <returns>1 for Monday, 7 for Sunday</returns>
    public static int GetDayOfWeekInt(DayOfWeek dayOfWeek)
    {
        return dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
    }

    /// <summary>
    /// Validates if the time range is valid (end time is after start time).
    /// </summary>
    /// <param name="startTime">The start time</param>
    /// <param name="endTime">The end time</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidTimeRange(TimeOnly startTime, TimeOnly endTime)
    {
        return endTime > startTime;
    }
}
