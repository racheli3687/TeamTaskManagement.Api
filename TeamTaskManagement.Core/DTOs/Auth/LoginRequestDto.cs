using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Auth;

public class LoginRequestDto
{
    [Required]
    public string UserName { get; set; } = default!;

    [Required]
    public string Password { get; set; } = default!;
}
