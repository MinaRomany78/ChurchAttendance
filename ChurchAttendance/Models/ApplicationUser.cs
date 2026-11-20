using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ChurchAttendance.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
