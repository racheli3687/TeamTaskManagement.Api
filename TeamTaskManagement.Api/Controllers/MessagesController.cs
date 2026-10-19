// TeamTaskManagement.Api/Controllers/MessagesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

/// <summary>
/// הודעות פנימיות של המשתמש המחובר בלבד.
/// </summary>
/// <remarks>
/// מי רשאי לכתוב למי נבדק ב-MessageService: מנהל לכל משתמש בחברה; כל משתמש למנהל ולמי שבצוות שלו.
/// </remarks>
[ApiController]
[Route("api/messages")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// תיבת הנכנסות או הנשלחות, עם סינון, חיפוש ו-pagination.
    /// </summary>
    /// <remarks>
    /// Box: Inbox (ברירת מחדל) או Sent. ToDate ללא שעה מתפרש כסוף אותו יום. FromDate מאוחר מ-ToDate: 400.
    /// </remarks>
    /// <response code="200">עמוד של הודעות.</response>
    /// <response code="400">פרמטרי סינון לא תקינים.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MessageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<MessageResponseDto>>> GetMessages(
        [FromQuery] MessageQueryParameters query,
        CancellationToken ct)
    {
        var result = await _messageService.GetMessagesAsync(query, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// מספר ההודעות שלא נקראו בתיבת הנכנסות.
    /// </summary>
    /// <response code="200">מספר ההודעות שלא נקראו.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var result = await _messageService.GetUnreadCountAsync(ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// שליחת הודעה למשתמש אחר באותה חברה.
    /// </summary>
    /// <remarks>
    /// 200 ולא 201: אין GET להודעה בודדת שה-Location יכול להצביע עליו.
    /// </remarks>
    /// <response code="200">ההודעה נשלחה.</response>
    /// <response code="400">נתונים לא תקינים, טקסט ריק או שליחה לעצמך.</response>
    /// <response code="403">אינך רשאי לכתוב לנמען זה.</response>
    /// <response code="404">הנמען לא נמצא בחברה.</response>
    [HttpPost]
    [ProducesResponseType(typeof(MessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponseDto>> Send(
        [FromBody] SendMessageRequestDto request,
        CancellationToken ct)
    {
        var result = await _messageService.SendAsync(request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// סימון הודעה כנקראה (הנמען בלבד).
    /// </summary>
    /// <remarks>
    /// אידמפוטנטי: הודעה שכבר נקראה מחזירה גם היא 204.
    /// </remarks>
    /// <response code="204">ההודעה סומנה כנקראה.</response>
    /// <response code="404">ההודעה לא נמצאה בתיבת הנכנסות שלך.</response>
    [HttpPut("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _messageService.MarkAsReadAsync(id, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// מחיקת הודעה מהתיבה של המשתמש המחובר בלבד.
    /// </summary>
    /// <remarks>
    /// הצד השני ממשיך לראות את ההודעה. כששני הצדדים מחקו, ההודעה נמחקת מהמסד.
    /// </remarks>
    /// <response code="204">ההודעה נמחקה מהתיבה שלך.</response>
    /// <response code="404">ההודעה לא נמצאה או כבר נמחקה מהתיבה שלך.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] int id,
        CancellationToken ct)
    {
        var result = await _messageService.DeleteAsync(id, ct);
        return result.ToActionResult(this);
    }
}
