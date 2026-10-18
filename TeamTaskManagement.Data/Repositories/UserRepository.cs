using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);

    public Task<bool> UserNameExistsAsync(string userName, int? excludeUserId = null, CancellationToken ct = default)
        => _context.Users.AnyAsync(
            u => u.UserName == userName && (excludeUserId == null || u.Id != excludeUserId), ct);

    public async Task<PagedResult<User>> GetPagedByTeamAsync(
        int teamId, PaginationParameters pagination, CancellationToken ct = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Where(u => u.TeamId == teamId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.UserName)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResult<User>
        {
            Items = items,
            TotalCount = total,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _context.Users.AddAsync(user, ct);
}