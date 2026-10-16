using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Users;

public class AddMemberRequestDto
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = default!;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = default!;
}
