using System.ComponentModel.DataAnnotations;

namespace TeamTaskManagement.Core.DTOs.Teams;

// גוף הבקשה של PUT api/teams/{teamId}/lead. לצוות יש ראש צוות אחד, ולכן lead הוא משאב יחיד.
public class AssignTeamLeadRequestDto
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
}