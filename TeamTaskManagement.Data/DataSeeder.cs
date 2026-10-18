using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Core.Services;

namespace TeamTaskManagement.Data;

public static class DataSeeder
{
    // מכניס נתוני דמו רק אם בסיס הנתונים ריק. בטוח להריץ בכל עלייה של האפליקציה.
    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        string demoPassword,
        CancellationToken ct = default)
    {
        if (await context.Organizations.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        var passwordHash = passwordHasher.Hash(demoPassword);

        var organization = new Organization { Name = "Demo Company" };

        var devTeam = new Team { Name = "Development", Organization = organization };
        var marketingTeam = new Team { Name = "Marketing", Organization = organization };

        // המנהל שייך לחברה ולא לצוות מסוים
        var manager = new User
        {
            UserName = "manager",
            PasswordHash = passwordHash,
            Role = UserRole.Manager,
            Organization = organization
        };
        var leadDev = new User
        {
            UserName = "lead_dev",
            PasswordHash = passwordHash,
            Role = UserRole.TeamLead,
            Organization = organization,
            Team = devTeam
        };
        var dev1 = new User
        {
            UserName = "dev1",
            PasswordHash = passwordHash,
            Role = UserRole.Member,
            Organization = organization,
            Team = devTeam
        };
        var dev2 = new User
        {
            UserName = "dev2",
            PasswordHash = passwordHash,
            Role = UserRole.Member,
            Organization = organization,
            Team = devTeam
        };
        var leadMkt = new User
        {
            UserName = "lead_mkt",
            PasswordHash = passwordHash,
            Role = UserRole.TeamLead,
            Organization = organization,
            Team = marketingTeam
        };
        var mkt1 = new User
        {
            UserName = "mkt1",
            PasswordHash = passwordHash,
            Role = UserRole.Member,
            Organization = organization,
            Team = marketingTeam
        };

        var tasks = new[]
        {
            new TaskItem { Title = "Set up CI pipeline", Description = "Build and test on every push.",
                           Team = devTeam, DueDate = now.AddDays(7) },
            new TaskItem { Title = "Fix login bug", Description = "Users get logged out after 5 minutes.",
                           Team = devTeam, DueDate = now.AddDays(2), Status = TaskItemStatus.InProgress },
            new TaskItem { Title = "Write API documentation", Team = devTeam, DueDate = now.AddDays(14) },
            new TaskItem { Title = "Review pull requests", Team = devTeam,
                           Status = TaskItemStatus.Done, CompletedAt = now.AddDays(-1) },
            new TaskItem { Title = "Prepare newsletter", Team = marketingTeam, DueDate = now.AddDays(5) },
            new TaskItem { Title = "Update landing page", Team = marketingTeam, DueDate = now.AddDays(10) }
        };

        // "Fix login bug" משויכת כרגע ל-dev1 (שיוך פעיל: UnassignedAt == null)
        var activeAssignment = new TaskAssignment
        {
            TaskItem = tasks[1],
            User = dev1,
            AssignedAt = now.AddHours(-3)
        };

        // "Review pull requests": dev2 לקח אותה, סיים, והיא כבר לא משויכת (היסטוריה)
        var pastAssignment = new TaskAssignment
        {
            TaskItem = tasks[3],
            User = dev2,
            AssignedAt = now.AddDays(-2),
            UnassignedAt = now.AddDays(-1)
        };

        var messages = new[]
        {
            new Message { Sender = manager, Recipient = leadDev, SentAt = now.AddHours(-5),
                          Text = "Welcome to the team! Please review the sprint tasks." },
            new Message { Sender = leadDev, Recipient = dev1, SentAt = now.AddHours(-4),
                          Text = "Can you take the login bug today?" }
        };

        context.Organizations.Add(organization);
        context.Teams.AddRange(devTeam, marketingTeam);
        context.Users.AddRange(manager, leadDev, dev1, dev2, leadMkt, mkt1);
        context.TaskItems.AddRange(tasks);
        context.TaskAssignments.AddRange(activeAssignment, pastAssignment);
        context.Messages.AddRange(messages);

        // שמירה אחת = טרנזקציה אחת: או שכל הנתונים נכנסים, או אף אחד
        await context.SaveChangesAsync(ct);
    }
}