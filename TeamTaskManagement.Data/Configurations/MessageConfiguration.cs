using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.Property(m => m.Text)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(m => m.Status)
            .IsRequired();

        // קשר ראשון: השולח. משתמש אחד - הרבה הודעות שנשלחו (SentMessages).
        builder.HasOne(m => m.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // קשר שני: הנמען. משתמש אחד - הרבה הודעות שהתקבלו (ReceivedMessages).
        builder.HasOne(m => m.Recipient)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        // תיבת נכנסות ותיבת נשלחות, ממוינות מהחדשה לישנה
        builder.HasIndex(m => new { m.RecipientId, m.SentAt });
        builder.HasIndex(m => new { m.SenderId, m.SentAt });
    }
}