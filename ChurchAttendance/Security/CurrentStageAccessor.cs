using ChurchAttendance.Data;
using ChurchAttendance.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Security;

public class CurrentStageAccessor : ICurrentStageAccessor
{
    private const string IdKey = "CurrentStageId";
    private const string NameKey = "CurrentStageName";

    private readonly IHttpContextAccessor _http;
    private readonly ApplicationDbContext _db;
    private readonly IAccessCodeHasher _hasher;
    private readonly IAuditService _audit;

    public CurrentStageAccessor(IHttpContextAccessor http, ApplicationDbContext db, IAccessCodeHasher hasher, IAuditService audit)
    {
        _http = http;
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    private ISession? Session => _http.HttpContext?.Session;

    public int? StageId => Session?.GetInt32(IdKey);
    public string? StageName => Session?.GetString(NameKey);
    public bool HasStage => StageId is not null;

    public async Task<bool> TrySelectAsync(int stageId, string? code)
    {
        var stage = await _db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId && s.IsActive);
        if (stage is null) return false;

        if (!string.IsNullOrEmpty(stage.AccessCodeHash))
        {
            if (string.IsNullOrWhiteSpace(code) || !_hasher.Verify(stage.AccessCodeHash, code.Trim())) return false;
        }

        Session?.SetInt32(IdKey, stage.Id);
        Session?.SetString(NameKey, stage.Name);

        await _audit.LogAsync("StageEntered", "Stage", stage.Id.ToString());
        return true;
    }

    public async Task<bool> IsValidAsync()
    {
        var id = StageId;
        return id is not null && await _db.Stages.AsNoTracking().AnyAsync(s => s.Id == id && s.IsActive);
    }

    public void Clear()
    {
        Session?.Remove(IdKey);
        Session?.Remove(NameKey);
    }
}
