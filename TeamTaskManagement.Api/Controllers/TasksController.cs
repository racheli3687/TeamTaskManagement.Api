// TeamTaskManagement.Api/Controllers/TasksController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

// משימות. רשימות לפי צוות או לפי חבר הן משאבי-משנה (~/ מתעלם מה-prefix של ה-Controller).
// התפיסה והשחרור הם יצירה ומחיקה של השיוך (assignment), בלי שם פעולה ב-URL.
[ApiController]
[Route("api/tasks")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    // סינון, חיפוש, מיון ו-pagination מתבצעים בשאילתה מול המסד
    [HttpGet("~/api/teams/{teamId:int}/tasks")]
    [ProducesResponseType(typeof(PagedResult<TaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskResponseDto>>> GetByTeam(
        [FromRoute] int teamId,
        [FromQuery] TaskQueryParameters query,
        CancellationToken ct)
    {
        var result = await _taskService.GetByTeamAsync(teamId, query, ct);
        return result.ToActionResult(this);
    }

    // חבר: רק את עצמו. ראש צוות: חברי הצוות שלו. מנהל: כל חבר בחברה
    [HttpGet("~/api/users/{memberId:int}/tasks")]
    [ProducesResponseType(typeof(PagedResult<TaskResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TaskResponseDto>>> GetByMember(
        [FromRoute] int memberId,
        [FromQuery] TaskQueryParameters query,
        CancellationToken ct)
    {
        var result = await _taskService.GetByMemberAsync(memberId, query, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponseDto>> GetById(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _taskService.GetByIdAsync(id, ct);
        return result.ToActionResult(this);
    }

    // נוצרת בצוות, אבל ה-Location מצביע על api/tasks/{id} (GetById של ה-Controller הזה)
    [HttpPost("~/api/teams/{teamId:int}/tasks")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponseDto>> Create(
        [FromRoute] int teamId,
        [FromBody] CreateTaskRequestDto request,
        CancellationToken ct)
    {
        var result = await _taskService.CreateAsync(teamId, request, ct);
        return result.ToCreatedResult(this, nameof(GetById), dto => new { id = dto.Id });
    }

    // עריכת התוכן בלבד. הסטטוס מתעדכן ב-PATCH .../status
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> Update(
        [FromRoute] int id,
        [FromBody] UpdateTaskRequestDto request,
        CancellationToken ct)
    {
        var result = await _taskService.UpdateAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    // ראש הצוות או החבר המחזיק: כלל מורכב, ולכן רק [Authorize] והבדיקה ב-Service
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> UpdateStatus(
        [FromRoute] int id,
        [FromBody] UpdateTaskStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _taskService.UpdateStatusAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _taskService.DeleteAsync(id, ct);
        return result.ToActionResult(this);
    }

    // תפיסה: הפעולה שמתחרים עליה. משימה תפוסה או התנגשות Version: 409
    [HttpPost("{id:int}/assignment")]
    [Authorize(Roles = nameof(UserRole.Member))]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> Claim(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _taskService.ClaimAsync(id, ct);
        return result.ToActionResult(this);
    }

    // שחרור: ראש הצוות או החבר המחזיק. מחזיר את המשימה המעודכנת (Open)
    [HttpDelete("{id:int}/assignment")]
    [ProducesResponseType(typeof(TaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TaskResponseDto>> Release(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _taskService.ReleaseAsync(id, ct);
        return result.ToActionResult(this);
    }
}