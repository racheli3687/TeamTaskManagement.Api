namespace TeamTaskManagement.Core.Common;

// תוצאה של פעולה בשכבת ה-Service. כך ה-Service לא תלוי ב-HTTP,
// וה-Controller יודע איזה סטטוס להחזיר.
public class ServiceResult
{
    public ResultStatus Status { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess => Status == ResultStatus.Success;

    public static ServiceResult Success() => new() { Status = ResultStatus.Success };

    public static ServiceResult Fail(ResultStatus status, string message) =>
        new() { Status = status, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data) =>
        new() { Status = ResultStatus.Success, Data = data };

    public new static ServiceResult<T> Fail(ResultStatus status, string message) =>
        new() { Status = status, Message = message };
}
