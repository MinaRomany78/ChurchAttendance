using Microsoft.AspNetCore.Identity;

namespace ChurchAttendance.Security;

public class ArabicIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "حدث خطأ غير متوقع." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "كلمة المرور الحالية غير صحيحة." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = "اسم المستخدم غير صالح." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = "البريد الإلكتروني غير صالح." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "اسم المستخدم مستخدم بالفعل." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = "البريد الإلكتروني مستخدم بالفعل." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"يجب ألا تقل كلمة المرور عن {length} أحرف." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "يجب أن تحتوي كلمة المرور على حرف إنجليزي صغير واحد على الأقل." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "يجب أن تحتوي كلمة المرور على حرف إنجليزي كبير واحد على الأقل." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "يجب أن تحتوي كلمة المرور على رمز خاص واحد على الأقل." };
}
