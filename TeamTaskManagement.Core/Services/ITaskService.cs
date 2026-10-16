using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;

namespace TeamTaskManagement.Core.Services;

public interface ITaskService
{
    // מנהל: כל צוות בחברה שלו. ראש צוות וחבר: רק הצוות שלהם.
    // סינון, מיון ו-pagination מתבצעים בשאילתה מול בסיס הנתונים.
    Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByTeamAsync(int teamId, TaskQueryParameters query, CancellationToken ct = default);

    // מנהל: כל חבר בחברה שלו. ראש צוות: חבר מהצוות שלו. חבר: רק את עצמו.
    // מחזיר את המשימות עם שיוך פעיל לחבר.
    Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByMemberAsync(int memberId, TaskQueryParameters query, CancellationToken ct = default);

    // אותן הרשאות כמו שליפה לפי צוות (המשימה חייבת להיות בצוות שהמשתמש רשאי לראות).
    Task<ServiceResult<TaskResponseDto>> GetByIdAsync(int taskId, CancellationToken ct = default);

    // ראש צוות בלבד, ורק לצוות שלו. נוצרת עם Status=Open.
    Task<ServiceResult<TaskResponseDto>> CreateAsync(int teamId, CreateTaskRequestDto request, CancellationToken ct = default);

    // ראש צוות בלבד, ורק למשימות של הצוות שלו. לא נוגע בסטטוס.
    Task<ServiceResult<TaskResponseDto>> UpdateAsync(int taskId, UpdateTaskRequestDto request, CancellationToken ct = default);

    // ראש צוות (משימות הצוות שלו), וחבר - רק למשימה ששייכת לו כרגע.
    Task<ServiceResult<TaskResponseDto>> UpdateStatusAsync(int taskId, UpdateTaskStatusRequestDto request, CancellationToken ct = default);

    // ראש צוות בלבד, ורק למשימות של הצוות שלו (הסרת משימה מהקבוצה).
    Task<ServiceResult> DeleteAsync(int taskId, CancellationToken ct = default);

    // חבר בלבד, ורק משימה בצוות שלו. זו הפעולה שמתחרים עליה.
    // המשימה חייבת להיות Open וללא שיוך פעיל, אחרת Conflict.
    // יוצר TaskAssignment ומעביר ל-InProgress באותה שמירה.
    // DbUpdateConcurrencyException: Conflict, וכתיבת Warning ללוג.
    Task<ServiceResult<TaskResponseDto>> ClaimAsync(int taskId, CancellationToken ct = default);

    // ראש צוות (משימות הצוות שלו) והחבר המחזיק במשימה (הסרת משימה מחבר).
    // סוגר את השיוך הפעיל (UnassignedAt) והמשימה חוזרת ל-Open. גם כאן יש בדיקת Version.
    Task<ServiceResult<TaskResponseDto>> ReleaseAsync(int taskId, CancellationToken ct = default);
}
