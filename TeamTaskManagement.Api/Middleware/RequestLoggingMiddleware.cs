// TeamTaskManagement.Api/Middleware/RequestLoggingMiddleware.cs
using System.Diagnostics;

namespace TeamTaskManagement.Api.Middleware;

// שורת לוג אחת לכל בקשה: שיטה, נתיב, סטטוס סופי ומשך.
// רץ אחרי CorrelationIdMiddleware (השורה נושאת את המזהה) ולפני ExceptionHandlingMiddleware,
// כך שהסטטוס הנרשם הוא הסופי, גם 500 ו-499.
// נרשם רק ה-Path: לא query string, לא כותרות (Authorization) ולא גוף (סיסמאות).
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            // finally: גם כשחריגה עוברת הלאה (תשובה שכבר התחילה להישלח), הבקשה נרשמת
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
}
