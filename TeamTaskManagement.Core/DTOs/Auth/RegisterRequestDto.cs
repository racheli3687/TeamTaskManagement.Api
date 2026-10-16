using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Auth;

// הרשמה של מנהל חברה חדשה
public class RegisterRequestDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string OrganizationName { get; set; } = default!;

    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = default!;
}
