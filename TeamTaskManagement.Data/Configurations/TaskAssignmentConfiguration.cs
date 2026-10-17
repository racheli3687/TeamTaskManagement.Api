using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        // משימה אחת - הרבה שיוכים לאורך הזמן. מחיקת משימה מוחקת גם את ההיסטוריה שלה.
        builder.HasOne(a => a.TaskItem)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // משתמש אחד - הרבה שיוכים. אי אפשר למחוק משתמש שיש לו היסטוריית שיוכים.
        builder.HasOne(a => a.User)
            .WithMany(u => u.Assignments)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // רשת ביטחון: לכל משימה מותר שיוך פעיל אחד בלבד (UnassignedAt == null).
        // גם אם שני חברים עקפו את בדיקת ה-Version, בסיס הנתונים עצמו יחסום את השני.
        builder.HasIndex(a => a.TaskItemId)
            .IsUnique()
            .HasFilter("\"UnassignedAt\" IS NULL");

        // שליפת המשימות הפעילות של חבר
        builder.HasIndex(a => new { a.UserId, a.UnassignedAt });
    }
}