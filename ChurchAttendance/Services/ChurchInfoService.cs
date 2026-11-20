using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ChurchAttendance.Services;

public class ChurchInfoService : IChurchInfoService
{
    private const string CacheKey = "church-info";
    private const string DefaultName = "كنيسة مار جرجس";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ChurchInfoService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ChurchInfo> GetAsync()
    {
        if (_cache.TryGetValue(CacheKey, out ChurchInfo? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var values = await _db.ChurchSettings.AsNoTracking()
                .Where(s => s.Key == SettingKeys.ChurchName || s.Key == SettingKeys.LogoPath)
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            var name = values.GetValueOrDefault(SettingKeys.ChurchName);
            var logo = values.GetValueOrDefault(SettingKeys.LogoPath);

            var info = new ChurchInfo(
                string.IsNullOrWhiteSpace(name) ? DefaultName : name,
                string.IsNullOrWhiteSpace(logo) ? null : logo);

            _cache.Set(CacheKey, info, TimeSpan.FromMinutes(10));
            return info;
        }
        catch (Exception)
        {
            // Never let branding break the error page or the login page.
            return new ChurchInfo(DefaultName, null);
        }
    }

    public void Invalidate() => _cache.Remove(CacheKey);
}
