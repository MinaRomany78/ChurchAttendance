namespace ChurchAttendance.Models;

public enum Gender { Male = 1, Female = 2 }

public enum AttendanceStatus { Present = 1, Absent = 2, Excused = 3, Late = 4 }

public enum RegistrationMethod { QrCamera = 1, BarcodeScanner = 2, StudentCode = 3, Manual = 4 }

public enum SessionType { Regular = 1, Special = 2, Activity = 3 }

/// <summary>
/// Ledger convention: Points is SIGNED. Earn/Refund/Adjustment(+) add, Deduct/Redemption(-) subtract.
/// A student's balance is always SUM(Points) over PointTransactions.
/// </summary>
public enum PointTransactionType { Earn = 1, Deduct = 2, Redemption = 3, Refund = 4, Adjustment = 5 }

public enum RedemptionStatus { Pending = 1, Approved = 2, Delivered = 3, Cancelled = 4 }
