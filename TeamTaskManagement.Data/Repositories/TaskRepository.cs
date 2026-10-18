using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    // טוען את השיוך הפעיל ואת המשתמש שלו, בשאילתה אחת (Include מסונן + ThenInclude)
    private IQueryable<TaskItem> WithActiveAssignment()
        => _context.TaskItems
            .Include(t => t.Assignments.Where(a => a.UnassignedAt == null))
            .ThenInclude(a => a.User);

    // קריאה בלבד
    public Task<TaskItem?> GetByIdAsync(int taskId, CancellationToken ct = default)
        => WithActiveAssignment()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

    // במעקב: ה-Version שנטען כאן הוא הערך שייכנס לתנאי ה-WHERE בשמירה
    public Task<TaskItem?> GetForUpdateAsync(int taskId, CancellationToken ct = default)
        => WithActiveAssignment()
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

    public Task<PagedResult<TaskItem>> GetPagedByTeamAsync(
        int teamId, TaskQueryParameters query, CancellationToken ct = default)
        => ToPagedAsync(WithActiveAssignment().Where(t => t.TeamId == teamId), query, ct);

    public Task<PagedResult<TaskItem>> GetPagedByAssignedUserAsync(
    int userId, int? teamId, TaskQueryParameters query, CancellationToken ct = default)
    {
        var source = WithActiveAssignment().Where(t =>
            t.Assignments.Any(a => a.UserId == userId && a.UnassignedAt == null));

        // התנאי נוסף ל-WHERE רק כשנדרש סינון לפי צוות
        if (teamId.HasValue)
            source = source.Where(t => t.TeamId == teamId.Value);

        return ToPagedAsync(source, query, ct);
    }

    public async Task AddAsync(TaskItem task, CancellationToken ct = default)
        => await _context.TaskItems.AddAsync(task, ct);

    public void Remove(TaskItem task)
        => _context.TaskItems.Remove(task);

    // סינון, חיפוש, מיון ו-pagination משותפים לשתי השאילתות
    private static async Task<PagedResult<TaskItem>> ToPagedAsync(
        IQueryable<TaskItem> source, TaskQueryParameters query, CancellationToken ct)
    {
        source = source.AsNoTracking();

        if (query.Status.HasValue)
            source = source.Where(t => t.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            source = source.Where(t => EF.Functions.ILike(t.Title, pattern));
        }

        var total = await source.CountAsync(ct);

        source = (query.SortBy, query.Descending) switch
        {
            (TaskSortBy.Title, false) => source.OrderBy(t => t.Title),
            (TaskSortBy.Title, true) => source.OrderByDescending(t => t.Title),
            (TaskSortBy.CreatedAt, false) => source.OrderBy(t => t.CreatedAt),
            (TaskSortBy.CreatedAt, true) => source.OrderByDescending(t => t.CreatedAt),
            (_, true) => source.OrderByDescending(t => t.DueDate),
            _ => source.OrderBy(t => t.DueDate)
        };

        // מיון משני לפי Id: מבטיח סדר יציב בין עמודים גם כשיש ערכים זהים
        var items = await ((IOrderedQueryable<TaskItem>)source)
            .ThenBy(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<TaskItem>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}