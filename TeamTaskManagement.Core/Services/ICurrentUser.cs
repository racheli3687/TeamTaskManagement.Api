using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Services;

// המשתמש המחובר לבקשה הנוכחית. הממשק יושב ב-Core, והמימוש ב-API (קורא את ה-claims של ה-JWT
// דרך IHttpContextAccessor). ככה ה-Services יכולים לבדוק הרשאות בלי להכיר את HTTP.
//
// כלל בסיס לכל ה-Services: כל משאב נבדק מול OrganizationId של המשתמש המחובר.
// משאב של חברה אחרת מחזיר NotFound (ולא Forbidden), כדי לא לחשוף שהוא קיים.
public interface ICurrentUser
{
    int Id { get; }
    UserRole Role { get; }
    int OrganizationId { get; }
    int? TeamId { get; }
}
