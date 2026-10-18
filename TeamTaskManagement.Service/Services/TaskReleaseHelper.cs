// TeamTaskManagement.Service/Services/TaskReleaseHelper.cs
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Service.Services;

// משותף ל-TeamService (מעבר בין צוותים) ול-UserService (הסרה מצוות).
// משנה ישויות במעקב בלבד ולא שומר: הקורא שומר פעם אחת יחד עם שאר השינויים, בטרנזקציה אחת.
internal static class TaskReleaseHelper
{
    public static async Task ReleaseActiveTasksAsync(
        ITaskAssignmentRepository assignments, int userId, CancellationToken ct)
    {
        var activeAssignments = await assignments.GetActiveByUserIdAsync(userId, ct);
        DateTime now = DateTime.UtcNow;

        foreach (var assignment in activeAssignments)
        {
            // משימה שהושלמה נשארת משויכת - תיעוד של מי השלים אותה
            if (assignment.TaskItem.Status == TaskItemStatus.Done)
                continue;

            assignment.UnassignedAt = now;
            assignment.TaskItem.Status = TaskItemStatus.Open; // ה-Version מתחלף ב-SaveChangesAsync
        }
    }
}