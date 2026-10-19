// TeamTaskManagement.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

/// <summary>
/// הרשמה והתחברות. פתוחות למשתמש שעדיין אין לו token.
/// </summary>
/// <remarks>
/// שתי הפעולות מחזירות 200 עם token (התשובה היא פרטי כניסה, לא ייצוג של משאב שנוצר).
/// </remarks>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// הרשמת חברה חדשה: יוצר את החברה ואת המנהל שלה, ומחזיר token.
    /// </summary>
    /// <response code="200">ההרשמה הצליחה. התשובה כוללת token ופרטי המשתמש.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="409">שם המשתמש כבר תפוס.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// התחברות לכל התפקידים (מנהל, ראש צוות, חבר צוות).
    /// </summary>
    /// <response code="200">ההתחברות הצליחה. התשובה כוללת token ופרטי המשתמש.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="401">שם משתמש או סיסמה שגויים (הודעה אחידה, ללא פירוט מה שגוי).</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.ToActionResult(this);
    }
}
