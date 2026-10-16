using System.ComponentModel.DataAnnotations;
using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Entities;

// לא קוראים לו Task - התנגשות עם System.Threading.Tasks.Task
// זה המשאב המוגבל: שני חברי צוות לא יכולים לקחת את אותה משימה
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    // תמיד UTC
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Open;

    public int TeamId { get; set; }
    public Team Team { get; set; } = default!;

    // Concurrency token (Optimistic Concurrency).
    // עובד גם ב-SQL Server וגם ב-PostgreSQL.
    // הערך מוחלף ב-SaveChangesAsync של ה-DbContext (Data) בכל עדכון.
    [ConcurrencyCheck]
    public Guid Version { get; set; } = Guid.NewGuid();

    // many-to-many (דרך ישות הקישור TaskAssignment)
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
}
