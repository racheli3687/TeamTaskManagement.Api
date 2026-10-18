// TeamTaskManagement.Service/Services/TeamService.cs
using AutoMapper;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Teams;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Services;

public class TeamService : ITeamService
{
    private const string ManagerOnlyMessage = "הפעולה מותרת למנהל החברה בלבד.";
    private const string TeamNotFoundMessage = "הצוות לא נמצא.";
    private const string DuplicateNameMessage = "כבר קיים צוות בשם זה בחברה.";

    private readonly ITeamRepository _teams;
    private readonly IUserRepository _users;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public TeamService(
        ITeamRepository teams,
        IUserRepository users,
        ITaskAssignmentRepository assignments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _teams = teams;
        _users = users;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    private bool IsManager => _currentUser.Role == UserRole.Manager;

    public async Task<ServiceResult<PagedResult<TeamResponseDto>>> GetAllAsync(
        PaginationParameters pagination, CancellationToken ct = default)
    {
        if (IsManager)
        {
            var paged = await _teams.GetPagedByOrganizationAsync(_currentUser.OrganizationId, pagination, ct);

            return ServiceResult<PagedResult<TeamResponseDto>>.Success(new PagedResult<TeamResponseDto>
            {
                Items = _mapper.Map<List<TeamResponseDto>>(paged.Items),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize
            });
        }

        // ראש צוות וחבר: רק הצוות שלהם - לכל היותר פריט אחד, ורק בעמוד הראשון
        var items = new List<TeamResponseDto>();
        int total = 0;

        if (_currentUser.TeamId is int ownTeamId)
        {
            var own = await _teams.GetWithMembersCountAsync(ownTeamId, ct);
            if (own is not null && own.Team.OrganizationId == _currentUser.OrganizationId)
            {
                total = 1;
                if (pagination.Page == 1)
                    items.Add(_mapper.Map<TeamResponseDto>(own));
            }
        }

        return ServiceResult<PagedResult<TeamResponseDto>>.Success(new PagedResult<TeamResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        });
    }

    public async Task<ServiceResult<TeamResponseDto>> GetByIdAsync(int teamId, CancellationToken ct = default)
    {
        var result = await _teams.GetWithMembersCountAsync(teamId, ct);

        if (result is null || result.Team.OrganizationId != _currentUser.OrganizationId)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.NotFound, TeamNotFoundMessage);

