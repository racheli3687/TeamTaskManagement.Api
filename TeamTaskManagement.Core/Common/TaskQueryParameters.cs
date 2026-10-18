using System.ComponentModel.DataAnnotations;
using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Common;

public class TaskQueryParameters : PaginationParameters
{
    // EnumDataType: ערך שלא מוגדר ב-enum (למשל ?status=99) מחזיר 400. null נחשב תקין.
    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus? Status { get; set; }

    public string? Search { get; set; }

    [EnumDataType(typeof(TaskSortBy))]
    public TaskSortBy SortBy { get; set; } = TaskSortBy.DueDate;

    public bool Descending { get; set; }
}