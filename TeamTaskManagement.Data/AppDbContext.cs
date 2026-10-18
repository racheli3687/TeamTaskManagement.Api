using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // טוען אוטומטית את כל מחלקות ה-IEntityTypeConfiguration שבפרויקט (משימה 1.3)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }



    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // לכל משימה שעומדת להתעדכן: מחליפים את ה-Version בערך חדש.
        // הערך המקורי (שנטען מבסיס הנתונים) הוא זה שנכנס לתנאי ה-WHERE.
        foreach (var entry in ChangeTracker.Entries<TaskItem>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.Version = Guid.NewGuid();
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}