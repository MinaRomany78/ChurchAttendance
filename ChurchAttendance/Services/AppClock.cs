using ChurchAttendance.Services.Interfaces;

namespace ChurchAttendance.Services;

public class AppClock : IAppClock
{
    public AppClock(IConfiguration config)
    {
        var id = config["App:TimeZoneId"] ?? "Africa/Cairo";
        TimeZone = Resolve(id);
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(UtcNow));

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime StartOfDayUtc(DateOnly localDate)
    {
        var local = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        // Egypt's DST change happens at local midnight, so 00:00 can be a non-existent time on that day.
        if (TimeZone.IsInvalidTime(local)) local = local.AddHours(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }

    private static TimeZoneInfo Resolve(string id)
    {
        foreach (var candidate in new[] { id, "Egypt Standard Time", "Africa/Cairo" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }
}
