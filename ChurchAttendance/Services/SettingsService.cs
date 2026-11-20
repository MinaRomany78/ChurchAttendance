using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class SettingsService : ISettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IChurchInfoService _church;

    public SettingsService(ApplicationDbContext db, IAuditService audit, IChurchInfoService church)
    {
        _db = db;
        _audit = audit;
        _church = church;
    }

    public Task<string?> GetAsync(string key) =>
        _db.ChurchSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();

    public async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        var value = await GetAsync(key);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue)
    {
        var value = await GetAsync(key);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<SettingsViewModel> GetAllAsync() => new()
    {
        ChurchName = await GetAsync(SettingKeys.ChurchName) ?? "كنيسة مار جرجس",
        AllowExcusedAbsence = await GetBoolAsync(SettingKeys.AllowExcusedAbsence, true),
        LateAfterMinutes = await GetIntAsync(SettingKeys.LateAfterMinutes, 15),
        AllowNegativePoints = await GetBoolAsync(SettingKeys.AllowNegativePoints, false),
        LogoPath = await GetAsync(SettingKeys.LogoPath)
    };

    public async Task<ServiceResult> SaveAsync(SettingsViewModel m)
    {
        var name = m.ChurchName.Trim();
        if (name.Length == 0) return ServiceResult.Fail("يرجى إدخال اسم الكنيسة.");
        if (m.LateAfterMinutes is < 0 or > 240) return ServiceResult.Fail("مدة التأخير يجب أن تكون بين 0 و 240 دقيقة.");

        var old = await GetAllAsync();

        await UpsertAsync(SettingKeys.ChurchName, name);
        await UpsertAsync(SettingKeys.AllowExcusedAbsence, m.AllowExcusedAbsence ? "true" : "false");
        await UpsertAsync(SettingKeys.LateAfterMinutes, m.LateAfterMinutes.ToString());
        await UpsertAsync(SettingKeys.AllowNegativePoints, m.AllowNegativePoints ? "true" : "false");

        _audit.Add("SettingsUpdated", "Settings", null,
            new { old.ChurchName, old.AllowExcusedAbsence, old.LateAfterMinutes, old.AllowNegativePoints },
            new { ChurchName = name, m.AllowExcusedAbsence, m.LateAfterMinutes, m.AllowNegativePoints });

        await _db.SaveChangesAsync();
        _church.Invalidate();
        return ServiceResult.Ok();
    }

    public async Task SetAsync(string key, string? value)
    {
        await UpsertAsync(key, value);
        await _db.SaveChangesAsync();
        _church.Invalidate();
    }

    private async Task UpsertAsync(string key, string? value)
    {
        var setting = await _db.ChurchSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null) _db.ChurchSettings.Add(new ChurchSetting { Key = key, Value = value });
        else setting.Value = value;
    }
}
