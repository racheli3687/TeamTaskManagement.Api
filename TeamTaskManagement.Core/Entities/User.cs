using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Entities;

public class User
{
    public int Id { get; set; }

    // שם הכניסה למערכת - ייחודי בכל המערכת (Unique Index ב-Fluent API),
    // כדי שההתחברות לא תדרוש לבחור חברה
    public string UserName { get; set; } = default!;

    // לעולם לא שומרים סיסמה גלויה, רק hash
    public string PasswordHash { get; set; } = default!;

    public UserRole Role { get; set; }

    // כל משתמש שייך לחברה, כולל המנהל
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;

    // Nullable: למנהל החברה אין צוות מסוים (הוא מעל כל הצוותים)
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    // many-to-many (דרך ישות הקישור TaskAssignment)
    public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();

    public ICollection<Message> SentMessages { get; set; } = new List<Message>();
    public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
}
