// TeamTaskManagement.Tests/Concurrency/TaskConcurrencyTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Data;
using TeamTaskManagement.Data.Repositories;
using TeamTaskManagement.Service.Services;
using TeamTaskManagement.Tests.Helpers;

namespace TeamTaskManagement.Tests.Concurrency;

// תחרות אמיתית מול בסיס נתונים: Optimistic Concurrency עם TaskItem.Version.
public class TaskConcurrencyTests : IDisposable
{
    private readonly SqliteTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    // TaskService עם Repositories ו-UnitOfWork אמיתיים על Context נתון
    private static TaskService CreateService(AppDbContext context, ICurrentUser currentUser) => new(
        new TaskRepository(context),
        new TaskAssignmentRepository(context),
        new TeamRepository(context),
        new UserRepository(context),
        new UnitOfWork(context),
        currentUser,
        TestMapper.Create(),
        NullLogger<TaskService>.Instance);

    private static ICurrentUser User(SeedData seed, int id, UserRole role) => new TestCurrentUser
    {
        Id = id,
        Role = role,
        OrganizationId = seed.OrganizationId,
        TeamId = seed.TeamId
    };

    [Fact]
    public async Task TwoContexts_ModifySameTask_FirstSavesSecondGetsConcurrencyConflict()
    {
        // Arrange: שני Contexts נפרדים קוראים את אותה משימה, עם אותו Version
        var seed = await _db.SeedAsync();

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();

        var taskInFirst = await first.TaskItems.SingleAsync(t => t.Id == seed.OpenTaskId);
        var taskInSecond = await second.TaskItems.SingleAsync(t => t.Id == seed.OpenTaskId);
        var originalVersion = taskInFirst.Version;
        Assert.Equal(originalVersion, taskInSecond.Version);

        // Act: שניהם משנים. הראשון שומר בהצלחה
        taskInFirst.Status = TaskItemStatus.InProgress;
        taskInSecond.Status = TaskItemStatus.Done;

        await new UnitOfWork(first).SaveChangesAsync();

        // Assert: השני נכשל, כי ה-Version שלו כבר לא תואם את השורה במסד
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => new UnitOfWork(second).SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(ex.InnerException);

        // במסד נשאר השינוי של הראשון, עם Version חדש
        await using var verify = _db.CreateContext();
        var saved = await verify.TaskItems.AsNoTracking().SingleAsync(t => t.Id == seed.OpenTaskId);
        Assert.Equal(TaskItemStatus.InProgress, saved.Status);
        Assert.NotEqual(originalVersion, saved.Version);
    }

    [Fact]
    public async Task TaskService_StatusUpdateAfterConcurrentRelease_ReturnsConflictAndKeepsFirstChange()
    {
        // Arrange
        var seed = await _db.SeedAsync();

        await using var leadContext = _db.CreateContext();
        await using var memberContext = _db.CreateContext();

        // ראש הצוות קורא את המשימה (בביצוע, משויכת לחבר 1) - לפני השחרור
        await new TaskRepository(leadContext).GetForUpdateAsync(seed.InProgressTaskId);

        // בינתיים החבר משחרר אותה ושומר
        var release = await CreateService(memberContext, User(seed, seed.Member1Id, UserRole.Member))
            .ReleaseAsync(seed.InProgressTaskId);
        Assert.Equal(ResultStatus.Success, release.Status);

        // Act: ראש הצוות מסמן Done על סמך הנתונים שקרא קודם.
        // ה-Context שלו מחזיק את הישות שכבר במעקב, עם ה-Version הישן.
        var update = await CreateService(leadContext, User(seed, seed.TeamLeadId, UserRole.TeamLead))
            .UpdateStatusAsync(seed.InProgressTaskId, new UpdateTaskStatusRequestDto { Status = TaskItemStatus.Done });

        // Assert: 409 ולא חריגה, והמסד במצב של השחרור
        Assert.Equal(ResultStatus.Conflict, update.Status);

        await using var verify = _db.CreateContext();
        var saved = await verify.TaskItems.AsNoTracking()
            .Include(t => t.Assignments)
            .SingleAsync(t => t.Id == seed.InProgressTaskId);
        Assert.Equal(TaskItemStatus.Open, saved.Status);
        Assert.Null(saved.CompletedAt);
        Assert.DoesNotContain(saved.Assignments, a => a.UnassignedAt == null);
    }

    [Fact]
    public async Task UniqueFilteredIndex_SecondActiveAssignmentForSameTask_IsRejected()
    {
        // Arrange: למשימה כבר יש שיוך פעיל (חבר 1)
        var seed = await _db.SeedAsync();
        await using var context = _db.CreateContext();

        context.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = seed.InProgressTaskId,
            UserId = seed.Member2Id,
            AssignedAt = DateTime.UtcNow
        });

        // Act + Assert: רשת הביטחון במסד חוסמת שיוך פעיל שני, גם בלי בדיקת Version
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}