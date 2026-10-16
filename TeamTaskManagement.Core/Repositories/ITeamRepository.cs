using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

// מוסכמה בכל ה-repositories: מתודות Get שמיועדות לעדכון מחזירות ישות במעקב (tracked),
// ושאילתות קריאה בלבד (רשימות, תצוגה) רצות עם AsNoTracking.
// עדכון = שינוי של הישות שנטענה ואז IUnitOfWork.SaveChangesAsync, ולכן אין מתודת Update.
public interface ITeamRepository
{
    // ישות במעקב, לעדכון ולמחיקה. null אם לא קיים.
    Task<Team?> GetByIdAsync(int teamId, CancellationToken ct = default);

    // לקריאה בלבד, כולל מספר חברים. null אם לא קיים.
    Task<TeamWithMembersCount?> GetWithMembersCountAsync(int teamId, CancellationToken ct = default);

    // כל הצוותים של חברה, עם pagination בשאילתה, ממוינים לפי שם.
    Task<PagedResult<TeamWithMembersCount>> GetPagedByOrganizationAsync(
        int organizationId, PaginationParameters pagination, CancellationToken ct = default);

    // בדיקת כפילות שם צוות בתוך חברה. excludeTeamId מאפשר לשנות שם בלי להתנגש בעצמו.
    Task<bool> NameExistsAsync(int organizationId, string name, int? excludeTeamId = null, CancellationToken ct = default);

    // האם יש בצוות משתמשים או משימות (חוסם מחיקה).
    Task<bool> HasUsersOrTasksAsync(int teamId, CancellationToken ct = default);

    Task AddAsync(Team team, CancellationToken ct = default);
    void Remove(Team team);
}
