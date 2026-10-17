using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        // שם צוות ייחודי בתוך חברה. בסיס הנתונים אוכף את זה גם אם הקוד שכח לבדוק.
        builder.HasIndex(t => new { t.OrganizationId, t.Name })
            .IsUnique();

        // צוות אחד - הרבה משתמשים. TeamId במשתמש הוא nullable (למנהל החברה אין צוות).
        builder.HasMany(t => t.Users)
            .WithOne(u => u.Team)
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // צוות אחד - הרבה משימות
        builder.HasMany(t => t.Tasks)
            .WithOne(x => x.Team)
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}