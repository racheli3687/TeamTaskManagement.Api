using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

// "שיוך פעיל" = TaskAssignment עם UnassignedAt == null.
// כל הטעינות כאן כוללות את השיוך הפעיל ואת המשתמש שלו (Include / ThenInclude),
// כדי להציג AssignedUserName בלי שאילתה נוספת לכל משימה.
public interface ITaskRepository
{
    // קריאה בלבד. null אם לא קיים.
    Task<TaskItem?> GetByIdAsync(int taskId, CancellationToken ct = default);

    // במעקב, לפעולות שמשנות את המשימה (Claim, Release, עדכון סטטוס, עריכה).
    // ה-Version שנטען כאן הוא הערך שנבדק בשמירה.
    Task<TaskItem?> GetForUpdateAsync(int taskId, CancellationToken ct = default);

    // משימות של צוות, עם סינון, חיפוש, מיון ו-pagination בשאילתה. קריאה בלבד.
    Task<PagedResult<TaskItem>> GetPagedByTeamAsync(int teamId, TaskQueryParameters query, CancellationToken ct = default);

    // משימות שמשויכות כרגע לחבר (שיוך פעיל), עם אותם סינון, מיון ו-pagination. קריאה בלבד.
    // teamId: הצגה רק בצוות זה (צפייה של אחרים בחבר). null: כל המשימות של החבר (החבר עצמו),
    // כולל משימות Done שנשארו משויכות אליו כתיעוד בצוות קודם.
    Task<PagedResult<TaskItem>> GetPagedByAssignedUserAsync(int userId, int? teamId, TaskQueryParameters query, CancellationToken ct = default);

    Task AddAsync(TaskItem task, CancellationToken ct = default);
    void Remove(TaskItem task);
}
