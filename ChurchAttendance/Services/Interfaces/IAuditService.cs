using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Adds an audit entry to the current DbContext WITHOUT saving. Use inside business transactions so the
    /// audit row commits or rolls back together with the change it describes.
    /// </summary>
    void Add(string action, string entityName, string? entityId = null,
             object? oldValues = null, object? newValues = null,
             string? actorUserId = null, string? actorUserName = null);

    /// <summary>Adds an audit entry and saves immediately.</summary>
    Task LogAsync(string action, string entityName, string? entityId = null,
                  object? oldValues = null, object? newValues = null,
                  string? actorUserId = null, string? actorUserName = null);

    Task<AuditIndexViewModel> SearchAsync(AuditQuery query);
}
