namespace TeamTaskManagement.Core.Entities;

// חברה (tenant). כל הנתונים במערכת שייכים לחברה אחת בלבד, ואף חברה לא רואה נתונים של חברה אחרת.
// נוצרת בהרשמה של מנהל החברה.
public class Organization
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    // one-to-many: חברה אחת - הרבה צוותים
    public ICollection<Team> Teams { get; set; } = new List<Team>();

    // one-to-many: חברה אחת - הרבה משתמשים (כולל המנהל)
    public ICollection<User> Users { get; set; } = new List<User>();
}
