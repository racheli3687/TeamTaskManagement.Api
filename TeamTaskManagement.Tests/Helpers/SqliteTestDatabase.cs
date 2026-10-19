// TeamTaskManagement.Tests/Helpers/SqliteTestDatabase.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeamTaskManagement.Core.Entities;
using TeamTaskManagement.Core.Enums;
using TeamTaskManagement.Data;

namespace TeamTaskManagement.Tests.Helpers;

// בסיס נתונים SQLite בזיכרון, משותף לכמה DbContext. כל Context פותח חיבור נפרד משלו,
// כמו שתי בקשות HTTP שרצות במקביל. הבסיס קיים כל עוד חיבור ה-keeper פתוח.
// לא InMemory provider: הוא אינו אוכף concurrency token ואינדקסים ייחודיים.
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _keeper;
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteTestDatabase()
    {
        // שם ייחודי לכל בדיקה: בדיקות שרצות במקביל לא חולקות נתונים
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"tests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        // הסכמה נבנית מה-Configurations (ה-Migrations נוצרו ל-PostgreSQL)
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() => new(_options);

    // חברה, צוות, ראש צוות, שני חברים, משימה פתוחה ומשימה בביצוע שמשויכת לחבר הראשון
    public async Task<SeedData> SeedAsync()
    {
        await using var context = CreateContext();

        var organization = new Organization { Name = "Org" };
        var team = new Team { Name = "Team", Organization = organization };

        User NewUser(string name, UserRole role) => new()
        {
            UserName = name,
            PasswordHash = "not-a-real-hash",
            Role = role,
            Organization = organization,
            Team = team
        };

        var teamLead = NewUser("lead", UserRole.TeamLead);
        var member1 = NewUser("member1", UserRole.Member);
        var member2 = NewUser("member2", UserRole.Member);

        var openTask = new TaskItem { Title = "Open task", Team = team, Status = TaskItemStatus.Open };
        var inProgressTask = new TaskItem { Title = "Claimed task", Team = team, Status = TaskItemStatus.InProgress };
        inProgressTask.Assignments.Add(new TaskAssignment { User = member1, AssignedAt = DateTime.UtcNow });

        context.AddRange(teamLead, member1, member2, openTask, inProgressTask);
        await context.SaveChangesAsync();

        return new SeedData(organization.Id, team.Id, teamLead.Id, member1.Id, member2.Id, openTask.Id, inProgressTask.Id);
    }

    public void Dispose() => _keeper.Dispose();
}

public record SeedData(
    int OrganizationId,
    int TeamId,
    int TeamLeadId,
    int Member1Id,
    int Member2Id,
    int OpenTaskId,
    int InProgressTaskId);