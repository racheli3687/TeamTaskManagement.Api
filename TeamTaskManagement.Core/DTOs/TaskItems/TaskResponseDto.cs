using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.DTOs.TaskItems;

public class TaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public TaskItemStatus Status { get; set; }
    public int TeamId { get; set; }

    // החבר שמחזיק במשימה כרגע (השיוך הפעיל), או null אם המשימה פנויה
    public int? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
}
