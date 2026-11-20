namespace ChurchAttendance.Services.Interfaces;

public record ChurchInfo(string Name, string? LogoPath);

public interface IChurchInfoService
{
    Task<ChurchInfo> GetAsync();

    /// <summary>Call after the church settings page saves new values.</summary>
    void Invalidate();
}
