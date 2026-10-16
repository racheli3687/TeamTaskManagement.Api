using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Teams;

public class UpdateTeamRequestDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = default!;
}
