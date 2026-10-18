using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Common;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;

    public TeamRepository(AppDbContext context)
    {
        _context = context;
    }

    // במעקב: לעדכון ולמחיקה
    public Task<Team?> GetByIdAsync(int teamId, CancellationToken ct = default)
        => _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);

    // קריאה בלבד. Select מחשב את מספר החברים ב-COUNT אחד בתוך אותה שאילתה.
    public Task<TeamWithMembersCount?> GetWithMembersCountAsync(int teamId, CancellationToken ct = default)
        => _context.Teams
            .AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => new TeamWithMembersCount(t, t.Users.Count))
            .FirstOrDefaultAsync(ct);

    public async Task<PagedResult<TeamWithMembersCount>> GetPagedByOrganizationAsync(
        int organizationId, PaginationParameters pagination, CancellationToken ct = default)
    {
        var query = _context.Teams
            .AsNoTracking()
            .Where(t => t.OrganizationId == organizationId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.Name)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(t => new TeamWithMembersCount(t, t.Users.Count))
            .ToListAsync(ct);

        return new PagedResult<TeamWithMembersCount>
        {
            Items = items,
            TotalCount = total,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public Task<bool> NameExistsAsync(
        int organizationId, string name, int? excludeTeamId = null, CancellationToken ct = default)
        => _context.Teams.AnyAsync(
            t => t.OrganizationId == organizationId
                 && t.Name == name
                 && (excludeTeamId == null || t.Id != excludeTeamId), ct);

    public async Task<bool> HasUsersOrTasksAsync(int teamId, CancellationToken ct = default)
        => await _context.Users.AnyAsync(u => u.TeamId == teamId, ct)
           || await _context.TaskItems.AnyAsync(t => t.TeamId == teamId, ct);

    public async Task AddAsync(Team team, CancellationToken ct = default)
        => await _context.Teams.AddAsync(team, ct);

    public void Remove(Team team)
        => _context.Teams.Remove(team);
}