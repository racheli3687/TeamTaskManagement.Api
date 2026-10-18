// TeamTaskManagement.Service/Services/TaskService.cs
using AutoMapper;
using Microsoft.Extensions.Logging;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Service.Services;

public class TaskService : ITaskService
{
    private const string TaskNotFoundMessage = "המשימה לא נמצאה.";
    private const string OtherTeamMessage = "אין הרשאה לפעול על משימה של צוות אחר.";
    private const string ConcurrencyMessage = "המשימה עודכנה במקביל על ידי משתמש אחר. יש לרענן ולנסות שוב.";

    private readonly ITaskRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITeamRepository _teams;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository tasks,
        ITaskAssignmentRepository assignments,
        ITeamRepository teams,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper,
        ILogger<TaskService> logger)
    {
        _tasks = tasks;
        _assignments = assignments;
        _teams = teams;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    private bool IsManager => _currentUser.Role == UserRole.Manager;

    // ===== משימה 3.7 =====

    public Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByTeamAsync(
        int teamId, TaskQueryParameters query, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByMemberAsync(
        int memberId, TaskQueryParameters query, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult<TaskResponseDto>> GetByIdAsync(int taskId, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult<TaskResponseDto>> CreateAsync(
        int teamId, CreateTaskRequestDto request, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult<TaskResponseDto>> UpdateAsync(
        int taskId, UpdateTaskRequestDto request, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult<TaskResponseDto>> UpdateStatusAsync(
        int taskId, UpdateTaskStatusRequestDto request, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    public Task<ServiceResult> DeleteAsync(int taskId, CancellationToken ct = default)
        => throw new NotImplementedException("ימומש במשימה 3.7");

    // ===== התחרות על המשאב המוגבל =====

    public async Task<ServiceResult<TaskResponseDto>> ClaimAsync(int taskId, CancellationToken ct = default)
    {
        // 1. תפקיד: רק חבר צוות, ורק אם הוא משויך לצוות
        if (_currentUser.Role != UserRole.Member || _currentUser.TeamId is null)
            return Failure(ResultStatus.Forbidden, "רק חבר צוות רשאי לתפוס משימה.");

        // 2. המשימה במעקב: ה-Version שנטען כאן ייכנס לתנאי ה-WHERE בשמירה
        var task = await _tasks.GetForUpdateAsync(taskId, ct);
        if (task is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        // 3. חברה אחרת => NotFound, צוות אחר => Forbidden
        var teamError = await CheckTaskTeamAsync(task.TeamId, ct);
        if (teamError is not null)
            return teamError;

        // 4. כלל עסקי: פתוחה וללא שיוך פעיל (Open == אין שיוך פעיל)
        if (task.Status != TaskItemStatus.Open || task.Assignments.Any(a => a.UnassignedAt == null))
            return Failure(ResultStatus.Conflict, "המשימה כבר נתפסה או שאינה פתוחה.");

        // 5. שיוך חדש + מעבר ל-InProgress. שניהם נשמרים יחד, בטרנזקציה אחת.
        await _assignments.AddAsync(new TaskAssignment
        {
            TaskItemId = task.Id,
            UserId = _currentUser.Id,
            AssignedAt = DateTime.UtcNow
        }, ct);

        task.Status = TaskItemStatus.InProgress; // ה-Version מתחלף ב-SaveChangesAsync

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // חבר אחר הקדים: ה-Version השתנה, או שהאינדקס הייחודי חסם שיוך פעיל שני
            _logger.LogWarning(
                "Claim conflict: task {TaskId} was claimed concurrently. User {UserId} lost the race.",
                taskId, _currentUser.Id);
            return Failure(ResultStatus.Conflict, ConcurrencyMessage);
        }

        // טעינה מחדש לקבלת AssignedUserName (לשיוך החדש אין עדיין User טעון)
        var claimed = await _tasks.GetByIdAsync(taskId, ct);
        if (claimed is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(claimed));
    }

    public async Task<ServiceResult<TaskResponseDto>> ReleaseAsync(int taskId, CancellationToken ct = default)
    {
        // 1. תפקיד: ראש צוות או חבר צוות, עם צוות
        if (_currentUser.Role is not (UserRole.TeamLead or UserRole.Member) || _currentUser.TeamId is null)
            return Failure(ResultStatus.Forbidden, "רק ראש הצוות או החבר המחזיק במשימה רשאים לשחרר אותה.");

        // 2. המשימה במעקב, כולל השיוך הפעיל
        var task = await _tasks.GetForUpdateAsync(taskId, ct);
        if (task is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        // 3. חברה אחרת => NotFound, צוות אחר => Forbidden
        var teamError = await CheckTaskTeamAsync(task.TeamId, ct);
        if (teamError is not null)
            return teamError;

        // 4. אין שיוך פעיל. נבדק לפני בדיקת המחזיק, כדי שחבר שהמשימה שוחררה ממנו
        //    במקביל יקבל הודעה מדויקת ולא הודעת הרשאה.
        var assignment = task.Assignments.FirstOrDefault(a => a.UnassignedAt == null);
        if (assignment is null)
            return Failure(ResultStatus.Conflict, "המשימה אינה משויכת לאף חבר צוות.");

        // 5. חבר רשאי לשחרר רק משימה שמשויכת אליו
        if (_currentUser.Role == UserRole.Member && assignment.UserId != _currentUser.Id)
            return Failure(ResultStatus.Forbidden, "ניתן לשחרר רק משימה שמשויכת אליך.");

        // 6. משימה שהושלמה נשארת משויכת כתיעוד
        if (task.Status == TaskItemStatus.Done)
            return Failure(ResultStatus.Conflict, "משימה שהושלמה אינה ניתנת לשחרור.");

        // 7. סגירת השיוך והחזרה ל-Open, בשמירה אחת
        assignment.UnassignedAt = DateTime.UtcNow;
        task.Status = TaskItemStatus.Open; // תמיד שינוי בפועל (InProgress => Open), ולכן ה-Version נבדק

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Release conflict: task {TaskId} was modified concurrently. User {UserId}.",
                taskId, _currentUser.Id);
            return Failure(ResultStatus.Conflict, ConcurrencyMessage);
        }

        // אין צורך בטעינה מחדש: השיוך שנסגר מסונן ב-TaskProfile, ולכן AssignedUserId יוצא null
        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));
    }

    // ===== עזר =====

    // בודק שהמשתמש רשאי לפעול על משימה של הצוות teamId. null = מותר.
    // צוות של המשתמש עצמו: מותר בלי שאילתה (הצוות שלו תמיד בחברה שלו).
    // אחרת טוענים את הצוות: חברה אחרת => NotFound (לא חושפים שקיים), מנהל => מותר, אחר => Forbidden.
    private async Task<ServiceResult<TaskResponseDto>?> CheckTaskTeamAsync(int teamId, CancellationToken ct)
    {
        if (!IsManager && _currentUser.TeamId == teamId)
            return null;

        var team = await _teams.GetByIdAsync(teamId, ct);
        if (team is null || team.OrganizationId != _currentUser.OrganizationId)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        return IsManager ? null : Failure(ResultStatus.Forbidden, OtherTeamMessage);
    }

    private static ServiceResult<TaskResponseDto> Failure(ResultStatus status, string message)
        => ServiceResult<TaskResponseDto>.Fail(status, message);
}