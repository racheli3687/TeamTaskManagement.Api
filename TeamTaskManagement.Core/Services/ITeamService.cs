using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Teams;

namespace TeamTaskManagement.Core.Services;

public interface ITeamService
{
    // מנהל: כל הצוותים של החברה שלו. ראש צוות וחבר: רק הצוות שלהם.
    Task<ServiceResult<PagedResult<TeamResponseDto>>> GetAllAsync(PaginationParameters pagination, CancellationToken ct = default);

    // מנהל: כל צוות בחברה שלו. ראש צוות וחבר: רק הצוות שלהם (אחרת Forbidden).
    // צוות של חברה אחרת: NotFound.
    Task<ServiceResult<TeamResponseDto>> GetByIdAsync(int teamId, CancellationToken ct = default);

    // מנהל בלבד. נוצר בחברה של המנהל. שם צוות כפול באותה חברה: Conflict.
    Task<ServiceResult<TeamResponseDto>> CreateAsync(CreateTeamRequestDto request, CancellationToken ct = default);

    // מנהל בלבד, צוות מהחברה שלו.
    Task<ServiceResult<TeamResponseDto>> RenameAsync(int teamId, UpdateTeamRequestDto request, CancellationToken ct = default);

    // מנהל בלבד, צוות מהחברה שלו. צוות שיש בו משתמשים או משימות: Conflict (ולא מחיקה שקטה של נתונים).
    Task<ServiceResult> DeleteAsync(int teamId, CancellationToken ct = default);

    // מנהל בלבד. מקדם משתמש קיים מהחברה שלו לראש צוות של הצוות (Role=TeamLead, TeamId=teamId).
    Task<ServiceResult> AssignTeamLeadAsync(int teamId, int userId, CancellationToken ct = default);
}
