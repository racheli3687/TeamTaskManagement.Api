// TeamTaskManagement.Tests/Services/TaskServiceClaimTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Exceptions;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Service.Services;
using TeamTaskManagement.Tests.Helpers;

namespace TeamTaskManagement.Tests.Services;

// הלוגיקה של המשאב המוגבל: תפיסת משימה על ידי חבר צוות.
public class TaskServiceClaimTests
{
    private const int OrganizationId = 1;
    private const int TeamId = 10;
    private const int TaskId = 5;
    private const int MemberId = 100;
    private const int OtherMemberId = 200;

    private readonly Mock<ITaskRepository> _tasks = new();
    private readonly Mock<ITaskAssignmentRepository> _assignments = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static readonly ICurrentUser Member = new TestCurrentUser
    {
        Id = MemberId,
        Role = UserRole.Member,
        OrganizationId = OrganizationId,
        TeamId = TeamId
    };

    private TaskService CreateService(ICurrentUser currentUser) => new(
        _tasks.Object,
        _assignments.Object,
        _teams.Object,
        _users.Object,
        _unitOfWork.Object,
        currentUser,
        TestMapper.Create(),
        NullLogger<TaskService>.Instance);

    private static TaskItem CreateTask(TaskItemStatus status, int? assignedUserId = null)
    {
        var task = new TaskItem
        {
            Id = TaskId,
            Title = "Task",
            TeamId = TeamId,
            Status = status
        };

        if (assignedUserId is int userId)
        {
            task.Assignments.Add(new TaskAssignment
            {
                TaskItemId = TaskId,
                UserId = userId,
                User = new User { Id = userId, UserName = $"user{userId}" }
            });
        }

        return task;
    }

    [Fact]
    public async Task ClaimAsync_OpenTask_AssignsMemberAndReturnsInProgress()
    {
        // Arrange: משימה פתוחה ללא שיוך, בצוות של החבר
        var tracked = CreateTask(TaskItemStatus.Open);
        _tasks.Setup(r => r.GetForUpdateAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(tracked);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(2);

        // הטעינה מחדש אחרי השמירה מחזירה את המשימה עם השיוך החדש
        _tasks.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateTask(TaskItemStatus.InProgress, MemberId));

        // Act
        var result = await CreateService(Member).ClaimAsync(TaskId);

        // Assert
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(TaskItemStatus.InProgress, result.Data!.Status);
        Assert.Equal(MemberId, result.Data.AssignedUserId);
        Assert.Equal($"user{MemberId}", result.Data.AssignedUserName);

        Assert.Equal(TaskItemStatus.InProgress, tracked.Status);
        _assignments.Verify(r => r.AddAsync(
            It.Is<TaskAssignment>(a => a.TaskItemId == TaskId && a.UserId == MemberId && a.UnassignedAt == null),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public async Task ClaimAsync_TaskAlreadyAssigned_ReturnsConflictWithoutSaving(TaskItemStatus status)
    {
        // Arrange: המשימה כבר מוחזקת על ידי חבר אחר
        _tasks.Setup(r => r.GetForUpdateAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateTask(status, OtherMemberId));

        // Act
        var result = await CreateService(Member).ClaimAsync(TaskId);

        // Assert
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Null(result.Data);
        _assignments.Verify(r => r.AddAsync(It.IsAny<TaskAssignment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimAsync_ConcurrencyConflictOnSave_ReturnsConflict()
    {
        // Arrange: הבדיקה העסקית עוברת, אבל חבר אחר הקדים בין הקריאה לשמירה
        _tasks.Setup(r => r.GetForUpdateAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateTask(TaskItemStatus.Open));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new ConcurrencyConflictException("Concurrent update."));

        // Act
        var result = await CreateService(Member).ClaimAsync(TaskId);

        // Assert: Conflict ולא חריגה, ואין טעינה מחדש אחרי כישלון
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Null(result.Data);
        _tasks.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}