// TeamTaskManagement.Api/Controllers/UsersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.DTOs.Users;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

/// <summary>
/// הפרופיל של המשתמש המחובר.
/// </summary>
/// <remarks>
/// me הוא המשתמש מה-token, ולכן אין מזהה בנתיב.
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// הפרופיל של המשתמש המחובר.
    /// </summary>
    /// <response code="200">פרטי המשתמש.</response>
    /// <response code="404">המשתמש לא נמצא.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetMyProfile(CancellationToken ct)
    {
        var result = await _userService.GetMyProfileAsync(ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// עדכון הפרופיל של המשתמש המחובר.
    /// </summary>
    /// <response code="200">הפרופיל המעודכן.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="404">המשתמש לא נמצא.</response>
    /// <response code="409">שם המשתמש כבר תפוס.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponseDto>> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken ct)
    {
        var result = await _userService.UpdateProfileAsync(request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// שינוי סיסמה של המשתמש המחובר.
    /// </summary>
    /// <response code="204">הסיסמה שונתה.</response>
    /// <response code="400">נתונים לא תקינים או סיסמה נוכחית שגויה.</response>
    /// <response code="404">המשתמש לא נמצא.</response>
    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken ct)
    {
        var result = await _userService.ChangePasswordAsync(request, ct);
        return result.ToActionResult(this);
    }
}
