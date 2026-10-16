namespace TeamTaskManagement.Core.Repositories;

// כל ה-repositories חולקים DbContext אחד (Scoped), ולכן שמירה אחת מחילה את כל השינויים יחד,
// בטרנזקציה אחת. זו הדרך להבטיח שבדיקה עסקית ושמירה קורות באותה טרנזקציה.
public interface IUnitOfWork
{
    // זורק ConcurrencyConflictException כשמישהו אחר שינה שורה עם concurrency token
    // (למשל TaskItem.Version) בין הקריאה לשמירה.
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
