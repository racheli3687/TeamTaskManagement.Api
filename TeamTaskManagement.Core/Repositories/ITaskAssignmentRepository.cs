using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

public interface ITaskAssignmentRepository
{
    // השיוך הפעיל של משימה (UnassignedAt == null), במעקב. null אם המשימה פנויה.
    Task<TaskAssignment?> GetActiveByTaskIdAsync(int taskId, CancellationToken ct = default);

    // כל השיוכים הפעילים של חבר, במעקב וכולל ה-TaskItem. משמש בהסרת חבר מצוות,
    // כדי לשחרר את המשימות שלו.
    Task<List<TaskAssignment>> GetActiveByUserIdAsync(int userId, CancellationToken ct = default);

    Task AddAsync(TaskAssignment assignment, CancellationToken ct = default);
}
