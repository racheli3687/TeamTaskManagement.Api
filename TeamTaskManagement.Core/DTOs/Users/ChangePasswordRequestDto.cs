using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Users;

public class ChangePasswordRequestDto
{
    [Required]
    public string CurrentPassword { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = default!;
}
