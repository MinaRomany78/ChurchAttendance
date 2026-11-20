namespace ChurchAttendance.ViewModels;

public class StageSelectionViewModel
{
    public List<StageSelectionItem> Stages { get; set; } = new();
    public string? ReturnUrl { get; set; }
}
