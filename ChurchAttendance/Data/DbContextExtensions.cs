using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Data;

public static class DbContextExtensions
{
    /// <summary>
    /// Takes an exclusive row lock on the student until the current transaction ends. Every operation that
    /// reads a balance and then spends it (deduction, redemption) calls this first, so two simultaneous
    /// requests for the same student are processed one after the other and cannot overspend.
    /// MUST be called inside a database transaction.
    /// The lock hint is SQL Server specific; other providers (used only by the automated tests) skip it.
    /// </summary>
    public static Task LockStudentAsync(this ApplicationDbContext db, int studentId)
    {
        if (!db.Database.IsSqlServer()) return Task.CompletedTask;

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT Id FROM Students WITH (UPDLOCK, ROWLOCK) WHERE Id = {studentId}");
    }
}
