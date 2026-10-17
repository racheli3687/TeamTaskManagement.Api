using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .IsRequired();

        // Optimistic Concurrency: ב-UPDATE, EF מוסיף ל-WHERE את ערך ה-Version שנקרא.
        // אם מישהו אחר הספיק לשנות את השורה, אף שורה לא תתעדכן ותיזרק DbUpdateConcurrencyException.
        // (התכונה [ConcurrencyCheck] בישות כבר מגדירה את זה, וההגדרה כאן מפורשת לצורך בהירות.)
        builder.Property(t => t.Version)
            .IsConcurrencyToken();

        // אינדקס לשליפת משימות של צוות לפי סטטוס, השאילתה הנפוצה ביותר
        builder.HasIndex(t => new { t.TeamId, t.Status });
    }
}