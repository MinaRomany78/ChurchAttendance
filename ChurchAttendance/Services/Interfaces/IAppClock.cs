namespace ChurchAttendance.Services.Interfaces;

/// <summary>
/// All timestamps are stored in UTC. Session dates/times are stored as LOCAL church time.
/// Use this service whenever you need "today" or need to display a stored UTC value.
/// </summary>
public interface IAppClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
    TimeZoneInfo TimeZone { get; }
    DateTime ToLocal(DateTime utc);

    /// <summary>UTC instant at which the given LOCAL calendar day starts. Use [StartOfDayUtc(d), StartOfDayUtc(d+1)) to filter a day.</summary>
    DateTime StartOfDayUtc(DateOnly localDate);
}
