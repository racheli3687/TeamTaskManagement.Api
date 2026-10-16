using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.TaskItems;

public class CreateTaskRequestDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = default!;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }
}
