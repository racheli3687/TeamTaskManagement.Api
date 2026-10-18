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
    private const string TeamNotFoundMessage = "הצוות לא נמצא.";
    private const string OtherTeamMessage = "אין הרשאה לפעול על משימה של צוות אחר.";
    private const string ConcurrencyMessage = "המשימה עודכנה במקביל על ידי משתמש אחר. יש לרענן ולנסות שוב.";

    private const string TeamLeadOnlyMessage = "הפעולה מותרת לראש צוות בלבד.";
    private readonly ITaskRepository _tasks;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly ITeamRepository _teams;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ITaskRepository tasks,
        ITaskAssignmentRepository assignments,
        ITeamRepository teams,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IMapper mapper,
        ILogger<TaskService> logger)
    {
        _tasks = tasks;
        _assignments = assignments;
        _teams = teams;
        _users = users;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
    }

    private bool IsManager => _currentUser.Role == UserRole.Manager;

    // ===== קריאה =====

    public async Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByTeamAsync(
        int teamId, TaskQueryParameters query, CancellationToken ct = default)
    {
        // כל התפקידים רשאים לצפות. מנהל: כל צוות בחברה שלו. אחרים: רק הצוות שלהם.
        var denied = await CheckTeamAccessAsync(teamId, TeamNotFoundMessage, ct);
        if (denied is not null)
            return Forward<PagedResult<TaskResponseDto>>(denied);

        var paged = await _tasks.GetPagedByTeamAsync(teamId, query, ct);
        return ServiceResult<PagedResult<TaskResponseDto>>.Success(ToPagedDto(paged));
    }

    public async Task<ServiceResult<PagedResult<TaskResponseDto>>> GetByMemberAsync(
    int memberId, TaskQueryParameters query, CancellationToken ct = default)
    {
        // המשתמש עצמו: כל המשימות שלו, כולל משימות Done מצוות קודם (ההיסטוריה שלו)
        if (memberId == _currentUser.Id)
        {
            var own = await _tasks.GetPagedByAssignedUserAsync(memberId, teamId: null, query, ct);
            return ServiceResult<PagedResult<TaskResponseDto>>.Success(ToPagedDto(own));
        }

        // חבר: רק את המשימות של עצמו
        if (_currentUser.Role == UserRole.Member)
            return ServiceResult<PagedResult<TaskResponseDto>>.Fail(
                ResultStatus.Forbidden, "חבר צוות רשאי לצפות רק במשימות שלו.");

        // משתמש אחר: חייב להיות בחברה, ולראש צוות - גם באותו צוות
        var member = await _users.GetByIdAsync(memberId, ct);
        if (member is null || member.OrganizationId != _currentUser.OrganizationId)
            return ServiceResult<PagedResult<TaskResponseDto>>.Fail(
                ResultStatus.NotFound, "המשתמש לא נמצא.");

        if (!IsManager && member.TeamId != _currentUser.TeamId)
            return ServiceResult<PagedResult<TaskResponseDto>>.Fail(
                ResultStatus.Forbidden, "אין הרשאה לצפות במשימות של חבר מצוות אחר.");

        // אחרים רואים רק את הצוות הנוכחי של החבר. ללא צוות: אין משימות נוכחיות.
        if (member.TeamId is not int teamId)
            return ServiceResult<PagedResult<TaskResponseDto>>.Success(new PagedResult<TaskResponseDto>
            {
                Page = query.Page,
                PageSize = query.PageSize
            });

        var paged = await _tasks.GetPagedByAssignedUserAsync(memberId, teamId, query, ct);
        return ServiceResult<PagedResult<TaskResponseDto>>.Success(ToPagedDto(paged));
    }

    public async Task<ServiceResult<TaskResponseDto>> GetByIdAsync(int taskId, CancellationToken ct = default)
    {
        var task = await _tasks.GetByIdAsync(taskId, ct); // קריאה בלבד
        if (task is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        // מי שמשויך למשימה רשאי לצפות בה גם אם עבר לצוות אחר (עקבי עם GetByMemberAsync).
        // לצפייה בלבד: תפיסה, שחרור ועדכון סטטוס עדיין עוברים את בדיקת הצוות.
        bool isAssignee = task.Assignments.Any(a => a.UnassignedAt == null && a.UserId == _currentUser.Id);

        if (!isAssignee)
        {
            var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
            if (denied is not null)
                return Forward<TaskResponseDto>(denied);
        }

        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));
    }

    // ===== צעדים 2 ו-3 של משימה 3.7 =====

    public async Task<ServiceResult<TaskResponseDto>> CreateAsync(
    int teamId, CreateTaskRequestDto request, CancellationToken ct = default)
    {
        // 1. תפקיד
        if (_currentUser.Role != UserRole.TeamLead)
            return Failure(ResultStatus.Forbidden, TeamLeadOnlyMessage);

        // 2-3. חברה אחרת => NotFound, צוות אחר => Forbidden
        var denied = await CheckTeamAccessAsync(teamId, TeamNotFoundMessage, ct);
        if (denied is not null)
            return Forward<TaskResponseDto>(denied);

        // 4. כלל עסקי: ביצירה, תאריך יעד לא לפני היום
        var dueDate = NormalizeToUtc(request.DueDate);
        if (dueDate < DateTime.UtcNow.Date)
            return Failure(ResultStatus.ValidationFailed, "תאריך היעד אינו יכול להיות בעבר.");

        // נבנית במפורש (לפי כלל המיפוי). TeamId מהנתיב שנבדק, Status תמיד Open.
        var task = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DueDate = dueDate,
            TeamId = teamId,
            Status = TaskItemStatus.Open
        };

        await _tasks.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // משימה חדשה - אין שיוך, ולכן AssignedUserId ו-AssignedUserName יוצאים null
        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));
    }

    public async Task<ServiceResult<TaskResponseDto>> UpdateAsync(
        int taskId, UpdateTaskRequestDto request, CancellationToken ct = default)
    {
        if (_currentUser.Role != UserRole.TeamLead)
            return Failure(ResultStatus.Forbidden, TeamLeadOnlyMessage);

        // במעקב, כולל השיוך הפעיל והמשתמש שלו (לתשובה)
        var task = await _tasks.GetForUpdateAsync(taskId, ct);
        if (task is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
        if (denied is not null)
            return Forward<TaskResponseDto>(denied);

        // תוכן בלבד, הסטטוס לא משתנה. בעריכה תאריך בעבר מותר.
        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.DueDate = NormalizeToUtc(request.DueDate);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Update conflict: task {TaskId} was modified concurrently. User {UserId}.",
                taskId, _currentUser.Id);
            return Failure(ResultStatus.Conflict, ConcurrencyMessage);
        }

        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));
    }

    public async Task<ServiceResult> DeleteAsync(int taskId, CancellationToken ct = default)
    {
        if (_currentUser.Role != UserRole.TeamLead)
            return ServiceResult.Fail(ResultStatus.Forbidden, TeamLeadOnlyMessage);

        // במעקב: ה-Version נכנס לתנאי ה-WHERE של ה-DELETE
        var task = await _tasks.GetForUpdateAsync(taskId, ct);
        if (task is null)
            return ServiceResult.Fail(ResultStatus.NotFound, TaskNotFoundMessage);

        var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
        if (denied is not null)
            return denied;

        // השיוכים (פעיל והיסטוריה) נמחקים ב-Cascade
        _tasks.Remove(task);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // למשל: חבר תפס את המשימה בין הקריאה למחיקה
            _logger.LogWarning(
                "Delete conflict: task {TaskId} was modified concurrently. User {UserId}.",
                taskId, _currentUser.Id);
            return ServiceResult.Fail(ResultStatus.Conflict, ConcurrencyMessage);
        }

        return ServiceResult.Success();
    }



    public async Task<ServiceResult<TaskResponseDto>> UpdateStatusAsync(
    int taskId, UpdateTaskStatusRequestDto request, CancellationToken ct = default)
    {
        // 1. תפקיד: ראש צוות או חבר צוות, עם צוות
        if (_currentUser.Role is not (UserRole.TeamLead or UserRole.Member) || _currentUser.TeamId is null)
            return Failure(ResultStatus.Forbidden, "רק ראש הצוות או החבר המחזיק במשימה רשאים לעדכן את הסטטוס שלה.");

        // 2. המשימה במעקב, כולל השיוך הפעיל
        var task = await _tasks.GetForUpdateAsync(taskId, ct);
        if (task is null)
            return Failure(ResultStatus.NotFound, TaskNotFoundMessage);

        // 3. חברה אחרת => NotFound, צוות אחר => Forbidden
        var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
        if (denied is not null)
            return Forward<TaskResponseDto>(denied);

        // 4. ללא שיוך פעיל המשימה תמיד Open (הכלל הקבוע), ואין לה מחזיק שיעבוד עליה
        var assignment = task.Assignments.FirstOrDefault(a => a.UnassignedAt == null);
        if (assignment is null)
            return Failure(ResultStatus.Conflict, "המשימה אינה משויכת. יש לתפוס את המשימה תחילה.");

        // 5. חבר רשאי לעדכן רק משימה שמשויכת אליו
        bool isTeamLead = _currentUser.Role == UserRole.TeamLead;
        if (!isTeamLead && assignment.UserId != _currentUser.Id)
            return Failure(ResultStatus.Forbidden, "ניתן לעדכן רק משימה שמשויכת אליך.");

        var target = request.Status;

        // 6. אותו סטטוס: אין מה לשנות ואין שמירה
        if (target == task.Status)
            return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));

        // 7. חזרה ל-Open רק דרך שחרור, כדי שהשיוך ייסגר יחד עם הסטטוס
        if (target == TaskItemStatus.Open)
            return Failure(ResultStatus.ValidationFailed, "להחזרת המשימה לסטטוס פתוחה יש לשחרר אותה.");

        // 8. המעברים האפשריים מכאן: InProgress => Done, ו-Done => InProgress
        if (target == TaskItemStatus.Done)
        {
            task.Status = TaskItemStatus.Done;
            task.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            // Done => InProgress: פתיחה מחדש, ראש הצוות בלבד. השיוך נשאר אצל אותו חבר.
            if (!isTeamLead)
                return Failure(ResultStatus.Forbidden, "רק ראש הצוות רשאי לפתוח מחדש משימה שהושלמה.");

            task.Status = TaskItemStatus.InProgress;
            task.CompletedAt = null;
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Status update conflict: task {TaskId} was modified concurrently. User {UserId}.",
                taskId, _currentUser.Id);
            return Failure(ResultStatus.Conflict, ConcurrencyMessage);
        }

        return ServiceResult<TaskResponseDto>.Success(_mapper.Map<TaskResponseDto>(task));
    }

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
        var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
        if (denied is not null)
            return Forward<TaskResponseDto>(denied);

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
        var denied = await CheckTeamAccessAsync(task.TeamId, TaskNotFoundMessage, ct);
        if (denied is not null)
            return Forward<TaskResponseDto>(denied);

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

    // בודק שהמשתמש רשאי לפעול על הצוות teamId. מחזיר null אם מותר, אחרת את תוצאת הכישלון.
    // הצוות של המשתמש עצמו: מותר בלי שאילתה (הצוות שלו תמיד בחברה שלו).
    // אחרת טוענים את הצוות: חברה אחרת => NotFound (לא חושפים שקיים), מנהל => מותר, אחר => Forbidden.
    private async Task<ServiceResult?> CheckTeamAccessAsync(int teamId, string notFoundMessage, CancellationToken ct)
    {
        if (!IsManager && _currentUser.TeamId == teamId)
            return null;

        var team = await _teams.GetByIdAsync(teamId, ct);
        if (team is null || team.OrganizationId != _currentUser.OrganizationId)
            return ServiceResult.Fail(ResultStatus.NotFound, notFoundMessage);

        return IsManager ? null : ServiceResult.Fail(ResultStatus.Forbidden, OtherTeamMessage);
    }

    // מעביר כישלון מ-ServiceResult רגיל לטיפוס התוצאה של המתודה
    private static ServiceResult<T> Forward<T>(ServiceResult error)
        => ServiceResult<T>.Fail(error.Status, error.Message!);

    private static ServiceResult<TaskResponseDto> Failure(ResultStatus status, string message)
        => ServiceResult<TaskResponseDto>.Fail(status, message);

    // pagination נשמר כפי שחזר מה-Repository; רק הפריטים ממופים ל-DTO
    private PagedResult<TaskResponseDto> ToPagedDto(PagedResult<TaskItem> paged) => new()
    {
        Items = _mapper.Map<List<TaskResponseDto>>(paged.Items),
        TotalCount = paged.TotalCount,
        Page = paged.Page,
        PageSize = paged.PageSize
    };

    // Npgsql דוחה DateTime שאינו Utc בעמודת timestamptz. הלקוח עשוי לשלוח כל אחד משלושת הסוגים.
    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (value is not DateTime date)
            return null;

        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),            // נשלח עם היסט, למשל +03:00
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)        // נשלח בלי אזור זמן: מתפרש כ-UTC
        };
    }
}