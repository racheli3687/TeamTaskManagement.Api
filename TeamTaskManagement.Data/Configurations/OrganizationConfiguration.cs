using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(100);

        // חברה אחת - הרבה צוותים. מחיקת חברה לא מוחקת צוותים בשקט.
        builder.HasMany(o => o.Teams)
            .WithOne(t => t.Organization)
            .HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // חברה אחת - הרבה משתמשים
        builder.HasMany(o => o.Users)
            .WithOne(u => u.Organization)
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}