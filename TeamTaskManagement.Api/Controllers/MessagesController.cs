// TeamTaskManagement.Api/Controllers/MessagesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Api.Extensions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Controllers;

// ההודעות של המשתמש המחובר בלבד. מי רשאי לכתוב למי נבדק ב-MessageService.
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

    // תיבת נכנסות או נשלחות, עם סינון, חיפוש ו-pagination
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

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var result = await _messageService.GetUnreadCountAsync(ct);
        return result.ToActionResult(this);
    }

    // 200 ולא 201: אין GET להודעה בודדת שה-Location יכול להצביע עליו (החלטה 4.4)
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

    // אידמפוטנטי: הודעה שכבר נקראה מחזירה גם היא 204
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

    // מוחק מהתיבה של המשתמש בלבד
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