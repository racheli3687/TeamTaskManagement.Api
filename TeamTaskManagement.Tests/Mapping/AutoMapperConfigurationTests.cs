// TeamTaskManagement.Tests/Mapping/AutoMapperConfigurationTests.cs
using AutoMapper;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.DTOs.TaskItems;
using TeamTaskManagement.Core.DTOs.Teams;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Tests.Helpers;

namespace TeamTaskManagement.Tests.Mapping;

public class AutoMapperConfigurationTests
{
    private readonly IMapper _mapper = TestMapper.Create();

    // כל שדה יעד בכל ה-Profiles ממופה או מסומן Ignore
    [Fact]
    public void Configuration_IsValid()
        => TestMapper.Configuration.AssertConfigurationIsValid();

    [Fact]
    public void TaskItem_MapsOnlyActiveAssignment()
    {
        var task = new TaskItem { Id = 1, Title = "Task", TeamId = 10, Status = TaskItemStatus.InProgress };
        task.Assignments.Add(new TaskAssignment
        {
            UserId = 100,
            User = new User { Id = 100, UserName = "previous" },
            UnassignedAt = DateTime.UtcNow.AddDays(-1)
        });
        task.Assignments.Add(new TaskAssignment
        {
            UserId = 200,
            User = new User { Id = 200, UserName = "current" }
        });

        var dto = _mapper.Map<TaskResponseDto>(task);

        Assert.Equal(200, dto.AssignedUserId);
        Assert.Equal("current", dto.AssignedUserName);
        Assert.Equal(TaskItemStatus.InProgress, dto.Status);
    }

    [Fact]
    public void TaskItem_WithoutActiveAssignment_MapsNullAssignee()
    {
        var task = new TaskItem { Id = 1, Title = "Task", TeamId = 10, Status = TaskItemStatus.Open };
        task.Assignments.Add(new TaskAssignment
        {
            UserId = 100,
            User = new User { Id = 100, UserName = "previous" },
            UnassignedAt = DateTime.UtcNow
        });

        var dto = _mapper.Map<TaskResponseDto>(task);

        Assert.Null(dto.AssignedUserId);
        Assert.Null(dto.AssignedUserName);
    }

    [Fact]
    public void Message_MapsSenderAndRecipientNames()
    {
        var message = new Message
        {
            Id = 7,
            SenderId = 1,
            Sender = new User { Id = 1, UserName = "sender" },
            RecipientId = 2,
            Recipient = new User { Id = 2, UserName = "recipient" },
            Text = "Hello",
            Status = MessageStatus.Unread
        };

        var dto = _mapper.Map<MessageResponseDto>(message);

        Assert.Equal("sender", dto.SenderName);
        Assert.Equal("recipient", dto.RecipientName);
        Assert.Equal("Hello", dto.Text);
    }

    [Fact]
    public void TeamWithMembersCount_MapsTeamFieldsAndCount()
    {
        var source = new TeamWithMembersCount(new Team { Id = 3, Name = "Alpha", OrganizationId = 1 }, 4);

        var dto = _mapper.Map<TeamResponseDto>(source);

        Assert.Equal(3, dto.Id);
        Assert.Equal("Alpha", dto.Name);
        Assert.Equal(4, dto.MembersCount);
    }
}