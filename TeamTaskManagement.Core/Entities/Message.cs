using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Entities;

public class Message
{
    public int Id { get; set; }

    public int SenderId { get; set; }
    public User Sender { get; set; } = default!;

    public int RecipientId { get; set; }
    public User Recipient { get; set; } = default!;

    public string Text { get; set; } = default!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public MessageStatus Status { get; set; } = MessageStatus.Unread;

    // מחיקה "מהתיבה שלי": כל צד מוחק רק מהצד שלו. כששני הצדדים מחקו, ההודעה נמחקת פיזית.
    public bool IsDeletedBySender { get; set; }
    public bool IsDeletedByRecipient { get; set; }
}
