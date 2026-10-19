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

// צוותים, ראש הצוות וחברי הצוות (משאבי-משנה של הצוות).
// הרשאות מורכבות (איזה צוות, איזו חברה) נבדקות ב-Services. [Authorize(Roles)] הוא הגנה נוספת וגלויה.
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

    // מנהל: כל הצוותים בחברה. ראש צוות וחבר: הצוות שלהם
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

    // 201 + Location: api/teams/{id}
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

    // צוות שיש בו משתמשים או משימות: 409
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

    // מחליף את ראש הצוות. הקודם (אם יש) חוזר להיות Member
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

    // 200 ולא 201: אין GET למשתמש בודד שה-Location יכול להצביע עליו (החלטה 4.4)
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

    // מוציא את החבר מהצוות ומשחרר את המשימות הפעילות שלו
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