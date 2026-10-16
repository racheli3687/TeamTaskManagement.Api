namespace TeamTaskManagement.Core.Common;

// ה-Controller ממיר כל סטטוס לקוד HTTP:
// Success=200/201/204, ValidationFailed=400, Unauthorized=401, Forbidden=403, NotFound=404, Conflict=409
public enum ResultStatus
{
    Success = 1,
    ValidationFailed = 2,
    Unauthorized = 3,
    Forbidden = 4,
    NotFound = 5,
    Conflict = 6
}
