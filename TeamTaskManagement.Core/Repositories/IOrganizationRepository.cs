using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Core.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(int organizationId, CancellationToken ct = default);

    // נשמר רק אחרי IUnitOfWork.SaveChangesAsync. בהרשמה מוסיפים כאן את החברה, ב-IUserRepository
    // את המנהל, ושומרים פעם אחת - כך שניהם נוצרים או שאף אחד.
    Task AddAsync(Organization organization, CancellationToken ct = default);
}
