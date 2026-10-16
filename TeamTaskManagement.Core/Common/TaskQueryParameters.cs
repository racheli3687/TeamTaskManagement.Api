using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.Common;

public class TaskQueryParameters : PaginationParameters
{
    public TaskItemStatus? Status { get; set; }
    public string? Search { get; set; }
    public TaskSortBy SortBy { get; set; } = TaskSortBy.DueDate;
    public bool Descending { get; set; }
}
