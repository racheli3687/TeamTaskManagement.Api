using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Repositories;

namespace TeamTaskManagement.Data.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _context;

    public OrganizationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Organization?> GetByIdAsync(int organizationId, CancellationToken ct = default)
        => _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
        => await _context.Organizations.AddAsync(organization, ct);
}