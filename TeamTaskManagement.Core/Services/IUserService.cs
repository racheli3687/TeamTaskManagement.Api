using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Users;

namespace TeamTaskManagement.Core.Services;

public interface IUserService
{
    // מנהל: כל צוות בחברה שלו. ראש צוות: רק הצוות שלו.
    Task<ServiceResult<PagedResult<UserResponseDto>>> GetMembersByTeamAsync(int teamId, PaginationParameters pagination, CancellationToken ct = default);

    // מנהל: לכל צוות בחברה שלו. ראש צוות: רק לצוות שלו.
    // יוצר משתמש חדש בחברה עם Role=Member. כך המנהל יוצר את ראשי הצוותים: מוסיף משתמש
    // ואז מקדם אותו ב-ITeamService.AssignTeamLeadAsync.
    // שם משתמש תפוס: Conflict. הסיסמה נשמרת כ-hash.
    Task<ServiceResult<UserResponseDto>> AddMemberAsync(int teamId, AddMemberRequestDto request, CancellationToken ct = default);

    // מנהל: מכל צוות בחברה שלו. ראש צוות: רק מהצוות שלו. מוציא את החבר מהצוות (TeamId=null)
    // ומשחרר את המשימות הפעילות שלו (חוזרות ל-Open ו-Version מתחלף).
    // אי אפשר להסיר ראש צוות או מנהל: Forbidden.
    Task<ServiceResult> RemoveMemberAsync(int teamId, int userId, CancellationToken ct = default);

    // כל משתמש - הפרופיל של עצמו.
    Task<ServiceResult<UserResponseDto>> GetMyProfileAsync(CancellationToken ct = default);

    // כל משתמש - רק של עצמו. שם משתמש תפוס: Conflict.
    Task<ServiceResult<UserResponseDto>> UpdateProfileAsync(UpdateProfileRequestDto request, CancellationToken ct = default);

    // כל משתמש - רק של עצמו. סיסמה נוכחית שגויה: ValidationFailed.
    Task<ServiceResult> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken ct = default);
}
