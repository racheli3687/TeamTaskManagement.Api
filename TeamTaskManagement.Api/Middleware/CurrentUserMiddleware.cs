// TeamTaskManagement.Api/Middleware/CurrentUserMiddleware.cs
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using TeamTaskManagement.Api.Security;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Api.Middleware;

// רץ אחרי UseAuthentication ולפני UseAuthorization.
// מה-token נלקחת רק הזהות (sub). Role, OrganizationId ו-TeamId נטענים מהמסד, פעם אחת לכל בקשה.
// ה-role ב-principal מוחלף בתפקיד מהמסד, כדי ש-[Authorize(Roles = ...)] יבדוק את התפקיד העדכני
// ולא את מה שנכתב ב-token בזמן ההתחברות.
public class CurrentUserMiddleware
{
    private const string InvalidUserMessage = "המשתמש אינו מזוהה. יש להתחבר מחדש.";

    private readonly RequestDelegate _next;
    private readonly ILogger<CurrentUserMiddleware> _logger;

    public CurrentUserMiddleware(RequestDelegate next, ILogger<CurrentUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // CurrentUser ו-IUserRepository הם Scoped, ולכן מתקבלים כאן ולא ב-constructor
    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser, IUserRepository users)
    {
        // בקשה ללא token (התחברות, הרשמה): ממשיכים. [Authorize] ידחה אם ה-endpoint דורש משתמש.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var sub = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(sub, out var userId))
        {
            _logger.LogWarning("Authenticated request without a valid 'sub' claim.");
            await WriteUnauthorizedAsync(context);
            return;
        }

        var user = await users.GetByIdAsync(userId, context.RequestAborted);
        if (user is null)
        {
            // token תקף, אבל המשתמש כבר לא קיים במסד
            _logger.LogWarning("Token belongs to user {UserId}, who no longer exists.", userId);
            await WriteUnauthorizedAsync(context);
            return;
        }

        currentUser.Set(user);
        context.User = WithRole(context.User, user.Role);

        await _next(context);
    }

    // principal חדש עם כל ה-claims מה-token, פרט ל-role שמוחלף בתפקיד מהמסד.
    // סוגי ה-claims (name, role) נלקחים מה-identity המקורי, שהוגדר ב-TokenValidationParameters.
    private static ClaimsPrincipal WithRole(ClaimsPrincipal principal, UserRole role)
    {
        if (principal.Identity is not ClaimsIdentity source)
            return principal;

        var claims = source.Claims
            .Where(c => c.Type != source.RoleClaimType)
            .Append(new Claim(source.RoleClaimType, role.ToString()));

        var identity = new ClaimsIdentity(
            claims,
            source.AuthenticationType,
            source.NameClaimType,
            source.RoleClaimType);

        return new ClaimsPrincipal(identity);
    }

    private static Task WriteUnauthorizedAsync(HttpContext context)
        => Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: InvalidUserMessage)
            .ExecuteAsync(context);
}