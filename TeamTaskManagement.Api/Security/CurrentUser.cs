// TeamTaskManagement.Api/Security/CurrentUser.cs
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Api.Security;

// המשתמש המחובר לבקשה הנוכחית. Scoped: מופע אחד לכל בקשה.
// מתמלא פעם אחת ב-CurrentUserMiddleware: הזהות (sub) מה-token, ו-Role, OrganizationId ו-TeamId מהמסד,
// כדי שהסרה מצוות, מינוי או הורדה בתפקיד ייכנסו לתוקף מיד ולא רק כשה-token פג.
public class CurrentUser : ICurrentUser
{
    private const string NotLoadedMessage =
        "No authenticated user for this request. Is [Authorize] missing on the endpoint?";

    private int _id;
    private UserRole _role;
    private int _organizationId;
    private int? _teamId;

    public bool IsLoaded { get; private set; }

    public int Id => IsLoaded ? _id : throw new InvalidOperationException(NotLoadedMessage);
    public UserRole Role => IsLoaded ? _role : throw new InvalidOperationException(NotLoadedMessage);
    public int OrganizationId => IsLoaded ? _organizationId : throw new InvalidOperationException(NotLoadedMessage);
    public int? TeamId => IsLoaded ? _teamId : throw new InvalidOperationException(NotLoadedMessage);

    // הערכים מועתקים: שינוי בישות במהלך הבקשה לא משנה את ההרשאות של אותה בקשה
    public void Set(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        _id = user.Id;
        _role = user.Role;
        _organizationId = user.OrganizationId;
        _teamId = user.TeamId;
        IsLoaded = true;
    }
}