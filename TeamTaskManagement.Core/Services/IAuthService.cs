using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Auth;

namespace TeamTaskManagement.Core.Services;

public interface IAuthService
{
    // פתוח לכולם (בלי הרשאה). יוצר חברה חדשה ואת המנהל שלה (Role=Manager) באותה טרנזקציה,
    // ומחזיר JWT. שם משתמש תפוס: Conflict.
    Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default);

    // כניסה למערכת - כל התפקידים. מחזיר JWT.
    // שם משתמש או סיסמה שגויים: Unauthorized (בלי לחשוף מה מהם שגוי).
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
}
