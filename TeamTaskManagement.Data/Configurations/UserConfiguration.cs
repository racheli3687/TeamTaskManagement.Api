using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamTaskManagement.Core.Entities;

namespace TeamTaskManagement.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.UserName)
            .IsRequired()
            .HasMaxLength(50);

        // שם משתמש ייחודי בכל המערכת, כך שהתחברות לא דורשת לציין חברה
        builder.HasIndex(u => u.UserName)
            .IsUnique();

        // נשמר רק ה-hash של הסיסמה, לעולם לא הסיסמה עצמה
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(200);

        // התפקיד נשמר כמספר (Manager=1, TeamLead=2, Member=3)
        builder.Property(u => u.Role)
            .IsRequired();
    }
}