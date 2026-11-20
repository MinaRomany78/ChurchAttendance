using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "يرجى إدخال اسم المستخدم أو البريد الإلكتروني.")]
    [Display(Name = "اسم المستخدم أو البريد الإلكتروني")]
    [StringLength(256)]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال كلمة المرور.")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
