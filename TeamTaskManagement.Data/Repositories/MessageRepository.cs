using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _context;

    public MessageRepository(AppDbContext context)
    {
        _context = context;
    }

    // במעקב: לסימון כנקרא או למחיקה
    public Task<Message?> GetByIdAsync(int messageId, CancellationToken ct = default)
        => _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public async Task<PagedResult<Message>> GetPagedAsync(
        int userId, MessageQueryParameters query, CancellationToken ct = default)
    {
        var source = _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .AsQueryable();

        // תיבת נכנסות: אני הנמען. נשלחות: אני השולח. בכל מקרה, לא מה שמחקתי מהצד שלי.
        source = query.Box == MessageBox.Inbox
            ? source.Where(m => m.RecipientId == userId && !m.IsDeletedByRecipient)
            : source.Where(m => m.SenderId == userId && !m.IsDeletedBySender);

        if (query.Status.HasValue)
            source = source.Where(m => m.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            source = source.Where(m => EF.Functions.ILike(m.Text, pattern));
        }

        // הצד השני בשיחה: השולח בתיבת נכנסות, הנמען בנשלחות
        if (query.OtherUserId.HasValue)
        {
            var otherId = query.OtherUserId.Value;
            source = query.Box == MessageBox.Inbox
                ? source.Where(m => m.SenderId == otherId)
                : source.Where(m => m.RecipientId == otherId);
        }

        if (query.FromDate.HasValue)
            source = source.Where(m => m.SentAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            source = source.Where(m => m.SentAt <= query.ToDate.Value);

        var total = await source.CountAsync(ct);

        var items = await source
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Message>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => _context.Messages.CountAsync(
            m => m.RecipientId == userId
                 && m.Status == MessageStatus.Unread
                 && !m.IsDeletedByRecipient, ct);

    public async Task AddAsync(Message message, CancellationToken ct = default)
        => await _context.Messages.AddAsync(message, ct);

    public void Remove(Message message)
        => _context.Messages.Remove(message);
}