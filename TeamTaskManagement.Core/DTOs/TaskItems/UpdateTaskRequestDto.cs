using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.TaskItems;

// עריכת תוכן המשימה (ראש צוות). הסטטוס מתעדכן בנפרד.
public class UpdateTaskRequestDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = default!;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }
}
