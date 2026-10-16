using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.DTOs.Users;

// בלי PasswordHash - לעולם לא יוצא מה-API
public class UserResponseDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = default!;
    public UserRole Role { get; set; }
    public int OrganizationId { get; set; }
    public int? TeamId { get; set; }
}
