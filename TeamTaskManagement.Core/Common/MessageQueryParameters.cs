using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Common;

public class MessageQueryParameters : PaginationParameters
{
    public MessageBox Box { get; set; } = MessageBox.Inbox;
    public MessageStatus? Status { get; set; }

    // חיפוש בתוכן ההודעה
    public string? Search { get; set; }

    // סינון לפי הצד השני בשיחה (שולח בתיבת נכנסות, מקבל בנשלחות)
    public int? OtherUserId { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
