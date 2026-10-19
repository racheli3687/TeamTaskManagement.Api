// TeamTaskManagement.Tests/Services/MessageServiceAuthorizationTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.DTOs.Messages;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;
using TeamTaskManagement.Core.Services;
using TeamTaskManagement.Service.Services;
using TeamTaskManagement.Tests.Helpers;

namespace TeamTaskManagement.Tests.Services;

// כללי מי כותב למי (CanWriteTo) והפרדת חברות בהודעות.
public class MessageServiceAuthorizationTests
{
    private const int OrganizationId = 1;
    private const int OtherOrganizationId = 2;
    private const int SenderId = 100;
    private const int RecipientId = 200;
    private const int MessageId = 7;

    private readonly Mock<IMessageRepository> _messages = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private MessageService CreateService(ICurrentUser currentUser) => new(
        _messages.Object,
        _users.Object,
        _unitOfWork.Object,
        currentUser,
        TestMapper.Create(),
        NullLogger<MessageService>.Instance);

    private static ICurrentUser CreateCurrentUser(UserRole role, int? teamId) => new TestCurrentUser
    {
        Id = SenderId,
        Role = role,
        OrganizationId = OrganizationId,
        TeamId = teamId
    };

    private void SetupUser(int id, UserRole role, int? teamId, int organizationId = OrganizationId)
        => _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new User
                 {
                     Id = id,
                     UserName = $"user{id}",
                     Role = role,
                     TeamId = teamId,
                     OrganizationId = organizationId
                 });

    private static SendMessageRequestDto Request(int recipientId) => new() { RecipientId = recipientId, Text = "Hello" };

    private void VerifyNotSaved()
    {
        _messages.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // מנהל: לכל משתמש בחברה. כל אחד: למנהל ולכל מי שבצוות שלו.
    // חבר ללא צוות: למנהל בלבד. ראש צוות אינו כותב לראש צוות אחר.
    [Theory]
    [InlineData(UserRole.Manager, null, UserRole.Member, 10, ResultStatus.Success)]
    [InlineData(UserRole.Manager, null, UserRole.TeamLead, 20, ResultStatus.Success)]
    [InlineData(UserRole.Member, 10, UserRole.Manager, null, ResultStatus.Success)]
    [InlineData(UserRole.Member, null, UserRole.Manager, null, ResultStatus.Success)]
    [InlineData(UserRole.Member, 10, UserRole.Member, 10, ResultStatus.Success)]
    [InlineData(UserRole.Member, 10, UserRole.TeamLead, 10, ResultStatus.Success)]
    [InlineData(UserRole.TeamLead, 10, UserRole.Member, 10, ResultStatus.Success)]
    [InlineData(UserRole.Member, 10, UserRole.Member, 20, ResultStatus.Forbidden)]
    [InlineData(UserRole.Member, 10, UserRole.TeamLead, 20, ResultStatus.Forbidden)]
    [InlineData(UserRole.TeamLead, 10, UserRole.TeamLead, 20, ResultStatus.Forbidden)]
    [InlineData(UserRole.Member, null, UserRole.Member, 10, ResultStatus.Forbidden)]
    public async Task SendAsync_AppliesWhoCanWriteToWhomRules(
        UserRole senderRole, int? senderTeamId,
        UserRole recipientRole, int? recipientTeamId,
        ResultStatus expected)
    {
        SetupUser(SenderId, senderRole, senderTeamId);
        SetupUser(RecipientId, recipientRole, recipientTeamId);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateService(CreateCurrentUser(senderRole, senderTeamId))
            .SendAsync(Request(RecipientId));

        Assert.Equal(expected, result.Status);

        var times = expected == ResultStatus.Success ? Times.Once() : Times.Never();
        _messages.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), times);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);

        if (expected == ResultStatus.Success)
        {
            Assert.Equal($"user{SenderId}", result.Data!.SenderName);
            Assert.Equal($"user{RecipientId}", result.Data.RecipientName);
        }
    }

    [Fact]
    public async Task SendAsync_RecipientOfOtherOrganization_ReturnsNotFound()
    {
        // גם מנהל אינו כותב מחוץ לחברה שלו
        SetupUser(RecipientId, UserRole.Member, 10, OtherOrganizationId);

        var result = await CreateService(CreateCurrentUser(UserRole.Manager, null)).SendAsync(Request(RecipientId));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task SendAsync_RecipientDoesNotExist_ReturnsNotFound()
    {
        var result = await CreateService(CreateCurrentUser(UserRole.Manager, null)).SendAsync(Request(RecipientId));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task SendAsync_ToSelf_ReturnsValidationFailed()
    {
        var result = await CreateService(CreateCurrentUser(UserRole.Member, 10)).SendAsync(Request(SenderId));

        Assert.Equal(ResultStatus.ValidationFailed, result.Status);
        _users.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNotSaved();
    }

    // ===== הודעה של אחרים: NotFound, לא Forbidden =====

    private void SetupMessageBetweenOthers()
        => _messages.Setup(r => r.GetByIdAsync(MessageId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Message { Id = MessageId, SenderId = 300, RecipientId = RecipientId, Text = "Hi" });

    [Fact]
    public async Task MarkAsReadAsync_NotRecipient_ReturnsNotFound()
    {
        SetupMessageBetweenOthers();

        var result = await CreateService(CreateCurrentUser(UserRole.Manager, null)).MarkAsReadAsync(MessageId);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        VerifyNotSaved();
    }

    [Fact]
    public async Task DeleteAsync_NotSenderOrRecipient_ReturnsNotFound()
    {
        SetupMessageBetweenOthers();

        var result = await CreateService(CreateCurrentUser(UserRole.Manager, null)).DeleteAsync(MessageId);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        _messages.Verify(r => r.Remove(It.IsAny<Message>()), Times.Never);
        VerifyNotSaved();
    }
}