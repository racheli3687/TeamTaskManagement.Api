namespace TeamTaskManagement.Core.Entities;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;

    // one-to-many: צוות אחד - הרבה משתמשים
    public ICollection<User> Users { get; set; } = new List<User>();

    // one-to-many: צוות אחד - הרבה משימות
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
