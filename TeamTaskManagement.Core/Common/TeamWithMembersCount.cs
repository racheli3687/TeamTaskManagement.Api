using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Common;

// צוות יחד עם מספר החברים בו, מחושב בשאילתה אחת (COUNT).
// כך לא טוענים את כל המשתמשים של כל צוות, ולא נופלים ל-N+1.
public record TeamWithMembersCount(Team Team, int MembersCount);
