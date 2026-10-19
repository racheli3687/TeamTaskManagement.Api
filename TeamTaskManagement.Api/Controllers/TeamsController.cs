// TeamTaskManagement.Api/Controllers/TeamsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Teams;
using TeamTaskManagement.Core.DTOs.Users;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

/// <summary>
/// צוותים, ראש הצוות וחברי הצוות (משאבי-משנה של הצוות).
/// </summary>
/// <remarks>
/// הרשאות מורכבות (איזה צוות, איזו חברה) נבדקות ב-Services. [Authorize(Roles)] הוא הגנה נוספת וגלויה.
/// </remarks>
[ApiController]
[Route("api/teams")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class TeamsController : ControllerBase
{
    private const string ManagerOrTeamLead = nameof(UserRole.Manager) + "," + nameof(UserRole.TeamLead);

    private readonly ITeamService _teamService;
    private readonly IUserService _userService;

    public TeamsController(ITeamService teamService, IUserService userService)
    {
        _teamService = teamService;
        _userService = userService;
    }

    /// <summary>
    /// רשימת צוותים עם pagination.
    /// </summary>
    /// <remarks>
    /// מנהל: כל הצוותים בחברה. ראש צוות וחבר: הצוות שלהם בלבד.
    /// </remarks>
    /// <response code="200">עמוד של צוותים.</response>
    /// <response code="400">פרמטרי pagination לא תקינים.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TeamResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<TeamResponseDto>>> GetAll(
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct)
    {
        var result = await _teamService.GetAllAsync(pagination, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// צוות לפי מזהה.
    /// </summary>
    /// <response code="200">פרטי הצוות.</response>
    /// <response code="403">אינך רשאי לצפות בצוות זה.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TeamResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamResponseDto>> GetById(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _teamService.GetByIdAsync(id, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// יצירת צוות חדש (מנהל בלבד).
    /// </summary>
    /// <response code="201">הצוות נוצר. הכותרת Location מצביעה על api/teams/{id}.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="403">רק מנהל רשאי ליצור צוות.</response>
    /// <response code="409">כבר קיים צוות בשם זה בחברה.</response>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(typeof(TeamResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamResponseDto>> Create(
        [FromBody] CreateTeamRequestDto request,
        CancellationToken ct)
    {
        var result = await _teamService.CreateAsync(request, ct);
        return result.ToCreatedResult(this, nameof(GetById), dto => new { id = dto.Id });
    }

    /// <summary>
    /// שינוי שם הצוות (מנהל בלבד).
    /// </summary>
    /// <response code="200">הצוות המעודכן.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="403">רק מנהל רשאי לשנות שם צוות.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
    /// <response code="409">כבר קיים צוות בשם זה בחברה.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(typeof(TeamResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamResponseDto>> Rename(
        [FromRoute] int id,
        [FromBody] UpdateTeamRequestDto request,
        CancellationToken ct)
    {
        var result = await _teamService.RenameAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// מחיקת צוות (מנהל בלבד).
    /// </summary>
    /// <response code="204">הצוות נמחק.</response>
    /// <response code="403">רק מנהל רשאי למחוק צוות.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
    /// <response code="409">בצוות יש משתמשים או משימות.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _teamService.DeleteAsync(id, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// מינוי ראש צוות (מנהל בלבד).
    /// </summary>
    /// <remarks>
    /// לצוות ראש צוות אחד. ראש הצוות הקודם (אם יש) חוזר להיות Member באותו צוות.
    /// משתמש שמגיע מצוות אחר: המשימות הפעילות שלו בצוות הקודם משתחררות.
    /// </remarks>
    /// <response code="204">ראש הצוות מונה.</response>
    /// <response code="400">נתונים לא תקינים, או ניסיון למנות את מנהל החברה.</response>
    /// <response code="403">רק מנהל רשאי למנות ראש צוות.</response>
    /// <response code="404">הצוות או המשתמש לא נמצאו בחברה שלך.</response>
    /// <response code="409">התנגשות עם שינוי מקביל.</response>
    [HttpPut("{teamId:int}/lead")]
    [Authorize(Roles = nameof(UserRole.Manager))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTeamLead(
        [FromRoute] int teamId,
        [FromBody] AssignTeamLeadRequestDto request,
        CancellationToken ct)
    {
        var result = await _teamService.AssignTeamLeadAsync(teamId, request.UserId, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// חברי הצוות, עם pagination.
    /// </summary>
    /// <response code="200">עמוד של חברי צוות.</response>
    /// <response code="400">פרמטרי pagination לא תקינים.</response>
    /// <response code="403">אינך רשאי לצפות בחברי צוות זה.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
    [HttpGet("{teamId:int}/members")]
    [ProducesResponseType(typeof(PagedResult<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetMembers(
        [FromRoute] int teamId,
        [FromQuery] PaginationParameters pagination,
        CancellationToken ct)
    {
        var result = await _userService.GetMembersByTeamAsync(teamId, pagination, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// הוספת חבר חדש לצוות (מנהל, או ראש הצוות לצוות שלו).
    /// </summary>
    /// <remarks>
    /// 200 ולא 201: אין GET למשתמש בודד שה-Location יכול להצביע עליו.
    /// </remarks>
    /// <response code="200">החבר נוסף לצוות.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="403">אינך רשאי להוסיף חבר לצוות זה.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
    /// <response code="409">שם המשתמש כבר תפוס.</response>
    [HttpPost("{teamId:int}/members")]
    [Authorize(Roles = ManagerOrTeamLead)]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponseDto>> AddMember(
        [FromRoute] int teamId,
        [FromBody] AddMemberRequestDto request,
        CancellationToken ct)
    {
        var result = await _userService.AddMemberAsync(teamId, request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// הסרת חבר מהצוות (מנהל, או ראש הצוות לצוות שלו).
    /// </summary>
    /// <remarks>
    /// המשימות הפעילות של החבר (Open/InProgress) משתחררות וחוזרות ל-Open. משימות Done נשארות משויכות.
    /// </remarks>
    /// <response code="204">החבר הוסר מהצוות.</response>
    /// <response code="403">אינך רשאי להסיר משתמש זה.</response>
    /// <response code="404">הצוות או המשתמש לא נמצאו.</response>
    /// <response code="409">התנגשות עם שינוי מקביל.</response>
    [HttpDelete("{teamId:int}/members/{userId:int}")]
    [Authorize(Roles = ManagerOrTeamLead)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(
        [FromRoute] int teamId,
        [FromRoute] int userId,
        CancellationToken ct)
    {
        var result = await _userService.RemoveMemberAsync(teamId, userId, ct);
        return result.ToActionResult(this);
    }
}
