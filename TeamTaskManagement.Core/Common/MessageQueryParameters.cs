using System.ComponentModel.DataAnnotations;
using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Common;

public class MessageQueryParameters : PaginationParameters
{
    // EnumDataType: ערך שלא מוגדר ב-enum מחזיר 400. null נחשב תקין.
    [EnumDataType(typeof(MessageBox))]
    public MessageBox Box { get; set; } = MessageBox.Inbox;

    [EnumDataType(typeof(MessageStatus))]
    public MessageStatus? Status { get; set; }

    // חיפוש בתוכן ההודעה
    public string? Search { get; set; }

    // סינון לפי הצד השני בשיחה (שולח בתיבת נכנסות, מקבל בנשלחות)
    public int? OtherUserId { get; set; }

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}