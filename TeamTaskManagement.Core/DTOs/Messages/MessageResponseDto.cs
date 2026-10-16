using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.DTOs.Messages;

public class MessageResponseDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = default!;
    public int RecipientId { get; set; }
    public string RecipientName { get; set; } = default!;
    public string Text { get; set; } = default!;
    public DateTime SentAt { get; set; }
    public MessageStatus Status { get; set; }
}
