namespace ChurchAttendance.Security;

/// <summary>
/// The educational stage the signed-in user has entered for this browser session. Backed by ASP.NET Core
/// session state (server-side, not a value the client can forge). Reading StageId/StageName is cheap (no
/// database call); IsValidAsync re-checks the stage still exists and is active, and is what the enforcement
/// filter uses before allowing access to any stage-scoped page or action.
/// </summary>
public interface ICurrentStageAccessor
{
    int? StageId { get; }
    string? StageName { get; }
    bool HasStage { get; }

    /// <summary>Validates the code (if the stage requires one) and, on success, stores the stage in session.</summary>
    Task<bool> TrySelectAsync(int stageId, string? code);

    /// <summary>True when a stage is selected AND it still exists and is active.</summary>
    Task<bool> IsValidAsync();

    void Clear();
}
