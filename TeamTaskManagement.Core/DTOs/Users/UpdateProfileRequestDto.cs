using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Users;

public class UpdateProfileRequestDto
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; set; } = default!;
}
