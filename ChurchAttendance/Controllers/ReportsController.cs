using ChurchAttendance.Security;
using ChurchAttendance.Services;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.ViewReports)]
public class ReportsController : Controller
{
    private const int ViewMaxRows = 2000;
    private const int ExportMaxRows = 50000;
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IReportService _reports;
    private readonly ICatalogService _catalog;
    private readonly IChurchInfoService _church;
    private readonly IAuditService _audit;
    private readonly IAppClock _clock;

    public ReportsController(IReportService reports, ICatalogService catalog, IChurchInfoService church,
                             IAuditService audit, IAppClock clock)
    {
        _reports = reports;
        _catalog = catalog;
        _church = church;
        _audit = audit;
        _clock = clock;
    }

    public IActionResult Index() => View(_reports.Definitions);

    [HttpGet]
    public async Task<IActionResult> Show(string id, [FromQuery] ReportFilter filter)
    {
        var def = _reports.Find(id);
        if (def is null) return NotFound();

        ApplyDefaults(filter);
        Normalize(def, filter);

        var model = new ReportPageViewModel
        {
            Definition = def,
            Filter = filter,
            Stages = await _catalog.GetStageOptionsAsync(includeInactive: true),
            Groups = await _catalog.GetGroupOptionsAsync(includeInactive: true)
        };

        if (filter.From is not null && filter.To is not null && filter.From > filter.To)
            model.Error = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية.";
        else
            model.Result = await _reports.RunAsync(def, filter, ViewMaxRows);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string id, [FromQuery] ReportFilter filter)
    {
        var def = _reports.Find(id);
        if (def is null) return NotFound();

        ApplyDefaults(filter);
        Normalize(def, filter);

        if (filter.From is not null && filter.To is not null && filter.From > filter.To)
        {
            TempData["Error"] = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية.";
            return RedirectToAction(nameof(Show), new { id });
        }

        var result = await _reports.RunAsync(def, filter, ExportMaxRows);
        var church = await _church.GetAsync();
        var bytes = ReportExcelExporter.Export(result, church.Name);

        await _audit.LogAsync("ReportExported", "Report", def.Key, null,
            new { rows = result.Rows.Count, format = "xlsx" });

        // ASCII file name: works in every browser regardless of Arabic header encoding.
        return File(bytes, ExcelContentType, $"report-{def.Key}-{_clock.Today:yyyyMMdd}.xlsx");
    }

    /// <summary>First visit (no date keys in the query string at all) defaults to the last 30 days.</summary>
    private void ApplyDefaults(ReportFilter filter)
    {
        if (Request.Query.ContainsKey("from") || Request.Query.ContainsKey("to")) return;

        filter.To = _clock.Today;
        filter.From = _clock.Today.AddDays(-30);
    }

    /// <summary>Drops filters a report does not support so they cannot silently change its results.</summary>
    private static void Normalize(ReportDefinition def, ReportFilter filter)
    {
        if (!def.ShowGroup) filter.GroupId = null;
        if (!def.ShowStudent) filter.Student = null;
        if (def.StatusMode == ReportStatusMode.None) filter.Status = null;
    }
}
