// TeamTaskManagement.Api/Middleware/ExceptionHandlingMiddleware.cs
using Microsoft.AspNetCore.Mvc;

namespace TeamTaskManagement.Api.Middleware;

// שני ב-pipeline (אחרי CorrelationId). תופס כל חריגה מהשלבים שאחריו.
// לקוח שניתק: Information ו-499, לא שגיאת שרת.
// כל חריגה אחרת: Error בלוג, ו-500 עם ProblemDetails כללי, בלי פרטים פנימיים.
public sealed class ExceptionHandlingMiddleware
{
    // קוד מקובל (nginx) לבקשה שהלקוח סגר לפני התשובה. אין לו קבוע ב-StatusCodes
    public const int Status499ClientClosedRequest = 499;
    private const string ServerErrorMessage = "אירעה שגיאה בשרת. יש לנסות שוב מאוחר יותר.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // ביטול שלא נבע מניתוק הלקוח (למשל timeout פנימי) אינו נתפס כאן, ומטופל כ-500 למטה
            _logger.LogInformation(
                "Request {Method} {Path} was aborted by the client.",
                context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = Status499ClientClosedRequest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            // אם התשובה כבר התחילה להישלח, אי אפשר להחליף אותה
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = ServerErrorMessage
                },
                Exception = ex
            });
        }
    }
}