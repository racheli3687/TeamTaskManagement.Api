// TeamTaskManagement.Service/Services/DateTimeUtc.cs
namespace TeamTaskManagement.Service.Services;

// משותף ל-TaskService ול-MessageService.
// Npgsql דוחה DateTime שאינו Utc בעמודת timestamptz. הלקוח עשוי לשלוח כל אחד משלושת הסוגים.
internal static class DateTimeUtc
{
    public static DateTime? Normalize(DateTime? value)
    {
        if (value is not DateTime date)
            return null;

        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),            // נשלח עם היסט, למשל +03:00
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)        // נשלח בלי אזור זמן: מתפרש כ-UTC
        };
    }
}