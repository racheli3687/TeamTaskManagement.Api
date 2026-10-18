// TeamTaskManagement.Api/Extensions/ServiceResultExtensions.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TeamTaskManagement.Core.Common;

namespace TeamTaskManagement.Api.Extensions;

// ממיר ServiceResult לתשובת HTTP. ה-Service לא מכיר HTTP, וה-Controller נשאר דק:
// return result.ToActionResult(this);
// שגיאות מוחזרות כ-ProblemDetails, וההודעה מה-Service ב-title (עקבי עם CurrentUserMiddleware).
public static class ServiceResultExtensions
{
    // פעולה ללא ערך מוחזר (מחיקה, שינוי סיסמה וכו'): הצלחה = 204
    public static IActionResult ToActionResult(this ServiceResult result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? controller.NoContent()
            : ToProblem(result, controller);
    }

    // פעולה עם ערך מוחזר: הצלחה = 200 עם הנתונים
    public static ActionResult<T> ToActionResult<T>(this ServiceResult<T> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? controller.Ok(result.Data)
            : ToProblem(result, controller);
    }

    // פעולת יצירה: הצלחה = 201, Location נבנה מה-routing לפי ה-Action של ה-GET
    // דוגמה: result.ToCreatedResult(this, nameof(GetById), dto => new { id = dto.Id })
    public static ActionResult<T> ToCreatedResult<T>(
        this ServiceResult<T> result,
        ControllerBase controller,
        string actionName,
        Func<T, object> routeValues)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        ArgumentNullException.ThrowIfNull(routeValues);

        if (!result.IsSuccess)
            return ToProblem(result, controller);

        // יצירה מוצלחת תמיד מחזירה את המשאב שנוצר; אחרת זו שגיאת תכנות
        if (result.Data is null)
            throw new InvalidOperationException("A successful create result must contain data.");

        return controller.CreatedAtAction(actionName, routeValues(result.Data), result.Data);
    }

    // נקראת רק בכישלון. Problem() מוסיף traceId ו-type באופן אוטומטי.
    private static ObjectResult ToProblem(ServiceResult result, ControllerBase controller)
    {
        var statusCode = result.Status switch
        {
            ResultStatus.ValidationFailed => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            // סטטוס שלא ממופה (או Success שהגיע לכאן) הוא שגיאת תכנות
            _ => throw new InvalidOperationException($"Unsupported result status '{result.Status}'.")
        };

        return controller.Problem(statusCode: statusCode, title: result.Message);
    }
}