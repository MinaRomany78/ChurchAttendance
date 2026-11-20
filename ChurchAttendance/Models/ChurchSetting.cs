using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

/// <summary>Simple key/value store for church information and system settings (see SettingKeys).</summary>
public class ChurchSetting
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Value { get; set; }
}
