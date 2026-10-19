// TeamTaskManagement.Tests/Helpers/TestCurrentUser.cs
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Tests.Helpers;

// המשתמש המחובר בתרחיש בדיקה. מחליף את CurrentUser של ה-Api, שתלוי ב-HTTP.
public sealed class TestCurrentUser : ICurrentUser
{
    public int Id { get; init; }
    public UserRole Role { get; init; }
    public int OrganizationId { get; init; }
    public int? TeamId { get; init; }
}