        if (!IsManager && _currentUser.TeamId != teamId)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Forbidden, "אין לך הרשאה לצפות בצוות זה.");

        return ServiceResult<TeamResponseDto>.Success(_mapper.Map<TeamResponseDto>(result));
    }

    public async Task<ServiceResult<TeamResponseDto>> CreateAsync(
        CreateTeamRequestDto request, CancellationToken ct = default)
    {
        if (!IsManager)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Forbidden, ManagerOnlyMessage);

        string name = request.Name.Trim();

        if (await _teams.NameExistsAsync(_currentUser.OrganizationId, name, ct: ct))
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Conflict, DuplicateNameMessage);

        var team = _mapper.Map<Team>(request);
        team.Name = name;
        team.OrganizationId = _currentUser.OrganizationId; // תמיד מהמשתמש המחובר, לעולם לא מהלקוח

        await _teams.AddAsync(team, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Conflict, DuplicateNameMessage);
        }

        // צוות חדש - אין בו עדיין חברים
        return ServiceResult<TeamResponseDto>.Success(
            _mapper.Map<TeamResponseDto>(new TeamWithMembersCount(team, 0)));
    }

    public async Task<ServiceResult<TeamResponseDto>> RenameAsync(
        int teamId, UpdateTeamRequestDto request, CancellationToken ct = default)
    {
        if (!IsManager)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Forbidden, ManagerOnlyMessage);

        var team = await GetOwnTeamForUpdateAsync(teamId, ct);
        if (team is null)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.NotFound, TeamNotFoundMessage);

        string name = request.Name.Trim();

        if (await _teams.NameExistsAsync(_currentUser.OrganizationId, name, excludeTeamId: teamId, ct: ct))
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Conflict, DuplicateNameMessage);

        _mapper.Map(request, team);
        team.Name = name;

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.Conflict, DuplicateNameMessage);
        }

        // טעינה מחדש לקבלת מספר החברים העדכני
        var updated = await _teams.GetWithMembersCountAsync(teamId, ct);
        if (updated is null)
            return ServiceResult<TeamResponseDto>.Fail(ResultStatus.NotFound, TeamNotFoundMessage);

        return ServiceResult<TeamResponseDto>.Success(_mapper.Map<TeamResponseDto>(updated));
    }

    public async Task<ServiceResult> DeleteAsync(int teamId, CancellationToken ct = default)
    {
        if (!IsManager)
            return ServiceResult.Fail(ResultStatus.Forbidden, ManagerOnlyMessage);

        var team = await GetOwnTeamForUpdateAsync(teamId, ct);
        if (team is null)
            return ServiceResult.Fail(ResultStatus.NotFound, TeamNotFoundMessage);

        if (await _teams.HasUsersOrTasksAsync(teamId, ct))
            return ServiceResult.Fail(ResultStatus.Conflict, "לא ניתן למחוק צוות שיש בו משתמשים או משימות.");

        _teams.Remove(team);
        await _unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AssignTeamLeadAsync(int teamId, int userId, CancellationToken ct = default)
    {
        if (!IsManager)
            return ServiceResult.Fail(ResultStatus.Forbidden, ManagerOnlyMessage);

        var team = await GetOwnTeamForUpdateAsync(teamId, ct);
        if (team is null)
            return ServiceResult.Fail(ResultStatus.NotFound, TeamNotFoundMessage);

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.OrganizationId != _currentUser.OrganizationId)
            return ServiceResult.Fail(ResultStatus.NotFound, "המשתמש לא נמצא.");

        if (user.Role == UserRole.Manager)
            return ServiceResult.Fail(ResultStatus.ValidationFailed, "לא ניתן למנות את מנהל החברה לראש צוות.");

        // כבר ראש הצוות הזה - אין מה לשנות
        if (user.Role == UserRole.TeamLead && user.TeamId == teamId)
            return ServiceResult.Success();

        // מעבר מצוות אחר: שחרור המשימות הפעילות שלו שם (משימות שהושלמו נשארות כתיעוד)
        if (user.TeamId != teamId)
        {
            var activeAssignments = await _assignments.GetActiveByUserIdAsync(user.Id, ct);
            DateTime now = DateTime.UtcNow;

            foreach (var assignment in activeAssignments)
            {
                if (assignment.TaskItem.Status == TaskItemStatus.Done)
                    continue;

                assignment.UnassignedAt = now;
                assignment.TaskItem.Status = TaskItemStatus.Open; // ה-Version מתחלף ב-SaveChangesAsync
            }
        }

        // ראש צוות אחד בלבד: הקודם חוזר להיות חבר באותו צוות
        var currentLead = await _users.GetTeamLeadAsync(teamId, ct);
        if (currentLead is not null)
            currentLead.Role = UserRole.Member;

        user.Role = UserRole.TeamLead;
        user.TeamId = teamId;

        try
        {
            // הכל בשמירה אחת: שחרור משימות, הורדת הקודם ומינוי החדש - יחד, או כלום
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // אחת המשימות ששוחררו השתנתה במקביל (למשל עדכון סטטוס)
            return ServiceResult.Fail(ResultStatus.Conflict, "נתוני המשתמש השתנו במקביל. נסו שוב.");
        }

        return ServiceResult.Success();
    }

    // צוות במעקב, רק אם הוא שייך לחברה של המשתמש המחובר. אחרת null (=> NotFound).
    private async Task<Team?> GetOwnTeamForUpdateAsync(int teamId, CancellationToken ct)
    {
        var team = await _teams.GetByIdAsync(teamId, ct);
        return team is null || team.OrganizationId != _currentUser.OrganizationId ? null : team;
    }
}