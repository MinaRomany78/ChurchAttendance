using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface ISettingsService
{
    Task<string?> GetAsync(string key);
    Task<bool> GetBoolAsync(string key, bool defaultValue);
    Task<int> GetIntAsync(string key, int defaultValue);

    Task<SettingsViewModel> GetAllAsync();
    Task<ServiceResult> SaveAsync(SettingsViewModel model);

    /// <summary>Upserts one key and saves. Callers are responsible for their own audit entry.</summary>
    Task SetAsync(string key, string? value);
}
