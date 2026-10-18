using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class TaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly AppDbContext _context;

    public TaskAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    // במעקב: ה-Service ישנה את UnassignedAt ואז ישמור
    public Task<TaskAssignment?> GetActiveByTaskIdAsync(int taskId, CancellationToken ct = default)
        => _context.TaskAssignments
            .FirstOrDefaultAsync(a => a.TaskItemId == taskId && a.UnassignedAt == null, ct);

    // במעקב + Include של המשימה, כדי לשחרר אותה (הסטטוס שלה) בלי שאילתה נוספת לכל שיוך
    public Task<List<TaskAssignment>> GetActiveByUserIdAsync(int userId, CancellationToken ct = default)
        => _context.TaskAssignments
            .Include(a => a.TaskItem)
            .Where(a => a.UserId == userId && a.UnassignedAt == null)
            .ToListAsync(ct);

    public async Task AddAsync(TaskAssignment assignment, CancellationToken ct = default)
        => await _context.TaskAssignments.AddAsync(assignment, ct);
}