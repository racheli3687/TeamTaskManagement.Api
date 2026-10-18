// TeamTaskManagement.Core/Exceptions/UniqueConstraintException.cs
namespace TeamTaskManagement.Core.Exceptions;

// נזרקת מ-IUnitOfWork.SaveChangesAsync כשהשמירה מפרה Unique Index
// (למשל שם משתמש או שם צוות שכבר קיימים - גם כשהבדיקה המוקדמת עברה, בגלל בקשה מקבילה).
// המימוש ב-Data מתרגם את שגיאת בסיס הנתונים לחריגה הזו, כך שה-Service לא מכיר את EF Core.
// ה-Service תופס אותה ומחזיר Conflict (409).
public class UniqueConstraintException : Exception
{
    // שם האינדקס שהופר, לצורך לוג. לא מוצג ללקוח.
    public string? ConstraintName { get; }

    public UniqueConstraintException(string message, string? constraintName, Exception? innerException = null)
        : base(message, innerException)
    {
        ConstraintName = constraintName;
    }
}