namespace TeamTaskManagement.Core.Exceptions;

// נזרקת מ-IUnitOfWork.SaveChangesAsync כשהשורה השתנתה בין הקריאה לשמירה (Optimistic Concurrency).
// המימוש ב-Data תופס את DbUpdateConcurrencyException של EF Core וזורק את החריגה הזו במקומו,
// כך ששכבת ה-Service לא צריכה להכיר את EF Core. ה-Service תופס אותה ומחזיר Conflict (409).
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
