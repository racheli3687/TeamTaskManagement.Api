// TeamTaskManagement.Tests/Services/TaskServiceAuthorizationTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Service.Services;
using TeamTaskManagement.Tests.Helpers;

namespace TeamTaskManagement.Tests.Services;

// הרשאות לפי תפקיד, הפרדה בין צוותים והפרדה בין חברות.
// חברה אחרת => NotFound (לא חושפים שקיים), צוות אחר באותה חברה => Forbidden.
public class TaskServiceAuthorizationTests
{
    private const int OrganizationId = 1;
    private const int OtherOrganizationId = 2;
    private const int TeamId = 10;
    private const int OtherTeamId = 20;
    private const int TaskId = 5;
    private const int ManagerId = 1;
    private const int TeamLeadId = 50;
    private const int MemberId = 100;
    private const int OtherMemberId = 200;

    private readonly Mock<ITaskRepository> _tasks = new();
    private readonly Mock<ITaskAssignmentRepository> _assignments = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static ICurrentUser CreateUser(int id, UserRole role, int? teamId) => new TestCurrentUser
    {
        Id = id,
        Role = role,
        OrganizationId = OrganizationId,
        TeamId = teamId
    };

    private static readonly ICurrentUser Manager = CreateUser(ManagerId, UserRole.Manager, null);
    private static readonly ICurrentUser TeamLead = CreateUser(TeamLeadId, UserRole.TeamLead, TeamId);
    private static readonly ICurrentUser Member = CreateUser(MemberId, UserRole.Member, TeamId);

    private TaskService CreateService(ICurrentUser currentUser) => new(
        _tasks.Object,
        _assignments.Object,
        _teams.Object,
        _users.Object,
        _unitOfWork.Object,
        currentUser,
        TestMapper.Create(),
        NullLogger<TaskService>.Instance);

    private static TaskItem CreateTask(int teamId, TaskItemStatus status, int? assignedUserId = null)
    {
        var task = new TaskItem { Id = TaskId, Title = "Task", TeamId = teamId, Status = status };

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

    private void SetupTeam(int teamId, int organizationId)
        => _teams.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Team { Id = teamId, Name = $"team{teamId}", OrganizationId = organizationId });

    private void SetupTaskForUpdate(TaskItem task)
        => _tasks.Setup(r => r.GetForUpdateAsync(TaskId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(task);

    private void VerifyNotSaved()
        => _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

    // ===== תפיסה =====

    [Theory]
    [InlineData(UserRole.TeamLead, TeamId)]
    [InlineData(UserRole.Manager, null)]
    [InlineData(UserRole.Member, null)]   // חבר ללא צוות
    public async Task ClaimAsync_NotMemberWithTeam_ReturnsForbiddenWithoutLoadingTask(UserRole role, int? teamId)
    {
        var result = await CreateService(CreateUser(MemberId, role, teamId)).ClaimAsync(TaskId);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        _tasks.Verify(r => r.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNotSaved();
    }

    [Fact]
    public async Task ClaimAsync_TaskOfOtherTeamInSameOrganization_ReturnsForbidden()
    {
        SetupTaskForUpdate(CreateTask(OtherTeamId, TaskItemStatus.Open));
        SetupTeam(OtherTeamId, OrganizationId);

        var result = await CreateService(Member).ClaimAsync(TaskId);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task ClaimAsync_TaskOfOtherOrganization_ReturnsNotFound()
    {
        SetupTaskForUpdate(CreateTask(OtherTeamId, TaskItemStatus.Open));
        SetupTeam(OtherTeamId, OtherOrganizationId);

        var result = await CreateService(Member).ClaimAsync(TaskId);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        VerifyNotSaved();
    }

    // ===== צפייה: מנהל רואה כל צוות בחברה שלו, ורק בה =====

    [Fact]
    public async Task GetByIdAsync_ManagerTaskOfTeamInOwnOrganization_ReturnsSuccess()
    {
        _tasks.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateTask(OtherTeamId, TaskItemStatus.Open));
        SetupTeam(OtherTeamId, OrganizationId);

        var result = await CreateService(Manager).GetByIdAsync(TaskId);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal(TaskId, result.Data!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ManagerTaskOfOtherOrganization_ReturnsNotFound()
    {
        _tasks.Setup(r => r.GetByIdAsync(TaskId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateTask(OtherTeamId, TaskItemStatus.Open));
        SetupTeam(OtherTeamId, OtherOrganizationId);

        var result = await CreateService(Manager).GetByIdAsync(TaskId);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(result.Data);
    }

    // ===== יצירה: ראש צוות בלבד =====

    [Theory]
    [InlineData(UserRole.Member)]
    [InlineData(UserRole.Manager)]
    public async Task CreateAsync_NotTeamLead_ReturnsForbidden(UserRole role)
    {
        var request = new CreateTaskRequestDto { Title = "New task" };

        var result = await CreateService(CreateUser(MemberId, role, TeamId)).CreateAsync(TeamId, request);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        _tasks.Verify(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNotSaved();
    }

    // ===== שחרור ועדכון סטטוס =====

    [Fact]
    public async Task ReleaseAsync_MemberTaskHeldByOtherMember_ReturnsForbidden()
    {
        SetupTaskForUpdate(CreateTask(TeamId, TaskItemStatus.InProgress, OtherMemberId));

        var result = await CreateService(Member).ReleaseAsync(TaskId);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task UpdateStatusAsync_MemberReopensDoneTask_ReturnsForbidden()
    {
        SetupTaskForUpdate(CreateTask(TeamId, TaskItemStatus.Done, MemberId));
        var request = new UpdateTaskStatusRequestDto { Status = TaskItemStatus.InProgress };

        var result = await CreateService(Member).UpdateStatusAsync(TaskId, request);

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task UpdateStatusAsync_TeamLeadReopensDoneTask_ReturnsInProgress()
    {
        var tracked = CreateTask(TeamId, TaskItemStatus.Done, MemberId);
        tracked.CompletedAt = DateTime.UtcNow;
        SetupTaskForUpdate(tracked);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var request = new UpdateTaskStatusRequestDto { Status = TaskItemStatus.InProgress };

        var result = await CreateService(TeamLead).UpdateStatusAsync(TaskId, request);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal(TaskItemStatus.InProgress, result.Data!.Status);
        Assert.Equal(MemberId, result.Data.AssignedUserId);   // השיוך נשאר אצל אותו חבר
        Assert.Null(tracked.CompletedAt);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}