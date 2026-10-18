// TeamTaskManagement.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.DTOs.Auth;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

// הרשמה והתחברות: פתוחות למשתמש שעדיין אין לו token.
// שתי הפעולות מחזירות 200 עם token (התשובה היא פרטי כניסה, לא ייצוג של משאב שנוצר).
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

    // יוצר חברה חדשה ואת המנהל שלה, ומחזיר token
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

    // כניסה לכל התפקידים. פרטים שגויים: 401 עם הודעה אחידה
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