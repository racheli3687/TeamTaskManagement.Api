using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Messages;

public class SendMessageRequestDto
{
    [Range(1, int.MaxValue)]
    public int RecipientId { get; set; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Text { get; set; } = default!;
}
