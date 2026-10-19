// TeamTaskManagement.Api/Controllers/TasksController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

/// <summary>
/// משימות: יצירה, עריכה, סטטוס, תפיסה ושחרור.
/// </summary>
/// <remarks>
/// רשימות לפי צוות או לפי חבר הן משאבי-משנה (~/ מתעלם מה-prefix של ה-Controller).
/// התפיסה והשחרור הם יצירה ומחיקה של השיוך (assignment), בלי שם פעולה ב-URL.
/// </remarks>
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

    /// <summary>
    /// משימות הצוות, עם סינון, חיפוש, מיון ו-pagination.
    /// </summary>
    /// <remarks>
    /// הסינון, המיון וה-pagination מתבצעים בשאילתה מול המסד.
    /// </remarks>
    /// <response code="200">עמוד של משימות.</response>
    /// <response code="400">פרמטרי שאילתה לא תקינים.</response>
    /// <response code="403">אינך רשאי לצפות במשימות צוות זה.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
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

    /// <summary>
    /// המשימות המשויכות לחבר צוות, עם סינון, חיפוש, מיון ו-pagination.
    /// </summary>
    /// <remarks>
    /// חבר: רק את עצמו (כולל היסטוריה מצוות קודם). ראש צוות: חברי הצוות שלו. מנהל: כל חבר בחברה.
    /// ראש צוות ומנהל רואים רק משימות בצוות הנוכחי של החבר.
    /// </remarks>
    /// <response code="200">עמוד של משימות.</response>
    /// <response code="400">פרמטרי שאילתה לא תקינים.</response>
    /// <response code="403">אינך רשאי לצפות במשימות חבר זה.</response>
    /// <response code="404">המשתמש לא נמצא בחברה שלך.</response>
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

    /// <summary>
    /// משימה לפי מזהה.
    /// </summary>
    /// <response code="200">פרטי המשימה.</response>
    /// <response code="403">אינך רשאי לצפות במשימה זו.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
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

    /// <summary>
    /// יצירת משימה בצוות (ראש הצוות בלבד, לצוות שלו).
    /// </summary>
    /// <remarks>
    /// המשימה נוצרת במצב Open. תאריך יעד לפני היום: 400.
    /// </remarks>
    /// <response code="201">המשימה נוצרה. הכותרת Location מצביעה על api/tasks/{id}.</response>
    /// <response code="400">נתונים לא תקינים או תאריך יעד בעבר.</response>
    /// <response code="403">רק ראש הצוות רשאי ליצור משימה בצוות זה.</response>
    /// <response code="404">הצוות לא נמצא בחברה שלך.</response>
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

    /// <summary>
    /// עריכת תוכן המשימה (ראש הצוות בלבד).
    /// </summary>
    /// <remarks>
    /// הסטטוס אינו משתנה כאן; הוא מתעדכן ב-PATCH api/tasks/{id}/status.
    /// </remarks>
    /// <response code="200">המשימה המעודכנת.</response>
    /// <response code="400">נתונים לא תקינים.</response>
    /// <response code="403">רק ראש הצוות רשאי לערוך משימה זו.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
    /// <response code="409">המשימה שונתה במקביל (למשל נתפסה). יש לטעון אותה מחדש.</response>
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

    /// <summary>
    /// עדכון סטטוס המשימה (ראש הצוות או החבר המחזיק במשימה).
    /// </summary>
    /// <remarks>
    /// InProgress ל-Done: המחזיק או ראש הצוות. Done ל-InProgress: ראש הצוות בלבד.
    /// מעבר ל-Open אינו אפשרי כאן (400); הדרך היחידה היא שחרור המשימה.
    /// </remarks>
    /// <response code="200">המשימה המעודכנת.</response>
    /// <response code="400">נתונים לא תקינים או מעבר ל-Open.</response>
    /// <response code="403">אינך רשאי לשנות את סטטוס המשימה.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
    /// <response code="409">המשימה אינה תפוסה, או שונתה במקביל.</response>
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

    /// <summary>
    /// מחיקת משימה (ראש הצוות בלבד).
    /// </summary>
    /// <remarks>
    /// מותרת גם למשימה תפוסה; השיוכים נמחקים יחד איתה.
    /// </remarks>
    /// <response code="204">המשימה נמחקה.</response>
    /// <response code="403">רק ראש הצוות רשאי למחוק משימה זו.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
    /// <response code="409">המשימה שונתה במקביל.</response>
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

    /// <summary>
    /// תפיסת משימה (חבר צוות בלבד, משימה Open בצוות שלו).
    /// </summary>
    /// <remarks>
    /// זו הפעולה שמתחרים עליה. השדה Version מבטיח שרק חבר אחד יצליח לתפוס את המשימה;
    /// השני מקבל 409. המשימה עוברת ל-InProgress.
    /// </remarks>
    /// <response code="200">המשימה נתפסה. התשובה היא המשימה המעודכנת.</response>
    /// <response code="403">רק חבר בצוות המשימה רשאי לתפוס אותה.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
    /// <response code="409">המשימה כבר תפוסה, או נתפסה במקביל על ידי חבר אחר.</response>
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

    /// <summary>
    /// שחרור משימה (ראש הצוות או החבר המחזיק).
    /// </summary>
    /// <remarks>
    /// המשימה חוזרת ל-Open והשיוך נסגר. משימה במצב Done אינה ניתנת לשחרור.
    /// </remarks>
    /// <response code="200">המשימה שוחררה. התשובה היא המשימה המעודכנת (Open).</response>
    /// <response code="403">אינך רשאי לשחרר משימה זו.</response>
    /// <response code="404">המשימה לא נמצאה בחברה שלך.</response>
    /// <response code="409">המשימה אינה תפוסה, נמצאת במצב Done, או שונתה במקביל.</response>
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
