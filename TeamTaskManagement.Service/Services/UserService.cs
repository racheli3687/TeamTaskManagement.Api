// TeamTaskManagement.Service/Services/UserService.cs
using AutoMapper;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Users;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Services;

public class UserService : IUserService
{
    private const string UserNotFoundMessage = "המשתמש לא נמצא.";
    private const string DuplicateUserNameMessage = "שם המשתמש כבר תפוס.";

    private readonly IUserRepository _users;
    private readonly ITeamRepository _teams;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;

    public UserService(
        IUserRepository users,
        ITeamRepository teams,
        ITaskAssignmentRepository assignments,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        IMapper mapper)
    {
        _users = users;
        _teams = teams;
        _assignments = assignments;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
    }

    public async Task<ServiceResult<PagedResult<UserResponseDto>>> GetMembersByTeamAsync(
        int teamId, PaginationParameters pagination, CancellationToken ct = default)
    {
        var denied = await CheckTeamAccessAsync(teamId, allowMember: true, ct);
        if (denied is not null)
            return ServiceResult<PagedResult<UserResponseDto>>.Fail(denied.Status, denied.Message!);

        var paged = await _users.GetPagedByTeamAsync(teamId, pagination, ct);

        return ServiceResult<PagedResult<UserResponseDto>>.Success(new PagedResult<UserResponseDto>
        {
            Items = _mapper.Map<List<UserResponseDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        });
    }

    public async Task<ServiceResult<UserResponseDto>> AddMemberAsync(
        int teamId, AddMemberRequestDto request, CancellationToken ct = default)
    {
        var denied = await CheckTeamAccessAsync(teamId, allowMember: false, ct);
        if (denied is not null)
            return ServiceResult<UserResponseDto>.Fail(denied.Status, denied.Message!);

        string userName = request.UserName.Trim();

        if (await _users.UserNameExistsAsync(userName, ct: ct))
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.Conflict, DuplicateUserNameMessage);

        // כל השדות הרגישים נקבעים בשרת - מהבקשה מגיעים רק השם והסיסמה
        var member = new User
        {
            UserName = userName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Member,
            OrganizationId = _currentUser.OrganizationId,
            TeamId = teamId
        };

        await _users.AddAsync(member, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.Conflict, DuplicateUserNameMessage);
        }

        return ServiceResult<UserResponseDto>.Success(_mapper.Map<UserResponseDto>(member));
    }

    public async Task<ServiceResult> RemoveMemberAsync(int teamId, int userId, CancellationToken ct = default)
    {
        var denied = await CheckTeamAccessAsync(teamId, allowMember: false, ct);
        if (denied is not null)
            return denied;

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.OrganizationId != _currentUser.OrganizationId || user.TeamId != teamId)
            return ServiceResult.Fail(ResultStatus.NotFound, "המשתמש אינו חבר בצוות זה.");

        if (user.Role != UserRole.Member)
            return ServiceResult.Fail(ResultStatus.Forbidden, "לא ניתן להסיר ראש צוות או מנהל מהצוות.");

        await TaskReleaseHelper.ReleaseActiveTasksAsync(_assignments, user.Id, ct);
        user.TeamId = null;

        try
        {
            // שמירה אחת: שחרור המשימות והוצאה מהצוות יחד
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return ServiceResult.Fail(ResultStatus.Conflict, "משימות של המשתמש השתנו במקביל. נסו שוב.");
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<UserResponseDto>> GetMyProfileAsync(CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(_currentUser.Id, ct);
        if (user is null)
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.NotFound, UserNotFoundMessage);

        return ServiceResult<UserResponseDto>.Success(_mapper.Map<UserResponseDto>(user));
    }

    public async Task<ServiceResult<UserResponseDto>> UpdateProfileAsync(
        UpdateProfileRequestDto request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(_currentUser.Id, ct);
        if (user is null)
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.NotFound, UserNotFoundMessage);

        string userName = request.UserName.Trim();

        if (await _users.UserNameExistsAsync(userName, excludeUserId: user.Id, ct: ct))
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.Conflict, DuplicateUserNameMessage);

        user.UserName = userName;

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            return ServiceResult<UserResponseDto>.Fail(ResultStatus.Conflict, DuplicateUserNameMessage);
        }

        return ServiceResult<UserResponseDto>.Success(_mapper.Map<UserResponseDto>(user));
    }

    public async Task<ServiceResult> ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(_currentUser.Id, ct);
        if (user is null)
            return ServiceResult.Fail(ResultStatus.NotFound, UserNotFoundMessage);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return ServiceResult.Fail(ResultStatus.ValidationFailed, "הסיסמה הנוכחית שגויה.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _unitOfWork.SaveChangesAsync(ct);

        return ServiceResult.Success();
    }

    // גישה לחברי צוות: מנהל - כל צוות בחברה שלו; ראש צוות - רק הצוות שלו;
    // חבר - רק הצוות שלו, ורק כש-allowMember (צפייה). ניהול (הוספה/הסרה) - ללא חבר.
    // מחזיר null אם מותר, אחרת את תוצאת הכישלון.
    private async Task<ServiceResult?> CheckTeamAccessAsync(int teamId, bool allowMember, CancellationToken ct)
    {
        if (_currentUser.Role == UserRole.Member && !allowMember)
            return ServiceResult.Fail(ResultStatus.Forbidden, "אין לך הרשאה לנהל חברי צוות.");

        var team = await _teams.GetByIdAsync(teamId, ct);
        if (team is null || team.OrganizationId != _currentUser.OrganizationId)
            return ServiceResult.Fail(ResultStatus.NotFound, "הצוות לא נמצא.");

        // מי שאינו מנהל - רק הצוות שלו (ראש צוות וחבר כאחד)
        if (_currentUser.Role != UserRole.Manager && _currentUser.TeamId != teamId)
            return ServiceResult.Fail(ResultStatus.Forbidden, "אין לך הרשאה לצוות זה.");

        return null;
    }
}