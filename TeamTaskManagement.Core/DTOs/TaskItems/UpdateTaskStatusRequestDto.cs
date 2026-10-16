using System.ComponentModel.DataAnnotations;
using TeamTaskManagement.Core.Enums;

namespace TeamTaskManagement.Core.DTOs.TaskItems;

public class UpdateTaskStatusRequestDto
{
    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus Status { get; set; }
}
