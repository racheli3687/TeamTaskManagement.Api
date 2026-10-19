// TeamTaskManagement.Api/Middleware/CorrelationIdMiddleware.cs
namespace TeamTaskManagement.Api.Middleware;

// ראשון ב-pipeline. לכל בקשה מזהה אחד: מהכותרת X-Correlation-Id (אם תקינה) או Guid חדש.
// המזהה מוחזר בכותרת התשובה, נכנס ל-scope של הלוג, ומוצב ב-TraceIdentifier
// (ממנו נלקח correlationId ב-ProblemDetails, ראו AddProblemDetails ב-Program.cs).
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogScopeKey = "CorrelationId";
    private const int MaxLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreate(context.Request);
        context.TraceIdentifier = correlationId;

        // OnStarting: הכותרת נכתבת גם אם ExceptionHandlingMiddleware ניקה את התשובה
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // ה-scope משותף לכל ה-loggers בבקשה (גם ב-Services)
        using (_logger.BeginScope(new Dictionary<string, object> { [LogScopeKey] = correlationId }))
        {
            await _next(context);
        }
    }

    private static string GetOrCreate(HttpRequest request)
    {
        // כמה ערכים בכותרת מתחברים בפסיק, ולכן נפסלים בבדיקה
        var incoming = request.Headers[HeaderName].ToString();
        return IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");
    }

    // מונע הזרקה ללוג ולכותרות: אותיות וספרות ASCII, '-' ו-'_' בלבד, עד 64 תווים
    private static bool IsValid(string value)
        => value.Length is > 0 and <= MaxLength
           && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}