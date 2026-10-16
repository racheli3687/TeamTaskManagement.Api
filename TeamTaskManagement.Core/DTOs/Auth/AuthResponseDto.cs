using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = default!;
    public UserRole Role { get; set; }
    public int OrganizationId { get; set; }
    public int? TeamId { get; set; }
}
