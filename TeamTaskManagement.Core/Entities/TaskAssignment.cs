namespace TeamTaskManagement.Core.Entities;

// ישות קישור (join entity) בין TaskItem ל-User: many-to-many עם נתונים נוספים.
// שיוך פעיל = UnassignedAt == null. כשחבר מוותר על משימה לא מוחקים - מסמנים UnassignedAt,
// ובכך משימה יכולה לעבור בין כמה חברים לאורך הזמן.
public class TaskAssignment
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = default!;

    public int UserId { get; set; }
    public User User { get; set; } = default!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }
}
