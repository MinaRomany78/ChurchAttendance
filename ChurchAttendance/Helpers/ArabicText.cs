using ChurchAttendance.Models;

namespace ChurchAttendance.Helpers;

/// <summary>Arabic display text and badge styles for enums. Central place so views stay consistent.</summary>
public static class ArabicText
{
    public static string Status(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "حاضر",
        AttendanceStatus.Absent => "غائب",
        AttendanceStatus.Excused => "غائب بعذر",
        AttendanceStatus.Late => "متأخر",
        _ => s.ToString()
    };

    public static string StatusBadge(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "badge-soft-success",
        AttendanceStatus.Absent => "badge-soft-danger",
        AttendanceStatus.Excused => "badge-soft-info",
        AttendanceStatus.Late => "badge-soft-gold",
        _ => "badge-soft-info"
    };

    public static string Method(RegistrationMethod m) => m switch
    {
        RegistrationMethod.QrCamera => "كاميرا (QR)",
        RegistrationMethod.BarcodeScanner => "قارئ باركود",
        RegistrationMethod.StudentCode => "كود الطالب",
        RegistrationMethod.Manual => "يدوي",
        _ => m.ToString()
    };

    public static string GenderName(Gender? g) => g switch
    {
        Gender.Male => "ذكر",
        Gender.Female => "أنثى",
        _ => "—"
    };

    public static string TransactionTypeText(PointTransactionType t) => t switch
    {
        PointTransactionType.Earn => "إضافة نقاط",
        PointTransactionType.Deduct => "خصم نقاط",
        PointTransactionType.Redemption => "استبدال مكافأة",
        PointTransactionType.Refund => "استرجاع نقاط",
        PointTransactionType.Adjustment => "تسوية",
        _ => t.ToString()
    };

    public static string RedemptionStatusText(RedemptionStatus s) => s switch
    {
        RedemptionStatus.Pending => "قيد المراجعة",
        RedemptionStatus.Approved => "تمت الموافقة",
        RedemptionStatus.Delivered => "تم التسليم",
        RedemptionStatus.Cancelled => "ملغاة",
        _ => s.ToString()
    };

    public static string RedemptionBadge(RedemptionStatus s) => s switch
    {
        RedemptionStatus.Pending => "badge-soft-gold",
        RedemptionStatus.Approved => "badge-soft-info",
        RedemptionStatus.Delivered => "badge-soft-success",
        RedemptionStatus.Cancelled => "badge-soft-danger",
        _ => "badge-soft-info"
    };

    public static string SessionTypeText(SessionType t) => t switch
    {
        SessionType.Regular => "جلسة عادية",
        SessionType.Special => "مناسبة خاصة",
        SessionType.Activity => "نشاط",
        _ => t.ToString()
    };

    private static readonly Dictionary<string, string> AuditActions = new()
    {
        ["LoginSucceeded"] = "تسجيل دخول",
        ["LoginFailed"] = "محاولة دخول فاشلة",
        ["LoginLockedOut"] = "إيقاف الدخول مؤقتًا",
        ["Logout"] = "تسجيل خروج",
        ["PasswordChanged"] = "تغيير كلمة المرور",
        ["StageCreated"] = "إضافة مرحلة",
        ["StageUpdated"] = "تعديل مرحلة",
        ["StageActivated"] = "تفعيل مرحلة",
        ["StageDeactivated"] = "إيقاف مرحلة",
        ["StageEntered"] = "دخول مرحلة دراسية",
        ["StageAccessCodeChanged"] = "تغيير رمز دخول المرحلة",
        ["StageAccessCodeRemoved"] = "إلغاء رمز دخول المرحلة",
        ["GroupCreated"] = "إضافة مجموعة",
        ["GroupUpdated"] = "تعديل مجموعة",
        ["GroupActivated"] = "تفعيل مجموعة",
        ["GroupDeactivated"] = "إيقاف مجموعة",
        ["StudentCreated"] = "إضافة طالب",
        ["StudentUpdated"] = "تعديل طالب",
        ["StudentActivated"] = "تفعيل طالب",
        ["StudentDeactivated"] = "إيقاف طالب",
        ["StudentQrRegenerated"] = "إنشاء رمز QR جديد",
        ["AttendanceSessionOpened"] = "فتح جلسة حضور",
        ["AttendanceSessionClosed"] = "إغلاق جلسة حضور",
        ["AttendanceSessionReopened"] = "إعادة فتح جلسة",
        ["AttendanceCorrected"] = "تعديل حالة حضور",
        ["AttendanceUpdatedByScan"] = "تحديث حضور عبر المسح",
        ["PointsAwarded"] = "إضافة نقاط",
        ["PointsDeducted"] = "خصم نقاط",
        ["PointReasonCreated"] = "إضافة سبب نقاط",
        ["PointReasonUpdated"] = "تعديل سبب نقاط",
        ["PointReasonActivated"] = "تفعيل سبب نقاط",
        ["PointReasonDeactivated"] = "إيقاف سبب نقاط",
        ["RewardCreated"] = "إضافة مكافأة",
        ["RewardUpdated"] = "تعديل مكافأة",
        ["RewardActivated"] = "تفعيل مكافأة",
        ["RewardDeactivated"] = "إيقاف مكافأة",
        ["RewardRedeemed"] = "استبدال مكافأة",
        ["RedemptionApproved"] = "موافقة على استبدال",
        ["RedemptionDelivered"] = "تسليم مكافأة",
        ["RedemptionCancelled"] = "إلغاء استبدال",
        ["ReportExported"] = "تصدير تقرير",
        ["UserCreated"] = "إضافة مستخدم",
        ["UserUpdated"] = "تعديل مستخدم",
        ["UserActivated"] = "تفعيل مستخدم",
        ["UserDeactivated"] = "إيقاف مستخدم",
        ["UserPasswordReset"] = "إعادة تعيين كلمة مرور",
        ["SettingsUpdated"] = "تعديل الإعدادات",
        ["LogoUpdated"] = "تغيير الشعار",
        ["LogoRemoved"] = "حذف الشعار"
    };

    public static string AuditAction(string action) =>
        AuditActions.TryGetValue(action, out var text) ? text : action;
}
