using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using ChurchAttendance.Data;
using ChurchAttendance.Helpers;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class AuditService : IAuditService
{
    private const int PageSize = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // keep Arabic readable in stored JSON
    };

    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IAppClock _clock;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http, IAppClock clock)
    {
        _db = db;
        _http = http;
        _clock = clock;
    }

    public void Add(string action, string entityName, string? entityId = null,
                    object? oldValues = null, object? newValues = null,
                    string? actorUserId = null, string? actorUserName = null)
    {
        var ctx = _http.HttpContext;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId ?? ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = actorUserName ?? ctx?.User.Identity?.Name,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            Timestamp = DateTime.UtcNow,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
    }

    public async Task LogAsync(string action, string entityName, string? entityId = null,
                               object? oldValues = null, object? newValues = null,
                               string? actorUserId = null, string? actorUserName = null)
    {
        Add(action, entityName, entityId, oldValues, newValues, actorUserId, actorUserName);
        await _db.SaveChangesAsync();
    }

    public async Task<AuditIndexViewModel> SearchAsync(AuditQuery q)
    {
        IQueryable<AuditLog> query = _db.AuditLogs.AsNoTracking();

        if (q.From is DateOnly fromDate)
        {
            var start = _clock.StartOfDayUtc(fromDate);
            query = query.Where(a => a.Timestamp >= start);
        }

        if (q.To is DateOnly toDate)
        {
            var endExclusive = _clock.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(a => a.Timestamp < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(q.User))
        {
            var term = q.User.Trim();
            query = query.Where(a => a.UserName != null && a.UserName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(q.Entity)) query = query.Where(a => a.EntityName == q.Entity);

        if (!string.IsNullOrWhiteSpace(q.Action))
        {
            var term = q.Action.Trim();
            query = query.Where(a => a.Action.Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(q.Page, 1, totalPages);

        var raw = await query
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();

        return new AuditIndexViewModel
        {
            Query = q,
            Entities = await _db.AuditLogs.AsNoTracking().Select(a => a.EntityName).Distinct().OrderBy(n => n).ToListAsync(),
            Result = new PagedResult<AuditLogItem>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = total,
                Items = raw.Select(a => new AuditLogItem
                {
                    Id = a.Id,
                    LocalTime = _clock.ToLocal(a.Timestamp),
                    UserName = a.UserName ?? "—",
                    Action = a.Action,
                    ActionText = ArabicText.AuditAction(a.Action),
                    Entity = a.EntityName,
                    EntityId = a.EntityId,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    IpAddress = a.IpAddress
                }).ToList()
            }
        };
    }
}
