// TeamTaskManagement.Service/Services/MessageService.cs
using AutoMapper;
using Microsoft.Extensions.Logging;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Services;

public class MessageService : IMessageService
{
    private const string MessageNotFoundMessage = "ההודעה לא נמצאה.";
    private const string RecipientNotFoundMessage = "הנמען לא נמצא.";
    private const string SelfMessageMessage = "לא ניתן לשלוח הודעה לעצמך.";
    private const string EmptyTextMessage = "תוכן ההודעה אינו יכול להיות ריק.";
    private const string NotAllowedRecipientMessage = "אין הרשאה לשלוח הודעה למשתמש זה.";
    private const string InvalidDateRangeMessage = "תאריך ההתחלה מאוחר מתאריך הסיום.";
    private const string UserNotFoundMessage = "המשתמש המחובר לא נמצא.";

    private readonly IMessageRepository _messages;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository messages,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper,
        ILogger<MessageService> logger)
    {
        _messages = messages;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    // ===== שליחה =====

    public async Task<ServiceResult<MessageResponseDto>> SendAsync(
        SendMessageRequestDto request, CancellationToken ct = default)
    {
        // כלל עסקי שאינו דורש שאילתה: נבדק ראשון
        if (request.RecipientId == _currentUser.Id)
            return Failure(ResultStatus.ValidationFailed, SelfMessageMessage);

        // [Required] כבר דוחה מחרוזת ריקה או רווחים בלבד, אבל ה-Service לא מניח שעבר דרך Binding
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return Failure(ResultStatus.ValidationFailed, EmptyTextMessage);

        // נמען שלא קיים או של חברה אחרת: NotFound (לא חושפים שקיים)
        var recipient = await _users.GetByIdAsync(request.RecipientId, ct);
        if (recipient is null || recipient.OrganizationId != _currentUser.OrganizationId)
            return Failure(ResultStatus.NotFound, RecipientNotFoundMessage);

        if (!CanWriteTo(recipient))
            return Failure(ResultStatus.Forbidden, NotAllowedRecipientMessage);

        // במעקב: משמש לשיוך Sender, כדי שהמיפוי ימצא את שם השולח בלי שאילתה נוספת
        var sender = await _users.GetByIdAsync(_currentUser.Id, ct);
        if (sender is null)
            return Failure(ResultStatus.Unauthorized, UserNotFoundMessage);

        var message = new Message
        {
            SenderId = sender.Id,
            Sender = sender,
            RecipientId = recipient.Id,
            Recipient = recipient,
            Text = text,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Unread
        };

        await _messages.AddAsync(message, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ServiceResult<MessageResponseDto>.Success(_mapper.Map<MessageResponseDto>(message));
    }

    // ===== קריאה =====

    public async Task<ServiceResult<PagedResult<MessageResponseDto>>> GetMessagesAsync(
        MessageQueryParameters query, CancellationToken ct = default)
    {
        // Npgsql דורש UTC בהשוואה מול timestamptz
        query.FromDate = DateTimeUtc.Normalize(query.FromDate);
        query.ToDate = DateTimeUtc.Normalize(query.ToDate);

        // תאריך בלי שעה (00:00) מתפרש ככל היום. מיקרו-שנייה ולא tick: זה הדיוק של timestamptz.
        if (query.ToDate is DateTime to && to.TimeOfDay == TimeSpan.Zero)
            query.ToDate = to.AddDays(1).AddMicroseconds(-1);

        if (query.FromDate > query.ToDate)
            return ServiceResult<PagedResult<MessageResponseDto>>.Fail(
                ResultStatus.ValidationFailed, InvalidDateRangeMessage);

        // הסינון לפי המשתמש המחובר מתבצע בשאילתה, ולכן OtherUserId אינו דורש בדיקת הרשאה
        var paged = await _messages.GetPagedAsync(_currentUser.Id, query, ct);

        return ServiceResult<PagedResult<MessageResponseDto>>.Success(new PagedResult<MessageResponseDto>
        {
            Items = _mapper.Map<List<MessageResponseDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        });
    }

    public async Task<ServiceResult<int>> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var count = await _messages.CountUnreadAsync(_currentUser.Id, ct);
        return ServiceResult<int>.Success(count);
    }

    // ===== עדכון =====

    public async Task<ServiceResult> MarkAsReadAsync(int messageId, CancellationToken ct = default)
    {
        // רק הנמען, ורק הודעה שעדיין בתיבה שלו. כל מקרה אחר: NotFound (לא חושפים שקיימת)
        var message = await _messages.GetByIdAsync(messageId, ct);
        if (message is null || message.RecipientId != _currentUser.Id || message.IsDeletedByRecipient)
            return ServiceResult.Fail(ResultStatus.NotFound, MessageNotFoundMessage);

        // כבר נקראה: אין מה לשמור
        if (message.Status == MessageStatus.Read)
            return ServiceResult.Success();

        message.Status = MessageStatus.Read;
        await _unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int messageId, CancellationToken ct = default)
    {
        var message = await _messages.GetByIdAsync(messageId, ct);

        // שליחה לעצמך חסומה, ולכן המשתמש הוא לכל היותר אחד מהשניים
        var isSender = message is not null && message.SenderId == _currentUser.Id && !message.IsDeletedBySender;
        var isRecipient = message is not null && message.RecipientId == _currentUser.Id && !message.IsDeletedByRecipient;

        // לא שלו, או שכבר מחק מהצד שלו (כבר לא בתיבה שלו): NotFound
        if (!isSender && !isRecipient)
            return ServiceResult.Fail(ResultStatus.NotFound, MessageNotFoundMessage);

        if (isSender)
            message!.IsDeletedBySender = true;
        else
            message!.IsDeletedByRecipient = true;

        // שני הצדדים מחקו: מחיקה פיזית
        if (message.IsDeletedBySender && message.IsDeletedByRecipient)
            _messages.Remove(message);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // מחיקה מקבילה של אותה הודעה (למשל בקשה כפולה): השורה כבר נמחקה, והתוצאה זהה למבוקש
            _logger.LogWarning(
                "Delete conflict: message {MessageId} was already deleted concurrently. User {UserId}.",
                messageId, _currentUser.Id);
        }

        return ServiceResult.Success();
    }

    // ===== עזר =====

    // מנהל: לכל משתמש בחברה. כל אחד: למנהל, ולכל מי שבצוות שלו.
    // חבר ללא צוות: למנהל בלבד. ראש צוות אינו כותב לראש צוות אחר (דרך המנהל).
    // הנמען כבר נבדק שהוא באותה חברה.
    private bool CanWriteTo(User recipient)
    {
        if (_currentUser.Role == UserRole.Manager || recipient.Role == UserRole.Manager)
            return true;

        return _currentUser.TeamId is int teamId && recipient.TeamId == teamId;
    }

    private static ServiceResult<MessageResponseDto> Failure(ResultStatus status, string message)
        => ServiceResult<MessageResponseDto>.Fail(status, message);
}