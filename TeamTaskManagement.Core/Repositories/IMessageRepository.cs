using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

public interface IMessageRepository
{
    // במעקב. null אם לא קיימת.
    Task<Message?> GetByIdAsync(int messageId, CancellationToken ct = default);

    // הודעות של משתמש (נכנסות או נשלחות לפי query.Box), עם סינון, חיפוש ו-pagination בשאילתה,
    // ממוינות מהחדשה לישנה, כולל Sender ו-Recipient. לא מחזיר הודעות שהמשתמש מחק מהצד שלו.
    // קריאה בלבד.
    Task<PagedResult<Message>> GetPagedAsync(int userId, MessageQueryParameters query, CancellationToken ct = default);

    // מספר ההודעות הנכנסות שלא נקראו ולא נמחקו, למשתמש.
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);

    Task AddAsync(Message message, CancellationToken ct = default);
    void Remove(Message message);
}
