namespace TeamTaskManagement.Core.DTOs.Teams;

public class TeamResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public int MembersCount { get; set; }
}
