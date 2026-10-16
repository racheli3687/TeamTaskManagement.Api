using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;

namespace TeamTaskManagement.Core.Services;

public interface IMessageService
{
    // מנהל, ראש צוות וחבר. רק בתוך אותה חברה (נמען מחברה אחרת: NotFound). מי רשאי לכתוב למי (למשל חבר רק למנהל ולראש הצוות שלו)
    // הוא כלל עסקי שנבדק ב-Service. נמען שלא קיים: NotFound. אי אפשר לשלוח לעצמך.
    Task<ServiceResult<MessageResponseDto>> SendAsync(SendMessageRequestDto request, CancellationToken ct = default);

    // כל אחד רק את ההודעות שלו (נכנסות או נשלחות), עם סינון, חיפוש ו-pagination.
    Task<ServiceResult<PagedResult<MessageResponseDto>>> GetMessagesAsync(MessageQueryParameters query, CancellationToken ct = default);

    // רק הנמען. הודעה של מישהו אחר: NotFound (לא חושפים שהיא קיימת).
    Task<ServiceResult> MarkAsReadAsync(int messageId, CancellationToken ct = default);

    // מספר ההודעות הנכנסות שלא נקראו, למשתמש המחובר.
    Task<ServiceResult<int>> GetUnreadCountAsync(CancellationToken ct = default);

    // רק הנמען או השולח (מוחק מהתיבה שלו). הודעה של מישהו אחר: NotFound.
    Task<ServiceResult> DeleteAsync(int messageId, CancellationToken ct = default);
}
