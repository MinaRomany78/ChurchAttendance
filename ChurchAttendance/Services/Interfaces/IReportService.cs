using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IReportService
{
    IReadOnlyList<ReportDefinition> Definitions { get; }
    ReportDefinition? Find(string? key);

    /// <summary>Runs a report. Detail reports return at most maxRows rows and set Truncated when more exist.</summary>
    Task<ReportResult> RunAsync(ReportDefinition definition, ReportFilter filter, int maxRows);
}
