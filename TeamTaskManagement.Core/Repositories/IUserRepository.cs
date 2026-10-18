using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

public interface IUserRepository
{
    // ישות במעקב. null אם לא קיים.
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);

    // להתחברות. ייחודי בכל המערכת, ולכן לא מסננים לפי חברה.
    Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default);

    // בדיקת כפילות שם משתמש. excludeUserId מאפשר לעדכן פרופיל בלי להתנגש בעצמו.
    Task<bool> UserNameExistsAsync(string userName, int? excludeUserId = null, CancellationToken ct = default);

    // חברי צוות עם pagination בשאילתה, ממוינים לפי שם משתמש. קריאה בלבד.
    Task<PagedResult<User>> GetPagedByTeamAsync(int teamId, PaginationParameters pagination, CancellationToken ct = default);
    // ראש הצוות הנוכחי של הצוות, במעקב (לעדכון התפקיד). null אם אין לצוות ראש צוות.
    Task<User?> GetTeamLeadAsync(int teamId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
