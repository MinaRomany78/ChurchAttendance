using ChurchAttendance.Models;

namespace ChurchAttendance.ViewModels;

public enum ReportColumnKind { Text, Number, Percent, Date, DateTime }

public record ReportColumn(string Header, ReportColumnKind Kind = ReportColumnKind.Text);

public record SummaryItem(string Label, string Value);

public enum ReportStatusMode { None, AllStatuses, AbsenceOnly }

public class ReportDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "bi-file-earmark-bar-graph";
    public bool ShowGroup { get; init; } = true;
    public bool ShowStudent { get; init; } = true;
    public ReportStatusMode StatusMode { get; init; } = ReportStatusMode.None;
}

public class ReportFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? StageId { get; set; }
    public int? GroupId { get; set; }

    /// <summary>Free text: matches student name or student code.</summary>
    public string? Student { get; set; }

    public AttendanceStatus? Status { get; set; }
}

/// <summary>
/// One generic shape for every report. The HTML view and the Excel exporter both render this,
/// so a new report only needs a query that fills columns + rows.
/// Date / DateTime cells hold DateTime values (already converted to local church time).
/// </summary>
public class ReportResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FiltersText { get; set; } = string.Empty;
    public DateTime GeneratedAtLocal { get; set; }
    public List<ReportColumn> Columns { get; set; } = new();
    public List<object?[]> Rows { get; set; } = new();
    public List<SummaryItem> Summary { get; set; } = new();
    public bool Truncated { get; set; }
    public int MaxRows { get; set; }
}

public class ReportPageViewModel
{
    public ReportDefinition Definition { get; set; } = new();
    public ReportFilter Filter { get; set; } = new();
    public ReportResult? Result { get; set; }
    public string? Error { get; set; }
    public List<StageOption> Stages { get; set; } = new();
    public List<GroupOption> Groups { get; set; } = new();
}
